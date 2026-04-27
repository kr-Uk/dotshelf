package com.dotshelf.domain.asset.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.AssetWishlist;

public interface AssetWishlistRepository extends JpaRepository<AssetWishlist, Long> {

    boolean existsByUserIdAndAssetId(Long userId, Long assetId);

    Optional<AssetWishlist> findByUserIdAndAssetId(Long userId, Long assetId);

    void deleteByUserIdAndAssetId(Long userId, Long assetId);
}
