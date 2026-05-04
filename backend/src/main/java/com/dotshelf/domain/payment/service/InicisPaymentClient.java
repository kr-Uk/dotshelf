package com.dotshelf.domain.payment.service;

import java.net.URI;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeParseException;
import java.util.Map;
import java.util.Set;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;

import com.dotshelf.domain.payment.dto.PaymentDtos.InicisAuthReturnRequest;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Slf4j
@Component
@RequiredArgsConstructor
public class InicisPaymentClient {

    private static final String SUCCESS_CODE = "0000";
    private static final String CHARSET = "UTF-8";
    private static final String FORMAT_JSON = "JSON";
    private static final ZoneId KOREA_ZONE = ZoneId.of("Asia/Seoul");
    private static final DateTimeFormatter APPROVED_AT_FORMATTER = DateTimeFormatter.ofPattern("yyyyMMddHHmmss");
    private static final ParameterizedTypeReference<Map<String, Object>> RESPONSE_TYPE = new ParameterizedTypeReference<>() {
    };
    private static final Set<String> ALLOWED_HOSTS = Set.of(
            "stgstdpay.inicis.com",
            "fcstdpay.inicis.com",
            "ksstdpay.inicis.com"
    );
    private static final Map<String, String> IDC_HOSTS = Map.of(
            "stg", "stgstdpay.inicis.com",
            "fc", "fcstdpay.inicis.com",
            "ks", "ksstdpay.inicis.com"
    );

    private final RestClient.Builder restClientBuilder;
    private final InicisPaymentProperties properties;
    private final InicisSignatureGenerator signatureGenerator;

    public InicisApprovalResult approve(InicisAuthReturnRequest request, PaymentOrder order) {
        validateProviderUrl(request.authUrl(), request.idcName());
        String timestamp = String.valueOf(Instant.now().toEpochMilli());
        MultiValueMap<String, String> body = approvalBody(
                request.authToken(),
                timestamp,
                order.getAmountKrw(),
                request.charset()
        );
        try {
            Map<String, Object> response = restClientBuilder.build()
                    .post()
                    .uri(request.authUrl())
                    .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                    .body(body)
                    .retrieve()
                    .body(RESPONSE_TYPE);
            return InicisApprovalResult.from(response);
        } catch (RestClientException e) {
            log.warn("KG Inicis payment approve failed (orderId={})", order.getOrderId(), e);
            throw new CustomException(ErrorCode.PAYMENT_PROVIDER_UNAVAILABLE);
        }
    }

    public void netCancel(InicisAuthReturnRequest request, PaymentOrder order, String resultCode, String resultMessage) {
        if (request.netCancelUrl() == null || request.netCancelUrl().isBlank()) {
            return;
        }
        validateProviderUrl(request.netCancelUrl(), request.idcName());
        String timestamp = String.valueOf(Instant.now().toEpochMilli());
        MultiValueMap<String, String> body = approvalBody(
                request.authToken(),
                timestamp,
                order.getAmountKrw(),
                request.charset()
        );
        body.add("resultCode", resultCode);
        body.add("resultMsg", resultMessage);
        try {
            restClientBuilder.build()
                    .post()
                    .uri(request.netCancelUrl())
                    .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                    .body(body)
                    .retrieve()
                    .toBodilessEntity();
        } catch (RestClientException e) {
            log.warn("KG Inicis payment net cancel failed (orderId={})", order.getOrderId(), e);
        }
    }

    private MultiValueMap<String, String> approvalBody(String authToken, String timestamp, int price, String charset) {
        String signKey = requiredSignKey();
        MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
        body.add("mid", requiredMid());
        body.add("authToken", authToken);
        body.add("timestamp", timestamp);
        body.add("signature", signatureGenerator.approvalSignature(authToken, timestamp));
        body.add("verification", signatureGenerator.approvalVerification(authToken, signKey, timestamp));
        body.add("charset", blankToDefault(charset, CHARSET));
        body.add("format", FORMAT_JSON);
        body.add("price", String.valueOf(price));
        return body;
    }

    private void validateProviderUrl(String url, String idcName) {
        URI uri;
        try {
            uri = URI.create(url);
        } catch (IllegalArgumentException e) {
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED, "KG이니시스 승인 URL이 올바르지 않습니다.");
        }
        if (!"https".equalsIgnoreCase(uri.getScheme())) {
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED, "KG이니시스 승인 URL이 올바르지 않습니다.");
        }
        String host = uri.getHost();
        if (!ALLOWED_HOSTS.contains(host)) {
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED, "KG이니시스 승인 URL이 허용되지 않은 호스트입니다.");
        }
        String expectedHost = IDC_HOSTS.get(idcName);
        if (expectedHost != null && !expectedHost.equals(host)) {
            throw new CustomException(ErrorCode.PAYMENT_APPROVAL_FAILED, "KG이니시스 IDC와 승인 URL이 일치하지 않습니다.");
        }
    }

    private String requiredMid() {
        if (properties.mid() == null) {
            throw new CustomException(ErrorCode.PAYMENT_CONFIGURATION_MISSING, "INICIS_MID 설정이 필요합니다.");
        }
        return properties.mid();
    }

    private String requiredSignKey() {
        if (properties.signKey() == null) {
            throw new CustomException(ErrorCode.PAYMENT_CONFIGURATION_MISSING, "INICIS_SIGN_KEY 설정이 필요합니다.");
        }
        return properties.signKey();
    }

    private String blankToDefault(String value, String defaultValue) {
        if (value == null || value.isBlank()) {
            return defaultValue;
        }
        return value.trim();
    }

    public record InicisApprovalResult(
            String resultCode,
            String resultMessage,
            String tid,
            String mid,
            String orderId,
            int totalPrice,
            String payMethod,
            Instant approvedAt
    ) {

        private boolean isSuccess() {
            return SUCCESS_CODE.equals(resultCode);
        }

        public static InicisApprovalResult from(Map<String, Object> response) {
            if (response == null) {
                return failed("EMPTY_RESPONSE", "KG이니시스 승인 응답이 비어 있습니다.");
            }
            String resultCode = stringValue(response.get("resultCode"));
            String resultMessage = stringValue(response.get("resultMsg"));
            return new InicisApprovalResult(
                    resultCode,
                    resultMessage,
                    stringValue(response.get("tid")),
                    stringValue(response.get("mid")),
                    stringValue(response.get("MOID")),
                    intValue(response.get("TotPrice")),
                    stringValue(response.get("payMethod")),
                    approvedAt(response)
            );
        }

        public static InicisApprovalResult failed(String resultCode, String resultMessage) {
            return new InicisApprovalResult(resultCode, resultMessage, null, null, null, 0, null, null);
        }

        public boolean approved() {
            return isSuccess();
        }

        private static String stringValue(Object value) {
            if (value == null) {
                return null;
            }
            String text = String.valueOf(value).trim();
            return text.isBlank() ? null : text;
        }

        private static int intValue(Object value) {
            String text = stringValue(value);
            if (text == null) {
                return 0;
            }
            try {
                return Integer.parseInt(text);
            } catch (NumberFormatException e) {
                return 0;
            }
        }

        private static Instant approvedAt(Map<String, Object> response) {
            String date = stringValue(response.get("applDate"));
            String time = stringValue(response.get("applTime"));
            if (date == null || time == null) {
                return null;
            }
            try {
                return LocalDateTime.parse(date + time, APPROVED_AT_FORMATTER)
                        .atZone(KOREA_ZONE)
                        .toInstant();
            } catch (DateTimeParseException e) {
                return null;
            }
        }
    }
}
