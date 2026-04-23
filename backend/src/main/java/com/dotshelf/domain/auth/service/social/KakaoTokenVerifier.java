package com.dotshelf.domain.auth.service.social;

import java.util.LinkedHashMap;
import java.util.Map;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;

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
    public SocialUserInfo verify(String token) {
        Map<String, Object> response = requestUserInfo(token);
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
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
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
}
