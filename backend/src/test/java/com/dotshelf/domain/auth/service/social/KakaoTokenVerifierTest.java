package com.dotshelf.domain.auth.service.social;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.client.RestClient;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.security.SocialLoginProperties;
import com.sun.net.httpserver.HttpServer;

class KakaoTokenVerifierTest {

    private static final String REST_API_KEY = "kakao-rest-api-key";
    private static final String CLIENT_SECRET = "kakao-client-secret";
    private static final String CONFIGURED_REDIRECT_URI = "http://localhost:18080/oauth/kakao/callback";
    private static final String REQUEST_REDIRECT_URI = "http://127.0.0.1:18080/oauth/kakao/callback";

    private HttpServer kakaoServer;
    private String tokenUri;
    private String userInfoUri;
    private final AtomicReference<String> tokenRequestBody = new AtomicReference<>();
    private final AtomicReference<String> userInfoAuthorization = new AtomicReference<>();

    @BeforeEach
    void setUp() throws Exception {
        kakaoServer = HttpServer.create(new InetSocketAddress(0), 0);
        kakaoServer.createContext("/token", exchange -> {
            tokenRequestBody.set(new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8));
            byte[] response = "{\"access_token\":\"kakao-access-token\"}".getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        kakaoServer.createContext("/user", exchange -> {
            userInfoAuthorization.set(exchange.getRequestHeaders().getFirst("Authorization"));
            byte[] response = """
                    {
                      "id": 12345,
                      "kakao_account": {"email": "kakao@example.com", "profile": {"nickname": "Kakao User"}},
                      "properties": {"profile_image": "https://example.com/profile.png"}
                    }
                    """.getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        kakaoServer.start();
        String baseUri = "http://127.0.0.1:" + kakaoServer.getAddress().getPort();
        tokenUri = baseUri + "/token";
        userInfoUri = baseUri + "/user";
    }

    @AfterEach
    void tearDown() {
        if (kakaoServer != null) {
            kakaoServer.stop(0);
        }
    }

    @Test
    @DisplayName("Kakao authorization code 교환 시 요청 redirectUri를 redirect_uri로 전송한다")
    void verify_withAuthorizationCode_sendsRequestRedirectUri() {
        KakaoTokenVerifier verifier = verifier();

        SocialUserInfo userInfo = verifier.verify(
                "authorization-code",
                SocialTokenType.AUTHORIZATION_CODE,
                REQUEST_REDIRECT_URI,
                null
        );

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.KAKAO);
        assertThat(userInfo.providerUserId()).isEqualTo("12345");
        assertThat(userInfo.email()).isEqualTo("kakao@example.com");
        assertThat(userInfo.nickname()).isEqualTo("Kakao User");
        assertThat(userInfo.profileImageUrl()).isEqualTo("https://example.com/profile.png");
        assertThat(userInfoAuthorization.get()).isEqualTo("Bearer kakao-access-token");
        assertThat(tokenRequestBody.get())
                .contains("grant_type=authorization_code")
                .contains("client_id=" + REST_API_KEY)
                .contains("client_secret=" + CLIENT_SECRET)
                .contains("code=authorization-code")
                .contains("redirect_uri=" + URLEncoder.encode(REQUEST_REDIRECT_URI, StandardCharsets.UTF_8));
    }

    private KakaoTokenVerifier verifier() {
        return new KakaoTokenVerifier(RestClient.builder(), properties());
    }

    private SocialLoginProperties properties() {
        return new SocialLoginProperties(
                null,
                new SocialLoginProperties.Kakao(
                        REST_API_KEY,
                        CLIENT_SECRET,
                        CONFIGURED_REDIRECT_URI,
                        userInfoUri,
                        tokenUri
                ),
                null
        );
    }
}
