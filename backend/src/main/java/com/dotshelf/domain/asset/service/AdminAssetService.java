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
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlResponse;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
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
    private static final String AUDIT_ACTION_BUNDLE = "UPDATE_ASSET_BUNDLE";
    private static final String AUDIT_ACTION_PREVIEW_IMAGE = "UPDATE_ASSET_PREVIEW_IMAGE";

    // 본체는 Unity AssetBundle / 압축 번들이 들어온다. content-type 은 클라가 신뢰성 있게 못 정해서
    // octet-stream / zip 정도만 받고, 진짜 검증은 byteLength 와 운영자 검수에 맡긴다.
    private static final long MAX_BUNDLE_SIZE_BYTES = 200L * 1024 * 1024;
    private static final Set<String> ALLOWED_BUNDLE_TYPES = Set.of(
            "application/octet-stream",
            "application/zip");

    private static final long MAX_PREVIEW_IMAGE_SIZE_BYTES = 5L * 1024 * 1024;
    private static final Map<String, String> ALLOWED_PREVIEW_IMAGE_TYPES = Map.of(
            "image/jpeg", "jpg",
            "image/png", "png",
            "image/webp", "webp");

    private final AssetRepository assetRepository;
    private final UserService userService;
    private final AdminAuditLogRepository adminAuditLogRepository;
    private final S3StorageService s3StorageService;
    private final S3StorageProperties s3StorageProperties;

    public AdminAssetUploadUrlResponse createBundleUploadUrl(Long userId, Long assetId, AdminAssetUploadUrlRequest request) {
        requireAdmin(userId);
        Asset asset = getAsset(assetId);

        if (!ALLOWED_BUNDLE_TYPES.contains(request.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (request.byteLength() > MAX_BUNDLE_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String extension = "application/zip".equals(request.contentType()) ? "zip" : "bundle";
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
        verifyKeyAndExistence(request.key(), "assets/" + asset.getCode() + "/bundle/");
        String before = asset.getAssetUrl();
        String after = s3StorageService.publicUrl(request.key());
        asset.updateAssetUrl(after);
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
        verifyKeyAndExistence(request.key(), "assets/" + asset.getCode() + "/preview/");
        String before = asset.getPreviewImageUrl();
        String after = s3StorageService.publicUrl(request.key());
        asset.updatePreviewImageUrl(after);
        writeAudit(actor, AUDIT_ACTION_PREVIEW_IMAGE, asset.getId(), before, after, ipAddress);
        return AdminAssetResponse.from(asset);
    }

    private void verifyKeyAndExistence(String key, String expectedPrefix) {
        if (!key.startsWith(expectedPrefix)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        if (!s3StorageService.objectExists(key)) {
            throw new CustomException(ErrorCode.STORAGE_OBJECT_NOT_UPLOADED);
        }
    }

    private Asset getAsset(Long assetId) {
        return assetRepository.findById(assetId)
                .orElseThrow(() -> new CustomException(ErrorCode.ASSET_NOT_FOUND));
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
}
