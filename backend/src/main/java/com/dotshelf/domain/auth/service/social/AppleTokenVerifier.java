package com.dotshelf.domain.auth.service.social;

import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.security.KeyFactory;
import java.security.PrivateKey;
import java.security.spec.PKCS8EncodedKeySpec;
import java.time.Instant;
import java.util.Base64;
import java.util.Date;
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

import io.jsonwebtoken.JwtException;
import io.jsonwebtoken.Jwts;
import lombok.extern.slf4j.Slf4j;

@Slf4j
@Component
public class AppleTokenVerifier extends JwtSocialTokenVerifierSupport {

    private static final String APPLE_CLIENT_SECRET_AUDIENCE = "https://appleid.apple.com";
    private static final String APPLE_LOGIN_CONFIG_ERROR_MESSAGE = "APPLE 로그인 설정 오류입니다.";
    private static final long CLIENT_SECRET_TTL_SECONDS = 300L;
    private static final ParameterizedTypeReference<Map<String, Object>> RESPONSE_TYPE = new ParameterizedTypeReference<>() {
    };

    private final RestClient restClient;
    private final SocialLoginProperties properties;

    public AppleTokenVerifier(RestClient.Builder restClientBuilder, SocialLoginProperties properties) {
        super(
                properties.apple().clientIds(),
                properties.apple().issuers(),
                properties.apple().jwksUri(),
                "APPLE_CLIENT_IDS"
        );
        this.restClient = restClientBuilder.build();
        this.properties = properties;
    }

    @Override
    public SocialProvider supports() {
        return SocialProvider.APPLE;
    }

    @Override
    public SocialUserInfo verify(String token, SocialTokenType tokenType, String redirectUri, String nonce, String codeVerifier) {
        String idToken = resolveIdToken(token, tokenType, redirectUri);
        String expectedNonce = requiredNonce(nonce);
        Jwt jwt = decode(idToken);
        validateNonce(jwt, expectedNonce);
        return new SocialUserInfo(
                SocialProvider.APPLE,
                requiredSubject(jwt),
                jwt.getClaimAsString("email"),
                null,
                null
        );
    }

    private String resolveIdToken(String token, SocialTokenType tokenType, String redirectUri) {
        if (tokenType == null || tokenType == SocialTokenType.ID_TOKEN) {
            return token;
        }
        return switch (tokenType) {
            case AUTHORIZATION_CODE -> exchangeCodeForIdToken(token, redirectUri);
            case ACCESS_TOKEN -> throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE은 ACCESS_TOKEN을 지원하지 않습니다.");
            case ID_TOKEN -> token;
        };
    }

    private String exchangeCodeForIdToken(String code, String requestRedirectUri) {
        String clientId = desktopClientId();
        MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
        body.add("grant_type", "authorization_code");
        body.add("code", code);
        body.add("client_id", clientId);
        body.add("client_secret", buildClientSecret(clientId));
        body.add("redirect_uri", redirectUri(requestRedirectUri));

        try {
            Map<String, Object> response = restClient.post()
                    .uri(properties.apple().tokenUri())
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
            log.warn("Apple token exchange failed (code={})", mask(code), e);
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    private String buildClientSecret(String clientId) {
        Instant now = Instant.now();
        try {
            return Jwts.builder()
                    .header()
                    .keyId(requiredConfig(properties.apple().keyId(), "APPLE_KEY_ID"))
                    .and()
                    .issuer(requiredConfig(properties.apple().teamId(), "APPLE_TEAM_ID"))
                    .subject(clientId)
                    .audience()
                    .add(APPLE_CLIENT_SECRET_AUDIENCE)
                    .and()
                    .issuedAt(Date.from(now))
                    .expiration(Date.from(now.plusSeconds(CLIENT_SECRET_TTL_SECONDS)))
                    .signWith(parsePrivateKey(), Jwts.SIG.ES256)
                    .compact();
        } catch (JwtException | IllegalArgumentException e) {
            log.warn("Apple client_secret creation failed", e);
            throw new CustomException(ErrorCode.INVALID_TOKEN, APPLE_LOGIN_CONFIG_ERROR_MESSAGE);
        }
    }

    private PrivateKey parsePrivateKey() {
        String privateKey = requiredConfig(properties.apple().privateKey(), "APPLE_PRIVATE_KEY")
                .replace("\\n", "\n")
                .replace("\r", "")
                .replace("-----BEGIN PRIVATE KEY-----", "")
                .replace("-----END PRIVATE KEY-----", "")
                .replaceAll("\\s", "");
        try {
            byte[] keyBytes = Base64.getDecoder().decode(privateKey);
            return KeyFactory.getInstance("EC").generatePrivate(new PKCS8EncodedKeySpec(keyBytes));
        } catch (Exception e) {
            log.warn("Apple private key parsing failed", e);
            throw new CustomException(ErrorCode.INVALID_TOKEN, APPLE_LOGIN_CONFIG_ERROR_MESSAGE);
        }
    }

    private String desktopClientId() {
        return requiredConfig(properties.apple().desktopClientId(), "APPLE_DESKTOP_CLIENT_ID");
    }

    private String redirectUri(String requestRedirectUri) {
        String redirectUri = requestRedirectUri;
        if (redirectUri == null || redirectUri.isBlank()) {
            redirectUri = properties.apple().desktopRedirectUri();
        }
        if (redirectUri == null || redirectUri.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE redirectUri는 필수입니다.");
        }
        return URLDecoder.decode(redirectUri.trim(), StandardCharsets.UTF_8);
    }

    private String requiredConfig(String value, String name) {
        if (value == null || value.isBlank()) {
            log.warn("Apple login configuration is missing: {}", name);
            throw new CustomException(ErrorCode.INVALID_TOKEN, APPLE_LOGIN_CONFIG_ERROR_MESSAGE);
        }
        return value.trim();
    }

    private String requiredNonce(String nonce) {
        if (nonce == null || nonce.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "APPLE nonce는 필수입니다.");
        }
        return nonce.trim();
    }

    private void validateNonce(Jwt jwt, String expectedNonce) {
        String actualNonce = jwt.getClaimAsString("nonce");
        if (actualNonce == null || !actualNonce.equals(expectedNonce)) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
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
