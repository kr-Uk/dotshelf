package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.Instant;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageTransactionRepository;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentClient.InicisApprovalResult;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class PaymentOrderCompletionServiceTest {

    @Mock
    private PaymentOrderRepository paymentOrderRepository;

    @Mock
    private PageWalletRepository pageWalletRepository;

    @Mock
    private PageTransactionRepository pageTransactionRepository;

    @Mock
    private InicisPaymentProperties inicisPaymentProperties;

    @InjectMocks
    private PaymentOrderCompletionService completionService;

    @Test
    @DisplayName("KG이니시스 승인 결과가 성공이면 지갑 충전과 페이지 거래 이력을 저장한다")
    void completeInicisPayment_success_chargesWalletAndSavesTransaction() {
        PaymentOrder order = order();
        TestFixtures.setId(order, 100L);
        order.authenticate("auth-token", "https://stgstdpay.inicis.com/api", "https://stgstdpay.inicis.com/netCancel", "stg", Instant.now());
        PageWallet wallet = PageWallet.builder()
                .userId(1L)
                .balancePages(10)
                .build();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(paymentOrderRepository.findByProviderTransactionId("tid-1")).willReturn(Optional.empty());
        given(pageWalletRepository.findByUserIdForUpdate(1L)).willReturn(Optional.of(wallet));
        ArgumentCaptor<PageTransaction> captor = ArgumentCaptor.forClass(PageTransaction.class);

        var response = completionService.completeInicisPayment("order-1", approval(100));

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_DONE);
        assertThat(order.getProviderTransactionId()).isEqualTo("tid-1");
        assertThat(wallet.getBalancePages()).isEqualTo(110);
        assertThat(response.wallet().balancePages()).isEqualTo(110);
        then(pageTransactionRepository).should().save(captor.capture());
        PageTransaction transaction = captor.getValue();
        assertThat(transaction.getType()).isEqualTo(PageTransaction.TYPE_CHARGE);
        assertThat(transaction.getPagesDelta()).isEqualTo(100);
        assertThat(transaction.getBalanceAfter()).isEqualTo(110);
    }

    @Test
    @DisplayName("이미 완료된 주문의 같은 승인 요청은 중복 충전하지 않는다")
    void completeInicisPayment_alreadyDone_returnsCurrentWallet() {
        PaymentOrder order = order();
        TestFixtures.setId(order, 100L);
        order.complete("tid-1", "Card", Instant.now());
        PageWallet wallet = PageWallet.builder()
                .userId(1L)
                .balancePages(110)
                .build();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(pageWalletRepository.findByUserId(1L)).willReturn(Optional.of(wallet));

        var response = completionService.completeInicisPayment("order-1", approval(100));

        assertThat(response.wallet().balancePages()).isEqualTo(110);
        then(pageTransactionRepository).shouldHaveNoInteractions();
        then(pageWalletRepository).should().findByUserId(1L);
    }

    @Test
    @DisplayName("KG이니시스 승인 금액이 주문 금액과 다르면 실패 상태로 남기고 지갑을 충전하지 않는다")
    void completeInicisPayment_amountMismatch_failsOrder() {
        PaymentOrder order = order();
        TestFixtures.setId(order, 100L);
        order.authenticate("auth-token", "https://stgstdpay.inicis.com/api", "https://stgstdpay.inicis.com/netCancel", "stg", Instant.now());
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        assertThatThrownBy(() -> completionService.completeInicisPayment("order-1", approval(90)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AMOUNT_MISMATCH);

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        then(pageWalletRepository).shouldHaveNoInteractions();
        then(pageTransactionRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("주문 상태 조회는 소유자와 지갑 정보를 확인해 응답한다")
    void getOrderStatus_owner_returnsStatusWithWallet() {
        PaymentOrder order = order();
        PageWallet wallet = PageWallet.builder()
                .userId(1L)
                .balancePages(50)
                .build();
        given(paymentOrderRepository.findByOrderId("order-1")).willReturn(Optional.of(order));
        given(pageWalletRepository.findByUserId(1L)).willReturn(Optional.of(wallet));

        var response = completionService.getOrderStatus(1L, "order-1");

        assertThat(response.orderId()).isEqualTo("order-1");
        assertThat(response.status()).isEqualTo(PaymentOrder.STATUS_READY);
        assertThat(response.wallet().balancePages()).isEqualTo(50);
    }

    @Test
    @DisplayName("다른 사용자의 주문 상태 조회는 PAYMENT_ORDER_NOT_FOUND로 숨긴다")
    void getOrderStatus_otherUser_throwsNotFound() {
        given(paymentOrderRepository.findByOrderId("order-1")).willReturn(Optional.of(order()));

        assertThatThrownBy(() -> completionService.getOrderStatus(2L, "order-1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_ORDER_NOT_FOUND);
        then(pageWalletRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("완료 주문 조회는 DONE 상태가 아니면 PAYMENT_ORDER_INVALID_STATE를 던진다")
    void getCompletedOrder_notDone_throwsInvalidState() {
        given(paymentOrderRepository.findByOrderId("order-1")).willReturn(Optional.of(order()));

        assertThatThrownBy(() -> completionService.getCompletedOrder(1L, "order-1"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
    }

    @Test
    @DisplayName("KG이니시스 성공 returnUrl은 주문을 AUTHENTICATED 상태로 변경한다")
    void authenticateInicisReturn_successCallback_authenticatesOrder() {
        PaymentOrder order = order();
        InicisAuthReturnRequest request = returnRequest("0000", "order-1");
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(inicisPaymentProperties.mid()).willReturn("INIpayTest");

        PaymentOrder response = completionService.authenticateInicisReturn(request);

        assertThat(response.getStatus()).isEqualTo(PaymentOrder.STATUS_AUTHENTICATED);
        assertThat(response.getAuthToken()).isNull();
        assertThat(response.getIdcName()).isEqualTo("stg");
    }

    @Test
    @DisplayName("KG이니시스 returnUrl의 MID가 다르면 PAYMENT_AUTHENTICATION_FAILED를 던진다")
    void authenticateInicisReturn_midMismatch_throwsAuthenticationFailed() {
        PaymentOrder order = order();
        InicisAuthReturnRequest request = new InicisAuthReturnRequest(
                "0000", "정상", "OTHER_MID", "order-1", "auth-token", "stg",
                "https://stgstdpay.inicis.com/api", "https://stgstdpay.inicis.com/netCancel", "UTF-8", "order-1"
        );
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(inicisPaymentProperties.mid()).willReturn("INIpayTest");

        assertThatThrownBy(() -> completionService.authenticateInicisReturn(request))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AUTHENTICATION_FAILED);
        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_READY);
    }

    @Test
    @DisplayName("인증되지 않은 주문을 승인 완료 처리하면 PAYMENT_ORDER_INVALID_STATE를 던진다")
    void completeInicisPayment_notAuthenticated_throwsInvalidState() {
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order()));

        assertThatThrownBy(() -> completionService.completeInicisPayment("order-1", approval(100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
    }

    @Test
    @DisplayName("승인 실패 결과는 주문 실패 상태로 저장하고 PAYMENT_APPROVAL_FAILED를 던진다")
    void completeInicisPayment_failedApproval_failsOrder() {
        PaymentOrder order = authenticatedOrder();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        assertThatThrownBy(() -> completionService.completeInicisPayment(
                "order-1",
                new InicisApprovalResult("9999", "승인 실패", null, "INIpayTest", "order-1", 100, null, null)
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(order.getFailureCode()).isEqualTo("9999");
    }

    @Test
    @DisplayName("승인 주문번호가 다르면 주문 실패 상태로 저장하고 PAYMENT_APPROVAL_FAILED를 던진다")
    void completeInicisPayment_orderMismatch_failsOrder() {
        PaymentOrder order = authenticatedOrder();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        assertThatThrownBy(() -> completionService.completeInicisPayment("order-1", approval("tid-1", "other-order", 100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);
        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(order.getFailureCode()).isEqualTo("INICIS_ORDER_MISMATCH");
    }

    @Test
    @DisplayName("승인 TID가 없으면 주문 실패 상태로 저장하고 PAYMENT_APPROVAL_FAILED를 던진다")
    void completeInicisPayment_missingTid_failsOrder() {
        PaymentOrder order = authenticatedOrder();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        assertThatThrownBy(() -> completionService.completeInicisPayment("order-1", approval(null, "order-1", 100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);
        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(order.getFailureCode()).isEqualTo("INICIS_TID_MISSING");
    }

    @Test
    @DisplayName("이미 다른 주문에 사용된 providerTransactionId는 중복 승인으로 거절한다")
    void completeInicisPayment_duplicateProviderTransaction_throwsInvalidState() {
        PaymentOrder order = authenticatedOrder();
        TestFixtures.setId(order, 100L);
        PaymentOrder otherOrder = order();
        TestFixtures.setId(otherOrder, 101L);
        otherOrder.complete("tid-1", "Card", Instant.now());
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(paymentOrderRepository.findByProviderTransactionId("tid-1")).willReturn(Optional.of(otherOrder));

        assertThatThrownBy(() -> completionService.completeInicisPayment("order-1", approval(100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_ORDER_INVALID_STATE);
        then(pageWalletRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("KG이니시스 returnUrl의 MID와 merchantData가 맞으면 비성공 콜백으로 주문을 실패 처리한다")
    void authenticateInicisReturn_failureCallbackWithValidMetadata_failsOrder() {
        PaymentOrder order = order();
        InicisAuthReturnRequest request = returnRequest("V013", "order-1");
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(inicisPaymentProperties.mid()).willReturn("INIpayTest");

        PaymentOrder response = completionService.authenticateInicisReturn(request);

        assertThat(response.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(response.getFailureCode()).isEqualTo("V013");
    }

    @Test
    @DisplayName("실패 처리된 주문은 InicisReturnResponse에 실패 코드와 지갑 정보를 담는다")
    void failInicisOrder_marksFailedAndReturnsWallet() {
        PaymentOrder order = order();
        PageWallet wallet = PageWallet.builder()
                .userId(1L)
                .balancePages(10)
                .build();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(pageWalletRepository.findByUserId(1L)).willReturn(Optional.of(wallet));

        var response = completionService.failInicisOrder("order-1", "FAILED", "실패");

        assertThat(response.status()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(response.resultCode()).isEqualTo("FAILED");
        assertThat(response.wallet().balancePages()).isEqualTo(10);
    }

    @Test
    @DisplayName("망취소 실패 처리 코드는 NET_CANCEL_FAILED로 고정한다")
    void failNetCancelOrder_usesNetCancelFailedCode() {
        PaymentOrder order = order();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        var response = completionService.failNetCancelOrder("order-1", "망취소 실패");

        assertThat(response.resultCode()).isEqualTo("NET_CANCEL_FAILED");
        assertThat(order.getFailureCode()).isEqualTo("NET_CANCEL_FAILED");
    }

    @Test
    @DisplayName("취소 처리는 DONE이 아닌 주문만 CANCELED로 변경한다")
    void cancelInicisOrder_notDone_marksCanceled() {
        PaymentOrder order = order();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        completionService.cancelInicisOrder("order-1", "CANCELED", "사용자 취소");

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_CANCELED);
        assertThat(order.getFailureCode()).isEqualTo("CANCELED");
    }

    @Test
    @DisplayName("만료 주문 실패 처리는 소유자 일치와 READY 상태일 때만 실패로 변경한다")
    void failExpiredOrder_ownerReady_marksFailed() {
        PaymentOrder order = order();
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));

        completionService.failExpiredOrder(1L, "order-1");

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(order.getFailureCode()).isEqualTo("ORDER_EXPIRED");
    }

    @Test
    @DisplayName("KG이니시스 returnUrl의 merchantData가 주문과 다르면 상태를 바꾸지 않는다")
    void authenticateInicisReturn_invalidMerchantData_throwsWithoutStateChange() {
        PaymentOrder order = order();
        InicisAuthReturnRequest request = returnRequest("V013", "other-order");
        given(paymentOrderRepository.findByOrderIdForUpdate("order-1")).willReturn(Optional.of(order));
        given(inicisPaymentProperties.mid()).willReturn("INIpayTest");

        assertThatThrownBy(() -> completionService.authenticateInicisReturn(request))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_AUTHENTICATION_FAILED);

        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_READY);
    }

    private PaymentOrder order() {
        Instant now = Instant.now();
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

    private InicisApprovalResult approval(int amountKrw) {
        return approval("tid-1", "order-1", amountKrw);
    }

    private InicisApprovalResult approval(String tid, String orderId, int amountKrw) {
        return new InicisApprovalResult(
                "0000",
                "정상 처리되었습니다.",
                tid,
                "INIpayTest",
                orderId,
                amountKrw,
                "Card",
                Instant.now()
        );
    }

    private InicisAuthReturnRequest returnRequest(String resultCode, String merchantData) {
        return new InicisAuthReturnRequest(
                resultCode,
                "결제가 취소되었습니다.",
                "INIpayTest",
                "order-1",
                null,
                "stg",
                null,
                null,
                "UTF-8",
                merchantData
        );
    }

    private PaymentOrder authenticatedOrder() {
        PaymentOrder order = order();
        order.authenticate("auth-token", "https://stgstdpay.inicis.com/api", "https://stgstdpay.inicis.com/netCancel", "stg", Instant.now());
        return order;
    }
}
