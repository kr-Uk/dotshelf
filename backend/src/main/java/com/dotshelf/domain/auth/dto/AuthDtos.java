package com.dotshelf.domain.auth.dto;

import com.dotshelf.domain.auth.entity.SocialProvider;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public final class AuthDtos {

    private AuthDtos() {
    }

    public record SocialLoginRequest(
            @Schema(description = "소셜 로그인 제공자", example = "GOOGLE")
            @NotNull(message = "provider는 필수입니다.")
            SocialProvider provider,
            @Schema(description = "제공자 토큰. GOOGLE=id_token 또는 server auth code, KAKAO=access_token, APPLE=identity_token")
            @NotBlank(message = "token은 필수입니다.")
            String token
    ) {
    }

    public record TokenRefreshRequest(
            @Schema(description = "DotShelf refresh token")
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
