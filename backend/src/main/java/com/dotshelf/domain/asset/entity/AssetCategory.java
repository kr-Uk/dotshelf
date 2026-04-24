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
@Table(name = "asset_categories")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AssetCategory extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    private Long parentCategoryId;

    @Column(nullable = false, unique = true, length = 50)
    private String code;

    @Column(nullable = false, length = 100)
    private String name;

    @Column(nullable = false)
    private int displayOrder = 0;

    @Column(nullable = false)
    private boolean isActive = true;

    @Builder
    private AssetCategory(Long parentCategoryId, String code, String name, int displayOrder) {
        this.parentCategoryId = parentCategoryId;
        this.code = code;
        this.name = name;
        this.displayOrder = displayOrder;
    }
}
