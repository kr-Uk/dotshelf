package com.dotshelf.integration;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.global.security.JwtTokenProvider;
import com.dotshelf.support.TestFixtures;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class SecurityFilterIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private JwtTokenProvider jwtTokenProvider;

    @Test
    @DisplayName("실제 Bearer access token은 SecurityContext principal로 주입되어 CurrentUser로 사용된다")
    void validBearerToken_populatesCurrentUser() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user(uniqueNickname("security-valid")));
        String accessToken = jwtTokenProvider.issueAccessToken(user.getId());

        mockMvc.perform(get("/v1/users/me")
                        .header("Authorization", "Bearer " + accessToken))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(user.getId()))
                .andExpect(jsonPath("$.result.nickname").value(user.getNickname()));
    }

    @Test
    @DisplayName("잘못된 Bearer token은 컨트롤러 진입 전에 JwtAuthenticationFilter가 401로 응답한다")
    void invalidBearerToken_returnsInvalidToken() throws Exception {
        mockMvc.perform(get("/v1/users/me")
                        .header("Authorization", "Bearer invalid-token"))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("A003"))
                .andExpect(jsonPath("$.success").value(false));
    }

    @Test
    @DisplayName("세션 무효화 시점보다 오래된 access token은 JwtAuthenticationFilter가 거부한다")
    void invalidatedSessionToken_returnsInvalidToken() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user(uniqueNickname("security-invalidated")));
        String accessToken = jwtTokenProvider.issueAccessToken(user.getId());
        user.invalidateSessions();
        userRepository.saveAndFlush(user);

        mockMvc.perform(get("/v1/users/me")
                        .header("Authorization", "Bearer " + accessToken))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("A003"));
    }

    private String uniqueNickname(String prefix) {
        return prefix + "-" + System.nanoTime();
    }
}
