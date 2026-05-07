package com.dotshelf.domain.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.util.ReflectionTestUtils;

import com.dotshelf.domain.auth.dto.AuthDtos.AuthTokenResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.SocialLoginRequest;
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
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.security.JwtTokenProvider;

@ExtendWith(MockitoExtension.class)
class AuthServiceTest {

    private static final Long USER_ID = 1L;
    private static final String ACCESS_TOKEN = "access-token";
    private static final String REFRESH_TOKEN = "refresh-token";

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
    @DisplayName("신규 소셜 로그인 시 사용자와 기본 공간을 함께 생성한다")
    void socialLogin_withNewSocialAccount_createsDefaultSpace() {
        SocialLoginRequest request = socialLoginRequest();
        SocialUserInfo socialUser = socialUser();
        when(socialTokenVerifierRegistry.verify(
                request.provider(),
                request.tokenType(),
                request.token(),
                request.redirectUri(),
                request.nonce(),
                request.codeVerifier()
        )).thenReturn(socialUser);
        when(socialAccountRepository.findByProviderAndProviderUserId(
                socialUser.provider(),
                socialUser.providerUserId()
        )).thenReturn(Optional.empty());
        when(userRepository.existsByEmail(socialUser.email())).thenReturn(false);
        when(userRepository.existsByNickname(socialUser.nickname())).thenReturn(false);
        when(userRepository.save(any(User.class))).thenAnswer(invocation -> userWithId(invocation.getArgument(0)));
        stubTokenIssue();

        AuthTokenResponse response = authService.socialLogin(request);

        assertThat(response.userId()).isEqualTo(USER_ID);
        assertThat(response.newUser()).isTrue();
        verify(spaceService).ensureDefaultSpace(USER_ID);
    }

    @Test
    @DisplayName("기존 소셜 로그인 시 기본 공간을 새로 만들지 않는다")
    void socialLogin_withExistingSocialAccount_doesNotCreateDefaultSpace() {
        SocialLoginRequest request = socialLoginRequest();
        SocialUserInfo socialUser = socialUser();
        when(socialTokenVerifierRegistry.verify(
                request.provider(),
                request.tokenType(),
                request.token(),
                request.redirectUri(),
                request.nonce(),
                request.codeVerifier()
        )).thenReturn(socialUser);
        when(socialAccountRepository.findByProviderAndProviderUserId(
                socialUser.provider(),
                socialUser.providerUserId()
        )).thenReturn(Optional.of(UserSocialAccount.builder()
                .userId(USER_ID)
                .provider(SocialProvider.GOOGLE)
                .providerUserId("google-user-id")
                .emailAtProvider("user@example.com")
                .build()));
        when(userService.getUser(USER_ID)).thenReturn(userWithId(User.builder()
                .email("user@example.com")
                .nickname("tester")
                .profileImageUrl("https://example.com/profile.png")
                .build()));
        stubTokenIssue();

        AuthTokenResponse response = authService.socialLogin(request);

        assertThat(response.userId()).isEqualTo(USER_ID);
        assertThat(response.newUser()).isFalse();
        verify(spaceService, never()).ensureDefaultSpace(any());
    }

    private SocialLoginRequest socialLoginRequest() {
        return new SocialLoginRequest(
                SocialProvider.GOOGLE,
                SocialTokenType.ACCESS_TOKEN,
                "provider-token",
                null,
                null,
                null
        );
    }

    private SocialUserInfo socialUser() {
        return new SocialUserInfo(
                SocialProvider.GOOGLE,
                "google-user-id",
                "user@example.com",
                "tester",
                "https://example.com/profile.png"
        );
    }

    private User userWithId(User user) {
        ReflectionTestUtils.setField(user, "id", USER_ID);
        return user;
    }

    private void stubTokenIssue() {
        when(jwtTokenProvider.issueAccessToken(USER_ID)).thenReturn(ACCESS_TOKEN);
        when(jwtTokenProvider.issueRefreshToken(USER_ID)).thenReturn(REFRESH_TOKEN);
        when(jwtTokenProvider.getRefreshTokenValiditySeconds()).thenReturn(86_400L);
        when(jwtTokenProvider.getAccessTokenValiditySeconds()).thenReturn(1_800L);
    }
}
