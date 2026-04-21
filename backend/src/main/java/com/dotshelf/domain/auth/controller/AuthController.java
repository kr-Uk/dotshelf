package com.dotshelf.domain.auth.controller;

import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
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

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/auth")
public class AuthController {

    private final AuthService authService;

    @PostMapping("/social-login")
    public ApiResponse<AuthTokenResponse> socialLogin(@RequestBody @Valid SocialLoginRequest request) {
        return ApiResponse.success(authService.socialLogin(request));
    }

    @PostMapping("/token/refresh")
    public ApiResponse<AuthTokenResponse> refresh(@RequestBody @Valid TokenRefreshRequest request) {
        return ApiResponse.success(authService.refresh(request));
    }

    @PostMapping("/logout")
    public ApiResponse<Void> logout(@RequestBody LogoutRequest request) {
        authService.logout(request.refreshToken());
        return ApiResponse.success();
    }

    @PostMapping("/guest-session")
    public ApiResponse<GuestSessionResponse> startGuestSession() {
        return ApiResponse.success(authService.startGuestSession());
    }

    @PostMapping("/guest-migration")
    public ApiResponse<IdResponse> importGuestData(
            @CurrentUser Long userId,
            @RequestBody @Valid GuestMigrationRequest request
    ) {
        return ApiResponse.success(authService.importGuestData(userId, request));
    }
}
