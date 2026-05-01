package com.dotshelf.domain.asset.service;

import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Pageable;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.asset.dto.AssetDtos.AssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetCategoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetPurchaseResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.InventoryResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.ThemeContentsResponse;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.AssetWishlist;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.AssetWishlistRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AssetService {

    private final AssetRepository assetRepository;
    private final AssetCategoryRepository assetCategoryRepository;
    private final UserAssetInventoryRepository inventoryRepository;
    private final AssetWishlistRepository wishlistRepository;
    private final AssetPurchaseRepository assetPurchaseRepository;
    private final PageWalletRepository pageWalletRepository;
    private final UserService userService;

    public AssetListResponse getAssets(Long userId, String category, String sort, int limit) {
        Pageable pageable = PageRequest.of(0, Math.min(limit, 50), resolveSort(sort));
        Long categoryId = null;
        if (category != null && !category.isBlank()) {
            categoryId = assetCategoryRepository.findByCode(category)
                    .map(AssetCategory::getId)
                    .orElse(null);
        }
        Long currentUserId = userId;
        return new AssetListResponse((categoryId == null
                ? assetRepository.findByIsActiveTrue(pageable)
                : assetRepository.findByCategoryIdAndIsActiveTrue(categoryId, pageable)).stream()
                .map(asset -> AssetResponse.from(asset, isOwned(currentUserId, asset.getId()), isWished(currentUserId, asset.getId())))
                .toList());
    }

    public AssetResponse getAsset(Long userId, Long assetId) {
        Asset asset = getAsset(assetId);
        return AssetResponse.from(asset, isOwned(userId, asset.getId()), isWished(userId, asset.getId()));
    }

    /**
     * 전체 테마 자산이 묶고 있는 카테고리별 자식 자산 ID를 반환.
     * compatibleTargets(Lob, JSON) 형식 가정:
     * {"wallpaper": 11, "floor": 12, "desk": 13, "dashboard": 14,
     *  "interiorLight": 15, "exteriorLight": 16, "bookshelfSkinDefault": 17}
     * 미설정 키는 null. 파싱 실패 시 모든 필드 null.
     * (admin-web에서 자산 등록 시 같은 JSON 키 사용 — 변경 시 동기화 필요)
     */
    public ThemeContentsResponse getThemeContents(Long themeAssetId) {
        Asset theme = getAsset(themeAssetId);
        String json = theme.getCompatibleTargets();
        if (json == null || json.isBlank()) {
            return new ThemeContentsResponse(theme.getId(), null, null, null, null, null, null, null);
        }
        try {
            com.fasterxml.jackson.databind.JsonNode node = OBJECT_MAPPER.readTree(json);
            return new ThemeContentsResponse(
                    theme.getId(),
                    asLong(node.get("wallpaper")),
                    asLong(node.get("floor")),
                    asLong(node.get("desk")),
                    asLong(node.get("dashboard")),
                    asLong(node.get("interiorLight")),
                    asLong(node.get("exteriorLight")),
                    asLong(node.get("bookshelfSkinDefault"))
            );
        } catch (Exception ex) {
            return new ThemeContentsResponse(theme.getId(), null, null, null, null, null, null, null);
        }
    }

    private static final com.fasterxml.jackson.databind.ObjectMapper OBJECT_MAPPER =
            new com.fasterxml.jackson.databind.ObjectMapper();

    private static Long asLong(com.fasterxml.jackson.databind.JsonNode node) {
        return (node == null || node.isNull()) ? null : node.asLong();
    }

    public InventoryResponse getInventory(Long userId) {
        return InventoryResponse.from(inventoryRepository.findByUserId(userService.requireUserId(userId)));
    }

    public AssetCategoryListResponse getCategories() {
        return new AssetCategoryListResponse(assetCategoryRepository.findByIsActiveTrueOrderByDisplayOrderAsc().stream()
                .map(AssetCategoryResponse::from)
                .toList());
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

    private boolean isOwned(Long userId, Long assetId) {
        return userId != null && inventoryRepository.existsByUserIdAndAssetId(userId, assetId);
    }

    private boolean isWished(Long userId, Long assetId) {
        return userId != null && wishlistRepository.existsByUserIdAndAssetId(userId, assetId);
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
