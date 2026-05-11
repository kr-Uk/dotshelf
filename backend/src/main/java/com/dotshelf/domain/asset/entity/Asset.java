package com.dotshelf.domain.asset.entity;

import java.time.LocalDateTime;

import com.dotshelf.global.common.BaseEntity;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "assets")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Asset extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long categoryId;

    @Column(nullable = false, unique = true, length = 80)
    private String code;

    @Column(nullable = false, length = 100)
    private String name;

    @Column(nullable = false, length = 30)
    private String tier = "NORMAL";

    @Column(nullable = false, length = 30)
    private String assetScope;

    @Column(nullable = false, length = 50)
    private String assetType;

    @Column(length = 80)
    private String collectionCode;

    @Column(length = 150)
    private String unityResourceKey;

    @Column(length = 500)
    private String assetUrl;

    @Column(length = 120)
    private String contentType;

    private Long fileSize;

    @Column(length = 160)
    private String fileHash;

    @Column(nullable = false)
    private int version = 1;

    @Column(length = 500)
    private String previewImageUrl;

    @Column(nullable = false)
    private int pricePages = 0;

    @Column(length = 100)
    private String creator;

    @Column(length = 100)
    private String supplier;

    @Lob
    private String compatibleTargets;

    private LocalDateTime releasedAt;

    private LocalDateTime validUntil;

    @Column(nullable = false)
    private boolean isDefault = false;

    @Column(nullable = false)
    private boolean isActive = true;

    /**
     * 전체 테마 자산에서만 의미. false면 해당 테마 적용 후 일부 카테고리 변경 시
     * 자동 해제(themeReset 응답)된다. 다른 assetType은 무시.
     */
    @Column(nullable = false)
    private boolean allowsCustomization = true;

    @Builder
    private Asset(Long categoryId, String code, String name, String tier, String assetScope, String assetType,
                  String collectionCode, String unityResourceKey, String assetUrl, String contentType,
                  Long fileSize, String fileHash, Integer version, String previewImageUrl, int pricePages,
                  String creator, String supplier, String compatibleTargets, Boolean isDefault,
                  Boolean isActive, Boolean allowsCustomization) {
        this.categoryId = categoryId;
        this.code = code;
        this.name = name;
        this.tier = tier == null ? "NORMAL" : tier;
        this.assetScope = assetScope;
        this.assetType = assetType;
        this.collectionCode = collectionCode;
        this.unityResourceKey = unityResourceKey;
        this.assetUrl = assetUrl;
        this.contentType = contentType;
        this.fileSize = fileSize;
        this.fileHash = fileHash;
        this.version = version == null ? 1 : version;
        this.previewImageUrl = previewImageUrl;
        this.pricePages = pricePages;
        this.creator = creator;
        this.supplier = supplier;
        this.compatibleTargets = compatibleTargets;
        this.isDefault = isDefault != null && isDefault;
        this.isActive = isActive == null || isActive;
        this.allowsCustomization = allowsCustomization == null ? true : allowsCustomization;
    }

    public void updateDetails(
            Long categoryId,
            String name,
            String tier,
            String assetScope,
            String assetType,
            String collectionCode,
            int pricePages,
            String creator,
            String supplier,
            String compatibleTargets,
            Boolean allowsCustomization) {
        this.categoryId = categoryId;
        this.name = name;
        this.tier = tier == null ? "NORMAL" : tier;
        this.assetScope = assetScope;
        this.assetType = assetType;
        this.collectionCode = collectionCode;
        this.pricePages = pricePages;
        this.creator = creator;
        this.supplier = supplier;
        this.compatibleTargets = compatibleTargets;
        this.allowsCustomization = allowsCustomization == null ? true : allowsCustomization;
    }

    public void updateAssetUrl(String assetUrl) {
        this.assetUrl = assetUrl;
    }

    public void updateBundle(String assetUrl, String contentType, long fileSize, String fileHash, Integer version) {
        if (version != null && version <= this.version) {
            throw new CustomException(ErrorCode.ASSET_VERSION_DOWNGRADE);
        }
        this.assetUrl = assetUrl;
        this.contentType = contentType;
        this.fileSize = fileSize;
        this.fileHash = fileHash;
        this.version = version == null ? this.version + 1 : version;
    }

    public void updatePreviewImageUrl(String previewImageUrl) {
        this.previewImageUrl = previewImageUrl;
    }

    public void publish() {
        this.isActive = true;
    }
}
