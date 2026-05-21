package com.dotshelf.global.common;

import org.springframework.http.HttpStatus;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Getter;

@Getter
@AllArgsConstructor(access = AccessLevel.PRIVATE)
@Schema(description = "공통 응답 envelope")
public class ApiResponse<T> {

    @Schema(description = "HTTP 상태 코드", example = "200")
    private final int httpStatus;

    @Schema(description = "성공 여부", example = "true")
    private final boolean isSuccess;

    @Schema(description = "사용자에게 보여줄 메시지", example = "요청이 성공했습니다.")
    private final String message;

    @Schema(description = "내부 결과 코드 (SUCCESS, U001 등 프론트 분기 키)", example = "SUCCESS")
    private final String code;

    @Schema(description = "실제 페이로드. 실패 시 null")
    private final T result;

    public static <T> ApiResponse<T> success(T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), true, "요청이 성공했습니다.", "SUCCESS", data);
    }

    public static <T> ApiResponse<T> success(String message, T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), true, message, "SUCCESS", data);
    }

    public static <T> ApiResponse<T> created(T data) {
        return new ApiResponse<>(HttpStatus.CREATED.value(), true, "요청이 성공했습니다.", "SUCCESS", data);
    }

    public static ApiResponse<Void> success() {
        return new ApiResponse<>(HttpStatus.OK.value(), true, "요청이 성공했습니다.", "SUCCESS", null);
    }

    public static <T> ApiResponse<T> error(String message) {
        return new ApiResponse<>(HttpStatus.BAD_REQUEST.value(), false, message, "ERROR", null);
    }

    public static <T> ApiResponse<T> error(int httpStatus, String code, String message) {
        return new ApiResponse<>(httpStatus, false, message, code, null);
    }
}
