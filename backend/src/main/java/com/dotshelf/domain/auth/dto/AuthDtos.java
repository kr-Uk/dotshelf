package com.dotshelf.domain.auth.dto;

import jakarta.validation.constraints.NotBlank;

public final class AuthDtos {

    private AuthDtos() {
    }

    public record SocialLoginRequest(
            @NotBlank(message = "provider는 필수입니다.")
            String provider,
            @NotBlank(message = "providerUserId는 필수입니다.")
            String providerUserId,
            String email,
            String nickname,
            String idToken
    ) {
    }

    public record TokenRefreshRequest(
            @NotBlank(message = "refreshToken은 필수입니다.")
            String refreshToken
    ) {
    }

    public record LogoutRequest(String refreshToken) {
    }

    public record GuestMigrationRequest(
            @NotBlank(message = "payload는 필수입니다.")
            String payload
    ) {
    }

    public record AuthTokenResponse(
            Long userId,
            String accessToken,
            String refreshToken,
            boolean newUser,
            boolean onboarded
    ) {
    }

    public record GuestSessionResponse(String guestToken, long expiresInSeconds) {
    }
}
