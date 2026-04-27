package com.dotshelf.domain.auth.service.social;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Date;
import java.util.List;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;
import com.nimbusds.jose.JOSEException;
import com.nimbusds.jose.JWSAlgorithm;
import com.nimbusds.jose.JWSHeader;
import com.nimbusds.jose.crypto.RSASSASigner;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.RSAKey;
import com.nimbusds.jose.jwk.gen.RSAKeyGenerator;
import com.nimbusds.jwt.JWTClaimsSet;
import com.nimbusds.jwt.SignedJWT;
import com.sun.net.httpserver.HttpServer;

class AppleTokenVerifierTest {

    private HttpServer jwksServer;
    private RSAKey rsaKey;
    private String jwksUri;

    @BeforeEach
    void setUp() throws Exception {
        rsaKey = new RSAKeyGenerator(2048)
                .keyID("apple-test-key")
                .generate();
        jwksServer = HttpServer.create(new InetSocketAddress(0), 0);
        jwksServer.createContext("/keys", exchange -> {
            byte[] response = new JWKSet(rsaKey.toPublicJWK())
                    .toString()
                    .getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        jwksServer.start();
        jwksUri = "http://127.0.0.1:" + jwksServer.getAddress().getPort() + "/keys";
    }

    @AfterEach
    void tearDown() {
        if (jwksServer != null) {
            jwksServer.stop(0);
        }
    }

    @Test
    @DisplayName("APPLE identity_token 검증 시 sub와 email을 사용자 정보로 반환한다")
    void verify_withAppleIdentityToken_returnsUserInfo() throws Exception {
        AppleTokenVerifier verifier = new AppleTokenVerifier(properties("com.jihocompany.dotshelf"));
        String token = appleIdentityToken(
                "com.jihocompany.dotshelf",
                "apple-user-1",
                "reader@example.com"
        );

        SocialUserInfo userInfo = verifier.verify(token, SocialTokenType.ID_TOKEN);

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.APPLE);
        assertThat(userInfo.providerUserId()).isEqualTo("apple-user-1");
        assertThat(userInfo.email()).isEqualTo("reader@example.com");
        assertThat(userInfo.nickname()).isNull();
        assertThat(userInfo.profileImageUrl()).isNull();
    }

    @Test
    @DisplayName("APPLE tokenType 생략 시 identity_token으로 처리한다")
    void verify_withoutTokenType_treatsTokenAsIdentityToken() throws Exception {
        AppleTokenVerifier verifier = new AppleTokenVerifier(properties("com.jihocompany.dotshelf"));
        String token = appleIdentityToken(
                "com.jihocompany.dotshelf",
                "apple-user-2",
                null
        );

        SocialUserInfo userInfo = verifier.verify(token, null);

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.APPLE);
        assertThat(userInfo.providerUserId()).isEqualTo("apple-user-2");
        assertThat(userInfo.email()).isNull();
    }

    @Test
    @DisplayName("APPLE은 ACCESS_TOKEN을 받으면 입력 예외를 던진다")
    void verify_withAccessToken_throwsInvalidInput() {
        AppleTokenVerifier verifier = new AppleTokenVerifier(properties("com.jihocompany.dotshelf"));

        assertThatThrownBy(() -> verifier.verify("apple-access-token", SocialTokenType.ACCESS_TOKEN))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    private SocialLoginProperties properties(String... clientIds) {
        return new SocialLoginProperties(
                null,
                null,
                new SocialLoginProperties.Apple(List.of(clientIds), null, jwksUri)
        );
    }

    private String appleIdentityToken(String audience, String subject, String email) throws JOSEException {
        Instant now = Instant.now();
        JWTClaimsSet.Builder claims = new JWTClaimsSet.Builder()
                .issuer("https://appleid.apple.com")
                .audience(audience)
                .subject(subject)
                .issueTime(Date.from(now))
                .expirationTime(Date.from(now.plusSeconds(300)));
        if (email != null) {
            claims.claim("email", email);
        }

        SignedJWT jwt = new SignedJWT(
                new JWSHeader.Builder(JWSAlgorithm.RS256)
                        .keyID(rsaKey.getKeyID())
                        .build(),
                claims.build()
        );
        jwt.sign(new RSASSASigner(rsaKey.toPrivateKey()));
        return jwt.serialize();
    }
}
