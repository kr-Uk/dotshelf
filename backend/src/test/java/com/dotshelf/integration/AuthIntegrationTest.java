package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.repository.AuthRefreshTokenRepository;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
import com.dotshelf.domain.auth.service.social.SocialTokenVerifierRegistry;
import com.dotshelf.domain.auth.service.social.SocialUserInfo;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
@Transactional
class AuthIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private UserSocialAccountRepository socialAccountRepository;

    @Autowired
    private UserNotificationSettingRepository notificationSettingRepository;

    @Autowired
    private SpaceRepository spaceRepository;

    @Autowired
    private AuthRefreshTokenRepository refreshTokenRepository;

    @MockitoBean
    private SocialTokenVerifierRegistry socialTokenVerifierRegistry;

    @Test
    @DisplayName("신규 소셜 로그인은 user/socialAccount/notificationSetting/space/refreshToken을 DB에 생성한다")
    void socialLogin_newUserCreatesDefaultRows() throws Exception {
        given(socialTokenVerifierRegistry.verify(any(), any(), any(), any(), any(), any()))
                .willReturn(new SocialUserInfo(SocialProvider.GOOGLE, "google-1", "tester@example.com", "tester", null));

        mockMvc.perform(post("/v1/auth/social-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"provider":"GOOGLE","tokenType":"ACCESS_TOKEN","token":"provider-token"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.newUser").value(true))
                .andExpect(jsonPath("$.result.accessToken").isString())
                .andExpect(jsonPath("$.result.refreshToken").isString());

        var user = userRepository.findByEmail("tester@example.com").orElseThrow();
        assertThat(socialAccountRepository.findByProviderAndProviderUserId(SocialProvider.GOOGLE, "google-1")).isPresent();
        assertThat(notificationSettingRepository.findByUserId(user.getId())).isPresent();
        assertThat(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(user.getId())).isPresent();
        assertThat(refreshTokenRepository.findAll()).hasSize(1);
    }

    @Test
    @DisplayName("기존 소셜 계정 로그인은 user/socialAccount/space를 중복 생성하지 않는다")
    void socialLogin_existingAccountDoesNotDuplicateRows() throws Exception {
        given(socialTokenVerifierRegistry.verify(any(), any(), any(), any(), any(), any()))
                .willReturn(new SocialUserInfo(SocialProvider.GOOGLE, "google-1", "tester@example.com", "tester", null));

        String body = """
                {"provider":"GOOGLE","tokenType":"ACCESS_TOKEN","token":"provider-token"}
                """;
        mockMvc.perform(post("/v1/auth/social-login").contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.newUser").value(true));
        mockMvc.perform(post("/v1/auth/social-login").contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.newUser").value(false));

        var user = userRepository.findByEmail("tester@example.com").orElseThrow();
        assertThat(userRepository.findAll()).hasSize(1);
        assertThat(socialAccountRepository.findAll()).hasSize(1);
        assertThat(spaceRepository.findAll()).extracting("ownerUserId").containsExactly(user.getId());
        assertThat(refreshTokenRepository.findAll()).hasSize(2);
    }

    @Test
    @DisplayName("refresh token 재발급은 기존 토큰을 revoke하고 재사용 탐지를 기록한다")
    void refresh_rotatesTokenAndDetectsReuse() throws Exception {
        given(socialTokenVerifierRegistry.verify(any(), any(), any(), any(), any(), any()))
                .willReturn(new SocialUserInfo(SocialProvider.GOOGLE, "google-refresh", "refresh@example.com", "refresh", null));
        socialLogin("GOOGLE", "provider-token");
        String oldRefreshToken = refreshTokenRepository.findAll().get(0).getTokenHash();

        mockMvc.perform(post("/v1/auth/token/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"%s"}
                                """.formatted(oldRefreshToken)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.accessToken").isString())
                .andExpect(jsonPath("$.result.refreshToken").isString());

        var rotatedToken = refreshTokenRepository.findByTokenHash(oldRefreshToken).orElseThrow();
        assertThat(rotatedToken.getRevokedAt()).isNotNull();
        assertThat(rotatedToken.getReplacedByTokenId()).isNotNull();
        assertThat(refreshTokenRepository.findAll()).hasSize(2);

        mockMvc.perform(post("/v1/auth/token/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"%s"}
                                """.formatted(oldRefreshToken)))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("A003"));
        assertThat(refreshTokenRepository.findByTokenHash(oldRefreshToken).orElseThrow().getReuseDetectedAt()).isNotNull();
    }

    @Test
    @DisplayName("로그아웃은 refresh token을 revoke하고 없는 토큰은 멱등적으로 성공한다")
    void logout_revokesRefreshTokenIdempotently() throws Exception {
        given(socialTokenVerifierRegistry.verify(any(), any(), any(), any(), any(), any()))
                .willReturn(new SocialUserInfo(SocialProvider.KAKAO, "kakao-logout", "logout@example.com", "logout", null));
        socialLogin("KAKAO", "provider-token");
        String refreshToken = refreshTokenRepository.findAll().get(0).getTokenHash();

        mockMvc.perform(post("/v1/auth/logout")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"%s"}
                                """.formatted(refreshToken)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));
        assertThat(refreshTokenRepository.findByTokenHash(refreshToken).orElseThrow().getRevokedAt()).isNotNull();

        mockMvc.perform(post("/v1/auth/logout")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"not-issued"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));
    }

    @Test
    @DisplayName("소셜 로그인 요청 validation 실패는 사용자 관련 row를 만들지 않는다")
    void socialLogin_validationFailureDoesNotCreateRows() throws Exception {
        mockMvc.perform(post("/v1/auth/social-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"provider":"GOOGLE","tokenType":"ACCESS_TOKEN","token":" "}
                                """))
                .andExpect(status().isBadRequest());

        assertThat(userRepository.findAll()).isEmpty();
        assertThat(socialAccountRepository.findAll()).isEmpty();
        assertThat(notificationSettingRepository.findAll()).isEmpty();
        assertThat(spaceRepository.findAll()).isEmpty();
        assertThat(refreshTokenRepository.findAll()).isEmpty();
    }

    private void socialLogin(String provider, String token) throws Exception {
        mockMvc.perform(post("/v1/auth/social-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"provider":"%s","tokenType":"ACCESS_TOKEN","token":"%s"}
                                """.formatted(provider, token)))
                .andExpect(status().isOk());
    }
}
