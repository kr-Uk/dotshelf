package com.dotshelf.domain.asset.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.AssetWishlist;
import com.dotshelf.domain.asset.entity.ThemeAssetItem;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.AssetWishlistRepository;
import com.dotshelf.domain.asset.repository.ThemeAssetItemRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class AssetServiceTest {

    @Mock
    private AssetRepository assetRepository;

    @Mock
    private AssetCategoryRepository assetCategoryRepository;

    @Mock
    private ThemeAssetItemRepository themeAssetItemRepository;

    @Mock
    private UserAssetInventoryRepository inventoryRepository;

    @Mock
    private AssetWishlistRepository wishlistRepository;

    @Mock
    private AssetPurchaseRepository assetPurchaseRepository;

    @Mock
    private PageWalletRepository pageWalletRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private AssetService assetService;

    @Test
    @DisplayName("테마 compatibleTargets JSON을 카테고리별 에셋 id로 파싱한다")
    void getThemeContents_validJson_returnsMappedIds() {
        Asset theme = TestFixtures.themeAsset(
                1L,
                10L,
                """
                        {"wallpaper":11,"floor":12,"desk":13,"dashboard":14,
                        "interiorLight":15,"exteriorLight":16,"bookshelfSkinDefault":17}
                        """,
                true
        );
        given(assetRepository.findById(1L)).willReturn(Optional.of(theme));

        var response = assetService.getThemeContents(1L);

        assertThat(response.wallpaper()).isEqualTo(11L);
        assertThat(response.floor()).isEqualTo(12L);
        assertThat(response.bookshelfSkinDefault()).isEqualTo(17L);
    }

    @Test
    @DisplayName("테마 compatibleTargets가 잘못된 JSON이면 빈 매핑으로 폴백한다")
    void getThemeContents_invalidJson_returnsEmptyContents() {
        Asset theme = TestFixtures.themeAsset(1L, 10L, "{invalid-json", true);
        given(assetRepository.findById(1L)).willReturn(Optional.of(theme));

        var response = assetService.getThemeContents(1L);

        assertThat(response.themeAssetId()).isEqualTo(1L);
        assertThat(response.wallpaper()).isNull();
        assertThat(response.floor()).isNull();
    }

    @Test
    @DisplayName("위시리스트 추가는 에셋 존재를 확인하고 중복이 없을 때만 저장한다")
    void wish_notExisting_savesWishlist() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(TestFixtures.asset(10L, 1L, "wallpaper", 100)));
        given(wishlistRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(false);
        ArgumentCaptor<AssetWishlist> captor = ArgumentCaptor.forClass(AssetWishlist.class);

        var response = assetService.wish(1L, 10L);

        assertThat(response.active()).isTrue();
        then(wishlistRepository).should().save(captor.capture());
        assertThat(captor.getValue().getUserId()).isEqualTo(1L);
        assertThat(captor.getValue().getAssetId()).isEqualTo(10L);
    }

    @Test
    @DisplayName("이미 보유한 에셋 구매는 ASSET_ALREADY_OWNED를 던진다")
    void purchase_alreadyOwned_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(TestFixtures.asset(10L, 1L, "wallpaper", 100)));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(true);

        assertThatThrownBy(() -> assetService.purchase(1L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.ASSET_ALREADY_OWNED);

        then(pageWalletRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("잔액이 부족하면 구매 이력과 인벤토리를 만들지 않는다")
    void purchase_insufficientPages_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(TestFixtures.asset(10L, 1L, "wallpaper", 100)));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(false);
        given(pageWalletRepository.findByUserId(1L)).willReturn(Optional.of(TestFixtures.wallet(1L, 50)));

        assertThatThrownBy(() -> assetService.purchase(1L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INSUFFICIENT_PAGES);

        then(inventoryRepository).should().existsByUserIdAndAssetId(1L, 10L);
        then(inventoryRepository).shouldHaveNoMoreInteractions();
        then(assetPurchaseRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("에셋 구매 성공 시 지갑 차감, 인벤토리 생성, 구매 이력 생성을 수행한다")
    void purchase_enoughPages_createsInventoryAndPurchase() {
        PageWallet wallet = TestFixtures.wallet(1L, 150);
        UserAssetInventory inventory = TestFixtures.inventory(20L, 1L, 10L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(TestFixtures.asset(10L, 1L, "wallpaper", 100)));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(false);
        given(pageWalletRepository.findByUserId(1L)).willReturn(Optional.of(wallet));
        given(inventoryRepository.save(any(UserAssetInventory.class))).willReturn(inventory);
        given(assetPurchaseRepository.save(any(AssetPurchase.class))).willAnswer(invocation -> {
            AssetPurchase purchase = invocation.getArgument(0);
            TestFixtures.setId(purchase, 30L);
            return purchase;
        });
        ArgumentCaptor<AssetPurchase> purchaseCaptor = ArgumentCaptor.forClass(AssetPurchase.class);

        var response = assetService.purchase(1L, 10L);

        assertThat(wallet.getBalancePages()).isEqualTo(50);
        assertThat(response.purchaseId()).isEqualTo(30L);
        assertThat(response.inventoryId()).isEqualTo(20L);
        then(assetPurchaseRepository).should().save(purchaseCaptor.capture());
        assertThat(purchaseCaptor.getValue().getPricePages()).isEqualTo(100);
    }

    @Test
    @DisplayName("인벤토리 조회는 현재 사용자 보유 에셋 id만 반환한다")
    void getInventory_returnsAssetIds() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(inventoryRepository.findByUserId(1L)).willReturn(List.of(
                TestFixtures.inventory(1L, 1L, 10L),
                TestFixtures.inventory(2L, 1L, 11L)
        ));

        var response = assetService.getInventory(1L);

        assertThat(response.assetIds()).containsExactly(10L, 11L);
        assertThat(response.items())
                .extracting(item -> item.inventoryId())
                .containsExactly(1L, 2L);
        assertThat(response.items())
                .extracting(item -> item.assetId())
                .containsExactly(10L, 11L);
    }

    @Test
    @DisplayName("단일 에셋 다운로드 manifest는 보유 여부를 검증하고 파일 메타데이터를 반환한다")
    void getDownloadManifest_ownedAsset_returnsFile() {
        Asset asset = Asset.builder()
                .categoryId(1L)
                .code("deep-sea-chair")
                .name("깊은바다 의자")
                .assetScope("SPACE")
                .assetType("CHAIR")
                .collectionCode("DEEP_SEA")
                .assetUrl("https://cdn.test/assets/chair.glb")
                .contentType("model/gltf-binary")
                .fileSize(100L)
                .fileHash("sha256:test")
                .version(3)
                .build();
        TestFixtures.setId(asset, 10L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(asset));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(true);

        var response = assetService.getDownloadManifest(1L, 10L);

        assertThat(response.collectionCode()).isEqualTo("DEEP_SEA");
        assertThat(response.files()).singleElement()
                .satisfies(file -> {
                    assertThat(file.assetId()).isEqualTo(10L);
                    assertThat(file.url()).isEqualTo("https://cdn.test/assets/chair.glb");
                    assertThat(file.version()).isEqualTo(3);
                });
    }

    @Test
    @DisplayName("테마 다운로드 manifest는 child asset을 배치 조회해 파일 목록을 만든다")
    void getDownloadManifest_ownedTheme_returnsChildFilesWithBatchQuery() {
        Asset theme = Asset.builder()
                .categoryId(1L)
                .code("deep-sea-theme")
                .name("깊은바다 테마")
                .assetScope("SPACE")
                .assetType("THEME")
                .collectionCode("DEEP_SEA")
                .build();
        Asset chair = downloadableAsset(10L, "deep-sea-chair", "CHAIR", "https://cdn.test/chair.glb");
        Asset desk = downloadableAsset(11L, "deep-sea-desk", "DESK", "https://cdn.test/desk.glb");
        TestFixtures.setId(theme, 100L);
        ThemeAssetItem chairItem = ThemeAssetItem.builder()
                .themeAssetId(100L)
                .childAssetId(10L)
                .role("CHAIR")
                .sortOrder(1)
                .build();
        ThemeAssetItem deskItem = ThemeAssetItem.builder()
                .themeAssetId(100L)
                .childAssetId(11L)
                .role("DESK")
                .sortOrder(2)
                .build();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(100L)).willReturn(Optional.of(theme));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 100L)).willReturn(true);
        given(themeAssetItemRepository.findByThemeAssetIdOrderBySortOrderAscIdAsc(100L))
                .willReturn(List.of(chairItem, deskItem));
        given(assetRepository.findAllById(List.of(10L, 11L))).willReturn(List.of(chair, desk));

        var response = assetService.getDownloadManifest(1L, 100L);

        assertThat(response.files())
                .extracting(file -> file.code())
                .containsExactly("deep-sea-chair", "deep-sea-desk");
        then(assetRepository).should().findAllById(List.of(10L, 11L));
        then(assetRepository).should(never()).findById(10L);
        then(assetRepository).should(never()).findById(11L);
    }

    @Test
    @DisplayName("다운로드 manifest는 보유하지 않은 에셋이면 ASSET_NOT_OWNED를 던진다")
    void getDownloadManifest_notOwned_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(assetRepository.findById(10L)).willReturn(Optional.of(TestFixtures.asset(10L, 1L, "chair", 0)));
        given(inventoryRepository.existsByUserIdAndAssetId(1L, 10L)).willReturn(false);

        assertThatThrownBy(() -> assetService.getDownloadManifest(1L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.ASSET_NOT_OWNED);
    }

    @Test
    @DisplayName("owned 필터는 인증 사용자 없이 요청하면 UNAUTHORIZED를 던진다")
    void getAssets_ownedFilterWithoutUser_throwsUnauthorized() {
        given(userService.requireUserId(null)).willThrow(new CustomException(ErrorCode.UNAUTHORIZED));

        assertThatThrownBy(() -> assetService.getAssets(null, null, null, null, null, true, "popular", 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.UNAUTHORIZED);

        then(assetRepository).shouldHaveNoInteractions();
    }

    private Asset downloadableAsset(Long id, String code, String assetType, String assetUrl) {
        Asset asset = Asset.builder()
                .categoryId(1L)
                .code(code)
                .name(code)
                .assetScope("SPACE")
                .assetType(assetType)
                .assetUrl(assetUrl)
                .contentType("model/gltf-binary")
                .fileSize(100L)
                .fileHash("sha256:" + code)
                .version(2)
                .build();
        TestFixtures.setId(asset, id);
        return asset;
    }
}
