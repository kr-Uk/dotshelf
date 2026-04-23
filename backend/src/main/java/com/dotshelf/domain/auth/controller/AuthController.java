package com.dotshelf.domain.auth.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.auth.dto.AuthDtos.AuthTokenResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestMigrationRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestSessionResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.LogoutRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.SocialLoginRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.TokenRefreshRequest;
import com.dotshelf.domain.auth.service.AuthService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.IdResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Auth", description = "소셜 로그인 / 토큰 발급 / 게스트 세션")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/auth")
public class AuthController {

    private final AuthService authService;

    @Operation(summary = "소셜 로그인", description = "소셜 provider 토큰 검증 후 자체 JWT 발급 (신규 가입 포함)")
    @SecurityRequirements({})
    @PostMapping("/social-login")
    public ApiResponse<AuthTokenResponse> socialLogin(@RequestBody @Valid SocialLoginRequest request) {
        return ApiResponse.success(authService.socialLogin(request));
    }

    @Operation(summary = "카카오 로그인 콜백", description = "카카오 REST OAuth authorization code 를 자체 JWT 로 교환")
    @SecurityRequirements({})
    @GetMapping("/kakao/callback")
    public ApiResponse<AuthTokenResponse> kakaoCallback(
            @RequestParam(required = false) String code,
            @RequestParam(required = false) String error
    ) {
        return ApiResponse.success(authService.kakaoCallback(code, error));
    }

    @Operation(summary = "토큰 리프레시", description = "refresh 토큰으로 access 토큰 재발급 (rotation + reuse detection)")
    @SecurityRequirements({})
    @PostMapping("/token/refresh")
    public ApiResponse<AuthTokenResponse> refresh(@RequestBody @Valid TokenRefreshRequest request) {
        return ApiResponse.success(authService.refresh(request));
    }

    @Operation(summary = "로그아웃", description = "refresh 토큰 revoke. 클라이언트는 저장된 토큰 삭제까지 책임.")
    @SecurityRequirements({})
    @PostMapping("/logout")
    public ApiResponse<Void> logout(@RequestBody LogoutRequest request) {
        authService.logout(request.refreshToken());
        return ApiResponse.success();
    }

    @Operation(summary = "게스트 세션 시작", description = "비로그인 사용자에게 임시 세션 id 발급")
    @SecurityRequirements({})
    @PostMapping("/guest-session")
    public ApiResponse<GuestSessionResponse> startGuestSession() {
        return ApiResponse.success(authService.startGuestSession());
    }

    @Operation(summary = "게스트 데이터 이관", description = "게스트 시절 payload 를 로그인된 사용자 계정으로 이전")
    @PostMapping("/guest-migration")
    public ApiResponse<IdResponse> importGuestData(
            @CurrentUser Long userId,
            @RequestBody @Valid GuestMigrationRequest request
    ) {
        return ApiResponse.success(authService.importGuestData(userId, request));
    }
}
