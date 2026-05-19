package com.dotshelf.domain.auth.service.social;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.security.KeyPairGenerator;
import java.security.spec.ECGenParameterSpec;
import java.time.Instant;
import java.util.Base64;
import java.util.Date;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.client.RestClient;

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

    private static final String TEST_CLIENT_ID = "test.apple.client";
    private static final String TEST_DESKTOP_CLIENT_ID = "test.apple.service";
    private static final String TEST_REDIRECT_URI = "https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback";
    private static final String TEST_NONCE = "test-nonce";

    private HttpServer jwksServer;
    private RSAKey rsaKey;
    private String jwksUri;
    private String tokenUri;
    private String applePrivateKeyPem;
    private final AtomicReference<String> tokenRequestBody = new AtomicReference<>();
    private final AtomicReference<String> tokenResponse = new AtomicReference<>("{}");

    @BeforeEach
    void setUp() throws Exception {
        rsaKey = new RSAKeyGenerator(2048)
                .keyID("apple-test-key")
                .generate();
        KeyPairGenerator keyPairGenerator = KeyPairGenerator.getInstance("EC");
        keyPairGenerator.initialize(new ECGenParameterSpec("secp256r1"));
        applePrivateKeyPem = privateKeyPem(keyPairGenerator.generateKeyPair().getPrivate().getEncoded());

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
        jwksServer.createContext("/token", exchange -> {
            tokenRequestBody.set(new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8));
            byte[] response = tokenResponse.get().getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        jwksServer.start();
        jwksUri = "http://127.0.0.1:" + jwksServer.getAddress().getPort() + "/keys";
        tokenUri = "http://127.0.0.1:" + jwksServer.getAddress().getPort() + "/token";
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
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), properties(TEST_CLIENT_ID));
        String token = appleIdentityToken(
                TEST_CLIENT_ID,
                "apple-user-1",
                "reader@example.com",
                TEST_NONCE
        );

        SocialUserInfo userInfo = verifier.verify(token, SocialTokenType.ID_TOKEN, null, TEST_NONCE, null);

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.APPLE);
        assertThat(userInfo.providerUserId()).isEqualTo("apple-user-1");
        assertThat(userInfo.email()).isEqualTo("reader@example.com");
        assertThat(userInfo.nickname()).isNull();
        assertThat(userInfo.profileImageUrl()).isNull();
    }

    @Test
    @DisplayName("APPLE tokenType 생략 시 identity_token으로 처리한다")
    void verify_withoutTokenType_treatsTokenAsIdentityToken() throws Exception {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), properties(TEST_CLIENT_ID));
        String token = appleIdentityToken(
                TEST_CLIENT_ID,
                "apple-user-2",
                null,
                TEST_NONCE
        );

        SocialUserInfo userInfo = verifier.verify(token, null, null, TEST_NONCE, null);

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.APPLE);
        assertThat(userInfo.providerUserId()).isEqualTo("apple-user-2");
        assertThat(userInfo.email()).isNull();
    }

    @Test
    @DisplayName("APPLE 요청 nonce가 없으면 입력 예외를 던진다")
    void verify_withoutNonce_throwsInvalidInput() throws Exception {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), properties(TEST_CLIENT_ID));
        String token = appleIdentityToken(
                TEST_CLIENT_ID,
                "apple-user-3",
                null,
                TEST_NONCE
        );

        assertThatThrownBy(() -> verifier.verify(token, SocialTokenType.ID_TOKEN, null, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("APPLE identity_token nonce가 요청 nonce와 다르면 토큰 예외를 던진다")
    void verify_withMismatchedNonce_throwsInvalidToken() throws Exception {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), properties(TEST_CLIENT_ID));
        String token = appleIdentityToken(
                TEST_CLIENT_ID,
                "apple-user-4",
                null,
                TEST_NONCE
        );

        assertThatThrownBy(() -> verifier.verify(token, SocialTokenType.ID_TOKEN, null, "different-nonce", null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
    }

    @Test
    @DisplayName("APPLE은 ACCESS_TOKEN을 받으면 입력 예외를 던진다")
    void verify_withAccessToken_throwsInvalidInput() {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), properties(TEST_CLIENT_ID));

        assertThatThrownBy(() -> verifier.verify("apple-access-token", SocialTokenType.ACCESS_TOKEN, null, TEST_NONCE, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("APPLE authorization code 검증 시 Apple token endpoint에서 id_token을 교환해 사용자 정보를 반환한다")
    void verify_withAuthorizationCode_exchangesCodeForIdToken() throws Exception {
        String idToken = appleIdentityToken(
                TEST_DESKTOP_CLIENT_ID,
                "apple-desktop-user",
                "desktop@example.com",
                TEST_NONCE
        );
        tokenResponse.set("{\"id_token\":\"" + idToken + "\"}");
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), desktopProperties());

        SocialUserInfo userInfo = verifier.verify(
                "apple-auth-code",
                SocialTokenType.AUTHORIZATION_CODE,
                TEST_REDIRECT_URI,
                TEST_NONCE,
                null
        );

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.APPLE);
        assertThat(userInfo.providerUserId()).isEqualTo("apple-desktop-user");
        assertThat(userInfo.email()).isEqualTo("desktop@example.com");

        Map<String, String> requestBody = parseFormBody(tokenRequestBody.get());
        assertThat(requestBody)
                .containsEntry("grant_type", "authorization_code")
                .containsEntry("code", "apple-auth-code")
                .containsEntry("client_id", TEST_DESKTOP_CLIENT_ID)
                .containsEntry("redirect_uri", TEST_REDIRECT_URI);
        assertThat(requestBody.get("client_secret")).isNotBlank();
        assertThat(requestBody.get("client_secret").split("\\.")).hasSize(3);
    }

    @Test
    @DisplayName("APPLE authorization code 요청에 redirectUri가 없으면 입력 예외를 던진다")
    void verify_withAuthorizationCodeAndMissingRedirectUri_throwsInvalidInput() {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), desktopProperties(null));

        assertThatThrownBy(() -> verifier.verify("apple-auth-code", SocialTokenType.AUTHORIZATION_CODE, null, TEST_NONCE, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        assertThat(tokenRequestBody.get()).isNull();
    }

    @Test
    @DisplayName("APPLE token endpoint 응답에 id_token이 없으면 토큰 예외를 던진다")
    void verify_tokenResponseWithoutIdToken_throwsInvalidToken() {
        AppleTokenVerifier verifier = new AppleTokenVerifier(RestClient.builder(), desktopProperties());

        assertThatThrownBy(() -> verifier.verify("apple-auth-code", SocialTokenType.AUTHORIZATION_CODE, TEST_REDIRECT_URI, TEST_NONCE, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
        assertThat(parseFormBody(tokenRequestBody.get()))
                .containsEntry("code", "apple-auth-code");
    }

    private SocialLoginProperties properties(String... clientIds) {
        return new SocialLoginProperties(
                null,
                null,
                new SocialLoginProperties.Apple(List.of(clientIds), null, jwksUri, null, null, null, null, null, null)
        );
    }

    private SocialLoginProperties desktopProperties() {
        return desktopProperties(TEST_REDIRECT_URI);
    }

    private SocialLoginProperties desktopProperties(String desktopRedirectUri) {
        return new SocialLoginProperties(
                null,
                null,
                new SocialLoginProperties.Apple(
                        List.of(TEST_CLIENT_ID),
                        null,
                        jwksUri,
                        TEST_DESKTOP_CLIENT_ID,
                        "TEAMID1234",
                        "KEYID1234",
                        applePrivateKeyPem,
                        tokenUri,
                        desktopRedirectUri
                )
        );
    }

    private String appleIdentityToken(String audience, String subject, String email, String nonce) throws JOSEException {
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
        if (nonce != null) {
            claims.claim("nonce", nonce);
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

    private String privateKeyPem(byte[] encodedPrivateKey) {
        return "-----BEGIN PRIVATE KEY-----\n"
                + Base64.getMimeEncoder(64, "\n".getBytes(StandardCharsets.UTF_8)).encodeToString(encodedPrivateKey)
                + "\n-----END PRIVATE KEY-----";
    }

    private Map<String, String> parseFormBody(String body) {
        Map<String, String> result = new LinkedHashMap<>();
        if (body == null || body.isBlank()) {
            return result;
        }
        for (String pair : body.split("&")) {
            String[] parts = pair.split("=", 2);
            String key = URLDecoder.decode(parts[0], StandardCharsets.UTF_8);
            String value = parts.length > 1 ? URLDecoder.decode(parts[1], StandardCharsets.UTF_8) : "";
            result.put(key, value);
        }
        return result;
    }
}
