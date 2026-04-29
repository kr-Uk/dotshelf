package com.dotshelf.domain.admin.entity;

import java.time.Instant;

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
@Table(name = "settlement_payout_batches", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SettlementPayoutBatch extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true, length = 80)
    private String batchCode;

    @Column(nullable = false)
    private int totalPages = 0;

    @Column(nullable = false)
    private int withdrawalCount = 0;

    @Column(nullable = false, length = 30)
    private String status = "READY";

    private Instant scheduledAt;

    private Instant completedAt;

    @Builder
    private SettlementPayoutBatch(String batchCode, int totalPages, int withdrawalCount,
                                  String status, Instant scheduledAt, Instant completedAt) {
        this.batchCode = batchCode;
        this.totalPages = totalPages;
        this.withdrawalCount = withdrawalCount;
        this.status = status == null ? "READY" : status;
        this.scheduledAt = scheduledAt;
        this.completedAt = completedAt;
    }
}
