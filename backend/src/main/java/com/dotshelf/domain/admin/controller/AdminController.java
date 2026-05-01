package com.dotshelf.domain.admin.controller;

import java.time.Instant;
import java.time.LocalDate;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.admin.dto.AdminDtos.AdminMemberResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminMeResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminRoleRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminUserDetailResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminUserNoteResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AnnouncementResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AuditLogResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.BalanceAdjustRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.BalanceAdjustResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.BulkReportActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAdminUserNoteRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.DashboardSummaryResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.MaintenanceRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.MaintenanceResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.OkResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.PageResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.PolicyVersionResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.RefundRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.RefundResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.ReportActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.ReportDetailResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.ReportListItemResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SearchItemResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementAccountChangeRequestResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementCreatorResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementPayoutBatchResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementReconciliationResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementWithdrawalResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.StatsKpiResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.StatsTimeseriesResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SystemHealthResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.UserHistoryResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserListItemResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserPurchaseResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserSpaceResponse;
import com.dotshelf.domain.admin.service.AdminService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Admin", description = "관리자 콘솔 API")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/admin")
public class AdminController {

    private final AdminService adminService;

    @Operation(summary = "관리자 내 계정")
    @GetMapping("/me")
    public ApiResponse<AdminMeResponse> getMe(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getMe(userId));
    }

    @Operation(summary = "관리자 대시보드 요약")
    @GetMapping("/dashboard/summary")
    public ApiResponse<DashboardSummaryResponse> getDashboardSummary(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getDashboardSummary(userId));
    }

    @Operation(summary = "신고 목록")
    @GetMapping("/reports")
    public ApiResponse<PageResponse<ReportListItemResponse>> getReports(
            @CurrentUser Long userId,
            @RequestParam(required = false) String status,
            @RequestParam(required = false) String targetType,
            @RequestParam(required = false) String q,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getReports(userId, status, targetType, q, cursor, limit));
    }

    @Operation(summary = "신고 상세")
    @GetMapping("/reports/{reportId}")
    public ApiResponse<ReportDetailResponse> getReport(@CurrentUser Long userId, @PathVariable Long reportId) {
        return ApiResponse.success(adminService.getReport(userId, reportId));
    }

    @Operation(summary = "신고 조치")
    @PostMapping("/reports/{reportId}/action")
    public ApiResponse<ReportListItemResponse> actReport(
            @CurrentUser Long userId,
            @PathVariable Long reportId,
            @RequestBody @Valid ReportActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.actReport(userId, reportId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "신고 일괄 조치")
    @PostMapping("/reports/bulk-action")
    public ApiResponse<OkResponse> bulkActReports(
            @CurrentUser Long userId,
            @RequestBody @Valid BulkReportActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.bulkActReports(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 목록")
    @GetMapping("/users")
    public ApiResponse<PageResponse<UserListItemResponse>> getUsers(
            @CurrentUser Long userId,
            @RequestParam(required = false) String q,
            @RequestParam(required = false) String status,
            @RequestParam(required = false) String provider,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUsers(userId, q, status, provider, cursor, limit));
    }

    @Operation(summary = "사용자 상세")
    @GetMapping("/users/{targetUserId}")
    public ApiResponse<AdminUserDetailResponse> getUser(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId
    ) {
        return ApiResponse.success(adminService.getUser(userId, targetUserId));
    }

    @Operation(summary = "사용자 상태 조치")
    @PostMapping("/users/{targetUserId}/action")
    public ApiResponse<AdminUserDetailResponse> actUser(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid UserActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.actUser(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 공간 목록")
    @GetMapping("/users/{targetUserId}/spaces")
    public ApiResponse<PageResponse<UserSpaceResponse>> getUserSpaces(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserSpaces(userId, targetUserId, limit));
    }

    @Operation(summary = "사용자 구매 내역")
    @GetMapping("/users/{targetUserId}/purchases")
    public ApiResponse<PageResponse<UserPurchaseResponse>> getUserPurchases(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserPurchases(userId, targetUserId, limit));
    }

    @Operation(summary = "사용자 신고/차단 이력")
    @GetMapping("/users/{targetUserId}/history")
    public ApiResponse<PageResponse<UserHistoryResponse>> getUserHistory(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserHistory(userId, targetUserId, limit));
    }

    @Operation(summary = "관리자 사용자 메모 목록")
    @GetMapping("/users/{targetUserId}/notes")
    public ApiResponse<PageResponse<AdminUserNoteResponse>> getUserNotes(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserNotes(userId, targetUserId, limit));
    }

    @Operation(summary = "관리자 사용자 메모 작성")
    @PostMapping("/users/{targetUserId}/notes")
    public ApiResponse<AdminUserNoteResponse> createUserNote(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid CreateAdminUserNoteRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.createUserNote(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 페이지 잔액 조정")
    @PostMapping("/users/{targetUserId}/adjust-balance")
    public ApiResponse<BalanceAdjustResponse> adjustBalance(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid BalanceAdjustRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.adjustBalance(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 에셋 구매 환불")
    @PostMapping("/users/{targetUserId}/refund")
    public ApiResponse<RefundResponse> refundPurchase(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid RefundRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.refundPurchase(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "정산 대사 요약")
    @GetMapping("/settlement/reconciliation")
    public ApiResponse<SettlementReconciliationResponse> getSettlementReconciliation(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getSettlementReconciliation(userId));
    }

    @Operation(summary = "정산 출금 요청 목록")
    @GetMapping("/settlement/withdrawals")
    public ApiResponse<PageResponse<SettlementWithdrawalResponse>> getSettlementWithdrawals(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementWithdrawals(userId, limit));
    }

    @Operation(summary = "정산 송금 배치 목록")
    @GetMapping("/settlement/payout-batches")
    public ApiResponse<PageResponse<SettlementPayoutBatchResponse>> getSettlementPayoutBatches(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementPayoutBatches(userId, limit));
    }

    @Operation(summary = "정산 계좌 변경 요청 목록")
    @GetMapping("/settlement/account-changes")
    public ApiResponse<PageResponse<SettlementAccountChangeRequestResponse>> getSettlementAccountChanges(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementAccountChanges(userId, limit));
    }

    @Operation(summary = "정산 크리에이터 요약")
    @GetMapping("/settlement/creators")
    public ApiResponse<PageResponse<SettlementCreatorResponse>> getSettlementCreators(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementCreators(userId, limit));
    }

    @Operation(summary = "통계 KPI")
    @GetMapping("/stats/kpi")
    public ApiResponse<StatsKpiResponse> getStatsKpi(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getStatsKpi(userId));
    }

    @Operation(summary = "통계 시계열")
    @GetMapping("/stats/timeseries")
    public ApiResponse<StatsTimeseriesResponse> getStatsTimeseries(
            @CurrentUser Long userId,
            @RequestParam(required = false) LocalDate from,
            @RequestParam(required = false) LocalDate to,
            @RequestParam(defaultValue = "day") String granularity
    ) {
        return ApiResponse.success(adminService.getStatsTimeseries(userId, from, to, granularity));
    }

    @Operation(summary = "운영 시스템 상태")
    @GetMapping("/operations/system-health")
    public ApiResponse<SystemHealthResponse> getSystemHealth(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getSystemHealth(userId));
    }

    @Operation(summary = "관리자 목록")
    @GetMapping("/operations/admins")
    public ApiResponse<PageResponse<AdminMemberResponse>> getAdmins(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getAdmins(userId, limit));
    }

    @Operation(summary = "관리자 role 변경")
    @PostMapping("/operations/admins/{targetUserId}/role")
    public ApiResponse<AdminMemberResponse> updateAdminRole(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid AdminRoleRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.updateAdminRole(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "공지 목록")
    @GetMapping("/operations/announcements")
    public ApiResponse<PageResponse<AnnouncementResponse>> getAnnouncements(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getAnnouncements(userId, limit));
    }

    @Operation(summary = "공지 생성")
    @PostMapping("/operations/announcements")
    public ApiResponse<AnnouncementResponse> createAnnouncement(
            @CurrentUser Long userId,
            @RequestBody @Valid CreateAnnouncementRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.createAnnouncement(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "정책 버전 목록")
    @GetMapping("/operations/policies")
    public ApiResponse<PageResponse<PolicyVersionResponse>> getPolicies(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getPolicies(userId, limit));
    }

    @Operation(summary = "점검 모드 토글")
    @PostMapping("/operations/maintenance")
    public ApiResponse<MaintenanceResponse> updateMaintenance(
            @CurrentUser Long userId,
            @RequestBody @Valid MaintenanceRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.updateMaintenance(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "감사 로그 목록")
    @GetMapping("/audit-logs")
    public ApiResponse<PageResponse<AuditLogResponse>> getAuditLogs(
            @CurrentUser Long userId,
            @RequestParam(required = false) Long actorUserId,
            @RequestParam(required = false) String action,
            @RequestParam(required = false) String targetType,
            @RequestParam(required = false) Instant from,
            @RequestParam(required = false) Instant to,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getAuditLogs(
                userId,
                actorUserId,
                action,
                targetType,
                from,
                to,
                cursor,
                limit
        ));
    }

    @Operation(summary = "관리자 전역 검색")
    @GetMapping("/search")
    public ApiResponse<PageResponse<SearchItemResponse>> search(
            @CurrentUser Long userId,
            @RequestParam(required = false) String q,
            @RequestParam(required = false) String types,
            @RequestParam(defaultValue = "12") int limit
    ) {
        return ApiResponse.success(adminService.search(userId, q, types, limit));
    }

    private String clientIp(HttpServletRequest request) {
        String forwardedFor = request.getHeader("X-Forwarded-For");
        if (forwardedFor != null && !forwardedFor.isBlank()) {
            return forwardedFor.split(",")[0].trim();
        }
        String realIp = request.getHeader("X-Real-IP");
        if (realIp != null && !realIp.isBlank()) {
            return realIp.trim();
        }
        return request.getRemoteAddr();
    }
}
