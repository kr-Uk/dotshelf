package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.hamcrest.Matchers.anyOf;
import static org.hamcrest.Matchers.hasItem;
import static org.hamcrest.Matchers.is;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.AssetWishlistRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.IntegrationTestSupport;
import com.dotshelf.support.TestFixtures;

class AssetIntegrationTest extends IntegrationTestSupport {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private AssetCategoryRepository categoryRepository;

    @Autowired
    private AssetRepository assetRepository;

    @Autowired
    private AssetWishlistRepository wishlistRepository;

    @Autowired
    private UserAssetInventoryRepository inventoryRepository;

    @Autowired
    private AssetPurchaseRepository purchaseRepository;

    @Autowired
    private PageWalletRepository walletRepository;

    @Autowired
    private SpaceRepository spaceRepository;

    @Test
    @DisplayName("HTTP wishlist 토글과 에셋 구매 성공은 inventory, purchase, wallet 상태를 DB에 반영한다")
    void wishlistAndPurchase_successPersistsInventoryPurchaseAndWallet() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("buyer"));
        Asset asset = paidAsset(80);
        walletRepository.saveAndFlush(PageWallet.builder()
                .userId(user.getId())
                .balancePages(100)
                .build());

        mockMvc.perform(post("/v1/assets/{assetId}/wishlist", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));
        assertThat(wishlistRepository.existsByUserIdAndAssetId(user.getId(), asset.getId())).isTrue();

        mockMvc.perform(get("/v1/assets/{assetId}", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.wished").value(true))
                .andExpect(jsonPath("$.result.owned").value(false));

        mockMvc.perform(delete("/v1/assets/{assetId}/wishlist", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));
        assertThat(wishlistRepository.existsByUserIdAndAssetId(user.getId(), asset.getId())).isFalse();

        mockMvc.perform(post("/v1/assets/{assetId}/purchase", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetId").value(asset.getId()))
                .andExpect(jsonPath("$.result.pricePages").value(80));

        var inventory = inventoryRepository.findByUserIdAndAssetId(user.getId(), asset.getId()).orElseThrow();
        assertThat(purchaseRepository.findByUserIdOrderByPurchasedAtDesc(user.getId()))
                .singleElement()
                .satisfies(purchase -> {
                    assertThat(purchase.getAssetId()).isEqualTo(asset.getId());
                    assertThat(purchase.getInventoryId()).isEqualTo(inventory.getId());
                    assertThat(purchase.getPricePages()).isEqualTo(80);
                });
        assertThat(walletRepository.findByUserId(user.getId()).orElseThrow().getBalancePages()).isEqualTo(20);

        mockMvc.perform(get("/v1/assets/me/inventory")
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetIds[0]").value(asset.getId()))
                .andExpect(jsonPath("$.result.items[0].inventoryId").value(inventory.getId()))
                .andExpect(jsonPath("$.result.items[0].assetId").value(asset.getId()));

        mockMvc.perform(post("/v1/assets/{assetId}/purchase", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("AS002"));
    }

    @Test
    @DisplayName("카테고리/에셋 목록은 category, sort, owned, wished 상태를 함께 반환한다")
    void getCategoriesAndAssets_returnsCatalogWithOwnershipFlags() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("catalog-user"));
        var category = categoryRepository.saveAndFlush(TestFixtures.assetCategory("WALLPAPER"));
        Asset expensive = assetRepository.saveAndFlush(TestFixtures.asset(null, category.getId(), "expensive-wallpaper", 200));
        Asset cheap = assetRepository.saveAndFlush(TestFixtures.asset(null, category.getId(), "cheap-wallpaper", 50));
        inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), cheap.getId()));
        wishlistRepository.saveAndFlush(TestFixtures.wishlist(user.getId(), cheap.getId()));

        mockMvc.perform(get("/v1/assets/categories"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.categories[0].code").value("WALLPAPER"));

        mockMvc.perform(get("/v1/assets")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .param("category", "WALLPAPER")
                        .param("sort", "price")
                        .param("limit", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assets[0].id").value(cheap.getId()))
                .andExpect(jsonPath("$.result.assets[0].owned").value(true))
                .andExpect(jsonPath("$.result.assets[0].wished").value(true));

        assertThat(expensive.getPricePages()).isEqualTo(200);
    }

    @Test
    @DisplayName("테마 contents 조회는 compatibleTargets JSON의 자식 에셋 ID를 카테고리별로 반환한다")
    void getThemeContents_parsesCompatibleTargets() throws Exception {
        var category = categoryRepository.saveAndFlush(TestFixtures.assetCategory("THEME"));
        Asset theme = assetRepository.saveAndFlush(TestFixtures.themeAsset(
                null,
                category.getId(),
                """
                        {
                          "wallpaper": 11,
                          "floor": 12,
                          "desk": 13,
                          "dashboard": 14,
                          "interiorLight": 15,
                          "exteriorLight": 16,
                          "bookshelfSkinDefault": 17
                        }
                        """,
                false
        ));

        mockMvc.perform(get("/v1/assets/{assetId}/contents", theme.getId()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.themeAssetId").value(theme.getId()))
                .andExpect(jsonPath("$.result.wallpaper").value(11))
                .andExpect(jsonPath("$.result.bookshelfSkinDefault").value(17));
    }

    @Test
    @DisplayName("Unity 테마 적용 플로우는 assetId가 아니라 inventoryId로만 성공한다")
    void themeApply_usesInventoryIdFromInventoryEndpoint() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("unity-theme-user"));
        var category = categoryRepository.saveAndFlush(TestFixtures.assetCategory("THEME"));
        Asset theme = assetRepository.saveAndFlush(TestFixtures.themeAsset(null, category.getId(), "{}", true));
        // assetId와 inventoryId가 우연히 같아지는 false positive를 피하려고 inventory sequence를 먼저 민다.
        var dummyCategory = categoryRepository.saveAndFlush(TestFixtures.assetCategory("DUMMY_THEME_TEST"));
        Asset dummyOne = assetRepository.saveAndFlush(TestFixtures.asset(null, dummyCategory.getId(), "dummy-one", 0));
        Asset dummyTwo = assetRepository.saveAndFlush(TestFixtures.asset(null, dummyCategory.getId(), "dummy-two", 0));
        inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), dummyOne.getId()));
        inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), dummyTwo.getId()));
        var inventory = inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), theme.getId()));
        assertThat(inventory.getId()).isNotEqualTo(theme.getId());

        mockMvc.perform(get("/v1/assets/me/inventory")
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetIds", hasItem(theme.getId().intValue())))
                .andExpect(jsonPath(
                        "$.result.items[?(@.assetId == " + theme.getId() + ")].inventoryId",
                        hasItem(inventory.getId().intValue())));

        mockMvc.perform(post("/v1/spaces/me/theme/{inventoryId}/apply", theme.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .param("mode", "RESET"))
                .andExpect(status().is4xxClientError())
                .andExpect(jsonPath("$.code", anyOf(is("S006"), is("S008"))));

        mockMvc.perform(patch("/v1/spaces/me/theme")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"inventoryId\":" + theme.getId() + "}"))
                .andExpect(status().is4xxClientError())
                .andExpect(jsonPath("$.code", anyOf(is("S006"), is("S008"))));

        mockMvc.perform(post("/v1/spaces/me/theme/{inventoryId}/apply", inventory.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .param("mode", "RESET"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.themeReset").value(true))
                .andExpect(jsonPath("$.result.space.currentThemeInventoryId").value(inventory.getId()));

        mockMvc.perform(patch("/v1/spaces/me/theme")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"inventoryId\":" + inventory.getId() + "}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.currentThemeInventoryId").value(inventory.getId()));

        var space = spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(user.getId()).orElseThrow();
        assertThat(space.getCurrentThemeInventoryId()).isEqualTo(inventory.getId());
    }

    @Test
    @DisplayName("잔액이 부족하면 HTTP 구매는 실패하고 inventory/purchase를 생성하지 않는다")
    void purchase_insufficientBalanceDoesNotCreateRows() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("poor-buyer"));
        Asset asset = paidAsset(80);
        walletRepository.saveAndFlush(PageWallet.builder()
                .userId(user.getId())
                .balancePages(10)
                .build());

        mockMvc.perform(post("/v1/assets/{assetId}/purchase", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isUnprocessableEntity())
                .andExpect(jsonPath("$.code").value("AS003"));

        assertThat(inventoryRepository.findByUserIdAndAssetId(user.getId(), asset.getId())).isEmpty();
        assertThat(purchaseRepository.findByUserIdOrderByPurchasedAtDesc(user.getId())).isEmpty();
        assertThat(walletRepository.findByUserId(user.getId()).orElseThrow().getBalancePages()).isEqualTo(10);
    }

    @Test
    @DisplayName("존재하지 않는 에셋 구매/찜 요청은 실패하고 사용자 지갑이나 wish row를 만들지 않는다")
    void missingAsset_purchaseAndWishlistReturnNotFoundWithoutSideEffects() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("missing-asset-user"));

        mockMvc.perform(post("/v1/assets/{assetId}/purchase", 999_999L)
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("AS001"));
        mockMvc.perform(post("/v1/assets/{assetId}/wishlist", 999_999L)
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("AS001"));

        assertThat(walletRepository.findByUserId(user.getId())).isEmpty();
        assertThat(inventoryRepository.findByUserIdAndAssetId(user.getId(), 999_999L)).isEmpty();
        assertThat(purchaseRepository.findByUserIdOrderByPurchasedAtDesc(user.getId())).isEmpty();
        assertThat(wishlistRepository.existsByUserIdAndAssetId(user.getId(), 999_999L)).isFalse();
    }

    @Test
    @DisplayName("무료 에셋 구매는 기존 지갑이 없어도 성공하고 잔액 0 지갑을 남긴다")
    void purchase_freeAssetCreatesZeroBalanceWalletAndInventory() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("free-asset-user"));
        Asset asset = paidAsset(0);

        mockMvc.perform(post("/v1/assets/{assetId}/purchase", asset.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetId").value(asset.getId()))
                .andExpect(jsonPath("$.result.pricePages").value(0));

        assertThat(walletRepository.findByUserId(user.getId())).isPresent()
                .get()
                .extracting(PageWallet::getBalancePages)
                .isEqualTo(0);
        assertThat(inventoryRepository.findByUserIdAndAssetId(user.getId(), asset.getId())).isPresent();
        assertThat(purchaseRepository.findByUserIdOrderByPurchasedAtDesc(user.getId()))
                .singleElement()
                .satisfies(purchase -> {
                    assertThat(purchase.getAssetId()).isEqualTo(asset.getId());
                    assertThat(purchase.getPricePages()).isZero();
                });
    }

    private Asset paidAsset(int pricePages) {
        var category = categoryRepository.saveAndFlush(TestFixtures.assetCategory("WALLPAPER"));
        return assetRepository.saveAndFlush(TestFixtures.asset(null, category.getId(), "wallpaper-" + pricePages, pricePages));
    }
}
