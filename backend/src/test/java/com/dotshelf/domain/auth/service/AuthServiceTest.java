package com.dotshelf.domain.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.auth.dto.AuthDtos.GuestMigrationRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.SocialLoginRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.TokenRefreshRequest;
import com.dotshelf.domain.auth.entity.AuthRefreshToken;
import com.dotshelf.domain.auth.entity.GuestMigrationImport;
import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.domain.auth.entity.UserSocialAccount;
import com.dotshelf.domain.auth.repository.AuthRefreshTokenRepository;
import com.dotshelf.domain.auth.repository.GuestMigrationImportRepository;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
import com.dotshelf.domain.auth.service.social.SocialTokenVerifierRegistry;
import com.dotshelf.domain.auth.service.social.SocialUserInfo;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserNotificationSetting;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.JwtTokenProvider;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class AuthServiceTest {

    @Mock
    private UserRepository userRepository;

    @Mock
    private UserSocialAccountRepository socialAccountRepository;

    @Mock
    private AuthRefreshTokenRepository refreshTokenRepository;

    @Mock
    private GuestMigrationImportRepository guestMigrationImportRepository;

    @Mock
    private UserService userService;

    @Mock
    private SpaceService spaceService;

    @Mock
    private UserNotificationSettingRepository notificationSettingRepository;

    @Mock
    private JwtTokenProvider jwtTokenProvider;

    @Mock
    private SocialTokenVerifierRegistry socialTokenVerifierRegistry;

    @InjectMocks
    private AuthService authService;

    @Test
    @DisplayName("신규 소셜 로그인은 사용자, 알림 설정, 기본 공간, 소셜 계정, refresh token을 생성한다")
    void socialLogin_newSocialAccount_createsUserAndRelatedRows() {
        SocialLoginRequest request = socialLoginRequest();
        SocialUserInfo socialUser = socialUser();
        User savedUser = TestFixtures.user(1L, "tester");
        given(socialTokenVerifierRegistry.verify(
                request.provider(), request.tokenType(), request.token(), request.redirectUri(), request.nonce(), request.codeVerifier()
        )).willReturn(socialUser);
        given(socialAccountRepository.findByProviderAndProviderUserId(SocialProvider.GOOGLE, "google-1"))
                .willReturn(Optional.empty());
        given(userRepository.existsByEmail("tester@example.com")).willReturn(false);
        given(userRepository.existsByNickname("tester")).willReturn(false);
        given(userRepository.save(any(User.class))).willReturn(savedUser);
        stubTokenIssue(1L, "access", "refresh");
        ArgumentCaptor<UserNotificationSetting> settingCaptor = ArgumentCaptor.forClass(UserNotificationSetting.class);
        ArgumentCaptor<UserSocialAccount> accountCaptor = ArgumentCaptor.forClass(UserSocialAccount.class);

        var response = authService.socialLogin(request);

        assertThat(response.userId()).isEqualTo(1L);
        assertThat(response.newUser()).isTrue();
        assertThat(response.accessToken()).isEqualTo("access");
        then(notificationSettingRepository).should().save(settingCaptor.capture());
        assertThat(settingCaptor.getValue().getUserId()).isEqualTo(1L);
        then(spaceService).should().ensureDefaultSpace(1L);
        then(socialAccountRepository).should().save(accountCaptor.capture());
        assertThat(accountCaptor.getValue().getProvider()).isEqualTo(SocialProvider.GOOGLE);
        assertThat(accountCaptor.getValue().getProviderUserId()).isEqualTo("google-1");
    }

    @Test
    @DisplayName("기존 소셜 계정 로그인은 사용자 중복 생성 없이 토큰만 새로 발급한다")
    void socialLogin_existingSocialAccount_issuesTokensWithoutCreatingUser() {
        SocialLoginRequest request = socialLoginRequest();
        SocialUserInfo socialUser = socialUser();
        User user = TestFixtures.user(1L, "tester");
        given(socialTokenVerifierRegistry.verify(
                request.provider(), request.tokenType(), request.token(), request.redirectUri(), request.nonce(), request.codeVerifier()
        )).willReturn(socialUser);
        given(socialAccountRepository.findByProviderAndProviderUserId(SocialProvider.GOOGLE, "google-1"))
                .willReturn(Optional.of(TestFixtures.socialAccount(1L, SocialProvider.GOOGLE, "google-1")));
        given(userService.getUser(1L)).willReturn(user);
        stubTokenIssue(1L, "access", "refresh");

        var response = authService.socialLogin(request);

        assertThat(response.newUser()).isFalse();
        assertThat(user.getLastLoginAt()).isNotNull();
        then(userRepository).shouldHaveNoInteractions();
        then(notificationSettingRepository).shouldHaveNoInteractions();
        then(spaceService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("refresh token은 기존 토큰을 revoke하고 새 refresh token id로 rotation한다")
    void refresh_activeToken_rotatesRefreshToken() {
        AuthRefreshToken oldToken = TestFixtures.refreshToken(10L, 1L, "old-refresh");
        AuthRefreshToken newToken = TestFixtures.refreshToken(11L, 1L, "new-refresh");
        given(jwtTokenProvider.parseRefreshUserId("old-refresh")).willReturn(1L);
        given(refreshTokenRepository.findByTokenHash("old-refresh")).willReturn(Optional.of(oldToken));
        given(jwtTokenProvider.issueAccessToken(1L)).willReturn("new-access");
        given(jwtTokenProvider.issueRefreshToken(1L)).willReturn("new-refresh");
        given(jwtTokenProvider.getRefreshTokenValiditySeconds()).willReturn(3600L);
        given(jwtTokenProvider.getAccessTokenValiditySeconds()).willReturn(1800L);
        given(refreshTokenRepository.findByTokenHash("new-refresh")).willReturn(Optional.of(newToken));
        given(userService.getUser(1L)).willReturn(TestFixtures.user(1L, "tester"));

        var response = authService.refresh(new TokenRefreshRequest("old-refresh"));

        assertThat(response.accessToken()).isEqualTo("new-access");
        assertThat(oldToken.getRevokedAt()).isNotNull();
        assertThat(oldToken.getReplacedByTokenId()).isEqualTo(11L);
    }

    @Test
    @DisplayName("이미 비활성화된 refresh token 재사용은 reuseDetectedAt을 기록하고 INVALID_TOKEN을 던진다")
    void refresh_inactiveToken_marksReuseDetected() {
        AuthRefreshToken oldToken = TestFixtures.refreshToken(10L, 1L, "old-refresh");
        oldToken.revoke();
        given(jwtTokenProvider.parseRefreshUserId("old-refresh")).willReturn(1L);
        given(refreshTokenRepository.findByTokenHash("old-refresh")).willReturn(Optional.of(oldToken));

        assertThatThrownBy(() -> authService.refresh(new TokenRefreshRequest("old-refresh")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);

        assertThat(oldToken.getReuseDetectedAt()).isNotNull();
    }

    @Test
    @DisplayName("로그아웃은 refresh token이 존재하면 revoke 처리한다")
    void logout_existingRefreshToken_revokesToken() {
        AuthRefreshToken token = TestFixtures.refreshToken(10L, 1L, "refresh");
        given(refreshTokenRepository.findByTokenHash("refresh")).willReturn(Optional.of(token));

        authService.logout("refresh");

        assertThat(token.getRevokedAt()).isNotNull();
    }

    @Test
    @DisplayName("게스트 데이터 이관은 세션 id를 trim해서 저장한다")
    void importGuestData_normalizesSessionId() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(guestMigrationImportRepository.save(any(GuestMigrationImport.class))).willAnswer(invocation -> {
            GuestMigrationImport migration = invocation.getArgument(0);
            TestFixtures.setId(migration, 99L);
            return migration;
        });
        ArgumentCaptor<GuestMigrationImport> captor = ArgumentCaptor.forClass(GuestMigrationImport.class);

        var response = authService.importGuestData(1L, new GuestMigrationRequest(" guest-1 ", "{\"books\":[]}"));

        assertThat(response.id()).isEqualTo(99L);
        then(guestMigrationImportRepository).should().save(captor.capture());
        assertThat(captor.getValue().getGuestSessionId()).isEqualTo("guest-1");
    }

    private void stubTokenIssue(Long userId, String accessToken, String refreshToken) {
        given(jwtTokenProvider.issueAccessToken(userId)).willReturn(accessToken);
        given(jwtTokenProvider.issueRefreshToken(userId)).willReturn(refreshToken);
        given(jwtTokenProvider.getRefreshTokenValiditySeconds()).willReturn(3600L);
        given(jwtTokenProvider.getAccessTokenValiditySeconds()).willReturn(1800L);
    }

    private SocialLoginRequest socialLoginRequest() {
        return new SocialLoginRequest(SocialProvider.GOOGLE, SocialTokenType.ACCESS_TOKEN, "provider-token", null, null, null);
    }

    private SocialUserInfo socialUser() {
        return new SocialUserInfo(SocialProvider.GOOGLE, "google-1", "tester@example.com", "tester", "https://example.com/profile.png");
    }
}
