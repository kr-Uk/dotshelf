package com.dotshelf.domain.asset.controller;

import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetConfirmRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetCreateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUpdateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryListResponse;
import com.dotshelf.domain.asset.service.AdminAssetService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Admin Asset", description = "관리자 콘솔 — 에셋 본체(Unity AssetBundle) / 프리뷰 이미지 업로드")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/admin/assets")
public class AdminAssetController {

    private final AdminAssetService adminAssetService;

    @Operation(
            summary = "에셋 목록 조회",
            description = "운영자 콘솔에서 사용. draft / 비활성 자산까지 포함하며 collection/type/active/keyword 필터와 커서 기반 페이지네이션을 지원한다."
    )
    @GetMapping
    public ApiResponse<AdminAssetListResponse> searchAssets(
            @CurrentUser Long userId,
            @RequestParam(required = false) String collection,
            @RequestParam(required = false) String type,
            @RequestParam(required = false, name = "q") String keyword,
            @RequestParam(required = false) Boolean active,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminAssetService.searchAssets(userId, collection, type, keyword, active, cursor, limit));
    }

    @Operation(
            summary = "에셋 카테고리 목록 조회",
            description = "운영자 콘솔의 에셋 생성/편집 폼에서 선택할 수 있는 카테고리 옵션을 조회한다. 비활성 카테고리도 포함한다."
    )
    @GetMapping("/categories")
    public ApiResponse<AssetCategoryListResponse> getCategories(@CurrentUser Long userId) {
        return ApiResponse.success(adminAssetService.listCategories(userId));
    }

    @Operation(
            summary = "에셋 상세 조회",
            description = "운영자 콘솔에서 본체/프리뷰 URL 까지 포함된 에셋 상세를 조회한다. 일반 사용자 API 와 달리 비활성 자산도 조회 가능."
    )
    @GetMapping("/{assetId}")
    public ApiResponse<AdminAssetResponse> getAsset(
            @CurrentUser Long userId,
            @PathVariable Long assetId
    ) {
        return ApiResponse.success(adminAssetService.getAssetDetail(userId, assetId));
    }

    @Operation(
            summary = "에셋 메타데이터 생성",
            description = "admin-web에서 에셋 코드/이름/카테고리/컬렉션/가격 등 메타데이터를 먼저 생성한다. 본체와 프리뷰는 별도 presigned upload 흐름으로 채운다."
    )
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public ApiResponse<AdminAssetResponse> createAsset(
            @CurrentUser Long userId,
            @RequestBody @Valid AdminAssetCreateRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.created(adminAssetService.createAsset(userId, request, clientIp(httpRequest)));
    }

    @Operation(
            summary = "에셋 메타데이터 수정",
            description = "admin-web에서 에셋 이름/카테고리/컬렉션/가격 등 본체 파일 외 메타데이터를 수정한다."
    )
    @PatchMapping("/{assetId}")
    public ApiResponse<AdminAssetResponse> updateAsset(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetUpdateRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminAssetService.updateAsset(userId, assetId, request, clientIp(httpRequest)));
    }

    @Operation(
            summary = "에셋 공개",
            description = "에셋 본체와 프리뷰 이미지가 모두 업로드된 에셋을 사용자 카탈로그에 노출한다."
    )
    @PutMapping("/{assetId}/publish")
    public ApiResponse<AdminAssetResponse> publishAsset(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminAssetService.publishAsset(userId, assetId, clientIp(httpRequest)));
    }

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
            @RequestBody @Valid AdminAssetConfirmRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminAssetService.confirmBundle(userId, assetId, request, clientIp(httpRequest)));
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
            @RequestBody @Valid AdminAssetConfirmRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminAssetService.confirmPreviewImage(userId, assetId, request, clientIp(httpRequest)));
    }

    @Operation(
            summary = "저폴리 3D 프리뷰 번들 업로드 URL 발급",
            description = "admin-web 이 본체 업로드 직후 wasm gltfpack 으로 저폴리화한 preview.glb 를 S3 에 직업로드하기 위한 presigned PUT URL 을 발급한다."
    )
    @PostMapping("/{assetId}/preview-bundle/upload-url")
    public ApiResponse<AdminAssetUploadUrlResponse> createPreviewBundleUploadUrl(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetUploadUrlRequest request
    ) {
        return ApiResponse.success(adminAssetService.createPreviewBundleUploadUrl(userId, assetId, request));
    }

    @Operation(
            summary = "저폴리 3D 프리뷰 번들 업로드 확정",
            description = "presigned PUT 으로 S3 직업로드를 마친 뒤 호출. BE 가 객체 존재 / contentType / 크기를 검증하고 Asset.previewBundleUrl 등을 갱신한다."
    )
    @PatchMapping("/{assetId}/preview-bundle")
    public ApiResponse<AdminAssetResponse> confirmPreviewBundle(
            @CurrentUser Long userId,
            @PathVariable Long assetId,
            @RequestBody @Valid AdminAssetConfirmRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminAssetService.confirmPreviewBundle(userId, assetId, request, clientIp(httpRequest)));
    }

    private String clientIp(HttpServletRequest request) {
        String forwardedFor = request.getHeader("X-Forwarded-For");
        if (forwardedFor != null && !forwardedFor.isBlank()) {
            return forwardedFor.split(",")[0].trim();
        }
        String realIp = request.getHeader("X-Real-IP");
        if (realIp != null && !realIp.isBlank()) {
            return realIp.trim();
        }
        return request.getRemoteAddr();
    }
}
