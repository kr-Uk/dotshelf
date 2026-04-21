package com.dotshelf.domain.asset.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(
        name = "asset_wishlists",
        uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "asset_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AssetWishlist extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private Long assetId;

    @Builder
    private AssetWishlist(Long userId, Long assetId) {
        this.userId = userId;
        this.assetId = assetId;
    }
}
