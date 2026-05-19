package com.dotshelf.global.security;

import java.util.List;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "social")
public record SocialLoginProperties(
        Google google,
        Kakao kakao,
        Apple apple
) {

    public SocialLoginProperties {
        google = google == null ? new Google(null, null, null, null, null, null, null, null) : google;
        kakao = kakao == null ? new Kakao(null, null, null, null, null) : kakao;
        apple = apple == null ? new Apple(null, null, null, null, null, null, null, null, null) : apple;
    }

    public record Google(
            String serverClientId,
            List<String> clientIds,
            List<String> issuers,
            String jwksUri,
            String clientSecret,
            String tokenUri,
            // Desktop OAuth용 client_id. PKCE 흐름에서 FE authorize 시 사용한 값과 정확히 일치해야 함.
            // 자동으로 clientIds(audience whitelist)에도 포함됨.
            String desktopClientId,
            // Google Desktop app type client는 binary에 박힌 "공개 secret"이지만 token endpoint가
            // 형식적으로 요구함 (client_secret is missing 400 응답 회피). 진짜 보안은 PKCE가 담당.
            // Google 공식 문서:
            //   "For desktop apps, Google issues a client ID and a client secret. The client
            //    secret is, of course, not really a secret."
            String desktopClientSecret
    ) {

        public Google {
            if (serverClientId != null && serverClientId.isBlank()) {
                serverClientId = null;
            }
            if (desktopClientId != null && desktopClientId.isBlank()) {
                desktopClientId = null;
            }
            if (desktopClientSecret != null && desktopClientSecret.isBlank()) {
                desktopClientSecret = null;
            }
            clientIds = normalize(clientIds);
            if (serverClientId != null && !clientIds.contains(serverClientId)) {
                clientIds = append(clientIds, serverClientId);
            }
            if (desktopClientId != null && !clientIds.contains(desktopClientId)) {
                clientIds = append(clientIds, desktopClientId);
            }
            issuers = normalize(issuers);
            if (issuers.isEmpty()) {
                issuers = List.of("https://accounts.google.com", "accounts.google.com");
            }
            if (jwksUri == null || jwksUri.isBlank()) {
                jwksUri = "https://www.googleapis.com/oauth2/v3/certs";
            }
            if (tokenUri == null || tokenUri.isBlank()) {
                tokenUri = "https://oauth2.googleapis.com/token";
            }
        }
    }

    public record Kakao(
            String restApiKey,
            String clientSecret,
            String redirectUri,
            String userInfoUri,
            String tokenUri
    ) {

        public Kakao {
            if (restApiKey != null && restApiKey.isBlank()) {
                restApiKey = null;
            }
            if (clientSecret != null && clientSecret.isBlank()) {
                clientSecret = null;
            }
            if (redirectUri != null && redirectUri.isBlank()) {
                redirectUri = null;
            }
            if (userInfoUri == null || userInfoUri.isBlank()) {
                userInfoUri = "https://kapi.kakao.com/v2/user/me";
            }
            if (tokenUri == null || tokenUri.isBlank()) {
                tokenUri = "https://kauth.kakao.com/oauth/token";
            }
        }
    }

    public record Apple(
            List<String> clientIds,
            List<String> issuers,
            String jwksUri,
            // Desktop/Web Sign in with Apple uses a Services ID as client_id.
            // Native iOS identity_token validation still uses clientIds above.
            String desktopClientId,
            String teamId,
            String keyId,
            String privateKey,
            String tokenUri,
            String desktopRedirectUri
    ) {

        public Apple {
            if (desktopClientId != null && desktopClientId.isBlank()) {
                desktopClientId = null;
            }
            if (teamId != null && teamId.isBlank()) {
                teamId = null;
            }
            if (keyId != null && keyId.isBlank()) {
                keyId = null;
            }
            if (privateKey != null && privateKey.isBlank()) {
                privateKey = null;
            }
            if (desktopRedirectUri != null && desktopRedirectUri.isBlank()) {
                desktopRedirectUri = null;
            }
            clientIds = normalize(clientIds);
            if (desktopClientId != null && !clientIds.contains(desktopClientId)) {
                clientIds = append(clientIds, desktopClientId);
            }
            issuers = normalize(issuers);
            if (issuers.isEmpty()) {
                issuers = List.of("https://appleid.apple.com");
            }
            if (jwksUri == null || jwksUri.isBlank()) {
                jwksUri = "https://appleid.apple.com/auth/keys";
            }
            if (tokenUri == null || tokenUri.isBlank()) {
                tokenUri = "https://appleid.apple.com/auth/token";
            }
        }
    }

    private static List<String> normalize(List<String> values) {
        if (values == null) {
            return List.of();
        }
        return values.stream()
                .filter(value -> value != null && !value.isBlank())
                .map(String::trim)
                .toList();
    }

    private static List<String> append(List<String> values, String value) {
        java.util.ArrayList<String> result = new java.util.ArrayList<>(values);
        result.add(value);
        return List.copyOf(result);
    }
}
