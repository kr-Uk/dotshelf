package com.dotshelf.domain.asset.service;

import java.time.Duration;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.repository.AdminAuditLogRepository;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetConfirmRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetCreateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUpdateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlResponse;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;

import lombok.RequiredArgsConstructor;

/**
 * 관리자 콘솔에서 호출하는 에셋 본체(Unity AssetBundle 등) / 프리뷰 이미지 업로드 흐름.
 * 흐름은 양쪽 모두 presigned PUT 2-step:
 *   1) upload-url 발급 — contentType / byteLength 검증 후 S3 PUT URL 반환
 *   2) confirm — S3 객체 존재 확인 후 Asset 엔티티의 해당 컬럼 갱신
 *
 * <p>본체는 대용량 바이너리라 presign TTL 을 길게 쓰고(presign-bundle-ttl), 프리뷰는 일반 TTL.
 */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminAssetService {

    private static final String ROLE_ADMIN = "ADMIN";
    private static final String AUDIT_TARGET_TYPE = "ASSET";
    private static final String AUDIT_ACTION_CREATE = "CREATE_ASSET";
    private static final String AUDIT_ACTION_UPDATE = "UPDATE_ASSET";
    private static final String AUDIT_ACTION_PUBLISH = "PUBLISH_ASSET";
    private static final String AUDIT_ACTION_BUNDLE = "UPDATE_ASSET_BUNDLE";
    private static final String AUDIT_ACTION_PREVIEW_IMAGE = "UPDATE_ASSET_PREVIEW_IMAGE";
    private static final String DEFAULT_ASSET_SCOPE = "SPACE";

    // 본체는 Unity AssetBundle / 압축 번들이 들어온다. content-type 은 클라가 신뢰성 있게 못 정해서
    // octet-stream / zip 정도만 받고, 진짜 검증은 byteLength 와 운영자 검수에 맡긴다.
    private static final long MAX_BUNDLE_SIZE_BYTES = 200L * 1024 * 1024;
    private static final Set<String> ALLOWED_BUNDLE_TYPES = Set.of(
            "application/octet-stream",
            "application/zip",
            "model/gltf-binary");

    private static final long MAX_PREVIEW_IMAGE_SIZE_BYTES = 5L * 1024 * 1024;
    private static final Map<String, String> ALLOWED_PREVIEW_IMAGE_TYPES = Map.of(
            "image/jpeg", "jpg",
            "image/png", "png",
            "image/webp", "webp");

    private final AssetRepository assetRepository;
    private final AssetCategoryRepository assetCategoryRepository;
    private final UserService userService;
    private final AdminAuditLogRepository adminAuditLogRepository;
    private final S3StorageService s3StorageService;
    private final S3StorageProperties s3StorageProperties;

    @Transactional
    public AdminAssetResponse createAsset(Long userId, AdminAssetCreateRequest request, String ipAddress) {
        User actor = requireAdmin(userId);
        if (assetRepository.existsByCode(request.code())) {
            throw new CustomException(ErrorCode.ASSET_CODE_ALREADY_EXISTS);
        }
        AssetCategory category = getCategory(request.categoryCode());
        Asset asset = Asset.builder()
                .categoryId(category.getId())
                .code(request.code())
                .name(request.name())
                .tier(request.tier())
                .assetScope(defaultString(request.assetScope(), DEFAULT_ASSET_SCOPE))
                .assetType(request.assetType())
                .collectionCode(normalizeBlank(request.collectionCode()))
                .pricePages(request.pricePages() == null ? 0 : request.pricePages())
                .creator(request.creator())
                .supplier(request.supplier())
                .compatibleTargets(request.compatibleTargets())
                .allowsCustomization(request.allowsCustomization())
                .isActive(request.active() != null && request.active())
                .build();
        Asset saved = assetRepository.save(asset);
        writeAudit(actor, AUDIT_ACTION_CREATE, saved.getId(), null, saved.getCode(), ipAddress);
        return AdminAssetResponse.from(saved);
    }

    @Transactional
    public AdminAssetResponse updateAsset(Long userId, Long assetId, AdminAssetUpdateRequest request, String ipAddress) {
        User actor = requireAdmin(userId);
        Asset asset = getAsset(assetId);
        String before = asset.getCode();
        Long categoryId = request.categoryCode() == null || request.categoryCode().isBlank()
                ? asset.getCategoryId()
                : getCategory(request.categoryCode()).getId();
        asset.updateDetails(
                categoryId,
                defaultString(request.name(), asset.getName()),
                defaultString(request.tier(), asset.getTier()),
                defaultString(request.assetScope(), asset.getAssetScope()),
                defaultString(request.assetType(), asset.getAssetType()),
                request.collectionCode() == null ? asset.getCollectionCode() : normalizeBlank(request.collectionCode()),
                request.pricePages() == null ? asset.getPricePages() : request.pricePages(),
                request.creator() == null ? asset.getCreator() : request.creator(),
                request.supplier() == null ? asset.getSupplier() : request.supplier(),
                request.compatibleTargets() == null ? asset.getCompatibleTargets() : request.compatibleTargets(),
                request.allowsCustomization() == null ? asset.isAllowsCustomization() : request.allowsCustomization());
        writeAudit(actor, AUDIT_ACTION_UPDATE, asset.getId(), before, asset.getCode(), ipAddress);
        return AdminAssetResponse.from(asset);
    }

    @Transactional
    public AdminAssetResponse publishAsset(Long userId, Long assetId, String ipAddress) {
        User actor = requireAdmin(userId);
        Asset asset = getAsset(assetId);
        if (isBlank(asset.getAssetUrl()) || isBlank(asset.getPreviewImageUrl())) {
            throw new CustomException(ErrorCode.ASSET_PUBLISH_NOT_READY);
        }
        if (asset.isActive()) {
            return AdminAssetResponse.from(asset);
        }
        boolean before = asset.isActive();
        asset.publish();
        writeAudit(actor, AUDIT_ACTION_PUBLISH, asset.getId(), String.valueOf(before), "true", ipAddress);
        return AdminAssetResponse.from(asset);
    }

    public AdminAssetUploadUrlResponse createBundleUploadUrl(Long userId, Long assetId, AdminAssetUploadUrlRequest request) {
        requireAdmin(userId);
        Asset asset = getAsset(assetId);

        if (!ALLOWED_BUNDLE_TYPES.contains(request.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (request.byteLength() > MAX_BUNDLE_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String extension = resolveBundleExtension(request.contentType());
        String key = "assets/" + asset.getCode() + "/bundle/" + UUID.randomUUID() + "." + extension;
        PresignedPut presigned = s3StorageService.presignPut(
                key,
                request.contentType(),
                request.byteLength(),
                Duration.ofSeconds(s3StorageProperties.presignBundleTtlSeconds()));
        return AdminAssetUploadUrlResponse.from(presigned);
    }

    @Transactional
    public AdminAssetResponse confirmBundle(Long userId, Long assetId, AdminAssetConfirmRequest request, String ipAddress) {
        User actor = requireAdmin(userId);
        Asset asset = getAsset(assetId);
        ObjectMetadata metadata = verifyConfirmedObject(
                request.key(),
                "assets/" + asset.getCode() + "/bundle/",
                ALLOWED_BUNDLE_TYPES::contains,
                MAX_BUNDLE_SIZE_BYTES);
        String before = asset.getAssetUrl();
        String after = s3StorageService.publicUrl(request.key());
        asset.updateBundle(after, metadata.contentType(), metadata.contentLength(), request.fileHash(), request.version());
        writeAudit(actor, AUDIT_ACTION_BUNDLE, asset.getId(), before, after, ipAddress);
        return AdminAssetResponse.from(asset);
    }

    public AdminAssetUploadUrlResponse createPreviewImageUploadUrl(Long userId, Long assetId, AdminAssetUploadUrlRequest request) {
        requireAdmin(userId);
        Asset asset = getAsset(assetId);

        if (!ALLOWED_PREVIEW_IMAGE_TYPES.containsKey(request.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (request.byteLength() > MAX_PREVIEW_IMAGE_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String extension = ALLOWED_PREVIEW_IMAGE_TYPES.get(request.contentType());
        String key = "assets/" + asset.getCode() + "/preview/" + UUID.randomUUID() + "." + extension;
        PresignedPut presigned = s3StorageService.presignPut(
                key,
                request.contentType(),
                request.byteLength(),
                Duration.ofSeconds(s3StorageProperties.presignTtlSeconds()));
        return AdminAssetUploadUrlResponse.from(presigned);
    }

    @Transactional
    public AdminAssetResponse confirmPreviewImage(Long userId, Long assetId, AdminAssetConfirmRequest request, String ipAddress) {
        User actor = requireAdmin(userId);
        Asset asset = getAsset(assetId);
        verifyConfirmedObject(
                request.key(),
                "assets/" + asset.getCode() + "/preview/",
                ALLOWED_PREVIEW_IMAGE_TYPES::containsKey,
                MAX_PREVIEW_IMAGE_SIZE_BYTES);
        String before = asset.getPreviewImageUrl();
        String after = s3StorageService.publicUrl(request.key());
        asset.updatePreviewImageUrl(after);
        writeAudit(actor, AUDIT_ACTION_PREVIEW_IMAGE, asset.getId(), before, after, ipAddress);
        return AdminAssetResponse.from(asset);
    }

    /**
     * presigned PUT 의 confirm 단계 공통 검증.
     *   1) key prefix 가 본 도메인 영역인지 (다른 자산/카테고리 객체 confirm 차단)
     *   2) headObject 로 객체 실존 확인
     *   3) S3 가 받은 contentType 이 도메인 화이트리스트에 있는지 (1단계 발급 시 신고 값과 일치 재확인)
     *   4) 실제 contentLength 가 한도 이내인지
     */
    private ObjectMetadata verifyConfirmedObject(
            String key,
            String expectedPrefix,
            java.util.function.Predicate<String> contentTypeAllowed,
            long maxSizeBytes) {
        if (!key.startsWith(expectedPrefix)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        ObjectMetadata metadata = s3StorageService.headObject(key);
        if (!contentTypeAllowed.test(metadata.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (metadata.contentLength() > maxSizeBytes) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        return metadata;
    }

    private Asset getAsset(Long assetId) {
        return assetRepository.findById(assetId)
                .orElseThrow(() -> new CustomException(ErrorCode.ASSET_NOT_FOUND));
    }

    private AssetCategory getCategory(String categoryCode) {
        return assetCategoryRepository.findByCode(categoryCode)
                .orElseThrow(() -> new CustomException(ErrorCode.ASSET_CATEGORY_NOT_FOUND));
    }

    private User requireAdmin(Long userId) {
        User user = userService.getUser(userService.requireUserId(userId));
        if (!ROLE_ADMIN.equalsIgnoreCase(user.getRole())) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return user;
    }

    /**
     * 본체/프리뷰 변경은 운영 영향이 커서 actor / before / after / ip 를 admin_audit_logs 에 기록한다.
     * 잘못된 번들 배포나 오염된 프리뷰 이미지 사고 시 추적이 가능하도록.
     */
    private void writeAudit(User actor, String action, Long assetId, String before, String after, String ipAddress) {
        adminAuditLogRepository.save(AdminAuditLog.builder()
                .actorUserId(actor.getId())
                .action(action)
                .targetType(AUDIT_TARGET_TYPE)
                .targetId(assetId)
                .beforePayload(before)
                .afterPayload(after)
                .ipAddress(ipAddress)
                .build());
    }

    private String resolveBundleExtension(String contentType) {
        if ("application/zip".equals(contentType)) {
            return "zip";
        }
        if ("model/gltf-binary".equals(contentType)) {
            return "glb";
        }
        return "bundle";
    }

    private static String defaultString(String value, String fallback) {
        return value == null || value.isBlank() ? fallback : value;
    }

    private static String normalizeBlank(String value) {
        return value == null || value.isBlank() ? null : value;
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
