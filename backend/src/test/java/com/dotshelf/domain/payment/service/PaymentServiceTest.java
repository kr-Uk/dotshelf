package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.util.List;
import java.util.Map;
import java.util.Optional;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeConfirmResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageTransactionRepository;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentClient.InicisApprovalResult;
import com.dotshelf.domain.payment.service.InicisPaymentFormFactory.InicisPaymentForm;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

@ExtendWith(MockitoExtension.class)
class PaymentServiceTest {

    @Mock
    private PageWalletRepository pageWalletRepository;

    @Mock
    private PageTransactionRepository pageTransactionRepository;

    @Mock
    private PaymentOrderRepository paymentOrderRepository;

    @Mock
    private AssetPurchaseRepository assetPurchaseRepository;

    @Mock
    private UserService userService;

    @Mock
    private InicisPaymentFormFactory inicisPaymentFormFactory;

    @Mock
    private InicisPaymentClient inicisPaymentClient;

    @Mock
    private PaymentOrderCompletionService paymentOrderCompletionService;

    private PaymentPolicyProperties paymentPolicyProperties;

    private PaymentService paymentService;

    @BeforeEach
    void setUp() {
        paymentPolicyProperties = new PaymentPolicyProperties(100_000, null, null, null, null, null, null, null);
        paymentService = new PaymentService(
                pageWalletRepository,
                pageTransactionRepository,
                paymentOrderRepository,
                assetPurchaseRepository,
                userService,
                inicisPaymentFormFactory,
                inicisPaymentClient,
                paymentOrderCompletionService,
                paymentPolicyProperties
        );
    }

    @Test
    @DisplayName("페이지 충전 주문 생성은 서버 금액 검증 후 READY 주문을 저장한다")
    void createPageChargeOrder_validRequest_savesReadyOrder() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(paymentOrderRepository.save(any(PaymentOrder.class)))
                .willAnswer(invocation -> invocation.getArgument(0));
        given(inicisPaymentFormFactory.create(any(PaymentOrder.class)))
                .willReturn(new InicisPaymentForm("https://stdpay.inicis.com/stdjs/INIStdPay.js", Map.of("mid", "INIpayTest")));
        ArgumentCaptor<PaymentOrder> captor = ArgumentCaptor.forClass(PaymentOrder.class);

        var response = paymentService.createPageChargeOrder(1L, orderRequest(100, 100));

        then(paymentOrderRepository).should().save(captor.capture());
        PaymentOrder order = captor.getValue();
        assertThat(order.getUserId()).isEqualTo(1L);
        assertThat(order.getOrderId()).startsWith("DS");
        assertThat(order.getOrderName()).isEqualTo("DotShelf 100P 충전 (유효기간 1년/자동갱신)");
        assertThat(order.getPagesAmount()).isEqualTo(100);
        assertThat(order.getAmountKrw()).isEqualTo(100);
        assertThat(order.getBuyerTel()).isEqualTo("010-1234-5678");
        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_READY);
        assertThat(response.paymentPageUrl()).isEqualTo("/v1/payments/inicis/request/" + order.getOrderId());
        assertThat(response.amountKrw()).isEqualTo(100);
        assertThat(response.inicisForm().parameters()).containsEntry("mid", "INIpayTest");
    }

    @Test
    @DisplayName("페이지 수와 원화 금액이 다르면 결제 주문을 만들지 않는다")
    void createPageChargeOrder_amountMismatch_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);

        assertThatThrownBy(() -> paymentService.createPageChargeOrder(1L, orderRequest(100, 99)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AMOUNT_MISMATCH);

        then(paymentOrderRepository).shouldHaveNoInteractions();
        then(inicisPaymentFormFactory).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("KG이니시스 1회 결제 한도를 초과하는 충전 주문은 만들지 않는다")
    void createPageChargeOrder_amountExceedsLimit_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);

        assertThatThrownBy(() -> paymentService.createPageChargeOrder(1L, orderRequest(100_001, 100_001)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AMOUNT_EXCEEDS_LIMIT);

        then(paymentOrderRepository).shouldHaveNoInteractions();
        then(inicisPaymentFormFactory).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("결제 정책 조회는 1회 한도와 카드결제 고정 정책을 응답에 담아 반환한다")
    void getPolicy_exposesMaxChargeAndCardOnlyPolicy() {
        PaymentPolicyResponse response = paymentService.getPolicy();

        assertThat(response.maxChargeKrwPerOrder()).isEqualTo(100_000);
        assertThat(response.currency()).isEqualTo("KRW");
        assertThat(response.paymentMethod()).isEqualTo("CARD");
        assertThat(response.validityNotice()).contains("1년").contains("자동");
    }

    @Test
    @DisplayName("페이지 충전·사용 내역 조회는 repository가 돌려준 최신순 결과를 PageTransactionResponse로 매핑한다")
    void getTransactions_mapsRepositoryResultToResponse() {
        given(userService.requireUserId(1L)).willReturn(1L);
        PageTransaction earlier = PageTransaction.charge(1L, 10L, 100, 100);
        PageTransaction later = PageTransaction.charge(1L, 11L, 50, 150);
        given(pageTransactionRepository.findByUserIdOrderByCreatedAtDesc(1L))
                .willReturn(List.of(later, earlier));

        PageTransactionListResponse response = paymentService.getTransactions(1L);

        assertThat(response.transactions()).hasSize(2);
        assertThat(response.transactions().get(0).balanceAfter()).isEqualTo(150);
        assertThat(response.transactions().get(0).pagesDelta()).isEqualTo(50);
        assertThat(response.transactions().get(0).type()).isEqualTo(PageTransaction.TYPE_CHARGE);
        assertThat(response.transactions().get(0).reason()).isEqualTo("INICIS_PAYMENT");
        assertThat(response.transactions().get(0).referenceType()).isEqualTo("PAYMENT_ORDER");
        assertThat(response.transactions().get(0).referenceId()).isEqualTo(11L);
        assertThat(response.transactions().get(1).balanceAfter()).isEqualTo(100);
    }

    @Test
    @DisplayName("기존 직접 충전 API는 지갑을 충전하지 않는다")
    void charge_directCharge_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);

        assertThatThrownBy(() -> paymentService.charge(1L, new PageChargeRequest(100, 100, "UNITY")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_DIRECT_CHARGE_DISABLED);

        then(pageWalletRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("KG이니시스 결제창 HTML은 주문 소유자만 열 수 있다")
    void getInicisPaymentPage_otherUser_throwsNotFound() {
        given(userService.requireUserId(2L)).willReturn(2L);
        given(paymentOrderRepository.findByOrderId("order-1")).willReturn(Optional.of(order()));

        assertThatThrownBy(() -> paymentService.getInicisPaymentPage(2L, "order-1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_ORDER_NOT_FOUND);

        then(inicisPaymentFormFactory).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("이미 완료된 KG이니시스 returnUrl 재호출은 재승인 없이 기존 완료 응답을 반환한다")
    void handleInicisReturn_doneOrder_returnsCompletedWithoutApprovingAgain() {
        PaymentOrder order = order();
        order.complete("tid-1", "Card", java.time.Instant.now());
        InicisAuthReturnRequest request = returnRequest("0000");
        given(paymentOrderCompletionService.authenticateInicisReturn(request)).willReturn(order);
        given(paymentOrderCompletionService.getCompletedOrder(1L, "order-1"))
                .willReturn(new PageChargeConfirmResponse(
                        "order-1",
                        "tid-1",
                        PaymentOrder.STATUS_DONE,
                        100,
                        100,
                        new WalletResponse(1L, 100)
                ));

        var response = paymentService.handleInicisReturn(request);

        assertThat(response.status()).isEqualTo(PaymentOrder.STATUS_DONE);
        assertThat(response.wallet().balancePages()).isEqualTo(100);
        then(inicisPaymentClient).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("승인 후 내부 반영 실패와 망취소 실패가 겹치면 주문을 취소가 아닌 실패로 남긴다")
    void handleInicisReturn_netCancelFails_marksOrderFailed() {
        PaymentOrder order = order();
        order.authenticate("auth-token", "https://stgstdpay.inicis.com/api", "https://stgstdpay.inicis.com/netCancel", "stg", java.time.Instant.now());
        InicisAuthReturnRequest request = returnRequest("0000");
        InicisApprovalResult approval = approval();
        CustomException completionFailure = new CustomException(ErrorCode.INTERNAL_SERVER_ERROR, "DB failure");
        given(paymentOrderCompletionService.authenticateInicisReturn(request)).willReturn(order);
        given(inicisPaymentClient.approve(request, order)).willReturn(approval);
        given(paymentOrderCompletionService.completeInicisPayment("order-1", approval))
                .willThrow(completionFailure);
        given(inicisPaymentClient.netCancel(request, order, "DB_PROCESS_FAILED", "DotShelf payment completion failed"))
                .willReturn(false);

        assertThatThrownBy(() -> paymentService.handleInicisReturn(request))
                .isSameAs(completionFailure);

        then(paymentOrderCompletionService).should().authenticateInicisReturn(request);
        then(paymentOrderCompletionService).should().completeInicisPayment("order-1", approval);
        then(paymentOrderCompletionService).should().failNetCancelOrder("order-1", "DB failure");
        then(paymentOrderCompletionService).shouldHaveNoMoreInteractions();
    }

    private PageChargeOrderRequest orderRequest(int pagesAmount, int amountKrw) {
        return new PageChargeOrderRequest(
                pagesAmount,
                amountKrw,
                "UNITY",
                "홍길동",
                "010-1234-5678",
                "buyer@example.com"
        );
    }

    private PaymentOrder order() {
        java.time.Instant now = java.time.Instant.now();
        return PaymentOrder.builder()
                .userId(1L)
                .orderId("order-1")
                .orderName("DotShelf 100P 충전 (유효기간 1년/자동갱신)")
                .pagesAmount(100)
                .amountKrw(100)
                .buyerName("홍길동")
                .buyerTel("010-1234-5678")
                .buyerEmail("buyer@example.com")
                .requestedAt(now)
                .expiresAt(now.plusSeconds(1800))
                .build();
    }

    private InicisAuthReturnRequest returnRequest(String resultCode) {
        return new InicisAuthReturnRequest(
                resultCode,
                "정상 처리되었습니다.",
                "INIpayTest",
                "order-1",
                "auth-token",
                "stg",
                "https://stgstdpay.inicis.com/api",
                "https://stgstdpay.inicis.com/netCancel",
                "UTF-8",
                "order-1"
        );
    }

    private InicisApprovalResult approval() {
        return new InicisApprovalResult(
                "0000",
                "정상 처리되었습니다.",
                "tid-1",
                "INIpayTest",
                "order-1",
                100,
                "Card",
                java.time.Instant.now()
        );
    }
}
