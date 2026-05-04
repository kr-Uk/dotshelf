package com.dotshelf.domain.payment.service;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "payment.inicis")
public record InicisPaymentProperties(
        String mid,
        String signKey,
        String returnUrl,
        String closeUrl,
        String requestScriptUrl
) {

    public InicisPaymentProperties {
        mid = blankToNull(mid);
        signKey = blankToNull(signKey);
        returnUrl = blankToNull(returnUrl);
        closeUrl = blankToNull(closeUrl);
        requestScriptUrl = blankToNull(requestScriptUrl);
        if (requestScriptUrl == null) {
            requestScriptUrl = "https://stdpay.inicis.com/stdjs/INIStdPay.js";
        }
    }

    private static String blankToNull(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return value.trim();
    }
}
