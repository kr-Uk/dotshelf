package com.dotshelf.domain.asset.repository;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.Asset;

public interface AssetRepository extends JpaRepository<Asset, Long> {

    Page<Asset> findByIsActiveTrue(Pageable pageable);

    Page<Asset> findByCategoryIdAndIsActiveTrue(Long categoryId, Pageable pageable);
}
