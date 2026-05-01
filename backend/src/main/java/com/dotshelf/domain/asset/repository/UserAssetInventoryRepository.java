package com.dotshelf.domain.asset.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.UserAssetInventory;

public interface UserAssetInventoryRepository extends JpaRepository<UserAssetInventory, Long> {

    boolean existsByUserIdAndAssetId(Long userId, Long assetId);

    List<UserAssetInventory> findByUserId(Long userId);

    Optional<UserAssetInventory> findByUserIdAndAssetId(Long userId, Long assetId);

    Optional<UserAssetInventory> findByIdAndUserId(Long id, Long userId);
}
