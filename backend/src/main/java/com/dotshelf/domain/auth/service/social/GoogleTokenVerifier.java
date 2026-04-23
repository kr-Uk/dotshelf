package com.dotshelf.domain.auth.service.social;

import java.util.Map;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.Jwt;
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
public class GoogleTokenVerifier extends JwtSocialTokenVerifierSupport {

    private static final ParameterizedTypeReference<Map<String, Object>> RESPONSE_TYPE = new ParameterizedTypeReference<>() {
    };

    private final RestClient restClient;
    private final SocialLoginProperties properties;

    public GoogleTokenVerifier(RestClient.Builder restClientBuilder, SocialLoginProperties properties) {
        super(
                properties.google().clientIds(),
                properties.google().issuers(),
                properties.google().jwksUri(),
                "GOOGLE_CLIENT_IDS"
        );
        this.restClient = restClientBuilder.build();
        this.properties = properties;
    }

    @Override
    public SocialProvider supports() {
        return SocialProvider.GOOGLE;
    }

    @Override
    public SocialUserInfo verify(String token, SocialTokenType tokenType) {
        String idToken = shouldExchangeCode(token, tokenType) ? exchangeCodeForIdToken(token) : token;
        Jwt jwt = decode(idToken);
        return new SocialUserInfo(
                SocialProvider.GOOGLE,
                requiredSubject(jwt),
                jwt.getClaimAsString("email"),
                jwt.getClaimAsString("name"),
                jwt.getClaimAsString("picture")
        );
    }

    private boolean isJwt(String token) {
        return token.chars().filter(character -> character == '.').count() == 2;
    }

    private boolean shouldExchangeCode(String token, SocialTokenType tokenType) {
        if (tokenType == null) {
            return !isJwt(token);
        }
        return switch (tokenType) {
            case AUTHORIZATION_CODE -> true;
            case ID_TOKEN -> false;
            case ACCESS_TOKEN -> throw new CustomException(ErrorCode.INVALID_INPUT, "GOOGLE은 ACCESS_TOKEN을 지원하지 않습니다.");
        };
    }

    private String exchangeCodeForIdToken(String code) {
        String clientId = serverClientId();
        String clientSecret = properties.google().clientSecret();
        if (clientSecret == null || clientSecret.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN, "GOOGLE_CLIENT_SECRET 설정이 필요합니다.");
        }

        MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
        body.add("grant_type", "authorization_code");
        body.add("code", code);
        body.add("client_id", clientId);
        body.add("client_secret", clientSecret);

        try {
            Map<String, Object> response = restClient.post()
                    .uri(properties.google().tokenUri())
                    .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                    .body(body)
                    .retrieve()
                    .body(RESPONSE_TYPE);
            String idToken = response == null ? null : stringValue(response.get("id_token"));
            if (idToken == null) {
                throw new CustomException(ErrorCode.INVALID_TOKEN);
            }
            return idToken;
        } catch (RestClientException e) {
            log.warn("Google token exchange failed (code={})", mask(code), e);
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    private String serverClientId() {
        String serverClientId = properties.google().serverClientId();
        if (serverClientId == null || serverClientId.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_TOKEN, "GOOGLE_SERVER_CLIENT_ID 설정이 필요합니다.");
        }
        return serverClientId;
    }

    private String stringValue(Object value) {
        if (value == null) {
            return null;
        }
        String text = String.valueOf(value).trim();
        return text.isBlank() ? null : text;
    }

    private String mask(String value) {
        if (value == null || value.isBlank()) {
            return "blank";
        }
        int visibleLength = Math.min(6, value.length());
        return value.substring(0, visibleLength) + "***";
    }
}
