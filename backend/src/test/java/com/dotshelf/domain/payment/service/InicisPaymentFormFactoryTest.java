package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

class InicisPaymentFormFactoryTest {

    private final InicisSignatureGenerator signatureGenerator = new InicisSignatureGenerator();

    @Test
    @DisplayName("KG이니시스 결제창 form은 필수 파라미터와 검증 해시를 포함한다")
    void create_buildsRequiredInicisFormParameters() {
        InicisPaymentProperties properties = new InicisPaymentProperties(
                " INIpayTest ",
                " sign-key ",
                " https://api.example.com/inicis/return ",
                " https://api.example.com/inicis/close ",
                " https://pay.example/stdpay.js "
        );
        InicisPaymentFormFactory factory = new InicisPaymentFormFactory(properties, signatureGenerator);
        PaymentOrder order = order("order-1", 100);

        var form = factory.create(order);

        assertThat(form.scriptUrl()).isEqualTo("https://pay.example/stdpay.js");
        assertThat(form.parameters())
                .containsEntry("version", "1.0")
                .containsEntry("gopaymethod", "Card")
                .containsEntry("mid", "INIpayTest")
                .containsEntry("oid", "order-1")
                .containsEntry("price", "100")
                .containsEntry("currency", "WON")
                .containsEntry("goodname", "DotShelf 100P 충전 (유효기간 1년/자동갱신)")
                .containsEntry("buyername", "홍길동")
                .containsEntry("buyertel", "010-1234-5678")
                .containsEntry("buyeremail", "buyer@example.com")
                .containsEntry("returnUrl", "https://api.example.com/inicis/return")
                .containsEntry("closeUrl", "https://api.example.com/inicis/close")
                .containsEntry("merchantData", "order-1")
                .containsEntry("use_chkfake", "Y")
                .containsEntry("acceptmethod", "centerCd(Y)");
        String timestamp = form.parameters().get("timestamp");
        assertThat(form.parameters().get("signature"))
                .isEqualTo(signatureGenerator.paymentSignature("order-1", 100, timestamp));
        assertThat(form.parameters().get("verification"))
                .isEqualTo(signatureGenerator.paymentVerification("order-1", 100, "sign-key", timestamp));
        assertThat(form.parameters().get("mKey")).isEqualTo(signatureGenerator.merchantKey("sign-key"));
    }

    @Test
    @DisplayName("requestScriptUrl이 비어 있으면 표준 INIStdPay 스크립트를 기본값으로 쓴다")
    void create_usesDefaultScriptUrlWhenMissing() {
        InicisPaymentProperties properties = new InicisPaymentProperties(
                "INIpayTest",
                "sign-key",
                "https://api.example.com/inicis/return",
                "https://api.example.com/inicis/close",
                " "
        );
        InicisPaymentFormFactory factory = new InicisPaymentFormFactory(properties, signatureGenerator);

        var form = factory.create(order("order-default", 50));

        assertThat(form.scriptUrl()).isEqualTo("https://stdpay.inicis.com/stdjs/INIStdPay.js");
    }

    @Test
    @DisplayName("MID가 없으면 결제창 form을 만들지 않고 설정 오류를 던진다")
    void create_missingMidThrowsConfigurationError() {
        InicisPaymentProperties properties = new InicisPaymentProperties(
                null,
                "sign-key",
                "https://api.example.com/inicis/return",
                "https://api.example.com/inicis/close",
                "https://pay.example/stdpay.js"
        );
        InicisPaymentFormFactory factory = new InicisPaymentFormFactory(properties, signatureGenerator);

        assertThatThrownBy(() -> factory.create(order("order-missing", 100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_CONFIGURATION_MISSING);
    }

    @Test
    @DisplayName("returnUrl 또는 closeUrl이 없으면 결제창 form을 만들지 않는다")
    void create_missingReturnOrCloseUrlThrowsConfigurationError() {
        InicisPaymentProperties missingReturnUrl = new InicisPaymentProperties(
                "INIpayTest",
                "sign-key",
                null,
                "https://api.example.com/inicis/close",
                "https://pay.example/stdpay.js"
        );
        InicisPaymentProperties missingCloseUrl = new InicisPaymentProperties(
                "INIpayTest",
                "sign-key",
                "https://api.example.com/inicis/return",
                null,
                "https://pay.example/stdpay.js"
        );

        assertThatThrownBy(() -> new InicisPaymentFormFactory(missingReturnUrl, signatureGenerator)
                .create(order("order-return", 100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_CONFIGURATION_MISSING);
        assertThatThrownBy(() -> new InicisPaymentFormFactory(missingCloseUrl, signatureGenerator)
                .create(order("order-close", 100)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_CONFIGURATION_MISSING);
    }

    private PaymentOrder order(String orderId, int amountKrw) {
        Instant now = Instant.now();
        return PaymentOrder.builder()
                .userId(1L)
                .orderId(orderId)
                .orderName("DotShelf " + amountKrw + "P 충전 (유효기간 1년/자동갱신)")
                .pagesAmount(amountKrw)
                .amountKrw(amountKrw)
                .buyerName("홍길동")
                .buyerTel("010-1234-5678")
                .buyerEmail("buyer@example.com")
                .requestedAt(now)
                .expiresAt(now.plusSeconds(1800))
                .build();
    }
}
