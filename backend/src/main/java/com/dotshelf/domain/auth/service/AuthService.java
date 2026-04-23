package com.dotshelf.domain.auth.service;

import java.time.Instant;
import java.util.UUID;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.auth.dto.AuthDtos.AuthTokenResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestMigrationRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestSessionResponse;
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
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.IdResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.JwtTokenProvider;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AuthService {

    private static final long GUEST_EXPIRES_IN_SECONDS = 86_400L;

    private final UserRepository userRepository;
    private final UserSocialAccountRepository socialAccountRepository;
    private final AuthRefreshTokenRepository refreshTokenRepository;
    private final GuestMigrationImportRepository guestMigrationImportRepository;
    private final UserService userService;
    private final JwtTokenProvider jwtTokenProvider;
    private final SocialTokenVerifierRegistry socialTokenVerifierRegistry;

    @Transactional
    public AuthTokenResponse socialLogin(SocialLoginRequest request) {
        SocialUserInfo socialUser = socialTokenVerifierRegistry.verify(request.provider(), request.tokenType(), request.token());
        return loginWithSocialUser(socialUser);
    }

    @Transactional
    public AuthTokenResponse kakaoCallback(String code, String error) {
        if (error != null && !error.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "카카오 로그인 인가 요청이 취소되었거나 실패했습니다.");
        }
        if (code == null || code.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "카카오 authorization code가 필요합니다.");
        }
        SocialUserInfo socialUser = socialTokenVerifierRegistry.verify(
                SocialProvider.KAKAO,
                SocialTokenType.AUTHORIZATION_CODE,
                code.trim()
        );
        return loginWithSocialUser(socialUser);
    }

    private AuthTokenResponse loginWithSocialUser(SocialUserInfo socialUser) {
        UserSocialAccount socialAccount = socialAccountRepository
                .findByProviderAndProviderUserId(socialUser.provider(), socialUser.providerUserId())
                .orElse(null);
        boolean newUser = socialAccount == null;
        User user = newUser ? createUserAndSocialAccount(socialUser) : userService.getUser(socialAccount.getUserId());
        user.markLogin();
        TokenPair tokenPair = issueTokens(user.getId());
        return new AuthTokenResponse(
                user.getId(),
                tokenPair.accessToken(),
                tokenPair.refreshToken(),
                newUser,
                user.isOnboarded()
        );
    }

    @Transactional
    public AuthTokenResponse refresh(TokenRefreshRequest request) {
        Long tokenUserId = jwtTokenProvider.parseRefreshUserId(request.refreshToken());
        AuthRefreshToken oldToken = refreshTokenRepository.findByTokenHash(request.refreshToken())
                .orElseThrow(() -> new CustomException(ErrorCode.INVALID_TOKEN));
        if (!oldToken.getUserId().equals(tokenUserId)) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
        if (!oldToken.isActive()) {
            oldToken.markReuseDetected();
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
        TokenPair tokenPair = issueTokens(oldToken.getUserId());
        AuthRefreshToken newToken = refreshTokenRepository.findByTokenHash(tokenPair.refreshToken())
                .orElseThrow(() -> new CustomException(ErrorCode.INTERNAL_SERVER_ERROR));
        oldToken.rotateTo(newToken.getId());
        User user = userService.getUser(oldToken.getUserId());
        return new AuthTokenResponse(user.getId(), tokenPair.accessToken(), tokenPair.refreshToken(), false, user.isOnboarded());
    }

    @Transactional
    public void logout(String refreshToken) {
        if (refreshToken == null || refreshToken.isBlank()) {
            return;
        }
        refreshTokenRepository.findByTokenHash(refreshToken).ifPresent(AuthRefreshToken::revoke);
    }

    public GuestSessionResponse startGuestSession() {
        return new GuestSessionResponse("guest-" + UUID.randomUUID(), GUEST_EXPIRES_IN_SECONDS);
    }

    @Transactional
    public IdResponse importGuestData(Long userId, GuestMigrationRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        GuestMigrationImport migrationImport = guestMigrationImportRepository.save(GuestMigrationImport.builder()
                .userId(currentUserId)
                .payload(request.payload())
                .build());
        return new IdResponse(migrationImport.getId());
    }

    private User createUserAndSocialAccount(SocialUserInfo socialUser) {
        String nickname = resolveNickname(socialUser.nickname());
        User user = userRepository.save(User.builder()
                .email(resolveEmail(socialUser.email()))
                .nickname(nickname)
                .profileImageUrl(socialUser.profileImageUrl())
                .build());
        socialAccountRepository.save(UserSocialAccount.builder()
                .userId(user.getId())
                .provider(socialUser.provider())
                .providerUserId(socialUser.providerUserId())
                .emailAtProvider(socialUser.email())
                .build());
        return user;
    }

    private String resolveEmail(String email) {
        if (email == null || email.isBlank()) {
            return null;
        }
        String normalizedEmail = email.trim();
        return userRepository.existsByEmail(normalizedEmail) ? null : normalizedEmail;
    }

    private String resolveNickname(String nickname) {
        String baseNickname = normalizeNickname(nickname);
        for (int i = 0; i < 10; i++) {
            String candidate = i == 0 ? baseNickname : appendNicknameSuffix(baseNickname);
            if (!userRepository.existsByNickname(candidate)) {
                return candidate;
            }
        }
        return "user-" + UUID.randomUUID().toString().substring(0, 12);
    }

    private String normalizeNickname(String nickname) {
        if (nickname == null || nickname.isBlank()) {
            return "user-" + UUID.randomUUID().toString().substring(0, 8);
        }
        String normalized = nickname.trim();
        return normalized.length() <= 50 ? normalized : normalized.substring(0, 50);
    }

    private String appendNicknameSuffix(String nickname) {
        String suffix = "-" + UUID.randomUUID().toString().substring(0, 4);
        int maxBaseLength = 50 - suffix.length();
        String base = nickname.length() <= maxBaseLength ? nickname : nickname.substring(0, maxBaseLength);
        return base + suffix;
    }

    private TokenPair issueTokens(Long userId) {
        String accessToken = jwtTokenProvider.issueAccessToken(userId);
        String refreshToken = jwtTokenProvider.issueRefreshToken(userId);
        Instant expiresAt = Instant.now().plusSeconds(jwtTokenProvider.getRefreshTokenValiditySeconds());
        refreshTokenRepository.save(AuthRefreshToken.builder()
                .userId(userId)
                .tokenHash(refreshToken)
                .expiresAt(expiresAt)
                .build());
        return new TokenPair(accessToken, refreshToken);
    }

    private record TokenPair(String accessToken, String refreshToken) {
    }
}
