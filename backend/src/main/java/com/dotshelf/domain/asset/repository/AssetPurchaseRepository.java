package com.dotshelf.domain.asset.repository;

import java.time.LocalDateTime;
import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.asset.entity.AssetPurchase;

import jakarta.persistence.LockModeType;

public interface AssetPurchaseRepository extends JpaRepository<AssetPurchase, Long> {

    List<AssetPurchase> findByUserIdOrderByPurchasedAtDesc(Long userId);

    Page<AssetPurchase> findByUserIdOrderByPurchasedAtDesc(Long userId, Pageable pageable);

    Optional<AssetPurchase> findByIdAndUserId(Long id, Long userId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("""
            select purchase
            from AssetPurchase purchase
            where purchase.id = :id and purchase.userId = :userId
            """)
    Optional<AssetPurchase> findByIdAndUserIdForUpdate(@Param("id") Long id, @Param("userId") Long userId);

    long countByUserId(Long userId);

    @Query("select coalesce(sum(purchase.pricePages), 0) from AssetPurchase purchase")
    long sumPricePages();

    @Query("""
            select coalesce(sum(purchase.pricePages), 0)
            from AssetPurchase purchase
            where purchase.purchasedAt >= :start and purchase.purchasedAt < :end
            """)
    long sumPricePagesByPurchasedAtBetween(
            @Param("start") LocalDateTime start,
            @Param("end") LocalDateTime end
    );

    @Query("""
            select coalesce(category.code, 'UNKNOWN') as category,
                   coalesce(sum(purchase.pricePages), 0) as pages
            from AssetPurchase purchase
            left join Asset asset on asset.id = purchase.assetId
            left join AssetCategory category on category.id = asset.categoryId
            group by category.code
            """)
    List<CategoryRevenueRow> sumPricePagesByCategory();

    @Query("""
            select purchase.userId as userId,
                   count(purchase) as count,
                   coalesce(sum(purchase.pricePages), 0) as pages
            from AssetPurchase purchase
            where purchase.userId in :userIds
            group by purchase.userId
            """)
    List<PurchaseSummaryRow> summarizeByUserIds(@Param("userIds") Collection<Long> userIds);

    interface CategoryRevenueRow {

        String getCategory();

        long getPages();
    }

    interface PurchaseSummaryRow {

        Long getUserId();

        long getCount();

        long getPages();
    }
}
