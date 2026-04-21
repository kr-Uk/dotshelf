package com.dotshelf.domain.payment.dto;

import java.util.List;

import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.payment.entity.PageWallet;

import jakarta.validation.constraints.Min;

public final class PaymentDtos {

    private PaymentDtos() {
    }

    public record PageChargeRequest(
            @Min(value = 1, message = "충전 페이지는 1 이상입니다.")
            int pagesAmount,
            @Min(value = 0, message = "결제 금액은 0 이상입니다.")
            int amountKrw,
            String platform
    ) {
    }

    public record WalletResponse(Long userId, int balancePages) {

        public static WalletResponse from(PageWallet wallet) {
            return new WalletResponse(wallet.getUserId(), wallet.getBalancePages());
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
