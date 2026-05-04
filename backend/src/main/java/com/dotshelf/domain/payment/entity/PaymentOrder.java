package com.dotshelf.domain.payment.entity;

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
@Table(name = "payment_orders")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PaymentOrder extends BaseEntity {

    public static final String PROVIDER_INICIS = "INICIS";
    public static final String STATUS_READY = "READY";
    public static final String STATUS_AUTHENTICATED = "AUTHENTICATED";
    public static final String STATUS_DONE = "DONE";
    public static final String STATUS_FAILED = "FAILED";
    public static final String STATUS_CANCELED = "CANCELED";

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false, unique = true, length = 40)
    private String orderId;

    @Column(unique = true, length = 40)
    private String providerTransactionId;

    @Column(nullable = false, length = 20)
    private String provider = PROVIDER_INICIS;

    @Column(nullable = false, length = 30)
    private String status = STATUS_READY;

    @Column(nullable = false, length = 100)
    private String orderName;

    @Column(nullable = false)
    private int pagesAmount;

    @Column(nullable = false)
    private int amountKrw;

    @Column(nullable = false, length = 3)
    private String currency = "KRW";

    @Column(length = 30)
    private String buyerName;

    @Column(length = 20)
    private String buyerTel;

    @Column(length = 60)
    private String buyerEmail;

    @Column(length = 500)
    private String authToken;

    @Column(length = 500)
    private String authUrl;

    @Column(length = 500)
    private String netCancelUrl;

    @Column(length = 20)
    private String idcName;

    @Column(length = 50)
    private String payMethod;

    @Column(nullable = false)
    private Instant requestedAt;

    @Column(nullable = false)
    private Instant expiresAt;

    private Instant approvedAt;

    private Instant authenticatedAt;

    private Instant failedAt;

    private Instant canceledAt;

    @Column(length = 100)
    private String failureCode;

    @Column(length = 500)
    private String failureMessage;

    @Builder
    private PaymentOrder(Long userId, String orderId, String orderName, int pagesAmount, int amountKrw,
                         String buyerName, String buyerTel, String buyerEmail, Instant requestedAt, Instant expiresAt) {
        this.userId = userId;
        this.orderId = orderId;
        this.orderName = orderName;
        this.pagesAmount = pagesAmount;
        this.amountKrw = amountKrw;
        this.buyerName = buyerName;
        this.buyerTel = buyerTel;
        this.buyerEmail = buyerEmail;
        this.requestedAt = requestedAt == null ? Instant.now() : requestedAt;
        this.expiresAt = expiresAt;
    }

    public boolean belongsTo(Long targetUserId) {
        return userId != null && userId.equals(targetUserId);
    }

    public boolean isReady() {
        return STATUS_READY.equals(status);
    }

    public boolean isAuthenticated() {
        return STATUS_AUTHENTICATED.equals(status);
    }

    public boolean isDone() {
        return STATUS_DONE.equals(status);
    }

    public boolean isExpired(Instant now) {
        return expiresAt != null && now != null && expiresAt.isBefore(now);
    }

    public boolean hasSameProviderTransaction(String targetProviderTransactionId, int targetAmountKrw) {
        return providerTransactionId != null && providerTransactionId.equals(targetProviderTransactionId)
                && amountKrw == targetAmountKrw;
    }

    public void authenticate(String receivedAuthToken, String receivedAuthUrl, String receivedNetCancelUrl,
                             String receivedIdcName, Instant authenticatedAt) {
        this.authToken = receivedAuthToken;
        this.authUrl = receivedAuthUrl;
        this.netCancelUrl = receivedNetCancelUrl;
        this.idcName = receivedIdcName;
        this.status = STATUS_AUTHENTICATED;
        this.authenticatedAt = authenticatedAt == null ? Instant.now() : authenticatedAt;
    }

    public void complete(String confirmedProviderTransactionId, String confirmedPayMethod, Instant confirmedApprovedAt) {
        this.providerTransactionId = confirmedProviderTransactionId;
        this.payMethod = confirmedPayMethod;
        this.status = STATUS_DONE;
        this.approvedAt = confirmedApprovedAt == null ? Instant.now() : confirmedApprovedAt;
        this.failureCode = null;
        this.failureMessage = null;
    }

    public void cancel(String code, String message, Instant canceledAt) {
        this.status = STATUS_CANCELED;
        this.canceledAt = canceledAt == null ? Instant.now() : canceledAt;
        this.failureCode = trimToLength(code, 100);
        this.failureMessage = trimToLength(message, 500);
    }

    public void fail(String code, String message, Instant failedAt) {
        this.status = STATUS_FAILED;
        this.failedAt = failedAt == null ? Instant.now() : failedAt;
        this.failureCode = trimToLength(code, 100);
        this.failureMessage = trimToLength(message, 500);
    }

    private String trimToLength(String value, int maxLength) {
        if (value == null) {
            return null;
        }
        String text = value.trim();
        if (text.length() <= maxLength) {
            return text;
        }
        return text.substring(0, maxLength);
    }
}
