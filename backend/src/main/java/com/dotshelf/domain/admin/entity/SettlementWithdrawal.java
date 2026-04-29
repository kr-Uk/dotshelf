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
@Table(name = "settlement_withdrawals", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SettlementWithdrawal extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long creatorUserId;

    @Column(nullable = false)
    private int requestedPages;

    @Column(nullable = false, length = 30)
    private String status = "REQUESTED";

    @Column(nullable = false)
    private Instant requestedAt = Instant.now();

    private Long reviewedByUserId;

    private Instant reviewedAt;

    @Column(length = 500)
    private String memo;

    @Builder
    private SettlementWithdrawal(Long creatorUserId, int requestedPages, String status, Instant requestedAt, String memo) {
        this.creatorUserId = creatorUserId;
        this.requestedPages = requestedPages;
        this.status = status == null ? "REQUESTED" : status;
        this.requestedAt = requestedAt == null ? Instant.now() : requestedAt;
        this.memo = memo;
    }
}
