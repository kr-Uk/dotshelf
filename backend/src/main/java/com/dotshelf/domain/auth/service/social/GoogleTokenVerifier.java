package com.dotshelf.domain.auth.service.social;

import java.util.Map;
import java.util.regex.Pattern;

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

    // RFC 7636 §4.1: code_verifier = unreserved chars [A-Za-z0-9-._~] 43~128 chars.
    private static final Pattern PKCE_VERIFIER_PATTERN = Pattern.compile("^[A-Za-z0-9\\-._~]{43,128}$");

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
    public SocialUserInfo verify(String token, SocialTokenType tokenType, String redirectUri, String nonce, String codeVerifier) {
        String idToken = shouldExchangeCode(token, tokenType) ? exchangeCodeForIdToken(token, redirectUri, codeVerifier) : token;
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

    private String exchangeCodeForIdToken(String code, String redirectUri, String codeVerifier) {
        // PKCE 흐름 (RFC 7636): code_verifier가 있으면 client_secret 없이 PKCE-only 교환.
        // Desktop/Native client는 client_secret이 본질적으로 public이라 PKCE가 정공법.
        // code_verifier 없으면 legacy server pair (client_secret) 흐름으로 fallback —
        // mobile/web에서 server auth code 흐름 (server-side OAuth)는 client_secret 사용.
        boolean usePkce = codeVerifier != null && !codeVerifier.isBlank();
        if (usePkce) {
            // 형식 가드: RFC 7636 §4.1 unreserved [A-Za-z0-9-._~] 43~128 chars.
            // FE 버그/공격 방어 — 잘못된 형식이면 Google이 어차피 거절하지만 빠른 INVALID_INPUT으로 짚어줌.
            if (!PKCE_VERIFIER_PATTERN.matcher(codeVerifier).matches()) {
                throw new CustomException(ErrorCode.INVALID_INPUT,
                        "codeVerifier는 RFC 7636 형식 (43~128 unreserved chars)이어야 합니다.");
            }
        }

        String clientId;
        String clientSecret;
        if (usePkce) {
            // PKCE 흐름은 FE authorize 시 사용한 client_id와 일치해야 함. Desktop client_id 필수.
            // 이전엔 server-client-id로 silent fallback이었으나 — Desktop authorize였으면 Google이
            // unauthorized_client로 거절해서 사용자에게 모호한 에러 노출. 명시적 throw가 운영자에게
            // 더 actionable (env 누락임을 즉시 인지).
            String desktopId = properties.google().desktopClientId();
            if (desktopId == null || desktopId.isBlank()) {
                throw new CustomException(ErrorCode.INVALID_TOKEN,
                        "GOOGLE_DESKTOP_CLIENT_ID 설정이 필요합니다 (PKCE 흐름).");
            }
            clientId = desktopId;
            clientSecret = null;
        } else {
            clientId = serverClientId();
            clientSecret = properties.google().clientSecret();
            if (clientSecret == null || clientSecret.isBlank()) {
                throw new CustomException(ErrorCode.INVALID_TOKEN, "GOOGLE_CLIENT_SECRET 설정이 필요합니다 (PKCE 흐름은 codeVerifier 전달 필요).");
            }
        }

        MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
        body.add("grant_type", "authorization_code");
        body.add("code", code);
        body.add("client_id", clientId);
        if (clientSecret != null) {
            body.add("client_secret", clientSecret);
        }
        if (usePkce) {
            body.add("code_verifier", codeVerifier);
        }
        body.add("redirect_uri", requiredRedirectUri(redirectUri));

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

    private String requiredRedirectUri(String redirectUri) {
        if (redirectUri == null || redirectUri.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "GOOGLE redirectUri는 필수입니다.");
        }
        return redirectUri.trim();
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
