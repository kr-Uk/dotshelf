package com.dotshelf.global.common;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class ApiResponseTest {

    @Test
    @DisplayName("success(data): 200 / SUCCESS / 기본 메시지로 감싼다")
    void success_wrapsPayloadWithDefaultMessage() {
        ApiResponse<String> response = ApiResponse.success("payload");

        assertThat(response.getHttpStatus()).isEqualTo(200);
        assertThat(response.isSuccess()).isTrue();
        assertThat(response.getCode()).isEqualTo("SUCCESS");
        assertThat(response.getMessage()).isEqualTo("요청이 성공했습니다.");
        assertThat(response.getResult()).isEqualTo("payload");
    }

    @Test
    @DisplayName("success(message, data): 사용자 지정 메시지를 유지한다")
    void success_withCustomMessage() {
        ApiResponse<List<Integer>> response = ApiResponse.success("책 목록 조회 완료", List.of(1, 2, 3));

        assertThat(response.getMessage()).isEqualTo("책 목록 조회 완료");
        assertThat(response.getCode()).isEqualTo("SUCCESS");
        assertThat(response.isSuccess()).isTrue();
        assertThat(response.getResult()).containsExactly(1, 2, 3);
    }

    @Test
    @DisplayName("success(): no-content 응답은 result 가 null")
    void success_noContent_hasNullResult() {
        ApiResponse<Void> response = ApiResponse.success();

        assertThat(response.getResult()).isNull();
        assertThat(response.isSuccess()).isTrue();
        assertThat(response.getCode()).isEqualTo("SUCCESS");
    }

    @Test
    @DisplayName("error(message): 400 / ERROR 코드 / result=null")
    void error_defaultsToBadRequest() {
        ApiResponse<Object> response = ApiResponse.error("잘못된 요청");

        assertThat(response.getHttpStatus()).isEqualTo(400);
        assertThat(response.isSuccess()).isFalse();
        assertThat(response.getCode()).isEqualTo("ERROR");
        assertThat(response.getMessage()).isEqualTo("잘못된 요청");
        assertThat(response.getResult()).isNull();
    }

    @Test
    @DisplayName("error(status, code, message): 커스텀 상태/도메인 에러코드를 보존한다")
    void error_customStatusAndCode() {
        ApiResponse<Object> response = ApiResponse.error(404, "U001", "유저를 찾을 수 없습니다");

        assertThat(response.getHttpStatus()).isEqualTo(404);
        assertThat(response.getCode()).isEqualTo("U001");
        assertThat(response.getMessage()).isEqualTo("유저를 찾을 수 없습니다");
        assertThat(response.isSuccess()).isFalse();
        assertThat(response.getResult()).isNull();
    }
}
