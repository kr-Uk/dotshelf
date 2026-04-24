package com.dotshelf.domain.report.dto;

import java.util.List;

import com.dotshelf.domain.report.entity.Report;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public final class ReportDtos {

    private ReportDtos() {
    }

    public record ReportRequest(
            Long reportedUserId,
            @NotBlank(message = "targetType은 필수입니다.")
            String targetType,
            @NotNull(message = "targetId는 필수입니다.")
            Long targetId,
            @NotBlank(message = "reasonType은 필수입니다.")
            String reasonType,
            String reasonDetail
    ) {
    }

    public record ReportResponse(
            Long id,
            Long reporterUserId,
            Long reportedUserId,
            String targetType,
            Long targetId,
            String reasonType,
            String status
    ) {

        public static ReportResponse from(Report report) {
            return new ReportResponse(
                    report.getId(),
                    report.getReporterUserId(),
                    report.getReportedUserId(),
                    report.getTargetType(),
                    report.getTargetId(),
                    report.getReasonType(),
                    report.getStatus()
            );
        }
    }

    public record ReportListResponse(List<ReportResponse> reports) {
    }
}
