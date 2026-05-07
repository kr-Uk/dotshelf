package com.dotshelf.domain.asset.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.asset.dto.AssetDtos.AssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetPurchaseResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.ThemeContentsResponse;
import com.dotshelf.domain.asset.service.AssetService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Asset", description = "꾸미기 에셋 카탈로그 / 구매 / 보유")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/assets")
public class AssetController {

    private final AssetService assetService;

    @Operation(summary = "에셋 목록 조회", description = "카테고리, 정렬 조건, 개수 제한으로 구매 가능한 꾸미기 에셋 목록을 조회합니다.")
    @GetMapping
    public ApiResponse<AssetListResponse> getAssets(
            @CurrentUser Long userId,
            @RequestParam(required = false) String category,
            @RequestParam(defaultValue = "popular") String sort,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(assetService.getAssets(userId, category, sort, limit));
    }

    @Operation(summary = "에셋 상세 조회", description = "에셋 ID로 가격, 보유 여부, 위시리스트 여부 등 에셋 상세 정보를 조회합니다.")
    @GetMapping("/{assetId}")
    public ApiResponse<AssetResponse> getAsset(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.getAsset(userId, assetId));
    }

    /**
     * 전체 테마 자산이 묶고 있는 카테고리별 자식 자산 ID. Frontend가 테마 preview/apply 시
     * 카테고리별 카탈로그에 매칭하는 용도.
     */
    @Operation(summary = "테마 구성 에셋 조회", description = "전체 테마 에셋에 포함된 카테고리별 자식 에셋 ID 목록을 조회합니다.")
    @GetMapping("/{assetId}/contents")
    public ApiResponse<ThemeContentsResponse> getThemeContents(@PathVariable Long assetId) {
        return ApiResponse.success(assetService.getThemeContents(assetId));
    }

    @Operation(summary = "에셋 카테고리 조회", description = "카탈로그 필터와 공간 꾸미기에서 사용할 에셋 카테고리 목록을 조회합니다.")
    @GetMapping("/categories")
    public ApiResponse<AssetCategoryListResponse> getCategories() {
        return ApiResponse.success(assetService.getCategories());
    }

    @Operation(summary = "내 인벤토리 조회", description = "현재 사용자가 구매해 보유 중인 에셋 인벤토리를 조회합니다.")
    @GetMapping("/me/inventory")
    public ApiResponse<InventoryResponse> getInventory(@CurrentUser Long userId) {
        return ApiResponse.success(assetService.getInventory(userId));
    }

    @Operation(summary = "에셋 위시리스트 추가", description = "현재 사용자의 위시리스트에 지정한 에셋을 추가하고 현재 토글 상태를 반환합니다.")
    @PostMapping("/{assetId}/wishlist")
    public ApiResponse<ToggleResponse> wish(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.wish(userId, assetId));
    }

    @Operation(summary = "에셋 위시리스트 삭제", description = "현재 사용자의 위시리스트에서 지정한 에셋을 제거하고 현재 토글 상태를 반환합니다.")
    @DeleteMapping("/{assetId}/wishlist")
    public ApiResponse<ToggleResponse> unwish(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.unwish(userId, assetId));
    }

    @Operation(summary = "에셋 구매", description = "현재 사용자의 페이지 잔액으로 에셋을 구매하고 구매 결과를 반환합니다.")
    @PostMapping("/{assetId}/purchase")
    public ApiResponse<AssetPurchaseResponse> purchase(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.purchase(userId, assetId));
    }
}
