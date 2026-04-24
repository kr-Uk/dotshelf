package com.dotshelf.domain.auth.service.social;

import java.util.LinkedHashMap;
import java.util.Map;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;

import lombok.extern.slf4j.Slf4j;

@Slf4j
@Component
public class KakaoTokenVerifier implements SocialTokenVerifier {

    private static final ParameterizedTypeReference<Map<String, Object>> RESPONSE_TYPE = new ParameterizedTypeReference<>() {
    };

    private final RestClient restClient;
    private final SocialLoginProperties properties;

    public KakaoTokenVerifier(RestClient.Builder restClientBuilder, SocialLoginProperties properties) {
        this.restClient = restClientBuilder.build();
        this.properties = properties;
    }

    @Override
    public SocialProvider supports() {
        return SocialProvider.KAKAO;
    }

    @Override
    public SocialUserInfo verify(String token, SocialTokenType tokenType) {
        String accessToken = resolveAccessToken(token, tokenType);
        Map<String, Object> response = requestUserInfo(accessToken);
        String providerUserId = stringValue(response.get("id"));
        if (providerUserId == null) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }

        Map<String, Object> kakaoAccount = mapValue(response, "kakao_account");
        Map<String, Object> profile = mapValue(kakaoAccount, "profile");
        Map<String, Object> propertiesMap = mapValue(response, "properties");

        String nickname = firstNonBlank(
                stringValue(profile.get("nickname")),
                stringValue(propertiesMap.get("nickname"))
        );
        String profileImageUrl = firstNonBlank(
                stringValue(profile.get("profile_image_url")),
                stringValue(propertiesMap.get("profile_image"))
        );

        return new SocialUserInfo(
                SocialProvider.KAKAO,
                providerUserId,
                stringValue(kakaoAccount.get("email")),
                nickname,
                profileImageUrl
        );
    }

    private String resolveAccessToken(String token, SocialTokenType tokenType) {
        if (tokenType == null) {
            return token;
        }
        return switch (tokenType) {
            case ACCESS_TOKEN -> token;
            case AUTHORIZATION_CODE -> exchangeCodeForAccessToken(token);
            case ID_TOKEN -> throw new CustomException(ErrorCode.INVALID_INPUT, "KAKAO는 ID_TOKEN을 지원하지 않습니다.");
        };
    }

    private String exchangeCodeForAccessToken(String code) {
        MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
        body.add("grant_type", "authorization_code");
        body.add("client_id", restApiKey());
        body.add("redirect_uri", redirectUri());
        body.add("code", code);
        String clientSecret = properties.kakao().clientSecret();
        if (clientSecret != null && !clientSecret.isBlank()) {
            body.add("client_secret", clientSecret);
        }

        try {
            Map<String, Object> response = restClient.post()
                    .uri(properties.kakao().tokenUri())
                    .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                    .body(body)
                    .retrieve()
                    .body(RESPONSE_TYPE);
            String accessToken = response == null ? null : stringValue(response.get("access_token"));
            if (accessToken == null) {
                throw new CustomException(ErrorCode.INVALID_TOKEN);
            }
            return accessToken;
        } catch (RestClientException e) {
            log.warn("Kakao token exchange failed (code={})", mask(code), e);
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    private Map<String, Object> requestUserInfo(String token) {
        try {
            Map<String, Object> response = restClient.get()
                    .uri(properties.kakao().userInfoUri())
                    .headers(headers -> headers.setBearerAuth(token))
                    .retrieve()
                    .body(RESPONSE_TYPE);
            if (response == null) {
                throw new CustomException(ErrorCode.INVALID_TOKEN);
            }
            return response;
        } catch (RestClientException e) {
            log.warn("Kakao user info request failed", e);
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    private String restApiKey() {
        String restApiKey = properties.kakao().restApiKey();
        if (restApiKey == null || restApiKey.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN, "KAKAO_REST_API_KEY 설정이 필요합니다.");
        }
        return restApiKey;
    }

    private String redirectUri() {
        String redirectUri = properties.kakao().redirectUri();
        if (redirectUri == null || redirectUri.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN, "KAKAO_REDIRECT_URI 설정이 필요합니다.");
        }
        return redirectUri;
    }

    private Map<String, Object> mapValue(Map<String, Object> source, String key) {
        Object value = source.get(key);
        if (!(value instanceof Map<?, ?> rawMap)) {
            return Map.of();
        }

        Map<String, Object> result = new LinkedHashMap<>();
        rawMap.forEach((rawKey, rawValue) -> {
            if (rawKey instanceof String stringKey) {
                result.put(stringKey, rawValue);
            }
        });
        return result;
    }

    private String stringValue(Object value) {
        if (value == null) {
            return null;
        }
        String text = String.valueOf(value).trim();
        return text.isBlank() ? null : text;
    }

    private String firstNonBlank(String first, String second) {
        return first == null ? second : first;
    }

    private String mask(String value) {
        if (value == null || value.isBlank()) {
            return "blank";
        }
        int visibleLength = Math.min(6, value.length());
        return value.substring(0, visibleLength) + "***";
    }
}
