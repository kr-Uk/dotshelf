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
        google = google == null ? new Google(null, null, null, null, null, null) : google;
        kakao = kakao == null ? new Kakao(null, null, null, null, null) : kakao;
        apple = apple == null ? new Apple(null, null, null) : apple;
    }

    public record Google(
            String serverClientId,
            List<String> clientIds,
            List<String> issuers,
            String jwksUri,
            String clientSecret,
            String tokenUri
    ) {

        public Google {
            if (serverClientId != null && serverClientId.isBlank()) {
                serverClientId = null;
            }
            clientIds = normalize(clientIds);
            if (serverClientId != null && !clientIds.contains(serverClientId)) {
                clientIds = append(clientIds, serverClientId);
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
            String jwksUri
    ) {

        public Apple {
            clientIds = normalize(clientIds);
            issuers = normalize(issuers);
            if (issuers.isEmpty()) {
                issuers = List.of("https://appleid.apple.com");
            }
            if (jwksUri == null || jwksUri.isBlank()) {
                jwksUri = "https://appleid.apple.com/auth/keys";
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
