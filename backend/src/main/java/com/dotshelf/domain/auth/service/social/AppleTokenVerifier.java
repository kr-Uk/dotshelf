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
    public SocialUserInfo verify(String token, SocialTokenType tokenType) {
        if (tokenType != null && tokenType != SocialTokenType.ID_TOKEN) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE은 ID_TOKEN만 지원합니다.");
        }
        Jwt jwt = decode(token);
        return new SocialUserInfo(
                SocialProvider.APPLE,
                requiredSubject(jwt),
                jwt.getClaimAsString("email"),
                null,
                null
        );
    }
}
