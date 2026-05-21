/*
 * Exemplar · Entity 모범 사례 · 갱신일 2026-04-21
 * 실제 com.dotshelf.domain.user.entity.User 와 내용 일치. 새 엔티티는 이 스타일을 거울삼는다.
 *
 * 이 파일에서 참고할 포인트:
 *  - BaseEntity 상속 (createdAt/updatedAt Instant 자동 관리)
 *  - @NoArgsConstructor(access = PROTECTED) + @Builder 조합 — 외부 new 금지, 상태는 Builder 또는 메서드로만
 *  - 시간 필드는 Instant (LocalDateTime 금지, patterns.md §6)
 *  - 상태 변경은 엔티티 메서드로 표현 (updateProfile, completeOnboarding, markLogin, delete)
 *  - setter 금지 — Service 는 user.delete() 호출만. 내부 invariant 는 엔티티가 지킨다.
 *  - @Column 은 "기본 이름·nullable·길이" 가 의미 있을 때만 명시
 *  - Soft delete 는 status 전이 + deletedAt 조합 (물리 삭제 금지)
 */
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

    @Column(unique = true, length = 100)
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

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = Instant.now();
    }
}
