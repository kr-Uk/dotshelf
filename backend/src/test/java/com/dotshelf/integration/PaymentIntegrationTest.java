package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.Instant;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.payment.repository.PageTransactionRepository;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.payment.repository.PaymentOrderRepository;
import com.dotshelf.domain.payment.service.InicisPaymentClient;
import com.dotshelf.domain.payment.service.InicisPaymentClient.InicisApprovalResult;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.TestFixtures;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class PaymentIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private PaymentOrderRepository paymentOrderRepository;

    @Autowired
    private PageWalletRepository pageWalletRepository;

    @Autowired
    private PageTransactionRepository pageTransactionRepository;

    @MockitoBean
    private InicisPaymentClient inicisPaymentClient;

    @BeforeEach
    void cleanDatabase() {
        pageTransactionRepository.deleteAll();
        pageWalletRepository.deleteAll();
        paymentOrderRepository.deleteAll();
        userRepository.deleteAll();
    }

    @Test
    @DisplayName("HTTP로 페이지 충전 주문을 생성하면 READY 주문이 DB에 저장된다")
    void createPageChargeOrder_persistsReadyOrder() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));

        mockMvc.perform(post("/v1/payments/page-charge/orders")
                        .header("X-Test-Admin-User-Id", user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "pagesAmount":100,
                                  "amountKrw":100,
                                  "platform":"WEB",
                                  "buyerName":"홍길동",
                                  "buyerTel":"010-1234-5678",
                                  "buyerEmail":"buyer@example.com"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.status").value("READY"))
                .andExpect(jsonPath("$.result.inicisForm.parameters.oid").isString());

        assertThat(paymentOrderRepository.findAll()).hasSize(1);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);
        assertThat(order.getUserId()).isEqualTo(user.getId());
        assertThat(order.getPagesAmount()).isEqualTo(100);
        assertThat(order.getStatus()).isEqualTo(PaymentOrder.STATUS_READY);
    }

    @Test
    @DisplayName("충전 페이지 수와 결제 금액이 다르면 주문을 만들지 않는다")
    void createPageChargeOrder_amountMismatchReturnsBadRequest() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));

        mockMvc.perform(post("/v1/payments/page-charge/orders")
                        .header("X-Test-Admin-User-Id", user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "pagesAmount":100,
                                  "amountKrw":999,
                                  "buyerName":"홍길동",
                                  "buyerTel":"010-1234-5678",
                                  "buyerEmail":"buyer@example.com"
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false));

        assertThat(paymentOrderRepository.findAll()).isEmpty();
    }

    @Test
    @DisplayName("지갑이 없으면 wallet 조회는 0 페이지를 반환한다")
    void getWallet_missingWalletReturnsZero() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));

        mockMvc.perform(get("/v1/payments/wallet")
                        .header("X-Test-Admin-User-Id", user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.userId").value(user.getId()))
                .andExpect(jsonPath("$.result.balancePages").value(0));
    }

    @Test
    @DisplayName("주문 소유자는 결제창 HTML과 주문 상태를 조회할 수 있다")
    void renderPaymentPageAndGetOrderStatus_ownerSucceeds() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));
        createOrder(user.getId(), 100);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);

        mockMvc.perform(get("/v1/payments/inicis/request/{orderId}", order.getOrderId())
                        .header("X-Test-Admin-User-Id", user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("SendPayForm_id")))
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("name=\"oid\" value=\"" + order.getOrderId() + "\"")));

        mockMvc.perform(get("/v1/payments/orders/{orderId}", order.getOrderId())
                        .header("X-Test-Admin-User-Id", user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.orderId").value(order.getOrderId()))
                .andExpect(jsonPath("$.result.status").value(PaymentOrder.STATUS_READY));
    }

    @Test
    @DisplayName("결제 수단과 INICIS close 페이지는 인증 사용자에게 안정적인 빈 응답/HTML을 반환한다")
    void getMethodsAndClosePage_returnStableResponses() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));

        mockMvc.perform(get("/v1/payments/methods")
                        .header("X-Test-Admin-User-Id", user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.methods").isArray());

        mockMvc.perform(get("/v1/payments/inicis/close"))
                .andExpect(status().isOk())
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("결제창이 닫혔습니다.")));
    }

    @Test
    @DisplayName("INICIS return 성공 콜백은 주문 DONE, 지갑 충전, page transaction 생성을 DB에 반영한다")
    void inicisReturn_successCompletesOrderAndChargesWallet() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));
        createOrder(user.getId(), 100);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);
        stubApprovalSuccess();

        performInicisReturn(order)
                .andExpect(status().isOk())
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("상태: DONE")));

        PaymentOrder completed = paymentOrderRepository.findByOrderId(order.getOrderId()).orElseThrow();
        assertThat(completed.getStatus()).isEqualTo(PaymentOrder.STATUS_DONE);
        assertThat(completed.getProviderTransactionId()).isEqualTo("tid-" + order.getOrderId());
        assertThat(pageWalletRepository.findByUserId(user.getId()).orElseThrow().getBalancePages()).isEqualTo(100);
        assertThat(pageTransactionRepository.findAll())
                .singleElement()
                .satisfies(transaction -> {
                    assertThat(transaction.getPaymentOrderId()).isEqualTo(completed.getId());
                    assertThat(transaction.getPagesDelta()).isEqualTo(100);
                    assertThat(transaction.getBalanceAfter()).isEqualTo(100);
                });
    }

    @Test
    @DisplayName("이미 완료된 INICIS return 재호출은 외부 승인과 지갑 충전을 반복하지 않는다")
    void inicisReturn_duplicateCallbackIsIdempotent() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));
        createOrder(user.getId(), 100);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);
        stubApprovalSuccess();

        performInicisReturn(order).andExpect(status().isOk());
        performInicisReturn(order)
                .andExpect(status().isOk())
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("이미 완료된 결제입니다.")));

        assertThat(pageWalletRepository.findByUserId(user.getId()).orElseThrow().getBalancePages()).isEqualTo(100);
        assertThat(pageTransactionRepository.findAll()).hasSize(1);
        then(inicisPaymentClient).should(times(1)).approve(any(), any());
    }

    @Test
    @DisplayName("INICIS return 실패 코드는 주문을 FAILED로 만들고 외부 승인/지갑 충전을 수행하지 않는다")
    void inicisReturn_failureCodeMarksFailedWithoutApproval() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("payer"));
        createOrder(user.getId(), 100);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);

        mockMvc.perform(post("/v1/payments/inicis/return")
                        .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                        .param("resultCode", "V801")
                        .param("resultMsg", "사용자 취소")
                        .param("mid", "test-mid")
                        .param("orderNumber", order.getOrderId())
                        .param("merchantData", order.getOrderId()))
                .andExpect(status().isOk())
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("상태: FAILED")))
                .andExpect(org.springframework.test.web.servlet.result.MockMvcResultMatchers.content()
                        .string(org.hamcrest.Matchers.containsString("사용자 취소")));

        PaymentOrder failed = paymentOrderRepository.findByOrderId(order.getOrderId()).orElseThrow();
        assertThat(failed.getStatus()).isEqualTo(PaymentOrder.STATUS_FAILED);
        assertThat(pageWalletRepository.findByUserId(user.getId())).isEmpty();
        assertThat(pageTransactionRepository.findAll()).isEmpty();
        then(inicisPaymentClient).should(never()).approve(any(), any());
    }

    @Test
    @DisplayName("다른 사용자는 결제 주문 상태를 조회할 수 없다")
    void getOrderStatus_otherUserReturnsNotFound() throws Exception {
        var owner = userRepository.save(TestFixtures.user("owner"));
        var other = userRepository.save(TestFixtures.user("other"));
        userRepository.flush();
        createOrder(owner.getId(), 100);
        PaymentOrder order = paymentOrderRepository.findAll().get(0);

        mockMvc.perform(get("/v1/payments/orders/{orderId}", order.getOrderId())
                        .header("X-Test-Admin-User-Id", other.getId().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.success").value(false));
    }

    private void createOrder(Long userId, int pagesAmount) throws Exception {
        mockMvc.perform(post("/v1/payments/page-charge/orders")
                        .header("X-Test-Admin-User-Id", userId.toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "pagesAmount":%d,
                                  "amountKrw":%d,
                                  "platform":"WEB",
                                  "buyerName":"홍길동",
                                  "buyerTel":"010-1234-5678",
                                  "buyerEmail":"buyer@example.com"
                                }
                                """.formatted(pagesAmount, pagesAmount)))
                .andExpect(status().isOk());
    }

    private org.springframework.test.web.servlet.ResultActions performInicisReturn(PaymentOrder order) throws Exception {
        return mockMvc.perform(post("/v1/payments/inicis/return")
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .param("resultCode", "0000")
                .param("resultMsg", "승인")
                .param("mid", "test-mid")
                .param("orderNumber", order.getOrderId())
                .param("authToken", "auth-token")
                .param("idc_name", "stg")
                .param("authUrl", "https://stgstdpay.inicis.com/api")
                .param("netCancelUrl", "https://stgstdpay.inicis.com/netCancel")
                .param("charset", "UTF-8")
                .param("merchantData", order.getOrderId()));
    }

    private void stubApprovalSuccess() {
        given(inicisPaymentClient.approve(any(), any(PaymentOrder.class))).willAnswer(invocation -> {
            PaymentOrder order = invocation.getArgument(1);
            return new InicisApprovalResult(
                    "0000",
                    "승인",
                    "tid-" + order.getOrderId(),
                    "test-mid",
                    order.getOrderId(),
                    order.getAmountKrw(),
                    "Card",
                    Instant.now()
            );
        });
    }
}
