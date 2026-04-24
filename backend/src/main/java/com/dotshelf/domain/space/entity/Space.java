package com.dotshelf.domain.space.entity;

import java.time.LocalDateTime;

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
@Table(name = "spaces")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Space extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long ownerUserId;

    private Long templateId;

    @Column(nullable = false, length = 100)
    private String spaceName;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    @Column(nullable = false, length = 20)
    private String guestbookVisibilityScope = "PUBLIC";

    @Column(nullable = false)
    private long visitCount = 0;

    @Column(nullable = false)
    private long likeCount = 0;

    @Column(nullable = false)
    private long layoutVersion = 0;

    private Long currentThemeInventoryId;

    private LocalDateTime deletedAt;

    @Builder
    private Space(Long ownerUserId, Long templateId, String spaceName) {
        this.ownerUserId = ownerUserId;
        this.templateId = templateId;
        this.spaceName = spaceName;
    }

    public void changeVisibility(String visibilityScope) {
        this.visibilityScope = visibilityScope;
    }

    public void changeGuestbookVisibility(String guestbookVisibilityScope) {
        this.guestbookVisibilityScope = guestbookVisibilityScope;
    }

    public void increaseVisitCount() {
        this.visitCount += 1;
    }

    public void like() {
        this.likeCount += 1;
    }

    public void unlike() {
        if (this.likeCount > 0) {
            this.likeCount -= 1;
        }
    }

    public long nextLayoutVersion() {
        this.layoutVersion += 1;
        return this.layoutVersion;
    }

    public void applyTheme(Long inventoryId) {
        this.currentThemeInventoryId = inventoryId;
    }
}
