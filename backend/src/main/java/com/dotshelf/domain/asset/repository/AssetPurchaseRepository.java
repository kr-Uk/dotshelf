package com.dotshelf.domain.asset.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.AssetPurchase;

public interface AssetPurchaseRepository extends JpaRepository<AssetPurchase, Long> {

    List<AssetPurchase> findByUserIdOrderByPurchasedAtDesc(Long userId);

    long countByUserId(Long userId);
}
