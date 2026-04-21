package com.dotshelf.domain.asset.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.asset.dto.AssetDtos.AssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetPurchaseResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryResponse;
import com.dotshelf.domain.asset.service.AssetService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Asset", description = "꾸미기 에셋 카탈로그 / 구매 / 보유")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/assets")
public class AssetController {

    private final AssetService assetService;

    @GetMapping
    public ApiResponse<AssetListResponse> getAssets(
            @CurrentUser Long userId,
            @RequestParam(required = false) String category,
            @RequestParam(defaultValue = "popular") String sort,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(assetService.getAssets(userId, category, sort, limit));
    }

    @GetMapping("/{assetId}")
    public ApiResponse<AssetResponse> getAsset(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.getAsset(userId, assetId));
    }

    @GetMapping("/me/inventory")
    public ApiResponse<InventoryResponse> getInventory(@CurrentUser Long userId) {
        return ApiResponse.success(assetService.getInventory(userId));
    }

    @PostMapping("/{assetId}/wishlist")
    public ApiResponse<ToggleResponse> wish(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.wish(userId, assetId));
    }

    @DeleteMapping("/{assetId}/wishlist")
    public ApiResponse<ToggleResponse> unwish(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.unwish(userId, assetId));
    }

    @PostMapping("/{assetId}/purchase")
    public ApiResponse<AssetPurchaseResponse> purchase(@CurrentUser Long userId, @PathVariable Long assetId) {
        return ApiResponse.success(assetService.purchase(userId, assetId));
    }
}
