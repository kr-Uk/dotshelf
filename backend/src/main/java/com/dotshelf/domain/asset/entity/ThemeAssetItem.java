package com.dotshelf.domain.asset.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "theme_asset_items")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ThemeAssetItem extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long themeAssetId;

    @Column(nullable = false)
    private Long childAssetId;

    @Column(nullable = false, length = 80)
    private String role;

    @Column(nullable = false)
    private boolean isRequired = true;

    @Column(columnDefinition = "TEXT")
    private String transformJson;

    @Column(nullable = false)
    private int sortOrder = 0;

    @Builder
    private ThemeAssetItem(
            Long themeAssetId,
            Long childAssetId,
            String role,
            Boolean isRequired,
            String transformJson,
            Integer sortOrder) {
        this.themeAssetId = themeAssetId;
        this.childAssetId = childAssetId;
        this.role = role;
        this.isRequired = isRequired == null || isRequired;
        this.transformJson = transformJson;
        this.sortOrder = sortOrder == null ? 0 : sortOrder;
    }
}
