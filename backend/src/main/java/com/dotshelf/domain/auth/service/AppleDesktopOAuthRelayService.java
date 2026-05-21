package com.dotshelf.domain.auth.service;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.Base64;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.util.UriComponentsBuilder;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

@Service
@Transactional(readOnly = true)
public class AppleDesktopOAuthRelayService {

    private static final String LOOPBACK_SCHEME = "http";
    private static final String LOOPBACK_HOST = "127.0.0.1";
    private static final String LOOPBACK_PATH = "/dotshelf/oauth/callback/";
    private static final int MIN_CALLBACK_PORT = 18080;
    private static final int MAX_CALLBACK_PORT = 18084;

    public URI buildUnityRedirectUri(String code, String state, String error, String errorDescription) {
        URI loopbackRedirectUri = decodeLoopbackRedirectUri(state);
        if (hasText(error)) {
            return appendQuery(
                    loopbackRedirectUri,
                    "error", error.trim(),
                    "error_description", normalize(errorDescription),
                    "state", state.trim()
            );
        }

        if (!hasText(code)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Apple authorization code가 필요합니다.");
        }
        return appendQuery(
                loopbackRedirectUri,
                "code", code.trim(),
                "state", state.trim()
        );
    }

    private URI decodeLoopbackRedirectUri(String state) {
        if (!hasText(state)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Apple desktop callback state가 필요합니다.");
        }

        int separatorIndex = state.lastIndexOf('.');
        if (separatorIndex < 0 || separatorIndex == state.length() - 1) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Apple desktop callback state가 올바르지 않습니다.");
        }

        String encodedLoopbackRedirectUri = state.substring(separatorIndex + 1);
        try {
            String redirectUri = new String(
                    Base64.getUrlDecoder().decode(encodedLoopbackRedirectUri),
                    StandardCharsets.UTF_8
            );
            URI uri = URI.create(redirectUri);
            validateLoopbackRedirectUri(uri);
            return uri;
        } catch (IllegalArgumentException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Apple desktop callback state가 올바르지 않습니다.");
        }
    }

    private void validateLoopbackRedirectUri(URI uri) {
        boolean valid = LOOPBACK_SCHEME.equalsIgnoreCase(uri.getScheme())
                && LOOPBACK_HOST.equals(uri.getHost())
                && LOOPBACK_PATH.equals(uri.getPath())
                && uri.getPort() >= MIN_CALLBACK_PORT
                && uri.getPort() <= MAX_CALLBACK_PORT
                && uri.getQuery() == null
                && uri.getFragment() == null;
        if (!valid) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Apple desktop callback redirectUri가 올바르지 않습니다.");
        }
    }

    private URI appendQuery(URI baseUri, String... pairs) {
        UriComponentsBuilder builder = UriComponentsBuilder.fromUri(baseUri);
        for (int i = 0; i < pairs.length; i += 2) {
            String value = pairs[i + 1];
            if (!hasText(value)) {
                continue;
            }
            builder.queryParam(pairs[i], value);
        }
        return builder.build().encode().toUri();
    }

    private String normalize(String value) {
        return value == null ? null : value.trim();
    }

    private boolean hasText(String value) {
        return value != null && !value.isBlank();
    }

}
