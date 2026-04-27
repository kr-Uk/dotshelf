package com.dotshelf.domain.payment.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.service.PaymentService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Payment", description = "페이지 충전 / 구매 내역 / 결제 수단")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/payments")
public class PaymentController {

    private final PaymentService paymentService;

    @PostMapping("/page-charge")
    public ApiResponse<WalletResponse> charge(
            @CurrentUser Long userId,
            @RequestBody @Valid PageChargeRequest request
    ) {
        return ApiResponse.success(paymentService.charge(userId, request));
    }

    @GetMapping("/purchases")
    public ApiResponse<PurchaseHistoryListResponse> getPurchases(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getPurchases(userId));
    }

    @GetMapping("/wallet")
    public ApiResponse<WalletResponse> getWallet(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getWallet(userId));
    }

    @GetMapping("/methods")
    public ApiResponse<PaymentMethodListResponse> getMethods(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getMethods(userId));
    }
}
