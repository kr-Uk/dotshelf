package com.dotshelf.domain.asset.controller;

import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCollectionResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetFilterResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetPurchaseResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetTypeResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.DownloadFileResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.DownloadManifestResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryItemResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.ThemeContentsResponse;
import com.dotshelf.domain.asset.service.AssetService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class AssetControllerTest {

    @Mock
    private AssetService assetService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new AssetController(assetService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("에셋 목록은 category, sort, limit, userId를 서비스로 전달한다")
    void getAssets_passesQueryAndCurrentUser() throws Exception {
        given(assetService.getAssets(1L, "wallpaper", "DEEP_SEA", "CHAIR", "의자", true, "recent", 12))
                .willReturn(new AssetListResponse(List.of(assetResponse(10L))));

        mockMvc.perform(get("/v1/assets")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("category", "wallpaper")
                        .param("collection", "DEEP_SEA")
                        .param("type", "CHAIR")
                        .param("q", "의자")
                        .param("owned", "true")
                        .param("sort", "recent")
                        .param("limit", "12"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.result.assets[0].id").value(10));

        then(assetService).should().getAssets(1L, "wallpaper", "DEEP_SEA", "CHAIR", "의자", true, "recent", 12);
    }

    @Test
    @DisplayName("에셋 목록은 기본 sort와 limit을 적용한다")
    void getAssets_usesDefaultQueryValues() throws Exception {
        given(assetService.getAssets(eq(1L), eq(null), eq(null), eq(null), eq(null), eq(null), eq("popular"), anyInt()))
                .willReturn(new AssetListResponse(List.of()));

        mockMvc.perform(get("/v1/assets").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assets").isArray());

        then(assetService).should().getAssets(1L, null, null, null, null, null, "popular", 20);
    }

    @Test
    @DisplayName("에셋 상세는 path assetId와 userId를 전달한다")
    void getAsset_passesAssetIdAndCurrentUser() throws Exception {
        given(assetService.getAsset(1L, 10L)).willReturn(assetResponse(10L));

        mockMvc.perform(get("/v1/assets/10").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.code").value("asset-10"));

        then(assetService).should().getAsset(1L, 10L);
    }

    @Test
    @DisplayName("테마 contents 조회는 assetId를 서비스로 전달한다")
    void getThemeContents_passesAssetId() throws Exception {
        given(assetService.getThemeContents(10L))
                .willReturn(new ThemeContentsResponse(10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L));

        mockMvc.perform(get("/v1/assets/10/contents"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.wallpaper").value(1))
                .andExpect(jsonPath("$.result.bookshelfSkinDefault").value(7));
    }

    @Test
    @DisplayName("카테고리 목록은 응답 envelope로 감싼다")
    void getCategories_wrapsResponse() throws Exception {
        given(assetService.getCategories())
                .willReturn(new AssetCategoryListResponse(List.of(new AssetCategoryResponse(1L, "WALLPAPER", "배경", 1))));

        mockMvc.perform(get("/v1/assets/categories"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.categories[0].code").value("WALLPAPER"));
    }

    @Test
    @DisplayName("필터 목록은 컬렉션과 에셋 타입을 응답 envelope로 감싼다")
    void getFilters_wrapsResponse() throws Exception {
        given(assetService.getFilters())
                .willReturn(new AssetFilterResponse(
                        List.of(new AssetCollectionResponse("DEEP_SEA", "DEEP_SEA")),
                        List.of(new AssetTypeResponse("CHAIR", "CHAIR"))));

        mockMvc.perform(get("/v1/assets/filters"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.collections[0].code").value("DEEP_SEA"))
                .andExpect(jsonPath("$.result.types[0].code").value("CHAIR"));
    }

    @Test
    @DisplayName("내 인벤토리 조회는 current userId를 전달한다")
    void getInventory_passesCurrentUser() throws Exception {
        given(assetService.getInventory(1L)).willReturn(new InventoryResponse(
                List.of(10L, 11L),
                List.of(new InventoryItemResponse(20L, 10L), new InventoryItemResponse(21L, 11L))));

        mockMvc.perform(get("/v1/assets/me/inventory").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetIds[1]").value(11))
                .andExpect(jsonPath("$.result.items[0].inventoryId").value(20))
                .andExpect(jsonPath("$.result.items[0].assetId").value(10));
    }

    @Test
    @DisplayName("위시리스트 추가/삭제는 ToggleResponse를 반환한다")
    void wishlist_toggles() throws Exception {
        given(assetService.wish(1L, 10L)).willReturn(new ToggleResponse(true, 3));
        given(assetService.unwish(1L, 10L)).willReturn(new ToggleResponse(false, 2));

        mockMvc.perform(post("/v1/assets/10/wishlist").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true))
                .andExpect(jsonPath("$.result.count").value(3));

        mockMvc.perform(delete("/v1/assets/10/wishlist").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false))
                .andExpect(jsonPath("$.result.count").value(2));
    }

    @Test
    @DisplayName("구매 요청은 current userId와 assetId를 서비스로 전달한다")
    void purchase_passesUserAndAssetId() throws Exception {
        given(assetService.purchase(1L, 10L)).willReturn(new AssetPurchaseResponse(99L, 10L, 88L, 100));

        mockMvc.perform(post("/v1/assets/10/purchase").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.purchaseId").value(99))
                .andExpect(jsonPath("$.result.inventoryId").value(88));
    }

    @Test
    @DisplayName("다운로드 manifest 조회는 current userId와 assetId를 서비스로 전달한다")
    void getDownloadManifest_passesUserAndAssetId() throws Exception {
        given(assetService.getDownloadManifest(1L, 10L))
                .willReturn(new DownloadManifestResponse(
                        10L,
                        "CHAIR",
                        "DEEP_SEA",
                        2,
                        List.of(new DownloadFileResponse(
                                10L,
                                "chair",
                                "chair",
                                "CHAIR",
                                "CHAIR",
                                "https://cdn.test/chair.glb",
                                "model/gltf-binary",
                                100L,
                                "sha256:test",
                                2,
                                true,
                                null))));

        mockMvc.perform(get("/v1/assets/10/download-manifest").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.files[0].url").value("https://cdn.test/chair.glb"));

        then(assetService).should().getDownloadManifest(1L, 10L);
    }

    private AssetResponse assetResponse(Long id) {
        return new AssetResponse(id, 1L, "asset-" + id, "asset", "COMMON", "WALLPAPER", null, 100, false, false, true);
    }
}
