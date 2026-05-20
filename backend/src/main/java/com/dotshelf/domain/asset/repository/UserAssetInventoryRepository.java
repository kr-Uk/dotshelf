package com.dotshelf.domain.asset.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.asset.entity.UserAssetInventory;

public interface UserAssetInventoryRepository extends JpaRepository<UserAssetInventory, Long> {

    boolean existsByUserIdAndAssetId(Long userId, Long assetId);

    List<UserAssetInventory> findByUserId(Long userId);

    Optional<UserAssetInventory> findByUserIdAndAssetId(Long userId, Long assetId);

    Optional<UserAssetInventory> findByIdAndUserId(Long id, Long userId);

    @Modifying
    @Query(value = """
            INSERT INTO user_asset_inventories (
                created_at,
                updated_at,
                user_id,
                asset_id,
                grant_type,
                acquired_at
            )
            VALUES (
                now(),
                now(),
                :userId,
                :assetId,
                'DEFAULT',
                now()
            )
            ON CONFLICT (user_id, asset_id) DO NOTHING
            """, nativeQuery = true)
    int insertDefaultGrantIfAbsent(@Param("userId") Long userId, @Param("assetId") Long assetId);
}
