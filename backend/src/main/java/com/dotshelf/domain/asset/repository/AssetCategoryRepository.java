package com.dotshelf.domain.asset.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.AssetCategory;

public interface AssetCategoryRepository extends JpaRepository<AssetCategory, Long> {

    Optional<AssetCategory> findByCode(String code);
}
