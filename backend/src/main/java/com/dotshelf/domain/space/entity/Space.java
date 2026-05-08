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

    @Column(length = 500)
    private String thumbnailKey;

    @Column(length = 500)
    private String thumbnailUrl;

    private Long currentThemeInventoryId;

    // 카테고리별 개별 적용 항목. currentThemeInventoryId(전체 테마)와 독립적으로
    // 동작하며, "테마1의 노트북 + 테마2의 바닥" 같은 믹스를 표현한다.
    // 책장 외형은 인스턴스별로 bookshelves.skin_inventory_id에 저장(별도 테이블).
    private Long currentWallpaperInventoryId;
    private Long currentFloorInventoryId;
    private Long currentDeskInventoryId;
    private Long currentDashboardInventoryId;
    private Long currentInteriorLightInventoryId;
    private Long currentExteriorLightInventoryId;

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

    public void updateThumbnail(String thumbnailKey, String thumbnailUrl) {
        this.thumbnailKey = thumbnailKey;
        this.thumbnailUrl = thumbnailUrl;
    }

    public void applyTheme(Long inventoryId) {
        this.currentThemeInventoryId = inventoryId;
    }

    public void changeWallpaper(Long inventoryId) {
        this.currentWallpaperInventoryId = inventoryId;
    }

    public void changeFloor(Long inventoryId) {
        this.currentFloorInventoryId = inventoryId;
    }

    public void changeDesk(Long inventoryId) {
        this.currentDeskInventoryId = inventoryId;
    }

    public void changeDashboard(Long inventoryId) {
        this.currentDashboardInventoryId = inventoryId;
    }

    public void changeInteriorLight(Long inventoryId) {
        this.currentInteriorLightInventoryId = inventoryId;
    }

    public void changeExteriorLight(Long inventoryId) {
        this.currentExteriorLightInventoryId = inventoryId;
    }

    /**
     * 커스터마이징 불가 테마 자동 해제 시 호출. 카테고리 6 ID + 전체 테마 ID 모두 비우고
     * layoutVersion을 올린다. bookshelves.skin_inventory_id 초기화는 별도(BookshelfRepository)에서.
     */
    public void resetCustomization() {
        this.currentThemeInventoryId = null;
        this.currentWallpaperInventoryId = null;
        this.currentFloorInventoryId = null;
        this.currentDeskInventoryId = null;
        this.currentDashboardInventoryId = null;
        this.currentInteriorLightInventoryId = null;
        this.currentExteriorLightInventoryId = null;
        this.layoutVersion += 1;
    }
}
