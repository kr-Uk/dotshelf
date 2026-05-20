package com.dotshelf.domain.space.service;

import java.time.Duration;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.multipart.MultipartFile;

import com.dotshelf.domain.asset.dto.AssetDtos.DownloadManifestResponse;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.asset.service.AssetService;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookSummary;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.space.dto.SpaceDtos.CategoryInventoryRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListItemResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceAssetManifestListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceMutationResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyMode;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailConfirmRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadUrlRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadUrlResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;
import com.dotshelf.domain.space.entity.SpaceLayoutSnapshot;
import com.dotshelf.domain.space.entity.SpaceLike;
import com.dotshelf.domain.space.entity.SpaceVisit;
import com.dotshelf.domain.space.repository.SpaceGuestbookPostItWhitelistRepository;
import com.dotshelf.domain.space.repository.SpaceLayoutSnapshotRepository;
import com.dotshelf.domain.space.repository.SpaceLikeRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceTemplateRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.CursorParser;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.ImageContentValidator;
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Service
@Slf4j
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SpaceService {

    private static final int DEFAULT_LIST_LIMIT = 20;
    private static final int MAX_LIST_LIMIT = 50;
    private static final int MEMORY_SORT_WARNING_THRESHOLD = 500;

    // 카테고리별 PATCH endpoint가 받는 inventory의 Asset.assetType 검증값.
    // admin-web에서 자산 등록 시 같은 문자열 사용해야 함.
    private static final String ASSET_TYPE_WALLPAPER = "WALLPAPER";
    private static final String ASSET_TYPE_FLOOR = "FLOOR";
    private static final String ASSET_TYPE_DESK_SET = "DESK_SET";
    private static final String ASSET_TYPE_BOOKSHELF_SKIN = "BOOKSHELF_SKIN";
    private static final String ASSET_TYPE_DASHBOARD_SKIN = "DASHBOARD_SKIN";
    private static final String ASSET_TYPE_NOTEBOOK = "NOTEBOOK";
    private static final String ASSET_TYPE_INTERIOR_LIGHT = "INTERIOR_LIGHT";
    private static final String ASSET_TYPE_EXTERIOR_LIGHT = "EXTERIOR_LIGHT";

    // Unity 자동 캡처 썸네일은 보통 수백 KB. 2MB 면 2k 해상도 jpg 도 충분히 들어간다.
    private static final long MAX_THUMBNAIL_SIZE_BYTES = 2L * 1024 * 1024;
    private static final Map<String, String> ALLOWED_THUMBNAIL_TYPES = Map.of(
            "image/jpeg", "jpg",
            "image/png", "png",
            "image/webp", "webp");
    private static final AtomicLong LAST_THUMBNAIL_ISSUED_AT_MILLIS = new AtomicLong();
    private static final ObjectMapper OBJECT_MAPPER = new ObjectMapper();

    private final SpaceRepository spaceRepository;
    private final SpaceTemplateRepository spaceTemplateRepository;
    private final SpaceVisitRepository spaceVisitRepository;
    private final SpaceLikeRepository spaceLikeRepository;
    private final SpaceLayoutSnapshotRepository layoutSnapshotRepository;
    private final SpaceGuestbookPostItWhitelistRepository whitelistRepository;
    private final FollowRepository followRepository;
    private final UserBlockRepository userBlockRepository;
    private final UserBookRepository userBookRepository;
    private final UserRepository userRepository;
    private final UserService userService;
    private final AssetService assetService;
    private final AssetRepository assetRepository;
    private final UserAssetInventoryRepository userAssetInventoryRepository;
    private final BookshelfRepository bookshelfRepository;
    private final S3StorageService s3StorageService;
    private final S3StorageProperties s3StorageProperties;

    @Transactional
    public SpaceResponse getMySpace(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    public SpaceResponse getSpace(Long currentUserId, Long spaceId) {
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        return SpaceResponse.from(space, isLiked(spaceId, currentUserId));
    }

    public SpaceAssetManifestListResponse getVisibleAssetManifests(Long currentUserId, Long spaceId) {
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);

        Set<Long> inventoryIds = new HashSet<>(currentInventoryIds(space));
        Set<Long> layoutAssetIds = new HashSet<>();
        Set<String> layoutUnityResourceKeys = new HashSet<>();
        layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(space.getId())
                .ifPresent(snapshot -> collectLayoutAssetReferences(
                        snapshot.getLayoutPayload(),
                        inventoryIds,
                        layoutAssetIds,
                        layoutUnityResourceKeys));

        List<UserAssetInventory> ownerInventories = userAssetInventoryRepository.findByUserId(space.getOwnerUserId());
        Map<Long, UserAssetInventory> ownerInventoriesById = ownerInventories.stream()
                .collect(Collectors.toMap(UserAssetInventory::getId, Function.identity(), (left, right) -> left));
        Set<Long> ownedAssetIds = ownerInventories.stream()
                .map(UserAssetInventory::getAssetId)
                .collect(Collectors.toSet());

        List<VisibleAssetManifestRef> manifestRefs = new ArrayList<>();
        for (Long inventoryId : inventoryIds) {
            UserAssetInventory inventory = ownerInventoriesById.get(inventoryId);
            if (inventory == null) {
                continue;
            }

            manifestRefs.add(new VisibleAssetManifestRef(inventory.getAssetId(), inventory.getId()));
        }
        layoutAssetIds.stream()
                .filter(ownedAssetIds::contains)
                .map(assetId -> new VisibleAssetManifestRef(assetId, null))
                .forEach(manifestRefs::add);
        if (!layoutUnityResourceKeys.isEmpty()) {
            assetRepository.findByUnityResourceKeyIn(layoutUnityResourceKeys).stream()
                    .filter(asset -> ownedAssetIds.contains(asset.getId()))
                    .map(Asset::getId)
                    .map(assetId -> new VisibleAssetManifestRef(assetId, null))
                    .forEach(manifestRefs::add);
        }

        Set<String> seenRefs = new HashSet<>();
        manifestRefs = manifestRefs.stream()
                .filter(ref -> seenRefs.add(ref.assetId() + ":" + ref.inventoryId()))
                .toList();
        if (manifestRefs.isEmpty()) {
            return new SpaceAssetManifestListResponse(space.getId(), space.getLayoutVersion(), List.of());
        }

        List<DownloadManifestResponse> manifests = manifestRefs.stream()
                .map(ref -> assetService.getDownloadManifestForVisibleAsset(ref.assetId())
                        .withInventoryId(ref.inventoryId()))
                .toList();
        return new SpaceAssetManifestListResponse(space.getId(), space.getLayoutVersion(), manifests);
    }

    public LayoutResponse getLayout(Long currentUserId, Long spaceId) {
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        SpaceLayoutSnapshot snapshot = layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(space.getId())
                .orElse(null);
        if (snapshot == null) {
            return new LayoutResponse(space.getId(), space.getLayoutVersion(), "{\"items\":[]}");
        }
        return new LayoutResponse(space.getId(), snapshot.getLayoutVersion(), snapshot.getLayoutPayload());
    }

    public SpaceListResponse getSpaces(Long userId, String keyword, String sort, int limit) {
        int validLimit = validateListLimit(limit);
        String normalizedKeyword = keyword == null ? null : keyword.trim().toLowerCase();
        List<Space> candidates = spaceRepository.findAll().stream()
                .filter(space -> space.getDeletedAt() == null)
                .filter(space -> "PUBLIC".equalsIgnoreCase(space.getVisibilityScope()))
                .toList();
        Map<Long, User> owners = ownersById(candidates);
        List<Space> spaces = candidates.stream()
                .filter(space -> normalizedKeyword == null
                        || normalizedKeyword.isBlank()
                        || ownerNicknameContains(owners.get(space.getOwnerUserId()), normalizedKeyword))
                .sorted(spaceComparator(sort))
                .limit(validLimit)
                .toList();
        return new SpaceListResponse(spaces.stream()
                .map(space -> toListItem(space, owners.get(space.getOwnerUserId()), userId))
                .toList(), null);
    }

    public DashboardResponse getDashboard(Long currentUserId, Long spaceId, String period) {
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        User owner = userService.getUser(space.getOwnerUserId());
        long weeklyVisitorCount = spaceVisitRepository
                .countDistinctVisitorsBySpaceIdSince(spaceId, LocalDate.now().minusDays(7));
        return new DashboardResponse(
                SpaceResponse.from(space, isLiked(spaceId, currentUserId)),
                followRepository.countByFollowingUserId(space.getOwnerUserId()),
                followRepository.countByFollowerUserId(space.getOwnerUserId()),
                space.getBookshelfCount(),
                space.getBookCount(),
                space.getReadingRecordCount(),
                weeklyVisitorCount,
                owner.getNickname(),
                owner.getProfileImageUrl(),
                owner.getIntroduction()
        );
    }

    private boolean isLiked(Long spaceId, Long viewerUserId) {
        return viewerUserId != null
                && spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, viewerUserId);
    }

    private static List<Long> currentInventoryIds(Space space) {
        if (space == null) {
            return List.of();
        }

        List<Long> ids = new ArrayList<>();
        addPositive(ids, space.getCurrentThemeInventoryId());
        addPositive(ids, space.getCurrentWallpaperInventoryId());
        addPositive(ids, space.getCurrentFloorInventoryId());
        addPositive(ids, space.getCurrentDeskInventoryId());
        addPositive(ids, space.getCurrentBookshelfSkinInventoryId());
        addPositive(ids, space.getCurrentDashboardInventoryId());
        addPositive(ids, space.getCurrentNotebookInventoryId());
        addPositive(ids, space.getCurrentInteriorLightInventoryId());
        addPositive(ids, space.getCurrentExteriorLightInventoryId());
        return ids.stream().distinct().toList();
    }

    private record VisibleAssetManifestRef(Long assetId, Long inventoryId) {
    }

    private static void addPositive(List<Long> ids, Long value) {
        if (value != null && value > 0L) {
            ids.add(value);
        }
    }

    private static void addPositive(Set<Long> ids, Long value) {
        if (value != null && value > 0L) {
            ids.add(value);
        }
    }

    public void ensureCanViewSpace(Long currentUserId, Space space) {
        ensureNotBlockedAgainstOwner(currentUserId, space.getOwnerUserId());
        if (!Objects.equals(space.getOwnerUserId(), currentUserId)
                && !"PUBLIC".equalsIgnoreCase(space.getVisibilityScope())) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
    }

    public void ensureCanViewSpace(Long currentUserId, Long spaceId) {
        ensureCanViewSpace(currentUserId, getSpaceEntity(spaceId));
    }

    private static void collectLayoutAssetReferences(
            String layoutPayload,
            Set<Long> inventoryIds,
            Set<Long> assetIds,
            Set<String> unityResourceKeys) {
        if (layoutPayload == null || layoutPayload.isBlank()) {
            return;
        }

        try {
            JsonNode items = OBJECT_MAPPER.readTree(layoutPayload).path("items");
            if (!items.isArray()) {
                return;
            }

            for (JsonNode item : items) {
                String key = textValue(item, "AssetId");
                if (key == null) {
                    key = textValue(item, "assetId");
                }
                collectLayoutAssetReference(key, inventoryIds, assetIds, unityResourceKeys);
            }
        } catch (Exception ex) {
            // Layout payload is user-generated client JSON. A malformed old snapshot
            // must not block space visits; it simply contributes no remote asset refs.
            log.warn("[SpaceLayout] Failed to parse layoutPayload for asset references: {}", ex.getMessage());
        }
    }

    private static String textValue(JsonNode node, String fieldName) {
        JsonNode value = node == null ? null : node.get(fieldName);
        return value == null || value.isNull() ? null : value.asText(null);
    }

    private static void collectLayoutAssetReference(
            String key,
            Set<Long> inventoryIds,
            Set<Long> assetIds,
            Set<String> unityResourceKeys) {
        if (key == null || key.isBlank()) {
            return;
        }
        if (key.startsWith("asset:")) {
            addPositive(assetIds, parsePositiveLong(key.substring("asset:".length())));
            return;
        }
        if (key.startsWith("inv:")) {
            addPositive(inventoryIds, parsePositiveLong(key.substring("inv:".length())));
            return;
        }
        if (key.startsWith("path:")) {
            String unityResourceKey = key.substring("path:".length()).trim();
            if (!unityResourceKey.isBlank()) {
                unityResourceKeys.add(unityResourceKey);
            }
        }
    }

    private static Long parsePositiveLong(String raw) {
        try {
            long value = Long.parseLong(raw);
            return value > 0L ? value : null;
        } catch (NumberFormatException ignored) {
            return null;
        }
    }

    @Transactional
    public ToggleResponse recordVisit(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        if (space.getOwnerUserId().equals(currentUserId)) {
            return new ToggleResponse(false, space.getVisitCount());
        }
        LocalDate today = LocalDate.now();
        if (!spaceVisitRepository.existsBySpaceIdAndVisitorUserIdAndVisitedDate(spaceId, currentUserId, today)) {
            spaceVisitRepository.save(SpaceVisit.builder()
                    .spaceId(spaceId)
                    .visitorUserId(currentUserId)
                    .visitedDate(today)
                    .build());
            space.increaseVisitCount();
        }
        return new ToggleResponse(true, space.getVisitCount());
    }

    @Transactional
    public ToggleResponse like(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        if (!spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, currentUserId)) {
            spaceLikeRepository.save(SpaceLike.builder()
                    .spaceId(spaceId)
                    .userId(currentUserId)
                    .build());
            space.like();
        }
        return new ToggleResponse(true, space.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(currentUserId, space);
        if (spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, currentUserId)) {
            spaceLikeRepository.deleteBySpaceIdAndUserId(spaceId, currentUserId);
            space.unlike();
        }
        return new ToggleResponse(false, space.getLikeCount());
    }

    @Transactional
    public LayoutSaveResponse saveMyLayout(Long userId, LayoutSaveRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        long version = space.nextLayoutVersion();
        layoutSnapshotRepository.save(SpaceLayoutSnapshot.builder()
                .spaceId(space.getId())
                .layoutVersion(version)
                .layoutPayload(request.layoutPayload())
                .savedByUserId(currentUserId)
                .build());
        return new LayoutSaveResponse(space.getId(), version);
    }

    /**
     * Unity 가 방 배치 변경을 감지해 자동으로 캡처한 썸네일을 받아 S3 에 저장하고 공간 thumbnailUrl 갱신.
     * 키에 layoutVersion 을 박아 CDN 캐시 무효화 없이 항상 최신 이미지를 노출한다 — 이전 버전은 라이프사이클 정책으로 정리.
     */
    @Transactional
    public ThumbnailUploadResponse uploadThumbnail(Long userId, MultipartFile file) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);

        String contentType = validateThumbnailFile(file);
        String extension = ALLOWED_THUMBNAIL_TYPES.get(contentType);
        String key = thumbnailUploadObjectKey(space.getId(), extension);

        s3StorageService.putObject(key, file, contentType);
        space.nextLayoutVersion();
        space.updateThumbnail(key, s3StorageService.publicUrl(key));
        return ThumbnailUploadResponse.from(space);
    }

    @Transactional
    public ThumbnailUploadUrlResponse createThumbnailUploadUrl(Long userId, ThumbnailUploadUrlRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);

        String contentType = validateThumbnailUploadRequest(request);
        String extension = ALLOWED_THUMBNAIL_TYPES.get(contentType);
        String key = thumbnailUploadObjectKey(space.getId(), extension);

        PresignedPut presigned = s3StorageService.presignPut(
                key,
                contentType,
                request.byteLength(),
                Duration.ofSeconds(s3StorageProperties.presignTtlSeconds()));
        return ThumbnailUploadUrlResponse.from(presigned, s3StorageService.publicUrl(key));
    }

    @Transactional
    public ThumbnailUploadResponse confirmThumbnail(Long userId, ThumbnailConfirmRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        String key = request != null ? request.key() : null;
        validateThumbnailUploadKey(key, space.getId());
        if (Objects.equals(space.getThumbnailKey(), key)) {
            return ThumbnailUploadResponse.from(space);
        }
        if (isOlderThanCurrentThumbnail(key, space.getThumbnailKey(), space.getId())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }

        verifyConfirmedThumbnailObject(key, space.getId());
        space.nextLayoutVersion();
        space.updateThumbnail(key, s3StorageService.publicUrl(key));
        return ThumbnailUploadResponse.from(space);
    }

    private String validateThumbnailFile(MultipartFile file) {
        if (file == null || file.isEmpty()) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        if (file.getSize() > MAX_THUMBNAIL_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String contentType = file.getContentType();
        if (contentType == null || !ALLOWED_THUMBNAIL_TYPES.containsKey(contentType)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        // 서버 경유 업로드라 파일 본체를 직접 볼 수 있다 — 클라가 image/jpeg 라고 신고해도
        // 실제 헤더가 JPEG/PNG/WebP signature 가 아니면 거부.
        ImageContentValidator.verifyMagicBytes(file, contentType);
        return contentType;
    }

    private String validateThumbnailUploadRequest(ThumbnailUploadUrlRequest request) {
        if (request == null || request.byteLength() == null || request.byteLength() <= 0) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        if (request.byteLength() > MAX_THUMBNAIL_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String contentType = request.contentType();
        if (contentType == null || !ALLOWED_THUMBNAIL_TYPES.containsKey(contentType)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        return contentType;
    }

    private void verifyConfirmedThumbnailObject(String key, Long spaceId) {
        ObjectMetadata metadata = s3StorageService.headObject(key);
        if (!ALLOWED_THUMBNAIL_TYPES.containsKey(metadata.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (metadata.contentLength() <= 0) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        if (metadata.contentLength() > MAX_THUMBNAIL_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        ImageContentValidator.verifyMagicBytes(s3StorageService.readObjectHeadBytes(key, 12), metadata.contentType());
    }

    private static void validateThumbnailUploadKey(String key, Long spaceId) {
        String prefix = thumbnailUploadPrefix(spaceId);
        if (key == null || key.isBlank() || !key.startsWith(prefix)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        String fileName = key.substring(prefix.length());
        int dashIndex = fileName.indexOf('-');
        int dotIndex = fileName.lastIndexOf('.');
        if (dashIndex <= 0 || dotIndex <= dashIndex + 1 || dotIndex == fileName.length() - 1) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        String issuedAtPart = fileName.substring(0, dashIndex);
        String uuidPart = fileName.substring(dashIndex + 1, dotIndex);
        String extension = fileName.substring(dotIndex + 1);
        if (!ALLOWED_THUMBNAIL_TYPES.containsValue(extension)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        try {
            Long.parseLong(issuedAtPart);
            UUID.fromString(uuidPart);
        } catch (IllegalArgumentException e) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
    }

    private static String thumbnailUploadObjectKey(Long spaceId, String extension) {
        return thumbnailUploadPrefix(spaceId) + nextThumbnailIssuedAtMillis() + "-" + UUID.randomUUID() + "." + extension;
    }

    private static String thumbnailUploadPrefix(Long spaceId) {
        return "spaces/" + spaceId + "/thumbnail/";
    }

    private static long nextThumbnailIssuedAtMillis() {
        return LAST_THUMBNAIL_ISSUED_AT_MILLIS.updateAndGet(previous ->
                Math.max(System.currentTimeMillis(), previous + 1));
    }

    private static boolean isOlderThanCurrentThumbnail(String candidateKey, String currentKey, Long spaceId) {
        Long candidateIssuedAt = thumbnailIssuedAtMillisOrNull(candidateKey, spaceId);
        Long currentIssuedAt = thumbnailIssuedAtMillisOrNull(currentKey, spaceId);
        return candidateIssuedAt != null && currentIssuedAt != null && candidateIssuedAt < currentIssuedAt;
    }

    private static Long thumbnailIssuedAtMillisOrNull(String key, Long spaceId) {
        String prefix = thumbnailUploadPrefix(spaceId);
        if (key == null || !key.startsWith(prefix)) {
            return null;
        }
        String fileName = key.substring(prefix.length());
        int dashIndex = fileName.indexOf('-');
        if (dashIndex <= 0) {
            return null;
        }
        try {
            return Long.parseLong(fileName.substring(0, dashIndex));
        } catch (NumberFormatException e) {
            return null;
        }
    }

    @Transactional
    public SpaceResponse applyTheme(Long userId, ThemeApplyRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Long inventoryId = request.inventoryId();
        if (inventoryId != null) {
            verifyOwnedInventoryWithType(inventoryId, currentUserId, "THEME");
        }
        Space space = findOrCreateMySpace(currentUserId);
        space.applyTheme(inventoryId);
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    @Transactional
    public SpaceMutationResponse changeWallpaper(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_WALLPAPER, Space::changeWallpaper);
    }

    @Transactional
    public SpaceMutationResponse changeFloor(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_FLOOR, Space::changeFloor);
    }

    @Transactional
    public SpaceMutationResponse changeDesk(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_DESK_SET, Space::changeDesk);
    }

    @Transactional
    public SpaceMutationResponse changeBookshelfSkin(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_BOOKSHELF_SKIN, Space::changeBookshelfSkin);
    }

    @Transactional
    public SpaceMutationResponse changeDashboard(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_DASHBOARD_SKIN, Space::changeDashboard);
    }

    @Transactional
    public SpaceMutationResponse changeNotebook(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_NOTEBOOK, Space::changeNotebook);
    }

    @Transactional
    public SpaceMutationResponse changeInteriorLight(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_INTERIOR_LIGHT, Space::changeInteriorLight);
    }

    @Transactional
    public SpaceMutationResponse changeExteriorLight(Long userId, CategoryInventoryRequest request) {
        return applyCategoryInventory(userId, request, ASSET_TYPE_EXTERIOR_LIGHT, Space::changeExteriorLight);
    }

    /**
     * POST /v1/spaces/me/theme/{inventoryId}/apply?mode=RESET|KEEP_BLOCKERS_ONLY
     * 전체 테마 적용(모드 명시) — 기존 PATCH /me/theme(mode 없음)과 별개.
     * - RESET: 모든 설치 오브젝트 보관함 이동 + 책장 skin 모두 초기화 후 테마 적용
     * - KEEP_BLOCKERS_ONLY: 테마 기본 배치와 충돌하는 설치 오브젝트만 보관함 이동
     *
     * **현재 KEEP_BLOCKERS_ONLY는 Layout 도메인 통합 미완료라 501 NOT_IMPLEMENTED 반환.**
     * 활성화는 Layout 도메인 통합 + 충돌 layout items 이동 로직 추가 후. 명세서 v1.0
     * 수정 계획 페이지 후속 항목 참고.
     */
    @Transactional
    public SpaceMutationResponse applyThemeWithMode(Long userId, Long inventoryId, ThemeApplyMode mode) {
        if (mode == null) {
            throw new CustomException(ErrorCode.THEME_INVALID_MODE);
        }
        if (mode == ThemeApplyMode.KEEP_BLOCKERS_ONLY) {
            // 현재 RESET과 같은 동작이라 노출 시 클라이언트가 두 모드 차이를 오해할 수 있음.
            // Layout 통합 후 활성화 — 그 전엔 501로 명확히 거절.
            throw new CustomException(ErrorCode.THEME_MODE_NOT_IMPLEMENTED);
        }
        Long currentUserId = userService.requireUserId(userId);
        verifyOwnedInventoryWithType(inventoryId, currentUserId, "THEME");

        Space space = findOrCreateMySpace(currentUserId);
        // RESET 모드: 카테고리 ID + 모든 책장 skin 초기화 (resetCustomization은 layoutVersion++도 처리)
        space.resetCustomization();
        List<Bookshelf> bookshelves = bookshelfRepository.findBySpaceIdOrderByDisplayOrderAsc(space.getId());
        for (Bookshelf b : bookshelves) {
            b.resetSkin();
        }

        space.applyTheme(inventoryId);
        return SpaceMutationResponse.of(space, isLiked(space.getId(), currentUserId), true);
    }

    /**
     * 카테고리별 PATCH 6개 endpoint 공통 로직.
     * 1) inventoryId가 null이 아니면 사용자 보유 검증 + 카테고리/자산 타입 일치 검증
     * 2) 현재 적용된 전체 테마가 customization 불가이면 자동 해제(reset) — themeReset=true
     * 3) 사용자가 요청한 카테고리에 inventoryId set (null이면 unset = 기본 외형)
     */
    private SpaceMutationResponse applyCategoryInventory(
            Long userId,
            CategoryInventoryRequest request,
            String expectedAssetType,
            java.util.function.BiConsumer<Space, Long> setter) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);

        Long invId = request.inventoryId();
        if (invId != null) {
            verifyOwnedInventoryWithType(invId, currentUserId, expectedAssetType);
        }

        boolean themeReset = resetIfThemeLocked(space);
        setter.accept(space, invId);
        return SpaceMutationResponse.of(space, isLiked(space.getId(), currentUserId), themeReset);
    }

    /**
     * inventoryId가 사용자 본인 인벤토리인지 + assetType이 카테고리에 맞는지 확인.
     * 위반 시 THEME_NOT_OWNED 또는 CATEGORY_INVENTORY_TYPE_MISMATCH.
     */
    private void verifyOwnedInventoryWithType(Long inventoryId, Long userId, String expectedAssetType) {
        UserAssetInventory inv = userAssetInventoryRepository.findByIdAndUserId(inventoryId, userId)
                .orElseThrow(() -> new CustomException(ErrorCode.THEME_NOT_OWNED));
        Asset asset = assetRepository.findById(inv.getAssetId())
                .orElseThrow(() -> new CustomException(ErrorCode.THEME_NOT_OWNED));
        if (expectedAssetType != null && !expectedAssetType.equalsIgnoreCase(asset.getAssetType())) {
            throw new CustomException(ErrorCode.CATEGORY_INVENTORY_TYPE_MISMATCH);
        }
    }

    /**
     * 현재 적용된 전체 테마가 `allows_customization == false`이면 모든 카테고리 ID와
     * 책장 skin을 초기화하고 true 반환. 적용 테마가 없거나 customization 허용이면 false.
     * (layout items 보관함 이동은 별도 마디 — Layout 도메인 통합 시점에 추가)
     */
    private boolean resetIfThemeLocked(Space space) {
        Long themeInvId = space.getCurrentThemeInventoryId();
        if (themeInvId == null) return false;

        Asset themeAsset = resolveAssetByInventoryId(themeInvId);
        if (themeAsset == null || themeAsset.isAllowsCustomization()) return false;

        // 잠긴 테마 — Space와 모든 책장 skin을 일괄 초기화
        space.resetCustomization();
        List<Bookshelf> bookshelves = bookshelfRepository.findBySpaceIdOrderByDisplayOrderAsc(space.getId());
        for (Bookshelf b : bookshelves) {
            b.resetSkin();
        }
        return true;
    }

    /**
     * UserAssetInventory.id → Asset 조회. 인벤토리 또는 자산이 없으면 null 반환
     * (호출부에서 null을 "검증 skip"으로 해석).
     */
    private Asset resolveAssetByInventoryId(Long inventoryId) {
        if (inventoryId == null) return null;
        UserAssetInventory inv = userAssetInventoryRepository.findById(inventoryId).orElse(null);
        if (inv == null) return null;
        return assetRepository.findById(inv.getAssetId()).orElse(null);
    }

    @Transactional
    public SpaceResponse updateVisibility(Long userId, VisibilityRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        space.changeVisibility(request.visibilityScope());
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    @Transactional
    public SpaceResponse updateGuestbookVisibility(Long userId, VisibilityRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        space.changeGuestbookVisibility(request.visibilityScope());
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    public SpaceTemplateListResponse getTemplates() {
        return new SpaceTemplateListResponse(spaceTemplateRepository.findByIsActiveTrueOrderByDisplayOrderAsc().stream()
                .map(SpaceTemplateResponse::from)
                .toList());
    }

    @Transactional
    public GuestbookPostItWhitelistResponse getGuestbookPostItWhitelist(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(space.getId()).orElse(null);
        if (whitelist == null) {
            return new GuestbookPostItWhitelistResponse(true, List.of());
        }
        return new GuestbookPostItWhitelistResponse(
                whitelist.isAllowAll(),
                new ArrayList<>(whitelist.getAllowedAssetIds())
        );
    }

    @Transactional
    public GuestbookPostItWhitelistResponse updateGuestbookPostItWhitelist(
            Long userId,
            GuestbookPostItWhitelistRequest request
    ) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        Set<Long> newAssetIds = request.allowedAssetIds() == null
                ? Set.of()
                : new HashSet<>(request.allowedAssetIds());
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(space.getId())
                .orElseGet(() -> SpaceGuestbookPostItWhitelist.builder()
                        .spaceId(space.getId())
                        .allowAll(true)
                        .build());
        whitelist.update(Boolean.TRUE.equals(request.allowAll()), newAssetIds);
        SpaceGuestbookPostItWhitelist saved = whitelistRepository.save(whitelist);
        return new GuestbookPostItWhitelistResponse(
                saved.isAllowAll(),
                new ArrayList<>(saved.getAllowedAssetIds())
        );
    }

    /**
     * 방명록·답장 작성 시 화이트리스트 enforcement에 사용. allowAll=true 또는 명시 허용된 assetId만 통과.
     * S4 stage에서 GuestbookService가 호출.
     *
     * <p>MR #60 #2 — 정책 명시: 화이트리스트 row가 아예 없는 공간은 <b>default-allow</b>로 처리한다.
     * 마이그레이션 V9 적용 직후 기존 공간들은 row가 없을 수 있으므로,
     * 신규 공간(row 미생성)도 기존 공간처럼 모든 포스트잇을 허용해야 한다.
     * 공간 주인이 화이트리스트를 설정/저장한 시점부터만 enforcement가 적용된다.
     */
    public boolean isPostItAllowed(Long spaceId, Long postItInventoryId) {
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(spaceId).orElse(null);
        if (whitelist == null) {
            return true;
        }
        return whitelist.allows(postItInventoryId);
    }

    public Space getSpaceEntity(Long spaceId) {
        Space space = spaceRepository.findById(spaceId)
                .orElseThrow(() -> new CustomException(ErrorCode.SPACE_NOT_FOUND));
        if (space.getDeletedAt() != null) {
            throw new CustomException(ErrorCode.SPACE_NOT_FOUND);
        }
        return space;
    }

    @Transactional
    public void ensureDefaultSpace(Long userId) {
        findOrCreateMySpace(userId);
    }

    public boolean hasActiveBookshelf(Long userId) {
        return spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(userId).stream()
                .anyMatch(space -> bookshelfRepository.countBySpaceIdAndStatus(space.getId(), "ACTIVE") > 0L);
    }

    @Transactional
    public Space findOrCreateMySpace(Long userId) {
        return spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(userId)
                .orElseGet(() -> spaceRepository.save(Space.builder()
                        .ownerUserId(userId)
                        .spaceName("나의 공간")
                        .build()));
    }

    private Map<Long, User> ownersById(List<Space> spaces) {
        List<Long> ownerIds = spaces.stream()
                .map(Space::getOwnerUserId)
                .distinct()
                .toList();
        if (ownerIds.isEmpty()) {
            return Map.of();
        }
        return userRepository.findAllById(ownerIds).stream()
                .collect(Collectors.toMap(User::getId, Function.identity()));
    }

    private boolean ownerNicknameContains(User owner, String normalizedKeyword) {
        return owner != null
                && owner.getNickname() != null
                && owner.getNickname().toLowerCase().contains(normalizedKeyword);
    }

    private SpaceListItemResponse toListItem(Space space, User owner, Long currentUserId) {
        boolean liked = currentUserId != null && spaceLikeRepository.existsBySpaceIdAndUserId(space.getId(), currentUserId);
        boolean following = currentUserId != null
                && !space.getOwnerUserId().equals(currentUserId)
                && followRepository.existsByFollowerUserIdAndFollowingUserId(currentUserId, space.getOwnerUserId());
        return new SpaceListItemResponse(
                space.getId(),
                space.getOwnerUserId(),
                owner == null ? null : owner.getNickname(),
                owner == null ? null : owner.getProfileImageUrl(),
                space.getSpaceName(),
                "공개 서재",
                null,
                space.getThumbnailUrl(),
                space.getVisibilityScope(),
                space.getVisitCount(),
                space.getLikeCount(),
                followRepository.countByFollowingUserId(space.getOwnerUserId()),
                liked,
                following
        );
    }

    private Comparator<Space> spaceComparator(String sort) {
        if ("RECENT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Space::getId, Comparator.reverseOrder());
        }
        if ("VISIT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Space::getVisitCount)
                    .reversed()
                    .thenComparing(Space::getId, Comparator.reverseOrder());
        }
        return Comparator.comparing(Space::getLikeCount)
                .reversed()
                .thenComparing(Space::getVisitCount, Comparator.reverseOrder())
                .thenComparing(Space::getId, Comparator.reverseOrder());
    }

    private int validateListLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_LIST_LIMIT;
        }
        return Math.min(limit, MAX_LIST_LIMIT);
    }

    /**
     * 대시보드 \"보유 책 풀 목록\" 모달용. v1.0 갱신 — 공간 주인의 활성 보유 책을 정렬·페이지네이션해서 반환.
     * <p>구현: 사용자당 보유 책 평균 ~50권 이내 가정으로 메모리 정렬 + 슬라이싱. 1000권 넘으면 server-side ORDER BY로 전환.
     * <p>권한: dashboard 진입 권한이 있는 viewer만 호출 가능 (controller가 진입 시 visibility 체크). 차단 관계만 추가 검증.
     */
    public OwnedBookListResponse getOwnedBooks(Long viewerUserId, Long spaceId, String sort, String cursor, int limit) {
        Space space = getSpaceEntity(spaceId);
        ensureCanViewSpace(viewerUserId, space);

        int validLimit = validateListLimit(limit);
        Long cursorId = CursorParser.parseId(cursor);

        List<OwnedBookSummary> all = userBookRepository.findOwnedBookSummaries(space.getOwnerUserId());
        // (MR #61 리뷰 #5) 메모리 정렬 fallback 한도. 가정이 무너지면 즉시 알 수 있도록 경고.
        // 임계점 도달 시 server-side ORDER BY + cursor 기반 페이징으로 전환 필요.
        if (all.size() > MEMORY_SORT_WARNING_THRESHOLD) {
            log.warn("[OwnedBooks] memory-sort threshold 초과: userId={} count={}. server-side ORDER BY 전환 검토 필요.",
                    space.getOwnerUserId(), all.size());
        }
        List<OwnedBookSummary> sorted = sortOwnedBooks(all, sort);

        int startIdx = 0;
        if (cursorId != null) {
            for (int i = 0; i < sorted.size(); i++) {
                if (cursorId.equals(sorted.get(i).userBookId())) {
                    startIdx = i + 1;
                    break;
                }
            }
        }
        int endIdx = Math.min(startIdx + validLimit, sorted.size());
        boolean hasMore = endIdx < sorted.size();
        String nextCursor = hasMore ? String.valueOf(sorted.get(endIdx - 1).userBookId()) : null;

        return new OwnedBookListResponse(sorted.subList(startIdx, endIdx), nextCursor);
    }

    private void ensureNotBlockedAgainstOwner(Long viewerUserId, Long ownerUserId) {
        if (viewerUserId == null || viewerUserId.equals(ownerUserId)) {
            return;
        }
        if (userBlockRepository.existsBlockBetween(viewerUserId, ownerUserId)) {
            throw new CustomException(ErrorCode.SPACE_ACCESS_BLOCKED);
        }
    }

    private static List<OwnedBookSummary> sortOwnedBooks(List<OwnedBookSummary> items, String sort) {
        Comparator<OwnedBookSummary> primary = switch (sort == null ? "ADDED_DESC" : sort.toUpperCase()) {
            case "PUBLISHED_DESC" ->
                    Comparator.comparing(OwnedBookSummary::publishedDate,
                            Comparator.nullsLast(Comparator.reverseOrder()));
            case "PUBLISHED_ASC" ->
                    Comparator.comparing(OwnedBookSummary::publishedDate,
                            Comparator.nullsLast(Comparator.naturalOrder()));
            case "TITLE_ASC" ->
                    Comparator.comparing(OwnedBookSummary::title,
                            Comparator.nullsLast(Comparator.naturalOrder()));
            default ->
                    Comparator.comparing(OwnedBookSummary::addedAt,
                            Comparator.nullsLast(Comparator.reverseOrder()));
        };
        Comparator<OwnedBookSummary> tiebreaker = Comparator.comparing(
                OwnedBookSummary::userBookId, Comparator.reverseOrder());
        return items.stream().sorted(primary.thenComparing(tiebreaker)).toList();
    }

}
