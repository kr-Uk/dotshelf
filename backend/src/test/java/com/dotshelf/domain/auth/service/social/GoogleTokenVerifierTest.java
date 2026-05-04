package com.dotshelf.domain.auth.service.social;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.client.RestClient;

import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;
import com.sun.net.httpserver.HttpServer;

class GoogleTokenVerifierTest {

    private static final String SERVER_CLIENT_ID = "server-client-id.apps.googleusercontent.com";
    private static final String CLIENT_SECRET = "google-client-secret";
    private static final String REDIRECT_URI = "http://127.0.0.1:4567/dotshelf/oauth/callback/";

    private HttpServer tokenServer;
    private String tokenUri;
    private final AtomicReference<String> tokenRequestBody = new AtomicReference<>();

    @BeforeEach
    void setUp() throws Exception {
        tokenServer = HttpServer.create(new InetSocketAddress(0), 0);
        tokenServer.createContext("/token", exchange -> {
            tokenRequestBody.set(new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8));
            byte[] response = "{\"id_token\":\"not-a-jwt\"}".getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        tokenServer.start();
        tokenUri = "http://127.0.0.1:" + tokenServer.getAddress().getPort() + "/token";
    }

    @AfterEach
    void tearDown() {
        if (tokenServer != null) {
            tokenServer.stop(0);
        }
    }

    @Test
    @DisplayName("Google authorization code 교환 시 redirect_uri를 함께 전송한다")
    void verify_withAuthorizationCode_sendsRedirectUri() {
        GoogleTokenVerifier verifier = verifier();

        assertThatThrownBy(() -> verifier.verify("authorization-code", SocialTokenType.AUTHORIZATION_CODE, REDIRECT_URI, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);

        assertThat(tokenRequestBody.get())
                .contains("grant_type=authorization_code")
                .contains("code=authorization-code")
                .contains("client_id=" + SERVER_CLIENT_ID)
                .contains("client_secret=" + CLIENT_SECRET)
                .contains("redirect_uri=" + URLEncoder.encode(REDIRECT_URI, StandardCharsets.UTF_8));
    }

    @Test
    @DisplayName("Google authorization code 교환 시 redirectUri가 없으면 INVALID_INPUT을 던진다")
    void verify_withAuthorizationCodeAndMissingRedirectUri_throwsInvalidInput() {
        GoogleTokenVerifier verifier = verifier();

        assertThatThrownBy(() -> verifier.verify("authorization-code", SocialTokenType.AUTHORIZATION_CODE, " ", null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        assertThat(tokenRequestBody.get()).isNull();
    }

    private GoogleTokenVerifier verifier() {
        return new GoogleTokenVerifier(RestClient.builder(), properties());
    }

    private SocialLoginProperties properties() {
        return new SocialLoginProperties(
                new SocialLoginProperties.Google(
                        SERVER_CLIENT_ID,
                        List.of(SERVER_CLIENT_ID),
                        List.of("https://accounts.google.com", "accounts.google.com"),
                        "http://127.0.0.1:1/keys",
                        CLIENT_SECRET,
                        tokenUri,
                        null  // desktopClientId — server pair 흐름 테스트
                ),
                null,
                null
        );
    }
}
