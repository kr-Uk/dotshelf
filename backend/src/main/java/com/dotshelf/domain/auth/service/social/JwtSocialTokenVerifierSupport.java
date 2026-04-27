package com.dotshelf.domain.auth.service.social;

import java.util.List;

import org.springframework.security.oauth2.core.DelegatingOAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2TokenValidator;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtException;
import org.springframework.security.oauth2.jwt.JwtValidators;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

abstract class JwtSocialTokenVerifierSupport implements SocialTokenVerifier {

    private final JwtDecoder jwtDecoder;
    private final List<String> clientIds;
    private final String clientIdPropertyName;

    JwtSocialTokenVerifierSupport(
            List<String> clientIds,
            List<String> issuers,
            String jwksUri,
            String clientIdPropertyName
    ) {
        this.clientIds = clientIds;
        this.clientIdPropertyName = clientIdPropertyName;
        NimbusJwtDecoder decoder = NimbusJwtDecoder.withJwkSetUri(jwksUri).build();
        OAuth2TokenValidator<Jwt> validator = new DelegatingOAuth2TokenValidator<>(
                JwtValidators.createDefault(),
                new JwtIssuerValidator(issuers),
                new JwtAudienceValidator(clientIds)
        );
        decoder.setJwtValidator(validator);
        this.jwtDecoder = decoder;
    }

    protected Jwt decode(String token) {
        if (clientIds.isEmpty()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN, clientIdPropertyName + " 설정이 필요합니다.");
        }
        try {
            return jwtDecoder.decode(token);
        } catch (JwtException e) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    protected String requiredSubject(Jwt jwt) {
        String subject = jwt.getSubject();
        if (subject == null || subject.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
        return subject;
    }
}
