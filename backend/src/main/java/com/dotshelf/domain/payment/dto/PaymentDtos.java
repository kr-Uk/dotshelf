package com.dotshelf.domain.payment.dto;

import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.service.InicisPaymentFormFactory.InicisPaymentForm;

import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public final class PaymentDtos {

    private PaymentDtos() {
    }

    public record PageChargeRequest(
            @Min(value = 1, message = "충전 페이지는 1 이상입니다.")
            int pagesAmount,
            @Min(value = 1, message = "결제 금액은 1 이상입니다.")
            int amountKrw,
            String platform
    ) {
    }

    public record PageChargeOrderRequest(
            @Min(value = 1, message = "충전 페이지는 1 이상입니다.")
            int pagesAmount,
            @Min(value = 1, message = "결제 금액은 1 이상입니다.")
            int amountKrw,
            String platform,
            @NotBlank(message = "구매자 이름은 필수입니다.")
            @Size(max = 30, message = "구매자 이름은 30자 이하여야 합니다.")
            String buyerName,
            @NotBlank(message = "구매자 전화번호는 필수입니다.")
            @Size(max = 20, message = "구매자 전화번호는 20자 이하여야 합니다.")
            @Pattern(regexp = "^[0-9-]+$", message = "구매자 전화번호는 숫자와 '-'만 허용됩니다.")
            String buyerTel,
            @NotBlank(message = "구매자 이메일은 필수입니다.")
            @Size(max = 60, message = "구매자 이메일은 60자 이하여야 합니다.")
            @Email(message = "구매자 이메일 형식이 올바르지 않습니다.")
            String buyerEmail
    ) {
    }

    public record PageChargeOrderResponse(
            String orderId,
            String orderName,
            int pagesAmount,
            int amountKrw,
            String currency,
            String provider,
            String status,
            String paymentPageUrl,
            InicisPaymentFormResponse inicisForm,
            Instant requestedAt,
            Instant expiresAt
    ) {

        public static PageChargeOrderResponse from(PaymentOrder order, String paymentPageUrl, InicisPaymentForm inicisForm) {
            return new PageChargeOrderResponse(
                    order.getOrderId(),
                    order.getOrderName(),
                    order.getPagesAmount(),
                    order.getAmountKrw(),
                    order.getCurrency(),
                    order.getProvider(),
                    order.getStatus(),
                    paymentPageUrl,
                    InicisPaymentFormResponse.from(inicisForm),
                    order.getRequestedAt(),
                    order.getExpiresAt()
            );
        }
    }

    public record InicisPaymentFormResponse(String scriptUrl, Map<String, String> parameters) {

        public static InicisPaymentFormResponse from(InicisPaymentForm form) {
            return new InicisPaymentFormResponse(form.scriptUrl(), form.parameters());
        }
    }

    public record InicisAuthReturnRequest(
            String resultCode,
            String resultMessage,
            String mid,
            String orderId,
            String authToken,
            String idcName,
            String authUrl,
            String netCancelUrl,
            String charset,
            String merchantData
    ) {

        public static InicisAuthReturnRequest from(Map<String, String> parameters) {
            return new InicisAuthReturnRequest(
                    value(parameters, "resultCode"),
                    value(parameters, "resultMsg"),
                    value(parameters, "mid"),
                    value(parameters, "orderNumber"),
                    value(parameters, "authToken"),
                    value(parameters, "idc_name"),
                    value(parameters, "authUrl"),
                    value(parameters, "netCancelUrl"),
                    value(parameters, "charset"),
                    value(parameters, "merchantData")
            );
        }

        private static String value(Map<String, String> parameters, String key) {
            String value = parameters.get(key);
            if (value == null || value.isBlank()) {
                return null;
            }
            return value.trim();
        }
    }

    public record PageChargeConfirmResponse(
            String orderId,
            String providerTransactionId,
            String status,
            int chargedPages,
            int amountKrw,
            WalletResponse wallet
    ) {

        public static PageChargeConfirmResponse from(PaymentOrder order, PageWallet wallet) {
            return new PageChargeConfirmResponse(
                    order.getOrderId(),
                    order.getProviderTransactionId(),
                    order.getStatus(),
                    order.getPagesAmount(),
                    order.getAmountKrw(),
                    WalletResponse.from(wallet)
            );
        }
    }

    public record PaymentOrderStatusResponse(
            String orderId,
            String providerTransactionId,
            String status,
            int pagesAmount,
            int amountKrw,
            WalletResponse wallet
    ) {

        public static PaymentOrderStatusResponse from(PaymentOrder order, PageWallet wallet) {
            return new PaymentOrderStatusResponse(
                    order.getOrderId(),
                    order.getProviderTransactionId(),
                    order.getStatus(),
                    order.getPagesAmount(),
                    order.getAmountKrw(),
                    wallet == null ? null : WalletResponse.from(wallet)
            );
        }
    }

    public record InicisReturnResponse(
            String orderId,
            String status,
            String resultCode,
            String resultMessage,
            WalletResponse wallet
    ) {

        public static InicisReturnResponse from(PaymentOrder order, String resultCode, String resultMessage, PageWallet wallet) {
            return new InicisReturnResponse(
                    order.getOrderId(),
                    order.getStatus(),
                    resultCode,
                    resultMessage,
                    wallet == null ? null : WalletResponse.from(wallet)
            );
        }
    }

    public record InicisPaymentPageResponse(String orderId, String scriptUrl, Map<String, String> parameters) {

        public static InicisPaymentPageResponse from(String orderId, InicisPaymentForm form) {
            return new InicisPaymentPageResponse(orderId, form.scriptUrl(), new LinkedHashMap<>(form.parameters()));
        }
    }

    public record WalletResponse(Long userId, int balancePages, Instant updatedAt) {

        public WalletResponse(Long userId, int balancePages) {
            this(userId, balancePages, null);
        }

        public static WalletResponse from(PageWallet wallet) {
            return new WalletResponse(wallet.getUserId(), wallet.getBalancePages(), wallet.getUpdatedAt());
        }
    }

    public record PurchaseHistoryResponse(Long id, Long assetId, int pricePages, String status) {

        public static PurchaseHistoryResponse from(AssetPurchase purchase) {
            return new PurchaseHistoryResponse(
                    purchase.getId(),
                    purchase.getAssetId(),
                    purchase.getPricePages(),
                    purchase.getPurchaseStatus()
            );
        }
    }

    public record PurchaseHistoryListResponse(List<PurchaseHistoryResponse> purchases) {
    }

    public record PaymentMethodListResponse(List<String> methods) {
    }
}
