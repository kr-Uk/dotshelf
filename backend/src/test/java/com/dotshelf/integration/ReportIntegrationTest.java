package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.report.repository.ReportRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.TestFixtures;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
@Transactional
class ReportIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private ReportRepository reportRepository;

    @Test
    @DisplayName("HTTP로 신고를 생성하면 reports 테이블에 PENDING 신고가 저장된다")
    void createReport_persistsReport() throws Exception {
        var reporter = userRepository.saveAndFlush(TestFixtures.user("reporter"));

        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", reporter.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "reportedUserId": 2,
                                  "targetType": "BOOK",
                                  "targetId": 10,
                                  "reasonType": "SPAM",
                                  "reasonDetail": "spam"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.status").value("PENDING"));

        var reports = reportRepository.findByReporterUserIdOrderByIdDesc(reporter.getId());
        assertThat(reports).hasSize(1);
        assertThat(reports.get(0).getTargetType()).isEqualTo("BOOK");
    }

    @Test
    @DisplayName("동일 사용자가 같은 target을 중복 신고하면 409를 반환하고 row를 추가하지 않는다")
    void createReport_duplicateTargetReturnsConflict() throws Exception {
        var reporter = userRepository.saveAndFlush(TestFixtures.user("duplicate-reporter"));
        String body = """
                {
                  "reportedUserId": 2,
                  "targetType": "USER",
                  "targetId": 2,
                  "reasonType": "ABUSE",
                  "reasonDetail": "abuse"
                }
                """;

        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", reporter.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isOk());
        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", reporter.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("RP001"));

        assertThat(reportRepository.findByReporterUserIdOrderByIdDesc(reporter.getId())).hasSize(1);
    }

    @Test
    @DisplayName("내 신고 목록은 reporterUserId로 필터링하고 validation 실패는 row를 남기지 않는다")
    void getMyReports_filtersReporterAndValidationFailureDoesNotPersist() throws Exception {
        var reporter = userRepository.saveAndFlush(TestFixtures.user("list-reporter"));
        var other = userRepository.saveAndFlush(TestFixtures.user("other-reporter"));
        reportRepository.saveAndFlush(TestFixtures.report(reporter.getId(), "BOOK", 10L));
        reportRepository.saveAndFlush(TestFixtures.report(other.getId(), "BOOK", 20L));

        mockMvc.perform(get("/v1/reports/me")
                        .header("X-Test-Admin-User-Id", reporter.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.reports.length()").value(1))
                .andExpect(jsonPath("$.result.reports[0].targetId").value(10));

        mockMvc.perform(post("/v1/reports")
                        .header("X-Test-Admin-User-Id", reporter.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"reportedUserId":2,"targetType":" ","targetId":30,"reasonType":"SPAM"}
                                """))
                .andExpect(status().isBadRequest());

        assertThat(reportRepository.findAll()).hasSize(2);
    }
}
