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

import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Report", description = "콘텐츠 신고")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/reports")
public class ReportController {

    private final ReportService reportService;

    @PostMapping
    public ApiResponse<ReportResponse> report(@CurrentUser Long userId, @RequestBody @Valid ReportRequest request) {
        return ApiResponse.success(reportService.report(userId, request));
    }

    @GetMapping("/me")
    public ApiResponse<ReportListResponse> getMyReports(@CurrentUser Long userId) {
        return ApiResponse.success(reportService.getMyReports(userId));
    }
}
