package com.dotshelf.domain.asset.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.data.domain.PageRequest;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.util.ReflectionTestUtils;

import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class AssetRepositoryTest {

    @Autowired
    private AssetCategoryRepository assetCategoryRepository;

    @Autowired
    private AssetRepository assetRepository;

    @Autowired
    private UserAssetInventoryRepository inventoryRepository;

    @Autowired
    private AssetWishlistRepository wishlistRepository;

    @Autowired
    private AssetPurchaseRepository purchaseRepository;

    @Test
    @DisplayName("활성 카테고리만 displayOrder 오름차순으로 조회한다")
    void findActiveCategories_ordersByDisplayOrder() {
        AssetCategory second = AssetCategory.builder().code("SECOND").name("second").displayOrder(2).build();
        AssetCategory first = AssetCategory.builder().code("FIRST").name("first").displayOrder(1).build();
        AssetCategory inactive = AssetCategory.builder().code("INACTIVE").name("inactive").displayOrder(0).build();
        ReflectionTestUtils.setField(inactive, "isActive", false);
        assetCategoryRepository.save(second);
        assetCategoryRepository.save(first);
        assetCategoryRepository.save(inactive);

        var result = assetCategoryRepository.findByIsActiveTrueOrderByDisplayOrderAsc();

        assertThat(result).extracting(AssetCategory::getCode).containsExactly("FIRST", "SECOND");
    }

    @Test
    @DisplayName("카테고리별 활성 에셋만 페이지로 조회한다")
    void findByCategoryIdAndIsActiveTrue_filtersInactiveAssets() {
        var category = assetCategoryRepository.save(TestFixtures.assetCategory("WALLPAPER"));
        var active = assetRepository.save(TestFixtures.asset(null, category.getId(), "active", 10));
        var inactive = TestFixtures.asset(null, category.getId(), "inactive", 10);
        ReflectionTestUtils.setField(inactive, "isActive", false);
        assetRepository.save(inactive);

        var result = assetRepository.findByCategoryIdAndIsActiveTrue(category.getId(), PageRequest.of(0, 10));

        assertThat(result.getContent()).extracting("code").containsExactly(active.getCode());
    }

    @Test
    @DisplayName("인벤토리는 userId+assetId 중복을 unique 제약으로 막는다")
    void inventory_uniqueUserAndAsset() {
        inventoryRepository.saveAndFlush(TestFixtures.inventory(null, 1L, 10L));

        assertThatThrownBy(() -> inventoryRepository.saveAndFlush(TestFixtures.inventory(null, 1L, 10L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("위시리스트는 존재 여부 조회와 삭제를 지원한다")
    void wishlist_existsAndDelete() {
        wishlistRepository.saveAndFlush(TestFixtures.wishlist(1L, 10L));

        assertThat(wishlistRepository.existsByUserIdAndAssetId(1L, 10L)).isTrue();
        assertThat(wishlistRepository.findByUserIdAndAssetId(1L, 10L)).isPresent();

        wishlistRepository.deleteByUserIdAndAssetId(1L, 10L);
        wishlistRepository.flush();

        assertThat(wishlistRepository.existsByUserIdAndAssetId(1L, 10L)).isFalse();
    }

    @Test
    @DisplayName("구매 내역은 사용자별 최신 구매 순서와 합계 쿼리를 제공한다")
    void purchase_queriesByUserAndAggregate() {
        purchaseRepository.save(TestFixtures.assetPurchase(null, 1L, 10L, 100L, 30));
        purchaseRepository.save(TestFixtures.assetPurchase(null, 1L, 11L, 101L, 40));
        purchaseRepository.save(TestFixtures.assetPurchase(null, 2L, 12L, 102L, 50));
        purchaseRepository.flush();

        assertThat(purchaseRepository.findByUserIdOrderByPurchasedAtDesc(1L)).hasSize(2);
        assertThat(purchaseRepository.countByUserId(1L)).isEqualTo(2);
        assertThat(purchaseRepository.sumPricePages()).isEqualTo(120);
    }
}
