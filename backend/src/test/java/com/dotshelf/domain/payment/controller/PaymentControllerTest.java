package com.dotshelf.domain.payment.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisPaymentFormResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisPaymentPageResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisReturnResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse.CardLimitView;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse.ComplaintContactView;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse.LegalUrlsView;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse.MerchantView;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.service.PaymentService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class PaymentControllerTest {

    @Mock
    private PaymentService paymentService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new PaymentController(paymentService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("Deprecated page-charge는 validation과 service 호출을 수행한다")
    void charge_validatesAndCallsService() throws Exception {
        given(paymentService.charge(any(), any(PageChargeRequest.class))).willReturn(new WalletResponse(1L, 100));

        mockMvc.perform(post("/v1/payments/page-charge")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"pagesAmount":100,"amountKrw":1000,"platform":"TEST"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.balancePages").value(100));

        mockMvc.perform(post("/v1/payments/page-charge")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"pagesAmount":0,"amountKrw":0}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("페이지 충전 주문 생성은 구매자 정보를 검증한다")
    void createPageChargeOrder_validatesBuyerFields() throws Exception {
        given(paymentService.createPageChargeOrder(any(), any(PageChargeOrderRequest.class)))
                .willReturn(new PageChargeOrderResponse(
                        "order-1", "100P 충전", 100, 1000, "KRW", "INICIS", "READY",
                        "/v1/payments/inicis/request/order-1",
                        new InicisPaymentFormResponse("https://pay.example/script.js", Map.of("oid", "order-1")),
                        Instant.now(), Instant.now().plusSeconds(1800)
                ));

        mockMvc.perform(post("/v1/payments/page-charge/orders")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "pagesAmount":100,
                                  "amountKrw":1000,
                                  "platform":"WEB",
                                  "buyerName":"홍길동",
                                  "buyerTel":"010-1234-5678",
                                  "buyerEmail":"buyer@example.com"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.orderId").value("order-1"));

        mockMvc.perform(post("/v1/payments/page-charge/orders")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"pagesAmount":100,"amountKrw":1000,"buyerName":" ","buyerTel":"abc","buyerEmail":"bad"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("구매자")));
    }

    @Test
    @DisplayName("INICIS 결제 페이지 렌더링은 HTML escape를 적용한다")
    void renderInicisPaymentPage_escapesHtml() throws Exception {
        Map<String, String> parameters = new LinkedHashMap<>();
        parameters.put("oid", "order-1");
        parameters.put("buyername", "<script>");
        given(paymentService.getInicisPaymentPage(1L, "order-1"))
                .willReturn(new InicisPaymentPageResponse("order-1", "https://pay.example/stdpay.js", parameters));

        mockMvc.perform(get("/v1/payments/inicis/request/order-1").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_HTML))
                .andExpect(content().string(org.hamcrest.Matchers.containsString("INIStdPay.pay")))
                .andExpect(content().string(org.hamcrest.Matchers.containsString("&lt;script&gt;")));
    }

    @Test
    @DisplayName("INICIS return은 form 파라미터를 request DTO로 변환해 처리한다")
    void handleInicisReturn_mapsFormParameters() throws Exception {
        given(paymentService.handleInicisReturn(any()))
                .willReturn(new InicisReturnResponse("order-1", "DONE", "0000", "승인", new WalletResponse(1L, 100)));

        mockMvc.perform(post("/v1/payments/inicis/return")
                        .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                        .param("resultCode", "0000")
                        .param("resultMsg", "승인")
                        .param("orderNumber", "order-1")
                        .param("authToken", "token"))
                .andExpect(status().isOk())
                .andExpect(content().string(org.hamcrest.Matchers.containsString("order-1")))
                .andExpect(content().string(org.hamcrest.Matchers.containsString("DONE")));
    }

    @Test
    @DisplayName("INICIS close 페이지는 브라우저 닫기 스크립트를 반환한다")
    void closePage_returnsHtml() throws Exception {
        mockMvc.perform(get("/v1/payments/inicis/close"))
                .andExpect(status().isOk())
                .andExpect(content().string(org.hamcrest.Matchers.containsString("window.close")));
    }

    @Test
    @DisplayName("주문 상태/지갑/구매내역/결제수단 조회는 current userId를 전달한다")
    void paymentQueries_passCurrentUser() throws Exception {
        given(paymentService.getOrderStatus(1L, "order-1"))
                .willReturn(new PaymentOrderStatusResponse("order-1", null, "READY", 100, 1000, new WalletResponse(1L, 0)));
        given(paymentService.getPurchases(1L)).willReturn(new PurchaseHistoryListResponse(List.of()));
        given(paymentService.getWallet(1L)).willReturn(new WalletResponse(1L, 100));
        given(paymentService.getMethods(1L)).willReturn(new PaymentMethodListResponse(List.of("INICIS")));

        mockMvc.perform(get("/v1/payments/orders/order-1").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.orderId").value("order-1"));

        mockMvc.perform(get("/v1/payments/purchases").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.purchases").isArray());

        mockMvc.perform(get("/v1/payments/wallet").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.balancePages").value(100));

        mockMvc.perform(get("/v1/payments/methods").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.methods[0]").value("INICIS"));

        then(paymentService).should().getOrderStatus(1L, "order-1");
    }

    @Test
    @DisplayName("페이지 충전·사용 내역 조회는 200 과 최신순 PageTransactionResponse 목록을 응답한다")
    void getTransactions_returnsTransactionList() throws Exception {
        given(paymentService.getTransactions(1L)).willReturn(new PageTransactionListResponse(List.of(
                new PageTransactionResponse(2L, "CHARGE", 50, 150, "INICIS_PAYMENT", "PAYMENT_ORDER", 11L,
                        Instant.parse("2026-05-14T02:00:00Z")),
                new PageTransactionResponse(1L, "CHARGE", 100, 100, "INICIS_PAYMENT", "PAYMENT_ORDER", 10L,
                        Instant.parse("2026-05-14T01:00:00Z"))
        )));

        mockMvc.perform(get("/v1/payments/transactions").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.transactions").isArray())
                .andExpect(jsonPath("$.result.transactions[0].id").value(2))
                .andExpect(jsonPath("$.result.transactions[0].type").value("CHARGE"))
                .andExpect(jsonPath("$.result.transactions[0].pagesDelta").value(50))
                .andExpect(jsonPath("$.result.transactions[0].balanceAfter").value(150))
                .andExpect(jsonPath("$.result.transactions[0].reason").value("INICIS_PAYMENT"))
                .andExpect(jsonPath("$.result.transactions[0].referenceType").value("PAYMENT_ORDER"))
                .andExpect(jsonPath("$.result.transactions[0].referenceId").value(11))
                .andExpect(jsonPath("$.result.transactions[1].balanceAfter").value(100));

        then(paymentService).should().getTransactions(1L);
    }

    @Test
    @DisplayName("결제 정책 조회는 200 과 카드결제·1회 한도·법적 URL·사업자정보 묶음을 응답한다")
    void getPolicy_returnsPolicyBundle() throws Exception {
        given(paymentService.getPolicy()).willReturn(policyResponseFixture());

        mockMvc.perform(get("/v1/payments/policy"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.maxChargeKrwPerOrder").value(100_000))
                .andExpect(jsonPath("$.result.currency").value("KRW"))
                .andExpect(jsonPath("$.result.paymentMethod").value("CARD"))
                .andExpect(jsonPath("$.result.validityNotice").value(org.hamcrest.Matchers.containsString("1년")))
                .andExpect(jsonPath("$.result.responsibilityNotice").value(org.hamcrest.Matchers.containsString("DotShelf")))
                .andExpect(jsonPath("$.result.legalUrls.paymentTerms").value("https://example.com/terms"))
                .andExpect(jsonPath("$.result.legalUrls.refundPolicy").value("https://example.com/refund"))
                .andExpect(jsonPath("$.result.legalUrls.privacy").value("https://example.com/privacy"))
                .andExpect(jsonPath("$.result.legalUrls.businessInfo").value("https://example.com/biz"))
                .andExpect(jsonPath("$.result.merchant.name").value("DotShelf"))
                .andExpect(jsonPath("$.result.merchant.registrationNumber").value("123-45-67890"))
                .andExpect(jsonPath("$.result.complaintContact.name").value("홍길동"))
                .andExpect(jsonPath("$.result.complaintContact.tel").value("02-0000-0000"))
                .andExpect(jsonPath("$.result.cardLimits[0].issuer").value("BC"))
                .andExpect(jsonPath("$.result.cardLimits[0].description").value("1일 20만원"));

        then(paymentService).should().getPolicy();
    }

    private PaymentPolicyResponse policyResponseFixture() {
        return new PaymentPolicyResponse(
                100_000,
                "KRW",
                "CARD",
                "구매 포인트와 아이템 이용권한의 유효기간은 결제일로부터 1년이며, 서비스 운영 기간 동안 자동으로 갱신됩니다.",
                "DotShelf에서 운영되는 사이트 내 판매되는 모든 상품은 DotShelf에서 책임지고 있습니다.",
                new LegalUrlsView(
                        "https://example.com/terms",
                        "https://example.com/refund",
                        "https://example.com/privacy",
                        "https://example.com/biz"
                ),
                new MerchantView(
                        "DotShelf",
                        "123-45-67890",
                        "김지호",
                        "서울특별시 강남구",
                        "02-0000-0000",
                        "biz@dotshelf.digital",
                        "2026-서울강남-XXXX"
                ),
                new ComplaintContactView("홍길동", "02-0000-0000"),
                List.of(new CardLimitView("BC", "1일 20만원"))
        );
    }
}
