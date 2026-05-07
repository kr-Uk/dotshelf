package com.dotshelf.domain.asset.dto;

import java.util.List;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;

public final class AssetDtos {

    private AssetDtos() {
    }

    public record AssetResponse(
            Long id,
            Long categoryId,
            String code,
            String name,
            String tier,
            String assetType,
            String previewImageUrl,
            int pricePages,
            boolean owned,
            boolean wished,
            boolean allowsCustomization
    ) {

        public static AssetResponse from(Asset asset, boolean owned, boolean wished) {
            return new AssetResponse(
                    asset.getId(),
                    asset.getCategoryId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getTier(),
                    asset.getAssetType(),
                    asset.getPreviewImageUrl(),
                    asset.getPricePages(),
                    owned,
                    wished,
                    asset.isAllowsCustomization()
            );
        }
    }

    public record AssetListResponse(List<AssetResponse> assets) {
    }

    /**
     * 전체 테마 자산이 묶고 있는 자식 자산 ID 매핑.
     * Asset.compatibleTargets (Lob, JSON) 에 저장된 children에서 카테고리별로 파싱.
     * 미설정 키는 null. Frontend는 이 응답을 받아 카테고리별 카탈로그에 매칭.
     */
    public record ThemeContentsResponse(
            Long themeAssetId,
            Long wallpaper,
            Long floor,
            Long desk,
            Long dashboard,
            Long interiorLight,
            Long exteriorLight,
            Long bookshelfSkinDefault
    ) {
    }

    public record AssetCategoryResponse(
            Long categoryId,
            String code,
            String name,
            int displayOrder
    ) {

        public static AssetCategoryResponse from(AssetCategory category) {
            return new AssetCategoryResponse(
                    category.getId(),
                    category.getCode(),
                    category.getName(),
                    category.getDisplayOrder()
            );
        }
    }

    public record AssetCategoryListResponse(List<AssetCategoryResponse> categories) {
    }

    public record InventoryResponse(List<Long> assetIds) {

        public static InventoryResponse from(List<UserAssetInventory> inventories) {
            return new InventoryResponse(inventories.stream()
                    .map(UserAssetInventory::getAssetId)
                    .toList());
        }
    }

    public record AssetPurchaseResponse(Long purchaseId, Long assetId, Long inventoryId, int pricePages) {

        public static AssetPurchaseResponse from(AssetPurchase purchase) {
            return new AssetPurchaseResponse(
                    purchase.getId(),
                    purchase.getAssetId(),
                    purchase.getInventoryId(),
                    purchase.getPricePages()
            );
        }
    }

    /**
     * 관리자가 에셋 본체(Unity AssetBundle 등) 또는 프리뷰 이미지를 S3 에 직업로드하기 위해
     * 발급받는 presigned PUT 요청. 본체와 프리뷰 모두 같은 형식을 공유한다.
     */
    public record AdminAssetUploadUrlRequest(
            @NotBlank(message = "contentType은 필수입니다.")
            String contentType,
            @NotNull(message = "byteLength는 필수입니다.")
            @Positive(message = "byteLength는 양수여야 합니다.")
            Long byteLength
    ) {
    }

    public record AdminAssetUploadUrlResponse(
            String uploadUrl,
            String key,
            long expiresInSeconds
    ) {

        public static AdminAssetUploadUrlResponse from(PresignedPut presigned) {
            return new AdminAssetUploadUrlResponse(
                    presigned.uploadUrl(),
                    presigned.key(),
                    presigned.expiresInSeconds());
        }
    }

    public record AdminAssetConfirmRequest(
            @NotBlank(message = "key는 필수입니다.")
            String key
    ) {
    }

    /**
     * 관리자 콘솔에서 본체/프리뷰 갱신 후 받는 응답. 일반 사용자용 AssetResponse 와 달리
     * assetUrl(본체) 까지 노출한다 — 관리자가 검증용으로 직접 클릭해 확인하기 위함.
     */
    public record AdminAssetResponse(
            Long id,
            String code,
            String name,
            String assetUrl,
            String previewImageUrl
    ) {

        public static AdminAssetResponse from(Asset asset) {
            return new AdminAssetResponse(
                    asset.getId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getAssetUrl(),
                    asset.getPreviewImageUrl()
            );
        }
    }
}
