package com.dotshelf.domain.payment.service;

import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.Map;

import org.springframework.stereotype.Component;

import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Component
@RequiredArgsConstructor
public class InicisPaymentFormFactory {

    private static final String VERSION = "1.0";
    private static final String CURRENCY_WON = "WON";
    private static final String CHARSET = "UTF-8";
    private static final String ACCEPT_METHOD = "centerCd(Y)";

    private final InicisPaymentProperties properties;
    private final InicisSignatureGenerator signatureGenerator;

    public InicisPaymentForm create(PaymentOrder order) {
        String timestamp = String.valueOf(Instant.now().toEpochMilli());
        String signKey = requiredSignKey();
        Map<String, String> parameters = new LinkedHashMap<>();
        parameters.put("version", VERSION);
        parameters.put("gopaymethod", "");
        parameters.put("mid", requiredMid());
        parameters.put("oid", order.getOrderId());
        parameters.put("price", String.valueOf(order.getAmountKrw()));
        parameters.put("timestamp", timestamp);
        parameters.put("use_chkfake", "Y");
        parameters.put("signature", signatureGenerator.paymentSignature(order.getOrderId(), order.getAmountKrw(), timestamp));
        parameters.put("verification", signatureGenerator.paymentVerification(order.getOrderId(), order.getAmountKrw(), signKey, timestamp));
        parameters.put("mKey", signatureGenerator.merchantKey(signKey));
        parameters.put("currency", CURRENCY_WON);
        parameters.put("goodname", order.getOrderName());
        parameters.put("buyername", order.getBuyerName());
        parameters.put("buyertel", order.getBuyerTel());
        parameters.put("buyeremail", order.getBuyerEmail());
        parameters.put("returnUrl", requiredReturnUrl());
        parameters.put("closeUrl", requiredCloseUrl());
        parameters.put("acceptmethod", ACCEPT_METHOD);
        parameters.put("charset", CHARSET);
        parameters.put("merchantData", order.getOrderId());
        return new InicisPaymentForm(properties.requestScriptUrl(), parameters);
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

    private String requiredReturnUrl() {
        if (properties.returnUrl() == null) {
            throw new CustomException(ErrorCode.PAYMENT_CONFIGURATION_MISSING, "INICIS_RETURN_URL 설정이 필요합니다.");
        }
        return properties.returnUrl();
    }

    private String requiredCloseUrl() {
        if (properties.closeUrl() == null) {
            throw new CustomException(ErrorCode.PAYMENT_CONFIGURATION_MISSING, "INICIS_CLOSE_URL 설정이 필요합니다.");
        }
        return properties.closeUrl();
    }

    public record InicisPaymentForm(String scriptUrl, Map<String, String> parameters) {
    }
}
