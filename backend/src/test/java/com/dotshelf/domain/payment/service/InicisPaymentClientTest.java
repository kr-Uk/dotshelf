package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.hamcrest.Matchers.allOf;
import static org.hamcrest.Matchers.containsString;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.client.match.MockRestRequestMatchers.content;
import static org.springframework.test.web.client.match.MockRestRequestMatchers.method;
import static org.springframework.test.web.client.match.MockRestRequestMatchers.requestTo;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withServerError;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withSuccess;

import java.time.Instant;
import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;
import org.springframework.http.HttpMethod;
import org.springframework.http.MediaType;
import org.springframework.test.web.client.MockRestServiceServer;
import org.springframework.web.client.RestClient;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

class InicisPaymentClientTest {

    private final RestClient.Builder restClientBuilder = Mockito.mock(RestClient.Builder.class);
    private final InicisPaymentClient client = new InicisPaymentClient(
            restClientBuilder,
            new InicisPaymentProperties(
                    "INIpayTest",
                    "sign-key",
                    "https://api.example.com/inicis/return",
                    "https://api.example.com/inicis/close",
                    "https://pay.example/stdpay.js"
            ),
            new InicisSignatureGenerator()
    );

    @Test
    @DisplayName("KG이니시스 승인 응답은 공백 trim, 금액 파싱, 승인 시각 KST 변환을 수행한다")
    void approvalResultFrom_parsesProviderResponse() {
        var result = InicisPaymentClient.InicisApprovalResult.from(Map.of(
                "resultCode", " 0000 ",
                "resultMsg", " 정상 처리 ",
                "tid", " tid-1 ",
                "mid", " INIpayTest ",
                "MOID", " order-1 ",
                "TotPrice", "100",
                "payMethod", " Card ",
                "applDate", "20260507",
                "applTime", "153000"
        ));

        assertThat(result.approved()).isTrue();
        assertThat(result.resultMessage()).isEqualTo("정상 처리");
        assertThat(result.tid()).isEqualTo("tid-1");
        assertThat(result.orderId()).isEqualTo("order-1");
        assertThat(result.totalPrice()).isEqualTo(100);
        assertThat(result.payMethod()).isEqualTo("Card");
        assertThat(result.approvedAt()).isEqualTo(Instant.parse("2026-05-07T06:30:00Z"));
    }

    @Test
    @DisplayName("비어 있거나 깨진 KG이니시스 승인 응답은 실패 결과로 안전하게 파싱한다")
    void approvalResultFrom_handlesEmptyAndMalformedResponse() {
        var empty = InicisPaymentClient.InicisApprovalResult.from(null);
        var malformed = InicisPaymentClient.InicisApprovalResult.from(Map.of(
                "resultCode", "9999",
                "TotPrice", "not-number",
                "applDate", "bad",
                "applTime", "time"
        ));

        assertThat(empty.approved()).isFalse();
        assertThat(empty.resultCode()).isEqualTo("EMPTY_RESPONSE");
        assertThat(malformed.approved()).isFalse();
        assertThat(malformed.totalPrice()).isZero();
        assertThat(malformed.approvedAt()).isNull();
    }

    @Test
    @DisplayName("승인 URL은 HTTPS와 허용 호스트, IDC 일치 조건을 모두 만족해야 한다")
    void approve_rejectsUnsafeOrMismatchedProviderUrlBeforeHttpCall() {
        PaymentOrder order = order();

        assertThatThrownBy(() -> client.approve(request("http://stgstdpay.inicis.com/api", "stg", null), order))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);
        assertThatThrownBy(() -> client.approve(request("https://evil.example.com/api", "stg", null), order))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);
        assertThatThrownBy(() -> client.approve(request("https://fcstdpay.inicis.com/api", "stg", null), order))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_APPROVAL_FAILED);
        then(restClientBuilder).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("망취소 URL이 비어 있거나 검증에 실패하면 HTTP 호출 없이 false를 반환한다")
    void netCancel_returnsFalseWhenUrlMissingOrRejected() {
        PaymentOrder order = order();

        assertThat(client.netCancel(request("https://stgstdpay.inicis.com/api", "stg", null), order, "FAILED", "실패"))
                .isFalse();
        assertThat(client.netCancel(request(
                "https://stgstdpay.inicis.com/api",
                "stg",
                "https://evil.example.com/netCancel"
        ), order, "FAILED", "실패"))
                .isFalse();
        then(restClientBuilder).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("MID 설정이 없으면 승인 요청을 provider로 보내기 전에 설정 오류를 던진다")
    void approve_missingMidThrowsConfigurationError() {
        InicisPaymentClient missingMidClient = new InicisPaymentClient(
                restClientBuilder,
                new InicisPaymentProperties(
                        null,
                        "sign-key",
                        "https://api.example.com/inicis/return",
                        "https://api.example.com/inicis/close",
                        "https://pay.example/stdpay.js"
                ),
                new InicisSignatureGenerator()
        );

        assertThatThrownBy(() -> missingMidClient.approve(
                request("https://stgstdpay.inicis.com/api", "stg", null),
                order()
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_CONFIGURATION_MISSING);
        then(restClientBuilder).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("승인 요청은 form body를 provider에 전송하고 성공 응답을 승인 결과로 매핑한다")
    void approve_success_postsFormBodyAndMapsResponse() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).build();
        InicisPaymentClient httpClient = httpClient(builder, "INIpayTest", "sign-key");
        server.expect(requestTo("https://stgstdpay.inicis.com/api"))
                .andExpect(method(HttpMethod.POST))
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_FORM_URLENCODED))
                .andExpect(content().string(allOf(
                        containsString("mid=INIpayTest"),
                        containsString("authToken=auth-token"),
                        containsString("charset=UTF-8"),
                        containsString("format=JSON"),
                        containsString("price=100"),
                        containsString("signature="),
                        containsString("verification=")
                )))
                .andRespond(withSuccess("""
                        {
                          "resultCode": "0000",
                          "resultMsg": "정상 처리",
                          "tid": "tid-1",
                          "mid": "INIpayTest",
                          "MOID": "order-1",
                          "TotPrice": "100",
                          "payMethod": "Card",
                          "applDate": "20260507",
                          "applTime": "153000"
                        }
                        """, MediaType.APPLICATION_JSON));

        var result = httpClient.approve(request("https://stgstdpay.inicis.com/api", "stg", null), order());

        assertThat(result.approved()).isTrue();
        assertThat(result.tid()).isEqualTo("tid-1");
        assertThat(result.orderId()).isEqualTo("order-1");
        assertThat(result.totalPrice()).isEqualTo(100);
        server.verify();
    }

    @Test
    @DisplayName("승인 provider 호출 실패는 PAYMENT_PROVIDER_UNAVAILABLE로 변환한다")
    void approve_providerFailure_throwsProviderUnavailable() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).build();
        InicisPaymentClient httpClient = httpClient(builder, "INIpayTest", "sign-key");
        server.expect(requestTo("https://stgstdpay.inicis.com/api"))
                .andRespond(withServerError());

        assertThatThrownBy(() -> httpClient.approve(request("https://stgstdpay.inicis.com/api", "stg", null), order()))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_PROVIDER_UNAVAILABLE);
        server.verify();
    }

    @Test
    @DisplayName("망취소 성공 응답 0000은 true를 반환하고 실패 코드는 false를 반환한다")
    void netCancel_mapsSuccessCodeToBoolean() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).ignoreExpectOrder(true).build();
        InicisPaymentClient httpClient = httpClient(builder, "INIpayTest", "sign-key");
        server.expect(requestTo("https://stgstdpay.inicis.com/netCancel"))
                .andExpect(method(HttpMethod.POST))
                .andExpect(content().string(allOf(
                        containsString("resultCode=FAILED"),
                        containsString("resultMsg=%EC%8B%A4%ED%8C%A8")
                )))
                .andRespond(withSuccess("{\"resultCode\":\"0000\"}", MediaType.APPLICATION_JSON));
        server.expect(requestTo("https://stgstdpay.inicis.com/netCancel-rejected"))
                .andRespond(withSuccess("{\"resultCode\":\"9999\"}", MediaType.APPLICATION_JSON));

        boolean canceled = httpClient.netCancel(
                request("https://stgstdpay.inicis.com/api", "stg", "https://stgstdpay.inicis.com/netCancel"),
                order(),
                "FAILED",
                "실패"
        );
        boolean rejected = httpClient.netCancel(
                request("https://stgstdpay.inicis.com/api", "stg", "https://stgstdpay.inicis.com/netCancel-rejected"),
                order(),
                "FAILED",
                "실패"
        );

        assertThat(canceled).isTrue();
        assertThat(rejected).isFalse();
        server.verify();
    }

    @Test
    @DisplayName("SIGN KEY 설정이 없으면 provider 호출 전에 설정 오류를 던진다")
    void approve_missingSignKeyThrowsConfigurationError() {
        InicisPaymentClient missingSignKeyClient = new InicisPaymentClient(
                restClientBuilder,
                new InicisPaymentProperties(
                        "INIpayTest",
                        null,
                        "https://api.example.com/inicis/return",
                        "https://api.example.com/inicis/close",
                        "https://pay.example/stdpay.js"
                ),
                new InicisSignatureGenerator()
        );

        assertThatThrownBy(() -> missingSignKeyClient.approve(
                request("https://stgstdpay.inicis.com/api", "stg", null),
                order()
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.PAYMENT_CONFIGURATION_MISSING);
        then(restClientBuilder).shouldHaveNoInteractions();
    }

    private InicisAuthReturnRequest request(String authUrl, String idcName, String netCancelUrl) {
        return new InicisAuthReturnRequest(
                "0000",
                "정상 처리되었습니다.",
                "INIpayTest",
                "order-1",
                "auth-token",
                idcName,
                authUrl,
                netCancelUrl,
                " ",
                "order-1"
        );
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

    private InicisPaymentClient httpClient(RestClient.Builder builder, String mid, String signKey) {
        return new InicisPaymentClient(
                builder,
                new InicisPaymentProperties(
                        mid,
                        signKey,
                        "https://api.example.com/inicis/return",
                        "https://api.example.com/inicis/close",
                        "https://pay.example/stdpay.js"
                ),
                new InicisSignatureGenerator()
        );
    }
}
