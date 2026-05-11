package com.dotshelf.domain.asset.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.asset.entity.Asset;

public interface AssetRepository extends JpaRepository<Asset, Long> {

    Page<Asset> findByIsActiveTrue(Pageable pageable);

    Page<Asset> findByCategoryIdAndIsActiveTrue(Long categoryId, Pageable pageable);

    Optional<Asset> findByCode(String code);

    boolean existsByCode(String code);

    @Query("""
            select asset
            from Asset asset
            where asset.isActive = true
              and (:categoryId is null or asset.categoryId = :categoryId)
              and (:assetType is null or upper(asset.assetType) = upper(:assetType))
              and (:collectionCode is null or upper(asset.collectionCode) = upper(:collectionCode))
              and (
                    :keyword is null
                    or lower(asset.name) like lower(concat('%', :keyword, '%'))
                    or lower(asset.code) like lower(concat('%', :keyword, '%'))
                  )
              and (
                    :owned is null
                    or (:owned = true and exists (
                        select inventory.id
                        from UserAssetInventory inventory
                        where inventory.assetId = asset.id and inventory.userId = :userId
                    ))
                    or (:owned = false and not exists (
                        select inventory.id
                        from UserAssetInventory inventory
                        where inventory.assetId = asset.id and inventory.userId = :userId
                    ))
                  )
            """)
    Page<Asset> searchActive(
            @Param("userId") Long userId,
            @Param("categoryId") Long categoryId,
            @Param("assetType") String assetType,
            @Param("collectionCode") String collectionCode,
            @Param("keyword") String keyword,
            @Param("owned") Boolean owned,
            Pageable pageable);

    @Query("""
            select distinct asset.collectionCode
            from Asset asset
            where asset.isActive = true and asset.collectionCode is not null
            order by asset.collectionCode asc
            """)
    List<String> findActiveCollectionCodes();

    @Query("""
            select distinct asset.assetType
            from Asset asset
            where asset.isActive = true
            order by asset.assetType asc
            """)
    List<String> findActiveAssetTypes();
}
