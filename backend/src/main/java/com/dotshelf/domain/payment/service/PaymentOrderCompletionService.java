package com.dotshelf.domain.payment.service;

import java.time.Instant;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisReturnResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeConfirmResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageTransactionRepository;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentClient.InicisApprovalResult;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PaymentOrderCompletionService {

    private static final String INICIS_SUCCESS_CODE = "0000";

    private final PaymentOrderRepository paymentOrderRepository;
    private final PageWalletRepository pageWalletRepository;
    private final PageTransactionRepository pageTransactionRepository;

    public PaymentOrderStatusResponse getOrderStatus(Long userId, String orderId) {
        PaymentOrder order = paymentOrderRepository.findByOrderId(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (!order.belongsTo(userId)) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND);
        }
        return PaymentOrderStatusResponse.from(order, walletOrNull(userId));
    }

    public PageChargeConfirmResponse getCompletedOrder(Long userId, String orderId) {
        PaymentOrder order = paymentOrderRepository.findByOrderId(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (!order.belongsTo(userId) || !order.isDone()) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        }
        return PageChargeConfirmResponse.from(order, walletOrEmpty(userId));
    }

    @Transactional
    public PaymentOrder authenticateInicisReturn(InicisAuthReturnRequest request) {
        PaymentOrder order = paymentOrderRepository.findByOrderIdForUpdate(request.orderId())
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (order.isDone()) {
            return order;
        }
        if (!order.isReady() && !order.isAuthenticated()) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        }
        if (!INICIS_SUCCESS_CODE.equals(request.resultCode())) {
            order.fail(request.resultCode(), request.resultMessage(), Instant.now());
            return order;
        }
        order.authenticate(request.authToken(), request.authUrl(), request.netCancelUrl(), request.idcName(), Instant.now());
        return order;
    }

    @Transactional
    public PageChargeConfirmResponse completeInicisPayment(String orderId, InicisApprovalResult approvalResult) {
        PaymentOrder order = paymentOrderRepository.findByOrderIdForUpdate(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (order.isDone()) {
            if (order.hasSameProviderTransaction(approvalResult.tid(), approvalResult.totalPrice())) {
                return PageChargeConfirmResponse.from(order, walletOrEmpty(order.getUserId()));
            }
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        }
        if (!order.isAuthenticated()) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        }
        validateApprovalResult(order, approvalResult);

        paymentOrderRepository.findByProviderTransactionId(approvalResult.tid())
                .filter(savedOrder -> !savedOrder.getId().equals(order.getId()))
                .ifPresent(savedOrder -> {
                    throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
                });

        PageWallet wallet = pageWalletRepository.findByUserIdForUpdate(order.getUserId())
                .orElseGet(() -> pageWalletRepository.save(PageWallet.builder()
                        .userId(order.getUserId())
                        .balancePages(0)
                        .build()));
        wallet.charge(order.getPagesAmount());
        order.complete(approvalResult.tid(), approvalResult.payMethod(), approvalResult.approvedAt());
        pageTransactionRepository.save(PageTransaction.charge(
                order.getUserId(),
                order.getId(),
                order.getPagesAmount(),
                wallet.getBalancePages()
        ));
        return PageChargeConfirmResponse.from(order, wallet);
    }

    @Transactional
    public InicisReturnResponse failInicisOrder(String orderId, String resultCode, String resultMessage) {
        PaymentOrder order = paymentOrderRepository.findByOrderIdForUpdate(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (!order.isDone()) {
            order.fail(resultCode, resultMessage, Instant.now());
        }
        return InicisReturnResponse.from(order, resultCode, resultMessage, walletOrNull(order.getUserId()));
    }

    @Transactional
    public void cancelInicisOrder(String orderId, String resultCode, String resultMessage) {
        PaymentOrder order = paymentOrderRepository.findByOrderIdForUpdate(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (!order.isDone()) {
            order.cancel(resultCode, resultMessage, Instant.now());
        }
    }

    @Transactional
    public void failExpiredOrder(Long userId, String orderId) {
        PaymentOrder order = paymentOrderRepository.findByOrderIdForUpdate(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (order.belongsTo(userId) && order.isReady()) {
            order.fail("ORDER_EXPIRED", "결제 주문이 만료되었습니다.", Instant.now());
        }
    }

    private void validateApprovalResult(PaymentOrder order, InicisApprovalResult approvalResult) {
        if (!approvalResult.approved()) {
            order.fail(approvalResult.resultCode(), approvalResult.resultMessage(), Instant.now());
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED, approvalResult.resultMessage());
        }
        if (!order.getOrderId().equals(approvalResult.orderId())) {
            order.fail("INICIS_ORDER_MISMATCH", "KG이니시스 승인 주문번호가 일치하지 않습니다.", Instant.now());
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED);
        }
        if (order.getAmountKrw() != approvalResult.totalPrice()) {
            order.fail("INICIS_AMOUNT_MISMATCH", "KG이니시스 승인 금액이 주문 금액과 일치하지 않습니다.", Instant.now());
            throw new CustomException(ErrorCode.PAYMENT_AMOUNT_MISMATCH);
        }
        if (approvalResult.tid() == null) {
            order.fail("INICIS_TID_MISSING", "KG이니시스 거래번호가 없습니다.", Instant.now());
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED);
        }
    }

    private PageWallet walletOrNull(Long userId) {
        return pageWalletRepository.findByUserId(userId).orElse(null);
    }

    private PageWallet walletOrEmpty(Long userId) {
        return pageWalletRepository.findByUserId(userId)
                .orElseGet(() -> PageWallet.builder()
                        .userId(userId)
                        .balancePages(0)
                        .build());
    }
}
