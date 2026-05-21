package com.dotshelf.domain.asset.entity;

import java.time.LocalDateTime;

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
@Table(name = "asset_purchases")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AssetPurchase extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private Long assetId;

    private Long inventoryId;

    private Long paymentOrderId;

    private Long pageTransactionId;

    @Column(nullable = false)
    private int pricePages;

    @Column(nullable = false, length = 30)
    private String purchaseStatus = "COMPLETED";

    @Column(nullable = false)
    private LocalDateTime purchasedAt = LocalDateTime.now();

    @Builder
    private AssetPurchase(Long userId, Long assetId, Long inventoryId, int pricePages) {
        this.userId = userId;
        this.assetId = assetId;
        this.inventoryId = inventoryId;
        this.pricePages = pricePages;
    }

    public void refund() {
        this.purchaseStatus = "REFUNDED";
    }
}
