package com.dotshelf.global.common;

import org.springframework.http.HttpStatus;

import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Getter;

@Getter
@AllArgsConstructor(access = AccessLevel.PRIVATE)
public class ApiResponse<T> {

    private final int httpStatus;
    private final Boolean isSuccess;
    private final String message;
    private final String code;
    private final T result;

    public static <T> ApiResponse<T> success(T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), true, "요청이 성공했습니다.", "SUCCESS", data);
    }

    public static <T> ApiResponse<T> success(String message, T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), true, message, "SUCCESS", data);
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
