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

    // 같은 named parameter 를 `:x is null` 가드와 `upper(:x)` 비교에 동시에 쓰면
    // Hibernate 7 의 SQM 가 parameter 의 SQL type 을 byte[] (bytea) 로 잘못 추론하는 회귀가 있어
    // PostgreSQL 가 `function upper(bytea) does not exist` 로 SQL grammar error 를 던지며 500 으로 죽는다.
    // 모든 String 파라미터에 `cast(:x as string)` 을 명시해 type hint 를 강제한다.
    // 신규 가입 직후 BookshelfApiSyncController.RefreshBookshelfSkinCatalog 가 이 endpoint 를 호출하는데,
    // 500 이 떨어지면 클라 init chain 일부가 깨져 빈 화면 회귀로 이어진 사례가 있다 (#114 후속).
    @Query("""
            select asset
            from Asset asset
            where asset.isActive = true
              and (:categoryId is null or asset.categoryId = :categoryId)
              and (cast(:assetType as string) is null or upper(asset.assetType) = upper(cast(:assetType as string)))
              and (cast(:collectionCode as string) is null or upper(asset.collectionCode) = upper(cast(:collectionCode as string)))
              and (
                    cast(:keyword as string) is null
                    or lower(asset.name) like lower(concat('%', cast(:keyword as string), '%'))
                    or lower(asset.code) like lower(concat('%', cast(:keyword as string), '%'))
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

    /**
     * 운영자 콘솔 전용 검색. draft/비활성 자산까지 포함하므로 active 파라미터로
     * 명시적으로 필터링한다. 사용자용 searchActive 와 달리 owned/userId 의존이 없다.
     *
     * <p>searchActive 와 동일한 이유로 모든 String 파라미터에 cast(:x as string) 을 명시한다 —
     * Hibernate 7 의 SQM 이 parameter SQL type 을 bytea 로 잘못 추론해 PostgreSQL 이
     * `function upper(bytea) does not exist` 로 500 을 던지는 회귀를 막는다.
     */
    @Query("""
            select asset
            from Asset asset
            where (:active is null or asset.isActive = :active)
              and (cast(:assetType as string) is null or upper(asset.assetType) = upper(cast(:assetType as string)))
              and (cast(:collectionCode as string) is null or upper(asset.collectionCode) = upper(cast(:collectionCode as string)))
              and (
                    cast(:keyword as string) is null
                    or lower(asset.name) like lower(concat('%', cast(:keyword as string), '%'))
                    or lower(asset.code) like lower(concat('%', cast(:keyword as string), '%'))
                  )
            """)
    Page<Asset> searchForAdmin(
            @Param("active") Boolean active,
            @Param("assetType") String assetType,
            @Param("collectionCode") String collectionCode,
            @Param("keyword") String keyword,
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
