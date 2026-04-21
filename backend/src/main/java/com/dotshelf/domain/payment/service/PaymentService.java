package com.dotshelf.domain.payment.service;

import java.util.List;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.user.service.UserService;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PaymentService {

    private final PageWalletRepository pageWalletRepository;
    private final AssetPurchaseRepository assetPurchaseRepository;
    private final UserService userService;

    @Transactional
    public WalletResponse charge(Long userId, PageChargeRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        PageWallet wallet = pageWalletRepository.findByUserId(currentUserId)
                .orElseGet(() -> pageWalletRepository.save(PageWallet.builder()
                        .userId(currentUserId)
                        .balancePages(0)
                        .build()));
        wallet.charge(request.pagesAmount());
        return WalletResponse.from(wallet);
    }

    public PurchaseHistoryListResponse getPurchases(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new PurchaseHistoryListResponse(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDesc(currentUserId).stream()
                .map(PurchaseHistoryResponse::from)
                .toList());
    }

    public PaymentMethodListResponse getMethods(Long userId) {
        userService.requireUserId(userId);
        return new PaymentMethodListResponse(List.of());
    }
}
