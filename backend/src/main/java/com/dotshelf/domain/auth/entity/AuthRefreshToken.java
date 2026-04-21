package com.dotshelf.domain.auth.entity;

import java.time.Instant;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "auth_refresh_tokens")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AuthRefreshToken extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false, unique = true, length = 255)
    private String tokenHash;

    @Column(length = 100)
    private String deviceId;

    @Column(length = 500)
    private String userAgent;

    @Column(length = 45)
    private String ipAddress;

    @Column(nullable = false)
    private Instant issuedAt = Instant.now();

    @Column(nullable = false)
    private Instant expiresAt;

    private Instant revokedAt;

    private Long replacedByTokenId;

    private Instant reuseDetectedAt;

    @Builder
    private AuthRefreshToken(Long userId, String tokenHash, String deviceId,
                             String userAgent, String ipAddress, Instant expiresAt) {
        this.userId = userId;
        this.tokenHash = tokenHash;
        this.deviceId = deviceId;
        this.userAgent = userAgent;
        this.ipAddress = ipAddress;
        this.expiresAt = expiresAt;
    }

    public boolean isActive() {
        return revokedAt == null && expiresAt.isAfter(Instant.now());
    }

    public void rotateTo(Long newTokenId) {
        this.revokedAt = Instant.now();
        this.replacedByTokenId = newTokenId;
    }

    public void revoke() {
        this.revokedAt = Instant.now();
    }

    public void markReuseDetected() {
        this.reuseDetectedAt = Instant.now();
    }
}
