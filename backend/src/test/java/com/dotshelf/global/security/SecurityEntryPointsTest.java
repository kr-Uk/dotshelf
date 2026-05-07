package com.dotshelf.global.security;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.BadCredentialsException;

import tools.jackson.databind.json.JsonMapper;

class SecurityEntryPointsTest {

    private final SecurityEntryPoints entryPoints = new SecurityEntryPoints(new JsonMapper());

    @Test
    @DisplayName("AuthenticationEntryPoint는 401을 프로젝트 ApiResponse envelope로 내려준다")
    void authenticationEntryPoint_writesUnauthorizedEnvelope() throws Exception {
        MockHttpServletResponse response = new MockHttpServletResponse();

        entryPoints.authenticationEntryPoint().commence(
                new MockHttpServletRequest(),
                response,
                new BadCredentialsException("bad credentials")
        );

        assertThat(response.getStatus()).isEqualTo(401);
        assertThat(response.getContentType()).isEqualTo("application/json;charset=UTF-8");
        assertThat(response.getContentAsString())
                .contains("\"code\":\"A001\"")
                .contains("\"success\":false")
                .contains("인증이 필요합니다.");
    }

    @Test
    @DisplayName("AccessDeniedHandler는 403을 프로젝트 ApiResponse envelope로 내려준다")
    void accessDeniedHandler_writesForbiddenEnvelope() throws Exception {
        MockHttpServletResponse response = new MockHttpServletResponse();

        entryPoints.accessDeniedHandler().handle(
                new MockHttpServletRequest(),
                response,
                new AccessDeniedException("forbidden")
        );

        assertThat(response.getStatus()).isEqualTo(403);
        assertThat(response.getContentType()).isEqualTo("application/json;charset=UTF-8");
        assertThat(response.getContentAsString())
                .contains("\"code\":\"A002\"")
                .contains("\"success\":false")
                .contains("접근 권한이 없습니다.");
    }
}
