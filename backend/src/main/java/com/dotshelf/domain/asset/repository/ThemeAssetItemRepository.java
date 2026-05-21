package com.dotshelf.domain.asset.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.asset.entity.ThemeAssetItem;

public interface ThemeAssetItemRepository extends JpaRepository<ThemeAssetItem, Long> {

    List<ThemeAssetItem> findByThemeAssetIdOrderBySortOrderAscIdAsc(Long themeAssetId);
}
