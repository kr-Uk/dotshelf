package com.dotshelf.domain.admin.entity;

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
@Table(name = "settlement_adjustments", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SettlementAdjustment extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private int deltaPages;

    @Column(nullable = false, length = 80)
    private String reason;

    @Column(length = 500)
    private String memo;

    @Column(nullable = false)
    private Long adjustedByUserId;

    @Builder
    private SettlementAdjustment(Long userId, int deltaPages, String reason, String memo, Long adjustedByUserId) {
        this.userId = userId;
        this.deltaPages = deltaPages;
        this.reason = reason;
        this.memo = memo;
        this.adjustedByUserId = adjustedByUserId;
    }
}
