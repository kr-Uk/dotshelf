package com.dotshelf.domain.payment.service;

import java.util.List;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "payment.policy")
public record PaymentPolicyProperties(
        int maxChargeKrw,
        String validityNotice,
        String orderNameSuffix,
        String responsibilityNotice,
        LegalUrls legalUrls,
        Merchant merchant,
        ComplaintContact complaintContact,
        List<CardLimitInfo> cardLimits
) {

    private static final int DEFAULT_MAX_CHARGE_KRW = 100_000;
    private static final String DEFAULT_VALIDITY_NOTICE =
            "구매 포인트와 아이템 이용권한의 유효기간은 결제일로부터 1년이며, 서비스 운영 기간 동안 자동으로 갱신됩니다.";
    private static final String DEFAULT_ORDER_NAME_SUFFIX = "(유효기간 1년/자동갱신)";

    public PaymentPolicyProperties {
        if (maxChargeKrw <= 0) {
            maxChargeKrw = DEFAULT_MAX_CHARGE_KRW;
        }
        validityNotice = blankToDefault(validityNotice, DEFAULT_VALIDITY_NOTICE);
        orderNameSuffix = blankToDefault(orderNameSuffix, DEFAULT_ORDER_NAME_SUFFIX);
        responsibilityNotice = responsibilityNotice == null ? "" : responsibilityNotice.trim();
        legalUrls = legalUrls == null ? LegalUrls.empty() : legalUrls;
        merchant = merchant == null ? Merchant.empty() : merchant;
        complaintContact = complaintContact == null ? ComplaintContact.empty() : complaintContact;
        cardLimits = cardLimits == null ? List.of() : List.copyOf(cardLimits);
    }

    private static String blankToDefault(String value, String fallback) {
        if (value == null) {
            return fallback;
        }
        String trimmed = value.trim();
        return trimmed.isEmpty() ? fallback : trimmed;
    }

    public record LegalUrls(String paymentTerms, String refundPolicy, String privacy, String businessInfo) {

        public LegalUrls {
            paymentTerms = nullToEmpty(paymentTerms);
            refundPolicy = nullToEmpty(refundPolicy);
            privacy = nullToEmpty(privacy);
            businessInfo = nullToEmpty(businessInfo);
        }

        public static LegalUrls empty() {
            return new LegalUrls("", "", "", "");
        }
    }

    public record Merchant(
            String name,
            String registrationNumber,
            String representative,
            String address,
            String tel,
            String email,
            String mailOrderRegistrationNumber
    ) {

        public Merchant {
            name = nullToEmpty(name);
            registrationNumber = nullToEmpty(registrationNumber);
            representative = nullToEmpty(representative);
            address = nullToEmpty(address);
            tel = nullToEmpty(tel);
            email = nullToEmpty(email);
            mailOrderRegistrationNumber = nullToEmpty(mailOrderRegistrationNumber);
        }

        public static Merchant empty() {
            return new Merchant("", "", "", "", "", "", "");
        }
    }

    public record ComplaintContact(String name, String tel) {

        public ComplaintContact {
            name = nullToEmpty(name);
            tel = nullToEmpty(tel);
        }

        public static ComplaintContact empty() {
            return new ComplaintContact("", "");
        }
    }

    public record CardLimitInfo(String issuer, String description) {

        public CardLimitInfo {
            issuer = nullToEmpty(issuer);
            description = nullToEmpty(description);
        }
    }

    private static String nullToEmpty(String value) {
        return value == null ? "" : value.trim();
    }
}
