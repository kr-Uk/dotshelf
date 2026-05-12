package com.dotshelf.domain.asset.dto;

import java.time.Instant;
import java.util.List;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.ThemeAssetItem;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.PositiveOrZero;
import jakarta.validation.constraints.Size;

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
            String collectionCode,
            String previewImageUrl,
            Long fileSize,
            String fileHash,
            String contentType,
            int version,
            int pricePages,
            boolean owned,
            boolean wished,
            boolean allowsCustomization
    ) {

        public AssetResponse(
                Long id,
                Long categoryId,
                String code,
                String name,
                String tier,
                String assetType,
                String previewImageUrl,
                int pricePages,
                boolean owned,
                boolean wished,
                boolean allowsCustomization) {
            this(id, categoryId, code, name, tier, assetType, null, previewImageUrl,
                    null, null, null, 1, pricePages, owned, wished, allowsCustomization);
        }

        public static AssetResponse from(Asset asset, boolean owned, boolean wished) {
            return new AssetResponse(
                    asset.getId(),
                    asset.getCategoryId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getTier(),
                    asset.getAssetType(),
                    asset.getCollectionCode(),
                    asset.getPreviewImageUrl(),
                    asset.getFileSize(),
                    asset.getFileHash(),
                    asset.getContentType(),
                    asset.getVersion(),
                    asset.getPricePages(),
                    owned,
                    wished,
                    asset.isAllowsCustomization()
            );
        }
    }

    public record AssetListResponse(List<AssetResponse> assets) {
    }

    /**
     * 전체 테마 자산이 묶고 있는 자식 자산 ID 매핑.
     * Asset.compatibleTargets (Lob, JSON) 에 저장된 children에서 카테고리별로 파싱.
     * 미설정 키는 null. Frontend는 이 응답을 받아 카테고리별 카탈로그에 매칭.
     */
    public record ThemeContentsResponse(
            Long themeAssetId,
            Long wallpaper,
            Long floor,
            Long desk,
            Long dashboard,
            Long interiorLight,
            Long exteriorLight,
            Long bookshelfSkinDefault,
            List<ThemeContentItemResponse> children
    ) {

        public ThemeContentsResponse(
                Long themeAssetId,
                Long wallpaper,
                Long floor,
                Long desk,
                Long dashboard,
                Long interiorLight,
                Long exteriorLight,
                Long bookshelfSkinDefault) {
            this(themeAssetId, wallpaper, floor, desk, dashboard, interiorLight, exteriorLight, bookshelfSkinDefault, List.of());
        }
    }

    public record ThemeContentItemResponse(
            Long assetId,
            String assetType,
            String role,
            boolean required,
            String defaultTransform
    ) {

        public static ThemeContentItemResponse from(ThemeAssetItem item, Asset child) {
            return new ThemeContentItemResponse(
                    child.getId(),
                    child.getAssetType(),
                    item.getRole(),
                    item.isRequired(),
                    item.getTransformJson()
            );
        }
    }

    public record AssetCategoryResponse(
            Long categoryId,
            String code,
            String name,
            int displayOrder
    ) {

        public static AssetCategoryResponse from(AssetCategory category) {
            return new AssetCategoryResponse(
                    category.getId(),
                    category.getCode(),
                    category.getName(),
                    category.getDisplayOrder()
            );
        }
    }

    public record AssetCategoryListResponse(List<AssetCategoryResponse> categories) {
    }

    public record AssetFilterResponse(
            List<AssetCollectionResponse> collections,
            List<AssetTypeResponse> types
    ) {
    }

    public record AssetCollectionResponse(String code, String name) {

        public static AssetCollectionResponse from(String code) {
            return new AssetCollectionResponse(code, code);
        }
    }

    public record AssetTypeResponse(String code, String name) {

        public static AssetTypeResponse from(String code) {
            return new AssetTypeResponse(code, code);
        }
    }

    public record InventoryResponse(List<Long> assetIds, List<InventoryItemResponse> items) {

        public InventoryResponse(List<Long> assetIds) {
            this(assetIds, List.of());
        }

        public static InventoryResponse from(List<UserAssetInventory> inventories) {
            return new InventoryResponse(
                    inventories.stream()
                            .map(UserAssetInventory::getAssetId)
                            .toList(),
                    inventories.stream()
                            .map(InventoryItemResponse::from)
                            .toList()
            );
        }
    }

    public record InventoryItemResponse(Long inventoryId, Long assetId) {

        public static InventoryItemResponse from(UserAssetInventory inventory) {
            return new InventoryItemResponse(
                    inventory.getId(),
                    inventory.getAssetId()
            );
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

    public record DownloadManifestResponse(
            Long assetId,
            String assetType,
            String collectionCode,
            int version,
            List<DownloadFileResponse> files
    ) {

        public static DownloadManifestResponse of(Asset root, List<DownloadFileResponse> files) {
            return new DownloadManifestResponse(
                    root.getId(),
                    root.getAssetType(),
                    root.getCollectionCode(),
                    root.getVersion(),
                    files
            );
        }
    }

    public record DownloadFileResponse(
            Long assetId,
            String code,
            String name,
            String assetType,
            String role,
            String url,
            String contentType,
            Long fileSize,
            String fileHash,
            int version,
            boolean required,
            String defaultTransform
    ) {

        public static DownloadFileResponse from(Asset asset, String role, boolean required, String defaultTransform) {
            return new DownloadFileResponse(
                    asset.getId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getAssetType(),
                    role,
                    asset.getAssetUrl(),
                    asset.getContentType(),
                    asset.getFileSize(),
                    asset.getFileHash(),
                    asset.getVersion(),
                    required,
                    defaultTransform
            );
        }
    }

    public record AdminAssetCreateRequest(
            @NotBlank(message = "code는 필수입니다.")
            @Size(max = 80, message = "code는 80자 이하여야 합니다.")
            String code,
            @NotBlank(message = "name은 필수입니다.")
            @Size(max = 100, message = "name은 100자 이하여야 합니다.")
            String name,
            @NotBlank(message = "categoryCode는 필수입니다.")
            String categoryCode,
            @NotBlank(message = "assetType은 필수입니다.")
            String assetType,
            String collectionCode,
            String tier,
            String assetScope,
            @PositiveOrZero(message = "pricePages는 0 이상이어야 합니다.")
            Integer pricePages,
            String creator,
            String supplier,
            String compatibleTargets,
            Boolean allowsCustomization,
            Boolean active
    ) {
    }

    public record AdminAssetUpdateRequest(
            @Size(max = 100, message = "name은 100자 이하여야 합니다.")
            String name,
            String categoryCode,
            String assetType,
            String collectionCode,
            String tier,
            String assetScope,
            @PositiveOrZero(message = "pricePages는 0 이상이어야 합니다.")
            Integer pricePages,
            String creator,
            String supplier,
            String compatibleTargets,
            Boolean allowsCustomization
    ) {
    }

    /**
     * 관리자가 에셋 본체(Unity AssetBundle 등) 또는 프리뷰 이미지를 S3 에 직업로드하기 위해
     * 발급받는 presigned PUT 요청. 본체와 프리뷰 모두 같은 형식을 공유한다.
     */
    public record AdminAssetUploadUrlRequest(
            @NotBlank(message = "contentType은 필수입니다.")
            String contentType,
            @NotNull(message = "byteLength는 필수입니다.")
            @Positive(message = "byteLength는 양수여야 합니다.")
            Long byteLength
    ) {
    }

    public record AdminAssetUploadUrlResponse(
            String uploadUrl,
            String key,
            long expiresInSeconds
    ) {

        public static AdminAssetUploadUrlResponse from(PresignedPut presigned) {
            return new AdminAssetUploadUrlResponse(
                    presigned.uploadUrl(),
                    presigned.key(),
                    presigned.expiresInSeconds());
        }
    }

    public record AdminAssetConfirmRequest(
            @NotBlank(message = "key는 필수입니다.")
            String key,
            String fileHash,
            @Min(value = 1, message = "version은 1 이상이어야 합니다.")
            Integer version
    ) {

        public AdminAssetConfirmRequest(String key) {
            this(key, null, null);
        }
    }

    /**
     * 관리자 콘솔에서 본체/프리뷰 갱신 후 받는 응답. 일반 사용자용 AssetResponse 와 달리
     * assetUrl(본체) 까지 노출한다 — 관리자가 검증용으로 직접 클릭해 확인하기 위함.
     */
    public record AdminAssetResponse(
            Long id,
            String code,
            String name,
            String assetType,
            String collectionCode,
            String assetUrl,
            String previewImageUrl,
            String previewBundleUrl,
            String previewBundleContentType,
            Long previewBundleSize,
            String previewBundleHash,
            String contentType,
            Long fileSize,
            String fileHash,
            int version,
            boolean active
    ) {

        public static AdminAssetResponse from(Asset asset) {
            return new AdminAssetResponse(
                    asset.getId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getAssetType(),
                    asset.getCollectionCode(),
                    asset.getAssetUrl(),
                    asset.getPreviewImageUrl(),
                    asset.getPreviewBundleUrl(),
                    asset.getPreviewBundleContentType(),
                    asset.getPreviewBundleSize(),
                    asset.getPreviewBundleHash(),
                    asset.getContentType(),
                    asset.getFileSize(),
                    asset.getFileHash(),
                    asset.getVersion(),
                    asset.isActive()
            );
        }
    }

    /**
     * 운영자 콘솔 리스트 행에 표시할 요약. 본체/프리뷰 URL 자체는 노출 안 하고
     * 업로드 완료 여부(hasAssetUrl / hasPreviewImage) 플래그만 내려서 publish 가능
     * 여부만 한눈에 확인하게 한다. 상세 진입 시 AdminAssetResponse 로 전체 URL 확인.
     */
    public record AdminAssetListItemResponse(
            Long id,
            String code,
            String name,
            String assetType,
            String collectionCode,
            String tier,
            int version,
            int pricePages,
            boolean hasAssetUrl,
            boolean hasPreviewImage,
            boolean hasPreviewBundle,
            boolean active,
            Instant updatedAt
    ) {

        public static AdminAssetListItemResponse from(Asset asset) {
            return new AdminAssetListItemResponse(
                    asset.getId(),
                    asset.getCode(),
                    asset.getName(),
                    asset.getAssetType(),
                    asset.getCollectionCode(),
                    asset.getTier(),
                    asset.getVersion(),
                    asset.getPricePages(),
                    asset.getAssetUrl() != null && !asset.getAssetUrl().isBlank(),
                    asset.getPreviewImageUrl() != null && !asset.getPreviewImageUrl().isBlank(),
                    asset.getPreviewBundleUrl() != null && !asset.getPreviewBundleUrl().isBlank(),
                    asset.isActive(),
                    asset.getUpdatedAt()
            );
        }
    }

    public record AdminAssetListResponse(
            List<AdminAssetListItemResponse> items,
            String nextCursor,
            long totalCount
    ) {
    }
}
