package com.dotshelf.domain.report.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.report.dto.ReportDtos.ReportListResponse;
import com.dotshelf.domain.report.dto.ReportDtos.ReportRequest;
import com.dotshelf.domain.report.dto.ReportDtos.ReportResponse;
import com.dotshelf.domain.report.service.ReportService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class ReportControllerTest {

    @Mock
    private ReportService reportService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new ReportController(reportService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("신고 생성은 CurrentUser와 request body를 서비스에 전달한다")
    void report_passesCurrentUserAndRequest() throws Exception {
        given(reportService.report(any(), any(ReportRequest.class)))
                .willReturn(reportResponse(10L));

        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "reportedUserId":2,
                                  "targetType":"BOOK",
                                  "targetId":9,
                                  "reasonType":"SPAM",
                                  "reasonDetail":"spam"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10))
                .andExpect(jsonPath("$.result.status").value("PENDING"));

        then(reportService).should().report(any(), any(ReportRequest.class));
    }

    @Test
    @DisplayName("신고 생성은 targetType/targetId/reasonType 필수값을 검증한다")
    void report_validatesRequiredFields() throws Exception {
        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"reportedUserId":2,"targetId":9,"reasonType":"SPAM"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("targetType")));

        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"reportedUserId":2,"targetType":"BOOK","reasonType":"SPAM"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("targetId")));
    }

    @Test
    @DisplayName("내 신고 목록 조회는 CurrentUser를 서비스에 전달한다")
    void getMyReports_passesCurrentUser() throws Exception {
        given(reportService.getMyReports(1L))
                .willReturn(new ReportListResponse(List.of(reportResponse(10L))));

        mockMvc.perform(get("/v1/reports/me").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.reports[0].id").value(10));

        then(reportService).should().getMyReports(1L);
    }

    private ReportResponse reportResponse(Long id) {
        return new ReportResponse(id, 1L, 2L, "BOOK", 9L, "SPAM", "PENDING");
    }
}
