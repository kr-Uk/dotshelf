package com.dotshelf.global.security;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Date;
import java.util.UUID;

import javax.crypto.SecretKey;

import org.springframework.stereotype.Component;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.JwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;

/**
 * HS256 기반 JWT 발급/검증. Access 토큰은 `sub = userId`, `typ = access|refresh` 로 구분한다.
 * refresh 토큰도 JWT 로 발급하되, 서버는 DB 에 저장된 레코드와 쌍으로 대조해서 rotation/reuse 를 판정한다.
 */
@Component
public class JwtTokenProvider {

    private static final String CLAIM_TOKEN_TYPE = "typ";
    private static final String TOKEN_TYPE_ACCESS = "access";
    private static final String TOKEN_TYPE_REFRESH = "refresh";

    private final SecretKey key;
    private final JwtProperties properties;

    public JwtTokenProvider(JwtProperties properties) {
        this.properties = properties;
        this.key = Keys.hmacShaKeyFor(properties.secret().getBytes(StandardCharsets.UTF_8));
    }

    public String issueAccessToken(Long userId) {
        return buildToken(userId, TOKEN_TYPE_ACCESS, properties.accessTokenValiditySeconds());
    }

    public String issueRefreshToken(Long userId) {
        return buildToken(userId, TOKEN_TYPE_REFRESH, properties.refreshTokenValiditySeconds());
    }

    public long getAccessTokenValiditySeconds() {
        return properties.accessTokenValiditySeconds();
    }

    public long getRefreshTokenValiditySeconds() {
        return properties.refreshTokenValiditySeconds();
    }

    public Long parseUserId(String token) {
        Claims claims = parse(token);
        if (!TOKEN_TYPE_ACCESS.equals(claims.get(CLAIM_TOKEN_TYPE, String.class))) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
        return Long.valueOf(claims.getSubject());
    }

    public Long parseRefreshUserId(String token) {
        Claims claims = parse(token);
        if (!TOKEN_TYPE_REFRESH.equals(claims.get(CLAIM_TOKEN_TYPE, String.class))) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
        return Long.valueOf(claims.getSubject());
    }

    private String buildToken(Long userId, String tokenType, long validitySeconds) {
        Instant now = Instant.now();
        Instant expiresAt = now.plusSeconds(validitySeconds);
        return Jwts.builder()
                .subject(String.valueOf(userId))
                .issuer(properties.issuer())
                .issuedAt(Date.from(now))
                .expiration(Date.from(expiresAt))
                .id(UUID.randomUUID().toString())
                .claim(CLAIM_TOKEN_TYPE, tokenType)
                .signWith(key, Jwts.SIG.HS256)
                .compact();
    }

    private Claims parse(String token) {
        try {
            return Jwts.parser()
                    .verifyWith(key)
                    .build()
                    .parseSignedClaims(token)
                    .getPayload();
        } catch (JwtException | IllegalArgumentException e) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }
}
