package com.dotshelf.support;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.UserAssetInventory;

public class TestAssets {

    public AssetCategory category(String code) {
        return TestFixtures.assetCategory(code);
    }

    public Asset asset(Long id, Long categoryId, String code, int pricePages) {
        return TestFixtures.asset(id, categoryId, code, pricePages);
    }

    public UserAssetInventory inventory(Long id, Long userId, Long assetId) {
        return TestFixtures.inventory(id, userId, assetId);
    }
}
