package com.dotshelf.domain.asset.controller;

import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetConfirmRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlResponse;
import com.dotshelf.domain.asset.service.AdminAssetService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Admin Asset", description = "관리자 콘솔 — 에셋 본체(Unity AssetBundle) / 프리뷰 이미지 업로드")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/admin/assets")
public class AdminAssetController {

    private final AdminAssetService adminAssetService;

    @Operation(
            summary = "에셋 본체 업로드 URL 발급",
            description = "관리자가 Unity AssetBundle 등 에셋 본체 바이너리를 S3 에 직업로드하기 위한 presigned PUT URL 을 발급한다."
    )
    @PostMapping("/{assetId}/bundle/upload-url")
    public ApiResponse<AdminAssetUploadUrlResponse> createBundleUploadUrl(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetUploadUrlRequest request
    ) {
        return ApiResponse.success(adminAssetService.createBundleUploadUrl(userId, assetId, request));
    }

    @Operation(
            summary = "에셋 본체 업로드 확정",
            description = "presigned PUT 으로 S3 직업로드를 마친 뒤 호출. BE 가 객체 존재를 검증하고 Asset.assetUrl 을 갱신한다."
    )
    @PatchMapping("/{assetId}/bundle")
    public ApiResponse<AdminAssetResponse> confirmBundle(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetConfirmRequest request
    ) {
        return ApiResponse.success(adminAssetService.confirmBundle(userId, assetId, request));
    }

    @Operation(
            summary = "에셋 프리뷰 이미지 업로드 URL 발급",
            description = "관리자가 상점 카드/카탈로그용 프리뷰 이미지를 S3 에 직업로드하기 위한 presigned PUT URL 을 발급한다."
    )
    @PostMapping("/{assetId}/preview-image/upload-url")
    public ApiResponse<AdminAssetUploadUrlResponse> createPreviewImageUploadUrl(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetUploadUrlRequest request
    ) {
        return ApiResponse.success(adminAssetService.createPreviewImageUploadUrl(userId, assetId, request));
    }

    @Operation(
            summary = "에셋 프리뷰 이미지 업로드 확정",
            description = "presigned PUT 으로 S3 직업로드를 마친 뒤 호출. BE 가 객체 존재를 검증하고 Asset.previewImageUrl 을 갱신한다."
    )
    @PatchMapping("/{assetId}/preview-image")
    public ApiResponse<AdminAssetResponse> confirmPreviewImage(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetConfirmRequest request
    ) {
        return ApiResponse.success(adminAssetService.confirmPreviewImage(userId, assetId, request));
    }
}
