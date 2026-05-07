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
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
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
}
