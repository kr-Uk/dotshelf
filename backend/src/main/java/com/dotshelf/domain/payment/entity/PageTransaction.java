package com.dotshelf.domain.payment.entity;

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
@Table(name = "page_transactions")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PageTransaction extends BaseEntity {

    public static final String TYPE_CHARGE = "CHARGE";

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    private Long paymentOrderId;

    @Column(nullable = false, length = 30)
    private String type;

    @Column(nullable = false)
    private int pagesDelta;

    @Column(nullable = false)
    private int balanceAfter;

    @Column(length = 100)
    private String reason;

    @Column(length = 50)
    private String referenceType;

    private Long referenceId;

    @Builder
    private PageTransaction(Long userId, Long paymentOrderId, String type, int pagesDelta, int balanceAfter,
                            String reason, String referenceType, Long referenceId) {
        this.userId = userId;
        this.paymentOrderId = paymentOrderId;
        this.type = type;
        this.pagesDelta = pagesDelta;
        this.balanceAfter = balanceAfter;
        this.reason = reason;
        this.referenceType = referenceType;
        this.referenceId = referenceId;
    }

    public static PageTransaction charge(Long userId, Long paymentOrderId, int pagesAmount, int balanceAfter) {
        return PageTransaction.builder()
                .userId(userId)
                .paymentOrderId(paymentOrderId)
                .type(TYPE_CHARGE)
                .pagesDelta(pagesAmount)
                .balanceAfter(balanceAfter)
                .reason("INICIS_PAYMENT")
                .referenceType("PAYMENT_ORDER")
                .referenceId(paymentOrderId)
                .build();
    }
}
