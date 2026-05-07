package com.dotshelf.global.error;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.BadCredentialsException;

class GlobalExceptionHandlerTest {

    private final GlobalExceptionHandler handler = new GlobalExceptionHandler();

    @Test
    @DisplayName("CustomException은 도메인 ErrorCode의 status/code/message를 유지한다")
    void handleCustomException_returnsDomainError() {
        var response = handler.handleCustomException(new CustomException(ErrorCode.USER_NOT_FOUND));

        assertThat(response.getStatusCode().value()).isEqualTo(404);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("U001");
        assertThat(response.getBody().getMessage()).isEqualTo(ErrorCode.USER_NOT_FOUND.getMessage());
    }

    @Test
    @DisplayName("읽을 수 없는 JSON body는 INVALID_INPUT으로 응답한다")
    void handleNotReadable_returnsInvalidInput() {
        var response = handler.handleNotReadable(new HttpMessageNotReadableException("bad json", null));

        assertThat(response.getStatusCode().value()).isEqualTo(400);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("C001");
    }

    @Test
    @DisplayName("IllegalArgumentException은 예외 메시지를 응답 message로 유지한다")
    void handleIllegalArgument_returnsInvalidInputWithExceptionMessage() {
        var response = handler.handleIllegalArgument(new IllegalArgumentException("잘못된 cursor 형식입니다."));

        assertThat(response.getStatusCode().value()).isEqualTo(400);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("C001");
        assertThat(response.getBody().getMessage()).isEqualTo("잘못된 cursor 형식입니다.");
    }

    @Test
    @DisplayName("AuthenticationException은 UNAUTHORIZED envelope로 변환된다")
    void handleAuthentication_returnsUnauthorized() {
        var response = handler.handleAuthentication(new BadCredentialsException("bad credentials"));

        assertThat(response.getStatusCode().value()).isEqualTo(401);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("A001");
    }

    @Test
    @DisplayName("AccessDeniedException은 FORBIDDEN envelope로 변환된다")
    void handleAccessDenied_returnsForbidden() {
        var response = handler.handleAccessDenied(new AccessDeniedException("forbidden"));

        assertThat(response.getStatusCode().value()).isEqualTo(403);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("A002");
    }

    @Test
    @DisplayName("예상하지 못한 예외는 INTERNAL_SERVER_ERROR envelope로 변환된다")
    void handleException_returnsInternalServerError() {
        var response = handler.handleException(new RuntimeException("boom"));

        assertThat(response.getStatusCode().value()).isEqualTo(500);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getCode()).isEqualTo("C003");
    }
}
