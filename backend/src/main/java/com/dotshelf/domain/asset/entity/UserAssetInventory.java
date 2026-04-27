package com.dotshelf.domain.asset.entity;

import java.time.LocalDateTime;

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
        name = "user_asset_inventories",
        uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "asset_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserAssetInventory extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private Long assetId;

    @Column(nullable = false, length = 30)
    private String grantType;

    @Column(nullable = false)
    private LocalDateTime acquiredAt = LocalDateTime.now();

    @Builder
    private UserAssetInventory(Long userId, Long assetId, String grantType) {
        this.userId = userId;
        this.assetId = assetId;
        this.grantType = grantType;
    }
}
