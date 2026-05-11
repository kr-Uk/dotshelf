package com.dotshelf.domain.asset.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.Duration;
import java.util.Optional;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.repository.AdminAuditLogRepository;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetCreateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetConfirmRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUploadUrlRequest;
import com.dotshelf.domain.asset.entity.Asset;
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
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class AdminAssetServiceTest {

    @Mock
    private AssetRepository assetRepository;

    @Mock
    private AssetCategoryRepository assetCategoryRepository;

    @Mock
    private UserService userService;

    @Mock
    private AdminAuditLogRepository adminAuditLogRepository;

    @Mock
    private S3StorageService s3StorageService;

    private AdminAssetService adminAssetService;

    @BeforeEach
    void setUp() {
        adminAssetService = new AdminAssetService(
                assetRepository,
                assetCategoryRepository,
                userService,
                adminAuditLogRepository,
                s3StorageService,
                new S3StorageProperties("bucket", "ap-northeast-2", "", "", "https://cdn.test", 300, 1800));
    }

    @Test
    @DisplayName("에셋 생성은 관리자 권한, 코드 중복, 카테고리를 검증하고 비공개 에셋을 만든다")
    void createAsset_validRequest_createsDraftAsset() {
        User admin = TestFixtures.user(1L, "admin");
        admin.changeRole("ADMIN");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userService.getUser(1L)).willReturn(admin);
        given(assetRepository.existsByCode("DEEP_SEA_CHAIR_001")).willReturn(false);
        given(assetCategoryRepository.findByCode("FURNITURE"))
                .willReturn(Optional.of(TestFixtures.assetCategory("FURNITURE")));
        given(assetRepository.save(any(Asset.class))).willAnswer(invocation -> {
            Asset asset = invocation.getArgument(0);
            TestFixtures.setId(asset, 20L);
            return asset;
        });
        ArgumentCaptor<Asset> assetCaptor = ArgumentCaptor.forClass(Asset.class);

        var response = adminAssetService.createAsset(
                1L,
                new AdminAssetCreateRequest(
                        "DEEP_SEA_CHAIR_001",
                        "깊은바다 의자",
                        "FURNITURE",
                        "CHAIR",
                        "DEEP_SEA",
                        "NORMAL",
                        null,
                        300,
                        "DotShelf",
                        "DotShelf Studio",
                        null,
                        true,
                        false),
                "127.0.0.1");

        assertThat(response.id()).isEqualTo(20L);
        assertThat(response.collectionCode()).isEqualTo("DEEP_SEA");
        assertThat(response.active()).isFalse();
        then(assetRepository).should().save(assetCaptor.capture());
        assertThat(assetCaptor.getValue().getAssetScope()).isEqualTo("SPACE");
        assertThat(assetCaptor.getValue().getPricePages()).isEqualTo(300);
    }

    @Test
    @DisplayName("번들 업로드 URL은 관리자와 에셋을 검증하고 긴 TTL의 presigned PUT을 발급한다")
    void createBundleUploadUrl_validZip_returnsPresignedUrl() {
        Asset asset = assetWithUrls();
        givenAdminAndAsset(asset);
        given(s3StorageService.presignPut(
                anyString(),
                eq("application/zip"),
                eq(1024L),
                eq(Duration.ofSeconds(1800))))
                .willAnswer(invocation -> new PresignedPut("https://upload.test/bundle", invocation.getArgument(0), 1800));

        var response = adminAssetService.createBundleUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("application/zip", 1024L));

        assertThat(response.uploadUrl()).isEqualTo("https://upload.test/bundle");
        assertThat(response.key()).startsWith("assets/asset-code/bundle/");
        assertThat(response.key()).endsWith(".zip");
        assertThat(response.expiresInSeconds()).isEqualTo(1800L);
    }

    @Test
    @DisplayName("GLB 본체 업로드 URL은 model/gltf-binary를 허용하고 glb 확장자를 사용한다")
    void createBundleUploadUrl_validGlb_returnsGlbKey() {
        givenAdminAndAsset(assetWithUrls());
        given(s3StorageService.presignPut(
                anyString(),
                eq("model/gltf-binary"),
                eq(2048L),
                eq(Duration.ofSeconds(1800))))
                .willAnswer(invocation -> new PresignedPut("https://upload.test/glb", invocation.getArgument(0), 1800));

        var response = adminAssetService.createBundleUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("model/gltf-binary", 2048L));

        assertThat(response.key()).startsWith("assets/asset-code/bundle/");
        assertThat(response.key()).endsWith(".glb");
    }

    @Test
    @DisplayName("번들 업로드 URL은 허용되지 않은 contentType이면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void createBundleUploadUrl_invalidContentType_throwsInvalidContentType() {
        givenAdminAndAsset(assetWithUrls());

        assertThatThrownBy(() -> adminAssetService.createBundleUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("text/plain", 1024L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("번들 업로드 URL은 최대 크기를 넘으면 STORAGE_FILE_TOO_LARGE를 던진다")
    void createBundleUploadUrl_tooLarge_throwsFileTooLarge() {
        givenAdminAndAsset(assetWithUrls());

        assertThatThrownBy(() -> adminAssetService.createBundleUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("application/octet-stream", 200L * 1024 * 1024 + 1)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_FILE_TOO_LARGE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프리뷰 이미지 업로드 URL은 이미지 확장자와 일반 TTL을 사용한다")
    void createPreviewImageUploadUrl_validWebp_returnsPresignedUrl() {
        givenAdminAndAsset(assetWithUrls());
        given(s3StorageService.presignPut(
                anyString(),
                eq("image/webp"),
                eq(2048L),
                eq(Duration.ofSeconds(300))))
                .willAnswer(invocation -> new PresignedPut("https://upload.test/preview", invocation.getArgument(0), 300));

        var response = adminAssetService.createPreviewImageUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("image/webp", 2048L));

        assertThat(response.uploadUrl()).isEqualTo("https://upload.test/preview");
        assertThat(response.key()).startsWith("assets/asset-code/preview/");
        assertThat(response.key()).endsWith(".webp");
        assertThat(response.expiresInSeconds()).isEqualTo(300L);
    }

    @Test
    @DisplayName("번들 confirm은 key prefix와 S3 metadata를 검증하고 assetUrl 및 감사 로그를 남긴다")
    void confirmBundle_validObject_updatesAssetUrlAndWritesAudit() {
        Asset asset = assetWithUrls();
        givenAdminAndAsset(asset);
        String key = "assets/asset-code/bundle/new.bundle";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("application/octet-stream", 4096L));
        given(s3StorageService.publicUrl(key)).willReturn("https://cdn.test/" + key);
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminAssetService.confirmBundle(
                1L,
                10L,
                new AdminAssetConfirmRequest(key),
                "127.0.0.1");

        assertThat(response.assetUrl()).isEqualTo("https://cdn.test/" + key);
        assertThat(asset.getAssetUrl()).isEqualTo("https://cdn.test/" + key);
        assertThat(asset.getContentType()).isEqualTo("application/octet-stream");
        assertThat(asset.getFileSize()).isEqualTo(4096L);
        assertThat(asset.getVersion()).isEqualTo(2);
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getActorUserId()).isEqualTo(1L);
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("UPDATE_ASSET_BUNDLE");
        assertThat(auditCaptor.getValue().getTargetType()).isEqualTo("ASSET");
        assertThat(auditCaptor.getValue().getTargetId()).isEqualTo(10L);
        assertThat(auditCaptor.getValue().getBeforePayload()).isEqualTo("https://old.test/bundle");
        assertThat(auditCaptor.getValue().getAfterPayload()).isEqualTo("https://cdn.test/" + key);
        assertThat(auditCaptor.getValue().getIpAddress()).isEqualTo("127.0.0.1");
    }

    @Test
    @DisplayName("프리뷰 confirm은 다른 asset prefix의 key면 STORAGE_INVALID_KEY를 던진다")
    void confirmPreviewImage_wrongPrefix_throwsInvalidKey() {
        givenAdminAndAsset(assetWithUrls());

        assertThatThrownBy(() -> adminAssetService.confirmPreviewImage(
                1L,
                10L,
                new AdminAssetConfirmRequest("assets/other/preview/a.png"),
                "127.0.0.1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_KEY);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프리뷰 confirm은 S3 contentType이 이미지 화이트리스트 밖이면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void confirmPreviewImage_invalidContentType_throwsInvalidContentType() {
        givenAdminAndAsset(assetWithUrls());
        String key = "assets/asset-code/preview/a.png";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("text/plain", 10L));

        assertThatThrownBy(() -> adminAssetService.confirmPreviewImage(
                1L,
                10L,
                new AdminAssetConfirmRequest(key),
                "127.0.0.1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프리뷰 confirm은 S3 객체 크기가 한도를 넘으면 STORAGE_FILE_TOO_LARGE를 던진다")
    void confirmPreviewImage_tooLarge_throwsFileTooLarge() {
        givenAdminAndAsset(assetWithUrls());
        String key = "assets/asset-code/preview/a.png";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/png", 5L * 1024 * 1024 + 1));

        assertThatThrownBy(() -> adminAssetService.confirmPreviewImage(
                1L,
                10L,
                new AdminAssetConfirmRequest(key),
                "127.0.0.1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_FILE_TOO_LARGE);

        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("관리자가 아니면 에셋 조회 전에 FORBIDDEN을 던진다")
    void createPreviewImageUploadUrl_nonAdmin_throwsForbidden() {
        User user = TestFixtures.user(1L, "member");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userService.getUser(1L)).willReturn(user);

        assertThatThrownBy(() -> adminAssetService.createPreviewImageUploadUrl(
                1L,
                10L,
                new AdminAssetUploadUrlRequest("image/png", 100L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);

        then(assetRepository).shouldHaveNoInteractions();
    }

    private void givenAdminAndAsset(Asset asset) {
        User admin = TestFixtures.user(1L, "admin");
        admin.changeRole("ADMIN");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userService.getUser(1L)).willReturn(admin);
        given(assetRepository.findById(10L)).willReturn(Optional.of(asset));
    }

    private Asset assetWithUrls() {
        Asset asset = Asset.builder()
                .categoryId(1L)
                .code("asset-code")
                .name("asset name")
                .assetScope("SPACE")
                .assetType("WALLPAPER")
                .assetUrl("https://old.test/bundle")
                .previewImageUrl("https://old.test/preview.png")
                .pricePages(100)
                .build();
        TestFixtures.setId(asset, 10L);
        return asset;
    }
}
