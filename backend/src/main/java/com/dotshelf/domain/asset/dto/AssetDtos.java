package com.dotshelf.domain.asset.dto;

import java.util.List;

import com.dotshelf.domain.asset.entity.Asset;
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
            boolean wished
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
                    wished
            );
        }
    }

    public record AssetListResponse(List<AssetResponse> assets) {
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
