package com.dotshelf.domain.auth.dto;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;

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
            @Schema(description = "토큰 종류. 생략 시 provider 기본값 사용. KAKAO Desktop은 AUTHORIZATION_CODE 사용", example = "ACCESS_TOKEN")
            SocialTokenType tokenType,
            @Schema(description = "제공자 토큰. GOOGLE=id_token 또는 server auth code, KAKAO=access_token 또는 authorization code, APPLE=identity_token")
            @NotBlank(message = "token은 필수입니다.")
            String token,
            @Schema(description = "authorization code 교환 시 provider에 전달할 redirect URI", example = "http://127.0.0.1:12345/dotshelf/oauth/callback/")
            String redirectUri,
            @Schema(description = "Apple identityToken nonce 검증값. provider=APPLE일 때 Apple 요청에 넣은 nonce와 같은 값", example = "base64url-random-nonce")
            String nonce,
            @Schema(description = "PKCE code_verifier (RFC 7636). Desktop OAuth 흐름에서 native client가 authorize 시점에 생성한 random string. 있으면 BE는 token 교환 호출에 그대로 forward해서 OAuth 제공자가 SHA256(code_verifier) == code_challenge 검증함. null이면 legacy server pair(client_secret) fallback.", example = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")
            String codeVerifier
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
            @Schema(description = "guest-session 응답에서 받은 guestSessionId. 동일 게스트 세션의 데이터를 식별/검증할 때 사용한다.")
            String guestSessionId,
            @NotBlank(message = "payload는 필수입니다.")
            String payload
    ) {
    }

    public record AuthTokenResponse(
            Long userId,
            String accessToken,
            String refreshToken,
            @Schema(description = "토큰 타입. 항상 \"Bearer\".", example = "Bearer")
            String tokenType,
            @Schema(description = "access token 유효 시간(초). 클라이언트가 만료 카운트다운에 사용한다.", example = "1800")
            long expiresIn,
            boolean newUser,
            boolean onboarded
    ) {
    }

    public record GuestSessionResponse(
            String guestToken,
            long expiresInSeconds,
            @Schema(description = "게스트 세션 식별자. 이후 guest-migration 요청 시 동일 값을 함께 보낸다.")
            String guestSessionId
    ) {
    }
}
