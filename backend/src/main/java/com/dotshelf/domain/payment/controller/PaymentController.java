package com.dotshelf.domain.payment.controller;

import java.util.Map;

import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.util.HtmlUtils;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisPaymentPageResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.InicisReturnResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.CursorPageResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageTransactionResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentPolicyResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.service.PaymentService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Payment", description = "페이지 충전 / 구매 내역 / 결제 수단")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/payments")
public class PaymentController {

    private final PaymentService paymentService;

    @Deprecated
    @Operation(summary = "페이지 충전 즉시 처리", description = "테스트 또는 레거시 용도로 페이지 잔액을 즉시 충전합니다. 신규 결제 흐름에서는 주문 생성 API를 사용합니다.")
    @PostMapping("/page-charge")
    public ApiResponse<WalletResponse> charge(
            @CurrentUser Long userId,
            @RequestBody @Valid PageChargeRequest request
    ) {
        return ApiResponse.success(paymentService.charge(userId, request));
    }

    @Operation(summary = "페이지 충전 주문 생성", description = "INICIS 결제를 시작하기 위한 페이지 충전 주문을 생성합니다.")
    @PostMapping("/page-charge/orders")
    public ApiResponse<PageChargeOrderResponse> createPageChargeOrder(
            @CurrentUser Long userId,
            @RequestBody @Valid PageChargeOrderRequest request
    ) {
        return ApiResponse.success(paymentService.createPageChargeOrder(userId, request));
    }

    @Operation(summary = "INICIS 결제 페이지 렌더링", description = "생성된 주문 ID로 INICIS 표준 결제창을 호출하는 HTML 페이지를 반환합니다.")
    @GetMapping(value = "/inicis/request/{orderId}", produces = MediaType.TEXT_HTML_VALUE)
    public String renderInicisPaymentPage(
            @CurrentUser Long userId,
            @PathVariable String orderId
    ) {
        return renderPaymentPage(paymentService.getInicisPaymentPage(userId, orderId));
    }

    @Operation(summary = "INICIS 인증 결과 수신", description = "INICIS 표준 결제창에서 전달한 인증 결과를 받아 승인 처리 후 결과 HTML을 반환합니다.")
    @PostMapping(value = "/inicis/return",
            consumes = MediaType.APPLICATION_FORM_URLENCODED_VALUE,
            produces = MediaType.TEXT_HTML_VALUE)
    public String handleInicisReturn(@RequestParam Map<String, String> parameters) {
        InicisReturnResponse response = paymentService.handleInicisReturn(InicisAuthReturnRequest.from(parameters));
        return renderReturnPage(response);
    }

    @Operation(summary = "INICIS 결제창 닫힘 처리", description = "사용자가 INICIS 결제창을 닫았을 때 표시할 종료 HTML을 반환합니다.")
    @GetMapping(value = "/inicis/close", produces = MediaType.TEXT_HTML_VALUE)
    public String closeInicisPaymentPage() {
        return """
                <!doctype html>
                <html lang="ko">
                <head><meta charset="UTF-8"><title>DotShelf 결제창 닫힘</title></head>
                <body><p>결제창이 닫혔습니다.</p><script>window.close();</script></body>
                </html>
                """;
    }

    @Operation(summary = "결제 주문 상태 조회", description = "주문 ID 기준으로 페이지 충전 주문의 현재 결제 상태를 조회합니다.")
    @GetMapping("/orders/{orderId}")
    public ApiResponse<PaymentOrderStatusResponse> getOrderStatus(
            @CurrentUser Long userId,
            @PathVariable String orderId
    ) {
        return ApiResponse.success(paymentService.getOrderStatus(userId, orderId));
    }

    @Operation(summary = "구매 내역 조회", description = "현재 사용자의 에셋 구매 및 결제 내역을 조회합니다.")
    @GetMapping("/purchases")
    public ApiResponse<PurchaseHistoryListResponse> getPurchases(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getPurchases(userId));
    }

    @Operation(summary = "페이지 충전·사용 내역 조회 (list)",
            description = "현재 사용자의 페이지 충전(CHARGE) / 사용 내역을 최신순으로 조회합니다. "
                    + "응답 크기 cap: limit 미지정 시 100건, 최대 500건. "
                    + "트랜잭션이 많이 누적된 사용자는 /transactions/cursor 를 우선 사용. "
                    + "응답의 `referenceType` / `referenceId` 는 Phase 2 에서 제거 예정 — `paymentOrderId` 사용 권장.")
    @GetMapping("/transactions")
    public ApiResponse<PageTransactionListResponse> getTransactions(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "100") int limit
    ) {
        return ApiResponse.success(paymentService.getTransactions(userId, limit));
    }

    @Operation(summary = "페이지 충전·사용 내역 cursor paging 조회",
            description = "(createdAt DESC, id DESC) 정렬 기준 cursor paging. "
                    + "응답의 nextCursor 를 다음 요청 cursor 파라미터에 넣으면 다음 페이지. "
                    + "limit 미지정 시 20건, 최대 500건.")
    @GetMapping("/transactions/cursor")
    public ApiResponse<CursorPageResponse<PageTransactionResponse>> getTransactionsCursor(
            @CurrentUser Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(paymentService.getTransactionsCursor(userId, cursor, limit));
    }

    @Operation(summary = "에셋 구매 내역 cursor paging 조회",
            description = "(purchasedAt DESC, id DESC) 정렬 기준 cursor paging. "
                    + "응답의 nextCursor 를 다음 요청 cursor 파라미터에 넣으면 다음 페이지. "
                    + "limit 미지정 시 20건, 최대 500건.")
    @GetMapping("/purchases/cursor")
    public ApiResponse<CursorPageResponse<PurchaseHistoryResponse>> getPurchasesCursor(
            @CurrentUser Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(paymentService.getPurchasesCursor(userId, cursor, limit));
    }

    @Operation(summary = "지갑 조회", description = "현재 사용자의 페이지 잔액과 지갑 정보를 조회합니다.")
    @GetMapping("/wallet")
    public ApiResponse<WalletResponse> getWallet(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getWallet(userId));
    }

    @Operation(summary = "결제 수단 조회", description = "현재 사용자가 사용할 수 있는 결제 수단 목록을 조회합니다.")
    @GetMapping("/methods")
    public ApiResponse<PaymentMethodListResponse> getMethods(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getMethods(userId));
    }

    @Operation(summary = "결제 정책 조회",
            description = "1회 결제 한도, 결제 수단 제약, 유효기간 안내, 사업자 정보, 약관/환불 URL, 카드사별 한도 안내 등 결제 정책을 조회합니다.")
    @GetMapping("/policy")
    public ApiResponse<PaymentPolicyResponse> getPolicy() {
        return ApiResponse.success(paymentService.getPolicy());
    }

    private String renderPaymentPage(InicisPaymentPageResponse response) {
        return """
                <!doctype html>
                <html lang="ko">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>DotShelf 결제</title>
                    <script src="%s"></script>
                </head>
                <body>
                    <form id="SendPayForm_id" name="SendPayForm_id" method="post">
                %s
                    </form>
                    <script>
                        window.addEventListener('load', function () {
                            INIStdPay.pay('SendPayForm_id');
                        });
                    </script>
                </body>
                </html>
                """.formatted(escape(response.scriptUrl()), hiddenInputs(response.parameters()));
    }

    private String renderReturnPage(InicisReturnResponse response) {
        String message = response.resultMessage() == null ? "결제 처리가 완료되었습니다." : response.resultMessage();
        return """
                <!doctype html>
                <html lang="ko">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>DotShelf 결제 결과</title>
                </head>
                <body>
                    <p>주문번호: %s</p>
                    <p>상태: %s</p>
                    <p>결과: %s</p>
                    <script>
                        setTimeout(function () { window.close(); }, 1500);
                    </script>
                </body>
                </html>
                """.formatted(escape(response.orderId()), escape(response.status()), escape(message));
    }

    private String hiddenInputs(Map<String, String> parameters) {
        StringBuilder inputs = new StringBuilder();
        parameters.forEach((key, value) -> inputs
                .append("        <input type=\"hidden\" name=\"")
                .append(escape(key))
                .append("\" value=\"")
                .append(escape(value))
                .append("\">\n"));
        return inputs.toString();
    }

    private String escape(String value) {
        return HtmlUtils.htmlEscape(value == null ? "" : value);
    }
}
