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

    /**
     * 저폴리 3D 프리뷰 번들 URL (보통 .glb). 상세 페이지에서 회전 가능한 미리보기 용도.
     * 본체와 별도 객체로 S3 에 저장돼 다운로드 사이즈 절감 + 본체 IP 일부 보호.
     * admin-web 이 wasm gltfpack 으로 자동 생성해 올리거나 운영자가 직접 올린다.
     */
    @Column(length = 500)
    private String previewBundleUrl;

    @Column(length = 120)
    private String previewBundleContentType;

    private Long previewBundleSize;

    @Column(length = 160)
    private String previewBundleHash;

    @Column(nullable = false)
    private int pricePages = 0;

    @Column(length = 100)
    private String creator;

    @Column(length = 100)
    private String supplier;

    /**
     * 일반 Asset 의 호환 테마. FE 의 ThemeSelector 가 보내는 단일값(예: "FANTASY", "DEEPSEA").
     * 신규 등록부터 이 컬럼에만 쓰고 compatibleTargets JSON 에는 안 채운다 (V27 마이그레이션).
     */
    @Column(length = 30)
    private String theme;

    /**
     * 일반 Asset 의 배치 위치. CHECK 제약으로 'floor' / 'wall' 중 하나로 강제 (V27).
     */
    @Column(length = 10)
    private String placement;

    /**
     * Legacy + ThemeAsset 의 자식 자산 ID 매핑 ({"wallpaper":11,"floor":12,...}) 전용.
     * 일반 Asset 의 테마/배치 메타는 V27 마이그레이션 이후 theme/placement 컬럼으로 이전돼서
     * 신규 row 에서는 비워둔다 (AdminAssetService.createAsset 가 더 이상 안 채움).
     * AssetService.getThemeContents 가 ThemeAsset row 의 child mapping 을 읽는 용도로만 남음.
     */
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
                  String creator, String supplier, String theme, String placement, String compatibleTargets,
                  Boolean isDefault, Boolean isActive, Boolean allowsCustomization) {
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
        this.theme = theme;
        this.placement = placement;
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
            String theme,
            String placement,
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
        this.theme = theme;
        this.placement = placement;
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

    /**
     * 본체와 별개로 갱신된다. publish 게이트에 포함되지 않는다 — 자동 생성이 실패해도
     * 본체가 준비됐다면 공개는 가능해야 하기 때문.
     */
    public void updatePreviewBundle(String url, String contentType, long size, String hash) {
        this.previewBundleUrl = url;
        this.previewBundleContentType = contentType;
        this.previewBundleSize = size;
        this.previewBundleHash = hash;
    }

    public void publish() {
        this.isActive = true;
    }
}
