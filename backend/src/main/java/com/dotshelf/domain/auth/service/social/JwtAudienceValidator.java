package com.dotshelf.domain.auth.service.social;

import java.util.List;

import org.springframework.security.oauth2.core.OAuth2Error;
import org.springframework.security.oauth2.core.OAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.jwt.Jwt;

final class JwtAudienceValidator implements OAuth2TokenValidator<Jwt> {

    private final List<String> allowedAudiences;

    JwtAudienceValidator(List<String> allowedAudiences) {
        this.allowedAudiences = allowedAudiences;
    }

    @Override
    public OAuth2TokenValidatorResult validate(Jwt token) {
        boolean matched = token.getAudience().stream().anyMatch(allowedAudiences::contains);
        if (matched) {
            return OAuth2TokenValidatorResult.success();
        }
        OAuth2Error error = new OAuth2Error("invalid_token", "허용되지 않은 소셜 로그인 audience 입니다.", null);
        return OAuth2TokenValidatorResult.failure(error);
    }
}
