package com.dotshelf.domain.auth.service.social;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

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
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
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
    private final AtomicReference<String> tokenResponse = new AtomicReference<>("{\"access_token\":\"kakao-access-token\"}");
    private final AtomicReference<String> userInfoResponse = new AtomicReference<>("""
            {
              "id": 12345,
              "kakao_account": {"email": "kakao@example.com", "profile": {"nickname": "Kakao User"}},
              "properties": {"profile_image": "https://example.com/profile.png"}
            }
            """);

    @BeforeEach
    void setUp() throws Exception {
        kakaoServer = HttpServer.create(new InetSocketAddress(0), 0);
        kakaoServer.createContext("/token", exchange -> {
            tokenRequestBody.set(new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8));
            byte[] response = tokenResponse.get().getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, response.length);
            try (OutputStream outputStream = exchange.getResponseBody()) {
                outputStream.write(response);
            }
        });
        kakaoServer.createContext("/user", exchange -> {
            userInfoAuthorization.set(exchange.getRequestHeaders().getFirst("Authorization"));
            byte[] response = userInfoResponse.get().getBytes(StandardCharsets.UTF_8);
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
                null,
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

    @Test
    @DisplayName("Kakao access token 검증은 token 교환 없이 bearer userinfo를 호출한다")
    void verify_withAccessToken_skipsTokenExchange() {
        KakaoTokenVerifier verifier = verifier();

        SocialUserInfo userInfo = verifier.verify("direct-access-token", SocialTokenType.ACCESS_TOKEN, null, null, null);

        assertThat(userInfo.provider()).isEqualTo(SocialProvider.KAKAO);
        assertThat(userInfo.providerUserId()).isEqualTo("12345");
        assertThat(tokenRequestBody.get()).isNull();
        assertThat(userInfoAuthorization.get()).isEqualTo("Bearer direct-access-token");
    }

    @Test
    @DisplayName("Kakao는 ID_TOKEN 요청을 INVALID_INPUT으로 거절한다")
    void verify_withIdToken_throwsInvalidInput() {
        KakaoTokenVerifier verifier = verifier();

        assertThatThrownBy(() -> verifier.verify("id-token", SocialTokenType.ID_TOKEN, null, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        assertThat(tokenRequestBody.get()).isNull();
        assertThat(userInfoAuthorization.get()).isNull();
    }

    @Test
    @DisplayName("Kakao token 응답에 access_token이 없으면 INVALID_TOKEN을 던지고 userinfo를 호출하지 않는다")
    void verify_tokenResponseWithoutAccessToken_throwsInvalidToken() {
        tokenResponse.set("{}");
        KakaoTokenVerifier verifier = verifier();

        assertThatThrownBy(() -> verifier.verify("authorization-code", SocialTokenType.AUTHORIZATION_CODE, REQUEST_REDIRECT_URI, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
        assertThat(tokenRequestBody.get()).contains("code=authorization-code");
        assertThat(userInfoAuthorization.get()).isNull();
    }

    @Test
    @DisplayName("Kakao userinfo 응답에 id가 없으면 INVALID_TOKEN을 던진다")
    void verify_userInfoWithoutId_throwsInvalidToken() {
        userInfoResponse.set("""
                {
                  "kakao_account": {"email": "kakao@example.com"},
                  "properties": {"nickname": "fallback"}
                }
                """);
        KakaoTokenVerifier verifier = verifier();

        assertThatThrownBy(() -> verifier.verify("access-token", SocialTokenType.ACCESS_TOKEN, null, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
        assertThat(userInfoAuthorization.get()).isEqualTo("Bearer access-token");
    }

    @Test
    @DisplayName("Kakao authorization code 교환에서 rest api key가 없으면 INVALID_TOKEN을 던진다")
    void verify_missingRestApiKey_throwsInvalidToken() {
        KakaoTokenVerifier verifier = new KakaoTokenVerifier(RestClient.builder(), properties(null, CLIENT_SECRET, CONFIGURED_REDIRECT_URI));

        assertThatThrownBy(() -> verifier.verify("authorization-code", SocialTokenType.AUTHORIZATION_CODE, REQUEST_REDIRECT_URI, null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
        assertThat(tokenRequestBody.get()).isNull();
    }

    @Test
    @DisplayName("Kakao authorization code 교환에서 redirectUri가 없으면 INVALID_TOKEN을 던진다")
    void verify_missingRedirectUri_throwsInvalidToken() {
        KakaoTokenVerifier verifier = new KakaoTokenVerifier(RestClient.builder(), properties(REST_API_KEY, CLIENT_SECRET, null));

        assertThatThrownBy(() -> verifier.verify("authorization-code", SocialTokenType.AUTHORIZATION_CODE, " ", null, null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_TOKEN);
        assertThat(tokenRequestBody.get()).isNull();
    }

    private KakaoTokenVerifier verifier() {
        return new KakaoTokenVerifier(RestClient.builder(), properties());
    }

    private SocialLoginProperties properties() {
        return properties(REST_API_KEY, CLIENT_SECRET, CONFIGURED_REDIRECT_URI);
    }

    private SocialLoginProperties properties(String restApiKey, String clientSecret, String redirectUri) {
        return new SocialLoginProperties(
                null,
                new SocialLoginProperties.Kakao(
                        restApiKey,
                        clientSecret,
                        redirectUri,
                        userInfoUri,
                        tokenUri
                ),
                null
        );
    }
}
