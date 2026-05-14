package com.dotshelf.domain.payment.service;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisPaymentPageResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisReturnResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeConfirmResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentClient.InicisApprovalResult;
import com.dotshelf.domain.payment.service.InicisPaymentFormFactory.InicisPaymentForm;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PaymentService {

    private static final int PAGE_UNIT_PRICE_KRW = 1;
    private static final Duration ORDER_TTL = Duration.ofMinutes(30);
    private static final String PAYMENT_PAGE_PATH_PREFIX = "/v1/payments/inicis/request/";

    private final PageWalletRepository pageWalletRepository;
    private final PaymentOrderRepository paymentOrderRepository;
    private final AssetPurchaseRepository assetPurchaseRepository;
    private final UserService userService;
    private final InicisPaymentFormFactory inicisPaymentFormFactory;
    private final InicisPaymentClient inicisPaymentClient;
    private final PaymentOrderCompletionService paymentOrderCompletionService;

    @Transactional
    public WalletResponse charge(Long userId, PageChargeRequest request) {
        userService.requireUserId(userId);
        throw new CustomException(ErrorCode.PAYMENT_DIRECT_CHARGE_DISABLED,
                "페이지 충전은 결제 주문 생성 후 KG이니시스 승인으로 완료해야 합니다.");
    }

    @Transactional
    public PageChargeOrderResponse createPageChargeOrder(Long userId, PageChargeOrderRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        validatePageChargeAmount(request.pagesAmount(), request.amountKrw());
        Instant now = Instant.now();
        PaymentOrder order = paymentOrderRepository.save(PaymentOrder.builder()
                .userId(currentUserId)
                .orderId(generateOrderId())
                .orderName(orderName(request.pagesAmount()))
                .pagesAmount(request.pagesAmount())
                .amountKrw(request.amountKrw())
                .buyerName(request.buyerName().trim())
                .buyerTel(request.buyerTel().trim())
                .buyerEmail(request.buyerEmail().trim())
                .requestedAt(now)
                .expiresAt(now.plus(ORDER_TTL))
                .build());
        InicisPaymentForm form = inicisPaymentFormFactory.create(order);
        return PageChargeOrderResponse.from(order, paymentPageUrl(order.getOrderId()), form);
    }

    public InicisPaymentPageResponse getInicisPaymentPage(Long userId, String orderId) {
        Long currentUserId = userService.requireUserId(userId);
        PaymentOrder order = paymentOrderRepository.findByOrderId(orderId)
                .orElseThrow(() -> new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND));
        if (!order.belongsTo(currentUserId)) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND);
        }
        if (!order.isReady() && !order.isAuthenticated()) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        }
        if (order.isExpired(Instant.now())) {
            paymentOrderCompletionService.failExpiredOrder(order.getUserId(), order.getOrderId());
            throw new CustomException(ErrorCode.PAYMENT_ORDER_INVALID_STATE, "결제 주문이 만료되었습니다.");
        }
        return InicisPaymentPageResponse.from(order.getOrderId(), inicisPaymentFormFactory.create(order));
    }

    @Transactional(propagation = Propagation.NOT_SUPPORTED)
    public InicisReturnResponse handleInicisReturn(InicisAuthReturnRequest request) {
        validateInicisReturnRequest(request);
        PaymentOrder order = paymentOrderCompletionService.authenticateInicisReturn(request);
        if (order.isDone()) {
            PageChargeConfirmResponse confirmed = paymentOrderCompletionService.getCompletedOrder(
                    order.getUserId(),
                    order.getOrderId()
            );
            return new InicisReturnResponse(
                    confirmed.orderId(),
                    confirmed.status(),
                    InicisPaymentConstants.SUCCESS_CODE,
                    "이미 완료된 결제입니다.",
                    confirmed.wallet()
            );
        }
        if (!InicisPaymentConstants.SUCCESS_CODE.equals(request.resultCode())) {
            return InicisReturnResponse.from(order, request.resultCode(), request.resultMessage(), null);
        }
        InicisApprovalResult approvalResult = inicisPaymentClient.approve(request, order);
        if (!approvalResult.approved()) {
            return paymentOrderCompletionService.failInicisOrder(
                    order.getOrderId(),
                    approvalResult.resultCode(),
                    approvalResult.resultMessage()
            );
        }
        try {
            PageChargeConfirmResponse confirmed = paymentOrderCompletionService.completeInicisPayment(
                    order.getOrderId(),
                    approvalResult
            );
            return new InicisReturnResponse(
                    confirmed.orderId(),
                    confirmed.status(),
                    approvalResult.resultCode(),
                    approvalResult.resultMessage(),
                    confirmed.wallet()
            );
        } catch (RuntimeException e) {
            boolean canceled = inicisPaymentClient.netCancel(
                    request,
                    order,
                    "DB_PROCESS_FAILED",
                    "DotShelf payment completion failed"
            );
            if (canceled) {
                paymentOrderCompletionService.cancelInicisOrder(order.getOrderId(), "DB_PROCESS_FAILED", e.getMessage());
            } else {
                paymentOrderCompletionService.failNetCancelOrder(order.getOrderId(), e.getMessage());
            }
            throw e;
        }
    }

    public PaymentOrderStatusResponse getOrderStatus(Long userId, String orderId) {
        Long currentUserId = userService.requireUserId(userId);
        return paymentOrderCompletionService.getOrderStatus(currentUserId, orderId);
    }

    public PurchaseHistoryListResponse getPurchases(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new PurchaseHistoryListResponse(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDesc(currentUserId).stream()
                .map(PurchaseHistoryResponse::from)
                .toList());
    }

    public WalletResponse getWallet(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return pageWalletRepository.findByUserId(currentUserId)
                .map(WalletResponse::from)
                .orElseGet(() -> new WalletResponse(currentUserId, 0));
    }

    public PaymentMethodListResponse getMethods(Long userId) {
        userService.requireUserId(userId);
        return new PaymentMethodListResponse(List.of());
    }

    private void validatePageChargeAmount(int pagesAmount, int amountKrw) {
        int expectedAmount = pagesAmount * PAGE_UNIT_PRICE_KRW;
        if (amountKrw != expectedAmount) {
            throw new CustomException(ErrorCode.PAYMENT_AMOUNT_MISMATCH,
                    "충전 페이지와 결제 금액이 일치하지 않습니다.");
        }
    }

    private void validateInicisReturnRequest(InicisAuthReturnRequest request) {
        if (request.orderId() == null) {
            throw new CustomException(ErrorCode.PAYMENT_ORDER_NOT_FOUND);
        }
        if (request.resultCode() == null) {
            throw new CustomException(ErrorCode.PAYMENT_AUTHENTICATION_FAILED);
        }
        if (InicisPaymentConstants.SUCCESS_CODE.equals(request.resultCode())
                && (request.authToken() == null || request.authUrl() == null)) {
            throw new CustomException(ErrorCode.PAYMENT_AUTHENTICATION_FAILED);
        }
    }

    private String generateOrderId() {
        return "DS" + UUID.randomUUID().toString().replace("-", "");
    }

    private String paymentPageUrl(String orderId) {
        return PAYMENT_PAGE_PATH_PREFIX + orderId;
    }

    private String orderName(int pagesAmount) {
        return "DotShelf " + pagesAmount + "P 충전 (유효기간 1년/자동갱신)";
    }
}
