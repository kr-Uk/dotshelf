package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;

import org.springframework.test.util.ReflectionTestUtils;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.dto.PaymentDtos.CursorPageResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeConfirmResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.entity.PaymentTransactionReason;
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
        paymentPolicyProperties = new PaymentPolicyProperties(100_000, 1, null, null, null, null, null, null, null);
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
    @DisplayName("페이지 충전·사용 내역 조회는 limit을 반영한 PageRequest로 repository를 호출하고 결과를 매핑한다")
    void getTransactions_mapsRepositoryResultToResponse() {
        given(userService.requireUserId(1L)).willReturn(1L);
        PageTransaction earlier = PageTransaction.charge(1L, 10L, 100, 100);
        PageTransaction later = PageTransaction.charge(1L, 11L, 50, 150);
        ArgumentCaptor<org.springframework.data.domain.Pageable> pageableCaptor =
                ArgumentCaptor.forClass(org.springframework.data.domain.Pageable.class);
        given(pageTransactionRepository.findByUserIdOrderByCreatedAtDesc(eq(1L), any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of(later, earlier));

        PageTransactionListResponse response = paymentService.getTransactions(1L, 50);

        then(pageTransactionRepository).should().findByUserIdOrderByCreatedAtDesc(eq(1L), pageableCaptor.capture());
        assertThat(pageableCaptor.getValue().getPageSize()).isEqualTo(50);
        assertThat(pageableCaptor.getValue().getPageNumber()).isEqualTo(0);
        assertThat(response.transactions()).hasSize(2);
        assertThat(response.transactions().get(0).balanceAfter()).isEqualTo(150);
        assertThat(response.transactions().get(0).pagesDelta()).isEqualTo(50);
        assertThat(response.transactions().get(0).type()).isEqualTo(PageTransaction.TYPE_CHARGE);
        assertThat(response.transactions().get(0).reason()).isEqualTo(PaymentTransactionReason.INICIS_PAYMENT);
        assertThat(response.transactions().get(0).paymentOrderId()).isEqualTo(11L);
        assertThat(response.transactions().get(1).balanceAfter()).isEqualTo(100);
    }

    @Test
    @DisplayName("cursor 페이징 첫 페이지: fetchSize+1 결과 중 effectiveLimit 만 응답에 담고 hasMore=true + nextCursor 생성")
    void getTransactionsCursor_firstPage_hasMore() {
        given(userService.requireUserId(1L)).willReturn(1L);
        PageTransaction first = transactionFixture(102L, Instant.parse("2026-05-15T10:00:00Z"));
        PageTransaction second = transactionFixture(101L, Instant.parse("2026-05-15T09:50:00Z"));
        PageTransaction overflow = transactionFixture(100L, Instant.parse("2026-05-15T09:40:00Z"));
        given(pageTransactionRepository.findByUserIdOrderByCreatedAtDescIdDesc(eq(1L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of(first, second, overflow));

        CursorPageResponse<?> response = paymentService.getTransactionsCursor(1L, null, 2);

        assertThat(response.items()).hasSize(2);
        assertThat(response.hasMore()).isTrue();
        assertThat(response.nextCursor()).isNotNull();
        CursorCodec.CursorValue cv = CursorCodec.decode(response.nextCursor());
        assertThat(cv.id()).isEqualTo(101L);
        assertThat(cv.timestamp()).isEqualTo(Instant.parse("2026-05-15T09:50:00Z"));
    }

    @Test
    @DisplayName("cursor 페이징 마지막 페이지: 결과가 limit 이하면 nextCursor=null, hasMore=false")
    void getTransactionsCursor_lastPage_noCursor() {
        given(userService.requireUserId(1L)).willReturn(1L);
        PageTransaction only = transactionFixture(101L, Instant.parse("2026-05-15T10:00:00Z"));
        given(pageTransactionRepository.findByUserIdOrderByCreatedAtDescIdDesc(eq(1L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of(only));

        CursorPageResponse<?> response = paymentService.getTransactionsCursor(1L, null, 5);

        assertThat(response.items()).hasSize(1);
        assertThat(response.hasMore()).isFalse();
        assertThat(response.nextCursor()).isNull();
    }

    @Test
    @DisplayName("cursor 가 주어지면 decode 후 findUserTransactionsBeforeCursor 가 호출된다")
    void getTransactionsCursor_withCursor_callsBeforeCursorQuery() {
        given(userService.requireUserId(1L)).willReturn(1L);
        String cursor = CursorCodec.encodeFromInstant(Instant.parse("2026-05-15T10:00:00Z"), 101L);
        given(pageTransactionRepository.findUserTransactionsBeforeCursor(eq(1L), any(Instant.class), eq(101L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of());

        CursorPageResponse<?> response = paymentService.getTransactionsCursor(1L, cursor, 20);

        assertThat(response.items()).isEmpty();
        assertThat(response.hasMore()).isFalse();
        then(pageTransactionRepository).should()
                .findUserTransactionsBeforeCursor(eq(1L), any(Instant.class), eq(101L),
                        any(org.springframework.data.domain.Pageable.class));
    }

    @Test
    @DisplayName("microsecond 정밀도 cursor 는 round-trip 후 동일한 Instant 가 repo 호출 인자로 전달된다 (sub-millis row 누락 방지)")
    void getTransactionsCursor_microsecondPrecision_passedToRepoUntruncated() {
        given(userService.requireUserId(1L)).willReturn(1L);
        Instant sourceTimestamp = Instant.parse("2026-05-15T10:00:00.123456Z");
        String cursor = CursorCodec.encodeFromInstant(sourceTimestamp, 101L);
        ArgumentCaptor<Instant> instantCaptor = ArgumentCaptor.forClass(Instant.class);
        given(pageTransactionRepository.findUserTransactionsBeforeCursor(eq(1L), any(Instant.class), eq(101L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of());

        paymentService.getTransactionsCursor(1L, cursor, 20);

        then(pageTransactionRepository).should()
                .findUserTransactionsBeforeCursor(eq(1L), instantCaptor.capture(), eq(101L),
                        any(org.springframework.data.domain.Pageable.class));
        assertThat(instantCaptor.getValue()).isEqualTo(sourceTimestamp);
        assertThat(instantCaptor.getValue().getNano()).isEqualTo(123_456_000);
    }

    @Test
    @DisplayName("큰 pageUnitPriceKrw 환경에서도 long 곱셈으로 expectedAmount 가 계산되어 mismatch 가 정확히 잡힌다 (int overflow 회귀 회피)")
    void createPageChargeOrder_largeUnitPrice_longArithmeticPreventsOverflow() {
        given(userService.requireUserId(1L)).willReturn(1L);
        // pageUnitPriceKrw=10_000 + pagesAmount=500_000 → expectedAmount(long)=5_000_000_000L (int max 초과).
        // 기존 int 곱셈이면 wrap-around 로 우연히 amountKrw 와 일치할 수 있어 검증 우회. long 곱셈이면 mismatch.
        PaymentPolicyProperties bigUnitProps = new PaymentPolicyProperties(
                Integer.MAX_VALUE, 10_000, null, null, null, null, null, null, null);
        PaymentService bigUnitService = new PaymentService(
                pageWalletRepository, pageTransactionRepository, paymentOrderRepository,
                assetPurchaseRepository, userService, inicisPaymentFormFactory,
                inicisPaymentClient, paymentOrderCompletionService, bigUnitProps);

        assertThatThrownBy(() -> bigUnitService.createPageChargeOrder(1L, orderRequest(500_000, 1_000_000)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AMOUNT_MISMATCH);

        then(paymentOrderRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("구매 내역 cursor 페이징 첫 페이지: fetchSize+1 결과 중 effectiveLimit 만 응답에 담고 hasMore=true + nextCursor 생성")
    void getPurchasesCursor_firstPage_hasMore() {
        given(userService.requireUserId(1L)).willReturn(1L);
        com.dotshelf.domain.asset.entity.AssetPurchase first =
                purchaseFixture(202L, java.time.LocalDateTime.parse("2026-05-15T10:00:00"));
        com.dotshelf.domain.asset.entity.AssetPurchase second =
                purchaseFixture(201L, java.time.LocalDateTime.parse("2026-05-15T09:50:00"));
        com.dotshelf.domain.asset.entity.AssetPurchase overflow =
                purchaseFixture(200L, java.time.LocalDateTime.parse("2026-05-15T09:40:00"));
        given(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDescIdDesc(eq(1L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of(first, second, overflow));

        CursorPageResponse<?> response = paymentService.getPurchasesCursor(1L, null, 2);

        assertThat(response.items()).hasSize(2);
        assertThat(response.hasMore()).isTrue();
        assertThat(response.nextCursor()).isNotNull();
        CursorCodec.CursorValue cv = CursorCodec.decode(response.nextCursor());
        assertThat(cv.id()).isEqualTo(201L);
        assertThat(cv.asLocalDateTimeUtc()).isEqualTo(java.time.LocalDateTime.parse("2026-05-15T09:50:00"));
    }

    @Test
    @DisplayName("구매 내역 cursor 페이징 마지막 페이지: 결과가 limit 이하면 nextCursor=null, hasMore=false")
    void getPurchasesCursor_lastPage_noCursor() {
        given(userService.requireUserId(1L)).willReturn(1L);
        com.dotshelf.domain.asset.entity.AssetPurchase only =
                purchaseFixture(201L, java.time.LocalDateTime.parse("2026-05-15T10:00:00"));
        given(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDescIdDesc(eq(1L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of(only));

        CursorPageResponse<?> response = paymentService.getPurchasesCursor(1L, null, 5);

        assertThat(response.items()).hasSize(1);
        assertThat(response.hasMore()).isFalse();
        assertThat(response.nextCursor()).isNull();
    }

    @Test
    @DisplayName("구매 cursor 가 주어지면 decode 후 findUserPurchasesBeforeCursor 가 LocalDateTime UTC 인자로 호출된다")
    void getPurchasesCursor_withCursor_callsBeforeCursorQuery() {
        given(userService.requireUserId(1L)).willReturn(1L);
        java.time.LocalDateTime cursorAt = java.time.LocalDateTime.parse("2026-05-15T10:00:00.123456");
        String cursor = CursorCodec.encodeFromLocalDateTimeUtc(cursorAt, 201L);
        ArgumentCaptor<java.time.LocalDateTime> ldtCaptor = ArgumentCaptor.forClass(java.time.LocalDateTime.class);
        given(assetPurchaseRepository.findUserPurchasesBeforeCursor(eq(1L), any(java.time.LocalDateTime.class), eq(201L),
                any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of());

        paymentService.getPurchasesCursor(1L, cursor, 20);

        then(assetPurchaseRepository).should()
                .findUserPurchasesBeforeCursor(eq(1L), ldtCaptor.capture(), eq(201L),
                        any(org.springframework.data.domain.Pageable.class));
        assertThat(ldtCaptor.getValue()).isEqualTo(cursorAt);
        assertThat(ldtCaptor.getValue().getNano()).isEqualTo(123_456_000);
    }

    private com.dotshelf.domain.asset.entity.AssetPurchase purchaseFixture(Long id, java.time.LocalDateTime purchasedAt) {
        com.dotshelf.domain.asset.entity.AssetPurchase purchase =
                com.dotshelf.domain.asset.entity.AssetPurchase.builder()
                        .userId(1L)
                        .assetId(id)
                        .pricePages(100)
                        .build();
        ReflectionTestUtils.setField(purchase, "id", id);
        ReflectionTestUtils.setField(purchase, "purchasedAt", purchasedAt);
        return purchase;
    }

    private PageTransaction transactionFixture(Long id, Instant createdAt) {
        PageTransaction transaction = PageTransaction.charge(1L, id, 50, 100);
        ReflectionTestUtils.setField(transaction, "id", id);
        ReflectionTestUtils.setField(transaction, "createdAt", createdAt);
        return transaction;
    }

    @Test
    @DisplayName("페이지 충전·사용 내역 조회의 limit은 0 이하면 기본 100, 상한 초과면 500으로 clamp된다")
    void getTransactions_clampsLimitBoundaries() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(pageTransactionRepository.findByUserIdOrderByCreatedAtDesc(eq(1L), any(org.springframework.data.domain.Pageable.class)))
                .willReturn(List.of());
        ArgumentCaptor<org.springframework.data.domain.Pageable> pageableCaptor =
                ArgumentCaptor.forClass(org.springframework.data.domain.Pageable.class);

        paymentService.getTransactions(1L, 0);
        paymentService.getTransactions(1L, -5);
        paymentService.getTransactions(1L, 10_000);

        then(pageTransactionRepository).should(org.mockito.Mockito.times(3))
                .findByUserIdOrderByCreatedAtDesc(eq(1L), pageableCaptor.capture());
        assertThat(pageableCaptor.getAllValues())
                .extracting(org.springframework.data.domain.Pageable::getPageSize)
                .containsExactly(100, 100, 500);
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
