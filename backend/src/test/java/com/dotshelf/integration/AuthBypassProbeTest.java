package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.global.security.JwtTokenProvider;
import com.dotshelf.support.TestFixtures;

/**
 * 인증 우회 회귀 probe.
 *
 * <p>비공개 엔드포인트가 (1) 토큰 없이, (2) 과거 신뢰하던 {@code X-User-Id} 헤더만으로 진입 가능한지 실측한다.
 * 실패 메시지에 엔드포인트별 실제 status 를 전부 담아 근거로 쓸 수 있게 한다.
 */
@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class AuthBypassProbeTest {

    /** 전부 @CurrentUser 를 요구하는 비공개 GET 엔드포인트 (path variable 없는 것만). */
    private static final List<String> PROTECTED_ENDPOINTS = List.of(
            "/v1/users/me",
            "/v1/users/me/followers",
            "/v1/users/me/followings",
            "/v1/notifications",
            "/v1/payments/purchases",
            "/v1/payments/transactions",
            "/v1/admin/me",
            "/v1/admin/dashboard/summary",
            "/v1/admin/users",
            "/v1/admin/reports"
    );

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private JwtTokenProvider jwtTokenProvider;

    @Test
    @DisplayName("[probe] 토큰 없이 호출한 비공개 엔드포인트는 전부 401 이어야 한다")
    void noToken_everyProtectedEndpointIsRejected() throws Exception {
        Map<String, Integer> statuses = new LinkedHashMap<>();
        for (String endpoint : PROTECTED_ENDPOINTS) {
            statuses.put(endpoint, mockMvc.perform(get(endpoint)).andReturn().getResponse().getStatus());
        }

        List<String> notRejected = collectNotRejected(statuses);
        assertThat(notRejected)
                .as("토큰 없이 401 이 아닌 엔드포인트%n%s", render(statuses))
                .isEmpty();
    }

    @Test
    @DisplayName("[probe] X-User-Id 헤더만 붙인 호출도 전부 401 이어야 한다")
    void spoofedUserIdHeader_everyProtectedEndpointIsRejected() throws Exception {
        var victim = userRepository.saveAndFlush(TestFixtures.user("probe-victim-" + System.nanoTime()));

        Map<String, Integer> statuses = new LinkedHashMap<>();
        for (String endpoint : PROTECTED_ENDPOINTS) {
            statuses.put(endpoint, mockMvc.perform(get(endpoint)
                            .header("X-User-Id", String.valueOf(victim.getId())))
                    .andReturn().getResponse().getStatus());
        }

        List<String> notRejected = collectNotRejected(statuses);
        assertThat(notRejected)
                .as("X-User-Id 위조 헤더로 401 이 아닌 엔드포인트%n%s", render(statuses))
                .isEmpty();
    }

    @Test
    @DisplayName("[probe] X-User-Id 헤더로 피해자 계정 정보가 실제로 새는지 확인한다")
    void spoofedUserIdHeader_doesNotLeakVictimProfile() throws Exception {
        var victim = userRepository.saveAndFlush(TestFixtures.user("probe-leak-" + System.nanoTime()));

        MvcResult result = mockMvc.perform(get("/v1/users/me")
                        .header("X-User-Id", String.valueOf(victim.getId())))
                .andReturn();
        String body = result.getResponse().getContentAsString();

        assertThat(body)
                .as("GET /v1/users/me status=%d body=%s", result.getResponse().getStatus(), body)
                .doesNotContain(victim.getNickname());
    }

    @Test
    @DisplayName("[control] 정상 발급 토큰은 200 으로 통과한다")
    void validAccessToken_passes() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("probe-valid-" + System.nanoTime()));
        String accessToken = jwtTokenProvider.issueAccessToken(user.getId());

        int status = mockMvc.perform(get("/v1/users/me")
                        .header("Authorization", "Bearer " + accessToken))
                .andReturn().getResponse().getStatus();

        assertThat(status).isEqualTo(200);
    }

    private List<String> collectNotRejected(Map<String, Integer> statuses) {
        List<String> notRejected = new ArrayList<>();
        statuses.forEach((endpoint, status) -> {
            if (status != 401) {
                notRejected.add(endpoint + " -> " + status);
            }
        });
        return notRejected;
    }

    private String render(Map<String, Integer> statuses) {
        StringBuilder sb = new StringBuilder();
        statuses.forEach((endpoint, status) -> sb.append("  ")
                .append(status == 401 ? "BLOCKED" : "PASSED ")
                .append(" ").append(status)
                .append("  ").append(endpoint).append(System.lineSeparator()));
        return sb.toString();
    }
}
