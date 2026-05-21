package com.dotshelf.global.security;

import java.io.IOException;

import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.security.web.access.AccessDeniedHandler;
import org.springframework.stereotype.Component;

import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.error.ErrorCode;

import tools.jackson.databind.json.JsonMapper;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;

/**
 * 미인증/권한부족 시 Spring Security 기본 401/403 응답이 아니라 프로젝트 `ApiResponse` envelope 로 내려가도록 가로챈다.
 */
@Component
@RequiredArgsConstructor
public class SecurityEntryPoints {

    private final JsonMapper jsonMapper;

    public AuthenticationEntryPoint authenticationEntryPoint() {
        return (HttpServletRequest request, HttpServletResponse response, AuthenticationException e)
                -> writeError(response, ErrorCode.UNAUTHORIZED);
    }

    public AccessDeniedHandler accessDeniedHandler() {
        return (HttpServletRequest request, HttpServletResponse response, AccessDeniedException e)
                -> writeError(response, ErrorCode.FORBIDDEN);
    }

    private void writeError(HttpServletResponse response, ErrorCode errorCode) throws IOException {
        response.setStatus(errorCode.getStatus().value());
        response.setContentType("application/json;charset=UTF-8");
        ApiResponse<Void> body = ApiResponse.error(
                errorCode.getStatus().value(),
                errorCode.getCode(),
                errorCode.getMessage()
        );
        response.getWriter().write(jsonMapper.writeValueAsString(body));
    }
}
