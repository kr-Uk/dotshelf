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
                .orderName("DotShelf 100P 충전")
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
        return new InicisApprovalResult(
                "0000",
                "정상 처리되었습니다.",
                "tid-1",
                "INIpayTest",
                "order-1",
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
}
