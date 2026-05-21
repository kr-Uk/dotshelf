package com.dotshelf.domain.auth.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoSettings;
import org.mockito.quality.Strictness;
import org.mockito.MockitoAnnotations;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.auth.dto.AuthDtos.AuthTokenResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestMigrationRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.GuestSessionResponse;
import com.dotshelf.domain.auth.dto.AuthDtos.SocialLoginRequest;
import com.dotshelf.domain.auth.dto.AuthDtos.TokenRefreshRequest;
import com.dotshelf.domain.auth.service.AppleDesktopOAuthRelayService;
import com.dotshelf.domain.auth.service.AuthService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.IdResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

@MockitoSettings(strictness = Strictness.STRICT_STUBS)
class AuthControllerTest {

    @Mock
    private AuthService authService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);
        mockMvc = MockMvcBuilders
                .standaloneSetup(new AuthController(authService, new AppleDesktopOAuthRelayService()))
                .setControllerAdvice(new GlobalExceptionHandler())
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .build();
    }

    @Test
    @DisplayName("소셜 로그인 요청은 AuthService 결과를 ApiResponse envelope로 반환한다")
    void socialLogin_validRequest_returnsTokenEnvelope() throws Exception {
        given(authService.socialLogin(any(SocialLoginRequest.class)))
                .willReturn(new AuthTokenResponse(1L, "access", "refresh", "Bearer", 1800L, true, false));

        mockMvc.perform(post("/v1/auth/social-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"provider":"GOOGLE","tokenType":"ACCESS_TOKEN","token":"provider-token"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.result.userId").value(1))
                .andExpect(jsonPath("$.result.accessToken").value("access"))
                .andExpect(jsonPath("$.result.newUser").value(true));

        then(authService).should().socialLogin(any(SocialLoginRequest.class));
    }

    @Test
    @DisplayName("Apple 데스크톱 form_post 콜백은 Unity loopback 콜백으로 code와 state를 중계한다")
    void appleDesktopCallback_formPost_redirectsToUnityLoopback() throws Exception {
        String state = desktopAppleState("csrf-token", "http://127.0.0.1:18080/dotshelf/oauth/callback/");

        mockMvc.perform(post("/v1/auth/apple/desktop-callback")
                        .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                        .param("code", "apple-code")
                        .param("state", state))
                .andExpect(status().isFound())
                .andExpect(header().string(
                        "Location",
                        "http://127.0.0.1:18080/dotshelf/oauth/callback/?code=apple-code&state=" + state
                ));
    }

    @Test
    @DisplayName("Apple 데스크톱 query 콜백은 OAuth error도 Unity loopback으로 중계한다")
    void appleDesktopCallback_queryError_redirectsToUnityLoopback() throws Exception {
        String state = desktopAppleState("csrf-token", "http://127.0.0.1:18081/dotshelf/oauth/callback/");

        mockMvc.perform(get("/v1/auth/apple/desktop-callback")
                        .param("error", "access_denied")
                        .param("error_description", "User cancelled")
                        .param("state", state))
                .andExpect(status().isFound())
                .andExpect(header().string(
                        "Location",
                        "http://127.0.0.1:18081/dotshelf/oauth/callback/?error=access_denied&error_description=User%20cancelled&state=" + state
                ));
    }

    @Test
    @DisplayName("Apple 데스크톱 콜백 state의 redirectUri가 loopback 허용 범위를 벗어나면 validation 에러를 반환한다")
    void appleDesktopCallback_invalidLoopbackRedirectUri_returnsValidationError() throws Exception {
        String state = desktopAppleState("csrf-token", "https://evil.example/callback");

        mockMvc.perform(post("/v1/auth/apple/desktop-callback")
                        .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                        .param("code", "apple-code")
                        .param("state", state))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("redirectUri")));
    }

    @Test
    @DisplayName("소셜 로그인 token이 비어 있으면 validation 에러를 반환한다")
    void socialLogin_blankToken_returnsValidationError() throws Exception {
        mockMvc.perform(post("/v1/auth/social-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"provider":"GOOGLE","token":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("token")));
    }

    @Test
    @DisplayName("토큰 refresh 요청은 refreshToken validation 후 AuthService 결과를 반환한다")
    void refresh_validRequest_returnsTokenEnvelope() throws Exception {
        given(authService.refresh(any(TokenRefreshRequest.class)))
                .willReturn(new AuthTokenResponse(1L, "new-access", "new-refresh", "Bearer", 1800L, false, true));

        mockMvc.perform(post("/v1/auth/token/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"refresh-token"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.accessToken").value("new-access"))
                .andExpect(jsonPath("$.result.refreshToken").value("new-refresh"))
                .andExpect(jsonPath("$.result.onboarded").value(true));

        then(authService).should().refresh(any(TokenRefreshRequest.class));
    }

    @Test
    @DisplayName("토큰 refresh 요청에서 refreshToken이 비어 있으면 validation 에러를 반환한다")
    void refresh_blankToken_returnsValidationError() throws Exception {
        mockMvc.perform(post("/v1/auth/token/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("refreshToken")));
    }

    @Test
    @DisplayName("로그아웃은 refreshToken을 revoke하고 성공 envelope를 반환한다")
    void logout_callsServiceAndReturnsSuccess() throws Exception {
        mockMvc.perform(post("/v1/auth/logout")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"refreshToken":"refresh-token"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.result").doesNotExist());

        then(authService).should().logout("refresh-token");
    }

    @Test
    @DisplayName("게스트 세션 시작은 guest token과 세션 id를 반환한다")
    void startGuestSession_returnsGuestSession() throws Exception {
        given(authService.startGuestSession())
                .willReturn(new GuestSessionResponse("guest-token", 86400L, "session-1"));

        mockMvc.perform(post("/v1/auth/guest-session"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.guestToken").value("guest-token"))
                .andExpect(jsonPath("$.result.expiresInSeconds").value(86400))
                .andExpect(jsonPath("$.result.guestSessionId").value("session-1"));
    }

    @Test
    @DisplayName("게스트 데이터 이관은 CurrentUser와 payload를 서비스에 전달한다")
    void importGuestData_passesCurrentUserAndPayload() throws Exception {
        given(authService.importGuestData(any(), any(GuestMigrationRequest.class)))
                .willReturn(new IdResponse(99L));

        mockMvc.perform(post("/v1/auth/guest-migration")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"guestSessionId":"guest-1","payload":"{\\"books\\":[]}"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(99));

        then(authService).should().importGuestData(any(), any(GuestMigrationRequest.class));
    }

    @Test
    @DisplayName("게스트 데이터 이관 payload가 비어 있으면 validation 에러를 반환한다")
    void importGuestData_blankPayload_returnsValidationError() throws Exception {
        mockMvc.perform(post("/v1/auth/guest-migration")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"guestSessionId":"guest-1","payload":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("payload")));
    }

    private String desktopAppleState(String csrfToken, String loopbackRedirectUri) {
        String encodedLoopbackRedirectUri = Base64.getUrlEncoder()
                .withoutPadding()
                .encodeToString(loopbackRedirectUri.getBytes(StandardCharsets.UTF_8));
        return csrfToken + "." + encodedLoopbackRedirectUri;
    }
}
