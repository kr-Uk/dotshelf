package com.dotshelf.domain.auth.service.social;

import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Component;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;

@Component
public class AppleTokenVerifier extends JwtSocialTokenVerifierSupport {

    public AppleTokenVerifier(SocialLoginProperties properties) {
        super(
                properties.apple().clientIds(),
                properties.apple().issuers(),
                properties.apple().jwksUri(),
                "APPLE_CLIENT_IDS"
        );
    }

    @Override
    public SocialProvider supports() {
        return SocialProvider.APPLE;
    }

    @Override
    public SocialUserInfo verify(String token, SocialTokenType tokenType, String redirectUri, String nonce, String codeVerifier) {
        // Apple은 클라이언트 SDK가 직접 identityToken을 받아 BE로 전송 — code 교환 단계 없음 → PKCE 무관.
        if (tokenType != null && tokenType != SocialTokenType.ID_TOKEN) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE은 ID_TOKEN만 지원합니다.");
        }
        String expectedNonce = requiredNonce(nonce);
        Jwt jwt = decode(token);
        validateNonce(jwt, expectedNonce);
        return new SocialUserInfo(
                SocialProvider.APPLE,
                requiredSubject(jwt),
                jwt.getClaimAsString("email"),
                null,
                null
        );
    }

    private String requiredNonce(String nonce) {
        if (nonce == null || nonce.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE nonce는 필수입니다.");
        }
        return nonce.trim();
    }

    private void validateNonce(Jwt jwt, String expectedNonce) {
        String actualNonce = jwt.getClaimAsString("nonce");
        if (actualNonce == null || !actualNonce.equals(expectedNonce)) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }
}
