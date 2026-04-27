package com.dotshelf.domain.report.service;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.report.dto.ReportDtos.ReportListResponse;
import com.dotshelf.domain.report.dto.ReportDtos.ReportRequest;
import com.dotshelf.domain.report.dto.ReportDtos.ReportResponse;
import com.dotshelf.domain.report.entity.Report;
import com.dotshelf.domain.report.repository.ReportRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class ReportService {

    private final ReportRepository reportRepository;
    private final UserService userService;

    @Transactional
    public ReportResponse report(Long userId, ReportRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        if (reportRepository.existsByReporterUserIdAndTargetTypeAndTargetId(
                currentUserId,
                request.targetType(),
                request.targetId()
        )) {
            throw new CustomException(ErrorCode.REPORT_ALREADY_EXISTS);
        }
        Report report = reportRepository.save(Report.builder()
                .reporterUserId(currentUserId)
                .reportedUserId(request.reportedUserId())
                .targetType(request.targetType())
                .targetId(request.targetId())
                .reasonType(request.reasonType())
                .reasonDetail(request.reasonDetail())
                .build());
        return ReportResponse.from(report);
    }

    public ReportListResponse getMyReports(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new ReportListResponse(reportRepository.findByReporterUserIdOrderByIdDesc(currentUserId).stream()
                .map(ReportResponse::from)
                .toList());
    }
}
