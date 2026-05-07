package com.dotshelf.domain.report.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.report.dto.ReportDtos.ReportListResponse;
import com.dotshelf.domain.report.dto.ReportDtos.ReportRequest;
import com.dotshelf.domain.report.dto.ReportDtos.ReportResponse;
import com.dotshelf.domain.report.service.ReportService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Report", description = "콘텐츠 신고")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/reports")
public class ReportController {

    private final ReportService reportService;

    @Operation(summary = "콘텐츠 신고", description = "사용자가 문제 있는 콘텐츠를 신고하고 신고 접수 결과를 반환합니다.")
    @PostMapping
    public ApiResponse<ReportResponse> report(@CurrentUser Long userId, @RequestBody @Valid ReportRequest request) {
        return ApiResponse.success(reportService.report(userId, request));
    }

    @Operation(summary = "내 신고 목록 조회", description = "현재 사용자가 접수한 신고 목록과 처리 상태를 조회합니다.")
    @GetMapping("/me")
    public ApiResponse<ReportListResponse> getMyReports(@CurrentUser Long userId) {
        return ApiResponse.success(reportService.getMyReports(userId));
    }
}
