package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class InicisSignatureGeneratorTest {

    private final InicisSignatureGenerator signatureGenerator = new InicisSignatureGenerator();

    @Test
    @DisplayName("결제창 signature는 KG이니시스 규격 순서로 SHA-256 해시를 만든다")
    void paymentSignature_hashesInicisPaymentPayload() {
        String signature = signatureGenerator.paymentSignature("order-1", 1000, "1715000000000");

        assertThat(signature)
                .isEqualTo("3327521d282a317dd8b7c69a14857317e157b9d718abe15ea41756b18a872f22")
                .hasSize(64);
    }

    @Test
    @DisplayName("결제창 verification은 signKey를 포함해 SHA-256 해시를 만든다")
    void paymentVerification_hashesPayloadWithSignKey() {
        String verification = signatureGenerator.paymentVerification(
                "order-1",
                1000,
                "sign-key",
                "1715000000000"
        );

        assertThat(verification)
                .isEqualTo("f7d4e7d9d41225ff258f4c42be34cc30d02f1ed916c55f2e637a89b5d0695fca");
    }

    @Test
    @DisplayName("승인 signature와 verification은 authToken 기준으로 서로 다른 값을 만든다")
    void approvalHashes_includeAuthTokenAndSignKey() {
        String signature = signatureGenerator.approvalSignature("auth-token", "1715000000000");
        String verification = signatureGenerator.approvalVerification("auth-token", "sign-key", "1715000000000");

        assertThat(signature)
                .isEqualTo("5387cf13849195a12951299be6dfef20a738fd6834d28c3afe15ebd5f3d30f44");
        assertThat(verification)
                .isEqualTo("bcc6734c08092e4209fa81a42f1ad8851b3850d40ec78b8175ab53085efec0d3");
        assertThat(verification).isNotEqualTo(signature);
    }

    @Test
    @DisplayName("mKey는 signKey 단독 SHA-256 해시다")
    void merchantKey_hashesSignKeyOnly() {
        assertThat(signatureGenerator.merchantKey("sign-key"))
                .isEqualTo("23c2ed1950727e1a48c54a7a27eff525e69ee8aedbd7e704e053a409c62bec56");
    }
}
