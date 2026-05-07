package com.dotshelf.domain.report.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.util.List;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.report.dto.ReportDtos.ReportRequest;
import com.dotshelf.domain.report.entity.Report;
import com.dotshelf.domain.report.repository.ReportRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class ReportServiceTest {

    @Mock
    private ReportRepository reportRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private ReportService reportService;

    @Test
    @DisplayName("신고는 같은 사용자/대상 조합이 없을 때 PENDING 상태로 저장한다")
    void report_notExisting_savesReport() {
        ReportRequest request = new ReportRequest(2L, "READING_RECORD", 10L, "SPAM", "spam content");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(reportRepository.existsByReporterUserIdAndTargetTypeAndTargetId(1L, "READING_RECORD", 10L))
                .willReturn(false);
        given(reportRepository.save(any(Report.class))).willAnswer(invocation -> {
            Report report = invocation.getArgument(0);
            TestFixtures.setId(report, 99L);
            return report;
        });
        ArgumentCaptor<Report> captor = ArgumentCaptor.forClass(Report.class);

        var response = reportService.report(1L, request);

        assertThat(response.id()).isEqualTo(99L);
        assertThat(response.status()).isEqualTo("PENDING");
        then(reportRepository).should().save(captor.capture());
        assertThat(captor.getValue().getReporterUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("이미 신고한 대상이면 REPORT_ALREADY_EXISTS를 던진다")
    void report_duplicate_throwsException() {
        ReportRequest request = new ReportRequest(2L, "READING_RECORD", 10L, "SPAM", "spam content");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(reportRepository.existsByReporterUserIdAndTargetTypeAndTargetId(1L, "READING_RECORD", 10L))
                .willReturn(true);

        assertThatThrownBy(() -> reportService.report(1L, request))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.REPORT_ALREADY_EXISTS);

        then(reportRepository).shouldHaveNoMoreInteractions();
    }

    @Test
    @DisplayName("내 신고 목록은 reporterUserId 기준 최신순 조회 결과를 응답으로 변환한다")
    void getMyReports_returnsMyReports() {
        Report report = TestFixtures.report(1L, "BOOK", 10L);
        TestFixtures.setId(report, 5L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(reportRepository.findByReporterUserIdOrderByIdDesc(1L)).willReturn(List.of(report));

        var response = reportService.getMyReports(1L);

        assertThat(response.reports()).hasSize(1);
        assertThat(response.reports().get(0).id()).isEqualTo(5L);
    }
}
