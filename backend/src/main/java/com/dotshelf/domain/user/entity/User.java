package com.dotshelf.domain.user.entity;

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
@Table(name = "users")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    // email은 metadata. Identity는 (provider, provider_user_id) — user_social_accounts 테이블.
    // OAuth-only 인증이라 email 기반 로그인/재설정 없음. 같은 email이 다른 provider로 들어오면
    // 별도 user로 인정 (V17 migration: NOT NULL + UNIQUE 둘 다 제거).
    @Column(length = 100)
    private String email;

    @Column(nullable = false, length = 50)
    private String nickname;

    @Column(length = 500)
    private String profileImageUrl;

    @Column(length = 255)
    private String introduction;

    @Column(nullable = false, length = 20)
    private String role = "USER";

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    @Column(nullable = false)
    private boolean isOnboarded = false;

    private Instant lastLoginAt;

    private Instant sessionInvalidatedAt;

    private Instant deletedAt;

    @Builder
    private User(String email, String nickname, String profileImageUrl, String introduction) {
        this.email = email;
        this.nickname = nickname;
        this.profileImageUrl = profileImageUrl;
        this.introduction = introduction;
    }

    public void updateProfile(String nickname, String profileImageUrl, String introduction) {
        this.nickname = nickname;
        this.profileImageUrl = profileImageUrl;
        this.introduction = introduction;
    }

    public void completeOnboarding() {
        this.isOnboarded = true;
    }

    public void markLogin() {
        this.lastLoginAt = Instant.now();
    }

    public void invalidateSessions() {
        this.sessionInvalidatedAt = Instant.now();
    }

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = Instant.now();
        invalidateSessions();
    }

    public void changeStatus(String status) {
        this.status = status;
        if ("DELETED".equalsIgnoreCase(status)) {
            this.deletedAt = Instant.now();
            invalidateSessions();
        }
    }

    public void changeRole(String role) {
        this.role = role;
    }

    public void anonymize() {
        this.email = null;
        this.nickname = "deleted-user-" + this.id;
        this.profileImageUrl = null;
        this.introduction = null;
        this.status = "DELETED";
        this.deletedAt = Instant.now();
        invalidateSessions();
    }

    public boolean isDeleted() {
        return "DELETED".equalsIgnoreCase(this.status) || this.deletedAt != null;
    }

    public void reactivateForOnboarding(String email, String nickname, String profileImageUrl) {
        this.email = email;
        this.nickname = nickname;
        this.profileImageUrl = profileImageUrl;
        this.introduction = null;
        this.status = "ACTIVE";
        this.deletedAt = null;
        this.isOnboarded = false;
    }
}
