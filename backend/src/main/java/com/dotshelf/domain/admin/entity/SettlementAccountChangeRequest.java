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
@Table(name = "settlement_account_change_requests", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SettlementAccountChangeRequest extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false, length = 80)
    private String bankName;

    @Column(nullable = false, length = 80)
    private String accountNumberMasked;

    @Column(nullable = false, length = 80)
    private String accountHolder;

    @Column(nullable = false, length = 30)
    private String status = "REQUESTED";

    @Column(nullable = false)
    private Instant requestedAt = Instant.now();

    private Long reviewedByUserId;

    private Instant reviewedAt;

    @Builder
    private SettlementAccountChangeRequest(Long userId, String bankName, String accountNumberMasked,
                                           String accountHolder, String status, Instant requestedAt) {
        this.userId = userId;
        this.bankName = bankName;
        this.accountNumberMasked = accountNumberMasked;
        this.accountHolder = accountHolder;
        this.status = status == null ? "REQUESTED" : status;
        this.requestedAt = requestedAt == null ? Instant.now() : requestedAt;
    }
}
