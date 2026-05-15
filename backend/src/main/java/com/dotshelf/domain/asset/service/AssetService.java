package com.dotshelf.domain.asset.service;

import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Pageable;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.asset.dto.AssetDtos.AssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCollectionResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetFilterResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetPurchaseResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetTypeResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.DownloadFileResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.DownloadManifestResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.ThemeContentItemResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.ThemeContentsResponse;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.AssetWishlist;
import com.dotshelf.domain.asset.entity.ThemeAssetItem;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.AssetWishlistRepository;
import com.dotshelf.domain.asset.repository.ThemeAssetItemRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Service
@Slf4j
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AssetService {

    private static final int MAX_ASSET_LIST_SIZE = 50;
    private static final List<String> DEFAULT_BOOKSHELF_SKIN_CODES = List.of(
            "BOOKSHELF_PASTEL_CABIN_SKIN",
            "BOOKSHELF_UNDERWATER_SHELL_SKIN"
    );

    private final AssetRepository assetRepository;
    private final AssetCategoryRepository assetCategoryRepository;
    private final ThemeAssetItemRepository themeAssetItemRepository;
    private final UserAssetInventoryRepository inventoryRepository;
    private final AssetWishlistRepository wishlistRepository;
    private final AssetPurchaseRepository assetPurchaseRepository;
    private final PageWalletRepository pageWalletRepository;
    private final UserService userService;

    // ObjectMapper Bean 자동 등록이 prod profile에서 안 됐던 회귀 (MR #75 review #2 fix → dev 무한 restart).
    // Spring Bean 주입 권고는 결을 따르지만 환경 의존이라 Hotfix로 static 인스턴스 사용. ObjectMapper는
    // thread-safe + immutable 설정이라 static 사용 안전. 별도 정확한 fix(JacksonConfig 명시 등록 또는
    // starter-json 의존성 확인)는 후속 PR.
    private static final ObjectMapper OBJECT_MAPPER = new ObjectMapper();

    public AssetListResponse getAssets(Long userId, String category, String sort, int limit) {
        return getAssets(userId, category, null, null, null, null, sort, limit);
    }

    public AssetListResponse getAssets(
            Long userId,
            String category,
            String collection,
            String type,
            String keyword,
            Boolean owned,
            String sort,
            int limit) {
        Pageable pageable = PageRequest.of(0, Math.min(limit, MAX_ASSET_LIST_SIZE), resolveSort(sort));
        Long categoryId = null;
        if (category != null && !category.isBlank()) {
            categoryId = assetCategoryRepository.findByCode(category)
                    .map(AssetCategory::getId)
                    .orElse(null);
        }
        Long currentUserId = owned == null ? userId : userService.requireUserId(userId);
        return new AssetListResponse(assetRepository.searchActive(
                        currentUserId,
                        categoryId,
                        normalizeBlank(type),
                        normalizeBlank(collection),
                        normalizeBlank(keyword),
                        owned,
                        pageable).stream()
                .map(asset -> AssetResponse.from(asset, isOwned(currentUserId, asset.getId()), isWished(currentUserId, asset.getId())))
                .toList());
    }

    public AssetResponse getAsset(Long userId, Long assetId) {
        Asset asset = getAsset(assetId);
        return AssetResponse.from(asset, isOwned(userId, asset.getId()), isWished(userId, asset.getId()));
    }

    /**
     * 전체 테마 자산이 묶고 있는 카테고리별 자식 자산 ID를 반환.
     * compatibleTargets(Lob, JSON) 형식 가정 (camelCase 키):
     * {"wallpaper": 11, "floor": 12, "desk": 13, "dashboard": 14,
     *  "interiorLight": 15, "exteriorLight": 16, "bookshelfSkinDefault": 17}
     * 스키마 정의: API 명세서 260502 갱신의 "compatibleTargets JSON 스키마" 섹션.
     * 미설정 키는 null. 파싱 실패 시 모든 필드 null + warn 로그.
     */
    public ThemeContentsResponse getThemeContents(Long themeAssetId) {
        Asset theme = getAsset(themeAssetId);
        List<ThemeContentItemResponse> children = themeContentResponses(themeAssetId);
        String json = theme.getCompatibleTargets();
        if (json == null || json.isBlank()) {
            return emptyThemeContents(theme.getId(), children);
        }
        try {
            JsonNode node = OBJECT_MAPPER.readTree(json);
            return new ThemeContentsResponse(
                    theme.getId(),
                    asLong(node.get("wallpaper")),
                    asLong(node.get("floor")),
                    asLong(node.get("desk")),
                    asLong(node.get("dashboard")),
                    asLong(node.get("interiorLight")),
                    asLong(node.get("exteriorLight")),
                    asLong(node.get("bookshelfSkinDefault")),
                    children
            );
        } catch (Exception ex) {
            // admin-web 자산 등록 UI 합류 전까지 raw DB 입력으로 들어오므로 형식 어긋남이
            // 잠재적. 모든 필드 null로 폴백하되 raw + 예외 함께 남겨 admin이 추적 가능하게.
            log.warn("Theme contents JSON parse failed: assetId={}, raw={}", themeAssetId, json, ex);
            return emptyThemeContents(theme.getId(), children);
        }
    }

    private static ThemeContentsResponse emptyThemeContents(Long themeAssetId, List<ThemeContentItemResponse> children) {
        return new ThemeContentsResponse(themeAssetId, null, null, null, null, null, null, null, children);
    }

    private static Long asLong(JsonNode node) {
        return (node == null || node.isNull()) ? null : node.asLong();
    }

    public InventoryResponse getInventory(Long userId) {
        List<UserAssetInventory> inventories = inventoryRepository.findByUserId(userService.requireUserId(userId));
        if (inventories.isEmpty()) {
            return InventoryResponse.from(inventories);
        }

        List<Long> assetIds = inventories.stream()
                .map(UserAssetInventory::getAssetId)
                .distinct()
                .toList();
        Map<Long, Asset> assetsById = assetRepository.findAllById(assetIds).stream()
                .collect(Collectors.toMap(Asset::getId, Function.identity()));
        return InventoryResponse.from(inventories, assetsById);
    }

    @Transactional
    public void grantDefaultBookshelfSkins(Long userId) {
        if (userId == null) {
            return;
        }

        for (String code : DEFAULT_BOOKSHELF_SKIN_CODES) {
            assetRepository.findByCode(code)
                    .filter(Asset::isActive)
                    .ifPresent(asset -> grantDefaultAssetIfMissing(userId, asset));
        }
    }

    public AssetCategoryListResponse getCategories() {
        return new AssetCategoryListResponse(assetCategoryRepository.findByIsActiveTrueOrderByDisplayOrderAsc().stream()
                .map(AssetCategoryResponse::from)
                .toList());
    }

    public AssetFilterResponse getFilters() {
        return new AssetFilterResponse(
                assetRepository.findActiveCollectionCodes().stream()
                        .map(AssetCollectionResponse::from)
                        .toList(),
                assetRepository.findActiveAssetTypes().stream()
                        .map(AssetTypeResponse::from)
                        .toList()
        );
    }

    public DownloadManifestResponse getDownloadManifest(Long userId, Long assetId) {
        Long currentUserId = userService.requireUserId(userId);
        Asset root = getAsset(assetId);
        if (!inventoryRepository.existsByUserIdAndAssetId(currentUserId, assetId)) {
            throw new CustomException(ErrorCode.ASSET_NOT_OWNED);
        }

        return buildDownloadManifest(root);
    }

    public DownloadManifestResponse getDownloadManifestForVisibleAsset(Long assetId) {
        return buildDownloadManifest(getAsset(assetId));
    }

    private DownloadManifestResponse buildDownloadManifest(Asset root) {
        List<DownloadFileResponse> files;
        if ("THEME".equalsIgnoreCase(root.getAssetType())) {
            files = buildThemeDownloadFiles(root);
        } else {
            ensureDeliverable(root);
            files = isDownloadable(root)
                    ? List.of(DownloadFileResponse.from(root, root.getAssetType(), true, null))
                    : List.of();
        }
        return DownloadManifestResponse.of(root, files);
    }

    @Transactional
    public ToggleResponse wish(Long userId, Long assetId) {
        Long currentUserId = userService.requireUserId(userId);
        getAsset(assetId);
        if (!wishlistRepository.existsByUserIdAndAssetId(currentUserId, assetId)) {
            wishlistRepository.save(AssetWishlist.builder()
                    .userId(currentUserId)
                    .assetId(assetId)
                    .build());
        }
        return new ToggleResponse(true, 0);
    }

    @Transactional
    public ToggleResponse unwish(Long userId, Long assetId) {
        Long currentUserId = userService.requireUserId(userId);
        wishlistRepository.deleteByUserIdAndAssetId(currentUserId, assetId);
        return new ToggleResponse(false, 0);
    }

    @Transactional
    public AssetPurchaseResponse purchase(Long userId, Long assetId) {
        Long currentUserId = userService.requireUserId(userId);
        Asset asset = getAsset(assetId);
        if (inventoryRepository.existsByUserIdAndAssetId(currentUserId, assetId)) {
            throw new CustomException(ErrorCode.ASSET_ALREADY_OWNED);
        }
        PageWallet wallet = pageWalletRepository.findByUserId(currentUserId)
                .orElseGet(() -> pageWalletRepository.save(PageWallet.builder()
                        .userId(currentUserId)
                        .balancePages(0)
                        .build()));
        if (wallet.getBalancePages() < asset.getPricePages()) {
            throw new CustomException(ErrorCode.INSUFFICIENT_PAGES);
        }
        wallet.use(asset.getPricePages());
        UserAssetInventory inventory = inventoryRepository.save(UserAssetInventory.builder()
                .userId(currentUserId)
                .assetId(assetId)
                .grantType("PURCHASE")
                .build());
        AssetPurchase purchase = assetPurchaseRepository.save(AssetPurchase.builder()
                .userId(currentUserId)
                .assetId(assetId)
                .inventoryId(inventory.getId())
                .pricePages(asset.getPricePages())
                .build());
        return AssetPurchaseResponse.from(purchase);
    }

    public Asset getAsset(Long assetId) {
        return assetRepository.findById(assetId)
                .orElseThrow(() -> new CustomException(ErrorCode.ASSET_NOT_FOUND));
    }

    private List<DownloadFileResponse> buildThemeDownloadFiles(Asset theme) {
        List<ThemeAssetItem> items = themeAssetItems(theme.getId());
        if (items.isEmpty()) {
            ensureDeliverable(theme);
            return isDownloadable(theme)
                    ? List.of(DownloadFileResponse.from(theme, "THEME", true, null))
                    : List.of();
        }
        Map<Long, Asset> childrenById = childAssetsById(items);
        return items.stream()
                .map(item -> {
                    Asset child = childAsset(childrenById, item);
                    ensureDeliverable(child);
                    return isDownloadable(child)
                            ? DownloadFileResponse.from(child, item.getRole(), item.isRequired(), item.getTransformJson())
                            : null;
                })
                .filter(Objects::nonNull)
                .toList();
    }

    private List<ThemeContentItemResponse> themeContentResponses(Long themeAssetId) {
        List<ThemeAssetItem> items = themeAssetItems(themeAssetId);
        if (items.isEmpty()) {
            return List.of();
        }
        Map<Long, Asset> childrenById = childAssetsById(items);
        return items.stream()
                .map(item -> ThemeContentItemResponse.from(item, childAsset(childrenById, item)))
                .toList();
    }

    private Map<Long, Asset> childAssetsById(List<ThemeAssetItem> items) {
        List<Long> childIds = items.stream()
                .map(ThemeAssetItem::getChildAssetId)
                .distinct()
                .toList();
        return assetRepository.findAllById(childIds).stream()
                .collect(Collectors.toMap(Asset::getId, Function.identity()));
    }

    private Asset childAsset(Map<Long, Asset> childrenById, ThemeAssetItem item) {
        Asset child = childrenById.get(item.getChildAssetId());
        if (child == null) {
            throw new CustomException(ErrorCode.ASSET_NOT_FOUND);
        }
        return child;
    }

    private List<ThemeAssetItem> themeAssetItems(Long themeAssetId) {
        List<ThemeAssetItem> items = themeAssetItemRepository.findByThemeAssetIdOrderBySortOrderAscIdAsc(themeAssetId);
        return items == null ? List.of() : items;
    }

    private void ensureDeliverable(Asset asset) {
        if (!isDownloadable(asset) && !hasUnityResourceKey(asset)) {
            throw new CustomException(ErrorCode.ASSET_FILE_NOT_READY);
        }
    }

    private boolean isDownloadable(Asset asset) {
        return asset != null && asset.getAssetUrl() != null && !asset.getAssetUrl().isBlank();
    }

    private boolean hasUnityResourceKey(Asset asset) {
        return asset != null && asset.getUnityResourceKey() != null && !asset.getUnityResourceKey().isBlank();
    }

    private boolean isOwned(Long userId, Long assetId) {
        return userId != null && inventoryRepository.existsByUserIdAndAssetId(userId, assetId);
    }

    private void grantDefaultAssetIfMissing(Long userId, Asset asset) {
        if (inventoryRepository.existsByUserIdAndAssetId(userId, asset.getId())) {
            return;
        }

        inventoryRepository.save(UserAssetInventory.builder()
                .userId(userId)
                .assetId(asset.getId())
                .grantType("DEFAULT")
                .build());
    }

    private boolean isWished(Long userId, Long assetId) {
        return userId != null && wishlistRepository.existsByUserIdAndAssetId(userId, assetId);
    }

    private static String normalizeBlank(String value) {
        return value == null || value.isBlank() ? null : value;
    }

    private Sort resolveSort(String sort) {
        if ("price".equalsIgnoreCase(sort)) {
            return Sort.by(Sort.Direction.ASC, "pricePages");
        }
        if ("latest".equalsIgnoreCase(sort)) {
            return Sort.by(Sort.Direction.DESC, "createdAt");
        }
        return Sort.by(Sort.Direction.DESC, "id");
    }
}
