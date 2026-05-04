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
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeRequest;
import com.dotshelf.domain.payment.dto.PaymentDtos.PageChargeOrderResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentMethodListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PaymentOrderStatusResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.PurchaseHistoryListResponse;
import com.dotshelf.domain.payment.dto.PaymentDtos.WalletResponse;
import com.dotshelf.domain.payment.service.PaymentService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

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
    @PostMapping("/page-charge")
    public ApiResponse<WalletResponse> charge(
            @CurrentUser Long userId,
            @RequestBody @Valid PageChargeRequest request
    ) {
        return ApiResponse.success(paymentService.charge(userId, request));
    }

    @PostMapping("/page-charge/orders")
    public ApiResponse<PageChargeOrderResponse> createPageChargeOrder(
            @CurrentUser Long userId,
            @RequestBody @Valid PageChargeOrderRequest request
    ) {
        return ApiResponse.success(paymentService.createPageChargeOrder(userId, request));
    }

    @GetMapping(value = "/inicis/request/{orderId}", produces = MediaType.TEXT_HTML_VALUE)
    public String renderInicisPaymentPage(@PathVariable String orderId) {
        return renderPaymentPage(paymentService.getInicisPaymentPage(orderId));
    }

    @PostMapping(value = "/inicis/return",
            consumes = MediaType.APPLICATION_FORM_URLENCODED_VALUE,
            produces = MediaType.TEXT_HTML_VALUE)
    public String handleInicisReturn(@RequestParam Map<String, String> parameters) {
        InicisReturnResponse response = paymentService.handleInicisReturn(InicisAuthReturnRequest.from(parameters));
        return renderReturnPage(response);
    }

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

    @GetMapping("/orders/{orderId}")
    public ApiResponse<PaymentOrderStatusResponse> getOrderStatus(
            @CurrentUser Long userId,
            @PathVariable String orderId
    ) {
        return ApiResponse.success(paymentService.getOrderStatus(userId, orderId));
    }

    @GetMapping("/purchases")
    public ApiResponse<PurchaseHistoryListResponse> getPurchases(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getPurchases(userId));
    }

    @GetMapping("/wallet")
    public ApiResponse<WalletResponse> getWallet(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getWallet(userId));
    }

    @GetMapping("/methods")
    public ApiResponse<PaymentMethodListResponse> getMethods(@CurrentUser Long userId) {
        return ApiResponse.success(paymentService.getMethods(userId));
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
