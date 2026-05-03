package com.dotshelf.domain.asset.dto;

import java.util.List;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.UserAssetInventory;

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
}
