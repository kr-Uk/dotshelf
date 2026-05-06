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
    private static final String DESKTOP_CLIENT_ID = "desktop-client-id.apps.googleusercontent.com";
    private static final String CLIENT_SECRET = "google-client-secret";
    private static final String REDIRECT_URI = "http://127.0.0.1:4567/dotshelf/oauth/callback/";
    // RFC 7636 §4.1 unreserved chars [A-Za-z0-9-._~] 43~128 chars 형식 예시.
    private static final String VALID_CODE_VERIFIER = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk_extra_padding_to_43";

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

    @Test
    @DisplayName("PKCE 흐름은 desktop_client_id + code_verifier 전송, client_secret 누락")
    void verify_withPkce_sendsCodeVerifierAndOmitsClientSecret() {
        GoogleTokenVerifier verifier = verifierWithDesktopClientId();

        assertThatThrownBy(() -> verifier.verify("auth-code", SocialTokenType.AUTHORIZATION_CODE, REDIRECT_URI, null, VALID_CODE_VERIFIER))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);  // not-a-jwt id_token decode 실패

        assertThat(tokenRequestBody.get())
                .contains("client_id=" + DESKTOP_CLIENT_ID)
                .contains("code_verifier=" + VALID_CODE_VERIFIER)
                .doesNotContain("client_secret");
    }

    @Test
    @DisplayName("PKCE 흐름인데 GOOGLE_DESKTOP_CLIENT_ID 미설정이면 명시적 INVALID_TOKEN — silent fallback 금지")
    void verify_withPkce_butNoDesktopClientId_throwsInvalidToken() {
        GoogleTokenVerifier verifier = verifier();  // desktopClientId == null

        assertThatThrownBy(() -> verifier.verify("auth-code", SocialTokenType.AUTHORIZATION_CODE, REDIRECT_URI, null, VALID_CODE_VERIFIER))
                .isInstanceOf(CustomException.class)
                .hasMessageContaining("GOOGLE_DESKTOP_CLIENT_ID");
        assertThat(tokenRequestBody.get()).isNull();  // Google 호출 안 감
    }

    @Test
    @DisplayName("RFC 7636 형식 안 맞는 code_verifier는 INVALID_INPUT")
    void verify_withInvalidCodeVerifierFormat_throwsInvalidInput() {
        GoogleTokenVerifier verifier = verifierWithDesktopClientId();

        assertThatThrownBy(() -> verifier.verify("auth-code", SocialTokenType.AUTHORIZATION_CODE, REDIRECT_URI, null, "too-short"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        assertThat(tokenRequestBody.get()).isNull();
    }

    private GoogleTokenVerifier verifier() {
        return new GoogleTokenVerifier(RestClient.builder(), properties(null));
    }

    private GoogleTokenVerifier verifierWithDesktopClientId() {
        return new GoogleTokenVerifier(RestClient.builder(), properties(DESKTOP_CLIENT_ID));
    }

    private SocialLoginProperties properties(String desktopClientId) {
        return new SocialLoginProperties(
                new SocialLoginProperties.Google(
                        SERVER_CLIENT_ID,
                        List.of(SERVER_CLIENT_ID),
                        List.of("https://accounts.google.com", "accounts.google.com"),
                        "http://127.0.0.1:1/keys",
                        CLIENT_SECRET,
                        tokenUri,
                        desktopClientId,
                        null  // desktopClientSecret — PKCE 단독 테스트
                ),
                null,
                null
        );
    }
}
