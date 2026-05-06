package com.dotshelf.domain.payment.service;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

import org.springframework.stereotype.Component;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

@Component
public class InicisSignatureGenerator {

    public String paymentSignature(String orderId, int price, String timestamp) {
        return sha256("oid=" + orderId + "&price=" + price + "&timestamp=" + timestamp);
    }

    public String paymentVerification(String orderId, int price, String signKey, String timestamp) {
        return sha256("oid=" + orderId + "&price=" + price + "&signKey=" + signKey + "&timestamp=" + timestamp);
    }

    public String approvalSignature(String authToken, String timestamp) {
        return sha256("authToken=" + authToken + "&timestamp=" + timestamp);
    }

    public String approvalVerification(String authToken, String signKey, String timestamp) {
        return sha256("authToken=" + authToken + "&signKey=" + signKey + "&timestamp=" + timestamp);
    }

    public String merchantKey(String signKey) {
        return sha256(signKey);
    }

    private String sha256(String value) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] bytes = digest.digest(value.getBytes(StandardCharsets.UTF_8));
            StringBuilder result = new StringBuilder(bytes.length * 2);
            for (byte data : bytes) {
                result.append(String.format("%02x", data));
            }
            return result.toString();
        } catch (NoSuchAlgorithmException e) {
            throw new CustomException(ErrorCode.INTERNAL_SERVER_ERROR, "SHA-256 알고리즘을 사용할 수 없습니다.");
        }
    }
}
