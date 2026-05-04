package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentFormFactory.InicisPaymentForm;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

@ExtendWith(MockitoExtension.class)
class PaymentServiceTest {

    @Mock
    private PageWalletRepository pageWalletRepository;

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

    @InjectMocks
    private PaymentService paymentService;

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
        assertThat(order.getOrderName()).isEqualTo("DotShelf 100P 충전");
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
    @DisplayName("기존 직접 충전 API는 지갑을 충전하지 않는다")
    void charge_directCharge_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);

        assertThatThrownBy(() -> paymentService.charge(1L, new PageChargeRequest(100, 100, "UNITY")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_DIRECT_CHARGE_DISABLED);

        then(pageWalletRepository).shouldHaveNoInteractions();
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
}
