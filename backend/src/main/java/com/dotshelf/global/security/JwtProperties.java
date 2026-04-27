package com.dotshelf.global.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * JWT 관련 설정. 배포 환경에서 `JWT_SECRET` 환경변수로 주입받는다. secret 은 HS256 요구사항상 최소 32바이트.
 */
@ConfigurationProperties(prefix = "jwt")
public record JwtProperties(
        String secret,
        long accessTokenValiditySeconds,
        long refreshTokenValiditySeconds,
        String issuer
) {
    public JwtProperties {
        if (secret == null || secret.length() < 32) {
            throw new IllegalStateException("jwt.secret 은 최소 32바이트여야 합니다.");
        }
        if (accessTokenValiditySeconds <= 0) {
            accessTokenValiditySeconds = 1_800L; // 30분
        }
        if (refreshTokenValiditySeconds <= 0) {
            refreshTokenValiditySeconds = 60L * 60 * 24 * 30; // 30일
        }
        if (issuer == null || issuer.isBlank()) {
            issuer = "dotshelf";
        }
    }
}
