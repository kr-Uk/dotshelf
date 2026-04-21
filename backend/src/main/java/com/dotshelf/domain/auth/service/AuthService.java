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
import com.dotshelf.domain.auth.entity.UserSocialAccount;
import com.dotshelf.domain.auth.repository.AuthRefreshTokenRepository;
import com.dotshelf.domain.auth.repository.GuestMigrationImportRepository;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
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

    @Transactional
    public AuthTokenResponse socialLogin(SocialLoginRequest request) {
        UserSocialAccount socialAccount = socialAccountRepository
                .findByProviderAndProviderUserId(request.provider(), request.providerUserId())
                .orElse(null);
        boolean newUser = socialAccount == null;
        User user = newUser ? createUserAndSocialAccount(request) : userService.getUser(socialAccount.getUserId());
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

    private User createUserAndSocialAccount(SocialLoginRequest request) {
        String nickname = request.nickname() == null || request.nickname().isBlank()
                ? "user-" + UUID.randomUUID().toString().substring(0, 8)
                : request.nickname();
        if (userRepository.existsByNickname(nickname)) {
            nickname = nickname + "-" + UUID.randomUUID().toString().substring(0, 4);
        }
        User user = userRepository.save(User.builder()
                .email(request.email())
                .nickname(nickname)
                .build());
        socialAccountRepository.save(UserSocialAccount.builder()
                .userId(user.getId())
                .provider(request.provider())
                .providerUserId(request.providerUserId())
                .emailAtProvider(request.email())
                .build());
        return user;
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
