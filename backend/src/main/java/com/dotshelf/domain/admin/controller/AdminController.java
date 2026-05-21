package com.dotshelf.domain.admin.controller;

import java.time.Instant;
import java.time.LocalDate;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
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
import com.dotshelf.domain.admin.dto.AdminDtos.UpdateAnnouncementRequest;
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

    @Operation(summary = "관리자 내 계정", description = "현재 로그인한 관리자의 계정 정보와 권한을 조회합니다.")
    @GetMapping("/me")
    public ApiResponse<AdminMeResponse> getMe(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getMe(userId));
    }

    @Operation(summary = "관리자 대시보드 요약", description = "관리자 콘솔 메인 화면에 표시할 핵심 운영 지표를 조회합니다.")
    @GetMapping("/dashboard/summary")
    public ApiResponse<DashboardSummaryResponse> getDashboardSummary(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getDashboardSummary(userId));
    }

    @Operation(summary = "신고 목록", description = "신고 상태, 대상 유형, 검색어 조건으로 신고 목록을 커서 기반으로 조회합니다.")
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

    @Operation(summary = "신고 상세", description = "신고 ID로 신고 상세 내용과 처리 이력을 조회합니다.")
    @GetMapping("/reports/{reportId}")
    public ApiResponse<ReportDetailResponse> getReport(@CurrentUser Long userId, @PathVariable Long reportId) {
        return ApiResponse.success(adminService.getReport(userId, reportId));
    }

    @Operation(summary = "신고 조치", description = "관리자가 단일 신고에 대해 승인, 반려 등 처리 조치를 수행합니다.")
    @PostMapping("/reports/{reportId}/action")
    public ApiResponse<ReportListItemResponse> actReport(
            @CurrentUser Long userId,
            @PathVariable Long reportId,
            @RequestBody @Valid ReportActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.actReport(userId, reportId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "신고 일괄 조치", description = "관리자가 여러 신고를 한 번에 동일한 조치로 처리합니다.")
    @PostMapping("/reports/bulk-action")
    public ApiResponse<OkResponse> bulkActReports(
            @CurrentUser Long userId,
            @RequestBody @Valid BulkReportActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.bulkActReports(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 목록", description = "검색어, 상태, 가입 제공자 조건으로 사용자 목록을 커서 기반으로 조회합니다.")
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

    @Operation(summary = "사용자 상세", description = "관리자 콘솔에서 대상 사용자의 상세 프로필과 운영 상태를 조회합니다.")
    @GetMapping("/users/{targetUserId}")
    public ApiResponse<AdminUserDetailResponse> getUser(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId
    ) {
        return ApiResponse.success(adminService.getUser(userId, targetUserId));
    }

    @Operation(summary = "사용자 상태 조치", description = "관리자가 대상 사용자에게 정지, 해제 등 상태 변경 조치를 수행합니다.")
    @PostMapping("/users/{targetUserId}/action")
    public ApiResponse<AdminUserDetailResponse> actUser(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid UserActionRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.actUser(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 공간 목록", description = "대상 사용자가 소유한 공간 목록을 관리자 권한으로 조회합니다.")
    @GetMapping("/users/{targetUserId}/spaces")
    public ApiResponse<PageResponse<UserSpaceResponse>> getUserSpaces(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserSpaces(userId, targetUserId, limit));
    }

    @Operation(summary = "사용자 구매 내역", description = "대상 사용자의 에셋 구매 내역을 관리자 권한으로 조회합니다.")
    @GetMapping("/users/{targetUserId}/purchases")
    public ApiResponse<PageResponse<UserPurchaseResponse>> getUserPurchases(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserPurchases(userId, targetUserId, limit));
    }

    @Operation(summary = "사용자 신고/차단 이력", description = "대상 사용자의 신고 및 차단 관련 운영 이력을 조회합니다.")
    @GetMapping("/users/{targetUserId}/history")
    public ApiResponse<PageResponse<UserHistoryResponse>> getUserHistory(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserHistory(userId, targetUserId, limit));
    }

    @Operation(summary = "관리자 사용자 메모 목록", description = "대상 사용자에 대해 관리자가 남긴 내부 메모 목록을 조회합니다.")
    @GetMapping("/users/{targetUserId}/notes")
    public ApiResponse<PageResponse<AdminUserNoteResponse>> getUserNotes(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getUserNotes(userId, targetUserId, limit));
    }

    @Operation(summary = "관리자 사용자 메모 작성", description = "대상 사용자에 대한 관리자 내부 메모를 작성합니다.")
    @PostMapping("/users/{targetUserId}/notes")
    public ApiResponse<AdminUserNoteResponse> createUserNote(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid CreateAdminUserNoteRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.createUserNote(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 페이지 잔액 조정", description = "관리자가 대상 사용자의 페이지 잔액을 조정하고 감사 로그를 남깁니다.")
    @PostMapping("/users/{targetUserId}/adjust-balance")
    public ApiResponse<BalanceAdjustResponse> adjustBalance(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid BalanceAdjustRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.adjustBalance(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "사용자 에셋 구매 환불", description = "관리자가 대상 사용자의 에셋 구매 건을 환불 처리합니다.")
    @PostMapping("/users/{targetUserId}/refund")
    public ApiResponse<RefundResponse> refundPurchase(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid RefundRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.refundPurchase(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "정산 대사 요약", description = "결제, 구매, 정산 데이터의 대사 요약 정보를 조회합니다.")
    @GetMapping("/settlement/reconciliation")
    public ApiResponse<SettlementReconciliationResponse> getSettlementReconciliation(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getSettlementReconciliation(userId));
    }

    @Operation(summary = "정산 출금 요청 목록", description = "크리에이터 출금 요청 목록을 관리자 권한으로 조회합니다.")
    @GetMapping("/settlement/withdrawals")
    public ApiResponse<PageResponse<SettlementWithdrawalResponse>> getSettlementWithdrawals(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementWithdrawals(userId, limit));
    }

    @Operation(summary = "정산 송금 배치 목록", description = "정산 송금 배치 처리 목록과 상태를 조회합니다.")
    @GetMapping("/settlement/payout-batches")
    public ApiResponse<PageResponse<SettlementPayoutBatchResponse>> getSettlementPayoutBatches(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementPayoutBatches(userId, limit));
    }

    @Operation(summary = "정산 계좌 변경 요청 목록", description = "크리에이터 정산 계좌 변경 요청 목록을 조회합니다.")
    @GetMapping("/settlement/account-changes")
    public ApiResponse<PageResponse<SettlementAccountChangeRequestResponse>> getSettlementAccountChanges(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementAccountChanges(userId, limit));
    }

    @Operation(summary = "정산 크리에이터 요약", description = "정산 대상 크리에이터별 잔액과 정산 요약 정보를 조회합니다.")
    @GetMapping("/settlement/creators")
    public ApiResponse<PageResponse<SettlementCreatorResponse>> getSettlementCreators(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getSettlementCreators(userId, limit));
    }

    @Operation(summary = "통계 KPI", description = "관리자 통계 화면의 핵심 KPI 값을 조회합니다.")
    @GetMapping("/stats/kpi")
    public ApiResponse<StatsKpiResponse> getStatsKpi(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getStatsKpi(userId));
    }

    @Operation(summary = "통계 시계열", description = "기간과 집계 단위에 따라 운영 통계 시계열 데이터를 조회합니다.")
    @GetMapping("/stats/timeseries")
    public ApiResponse<StatsTimeseriesResponse> getStatsTimeseries(
            @CurrentUser Long userId,
            @RequestParam(required = false) LocalDate from,
            @RequestParam(required = false) LocalDate to,
            @RequestParam(defaultValue = "day") String granularity
    ) {
        return ApiResponse.success(adminService.getStatsTimeseries(userId, from, to, granularity));
    }

    @Operation(summary = "운영 시스템 상태", description = "관리자 콘솔에서 확인할 서버, 배치, 외부 연동 등의 운영 상태를 조회합니다.")
    @GetMapping("/operations/system-health")
    public ApiResponse<SystemHealthResponse> getSystemHealth(@CurrentUser Long userId) {
        return ApiResponse.success(adminService.getSystemHealth(userId));
    }

    @Operation(summary = "관리자 목록", description = "관리자 권한을 가진 사용자 목록을 조회합니다.")
    @GetMapping("/operations/admins")
    public ApiResponse<PageResponse<AdminMemberResponse>> getAdmins(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getAdmins(userId, limit));
    }

    @Operation(summary = "관리자 role 변경", description = "대상 관리자의 role을 변경하고 감사 로그를 남깁니다.")
    @PostMapping("/operations/admins/{targetUserId}/role")
    public ApiResponse<AdminMemberResponse> updateAdminRole(
            @CurrentUser Long userId,
            @PathVariable Long targetUserId,
            @RequestBody @Valid AdminRoleRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.updateAdminRole(userId, targetUserId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "공지 목록", description = "운영 공지 목록을 관리자 권한으로 조회합니다.")
    @GetMapping("/operations/announcements")
    public ApiResponse<PageResponse<AnnouncementResponse>> getAnnouncements(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getAnnouncements(userId, limit));
    }

    @Operation(summary = "공지 생성", description = "운영 공지를 생성하고 감사 로그를 남깁니다.")
    @PostMapping("/operations/announcements")
    public ApiResponse<AnnouncementResponse> createAnnouncement(
            @CurrentUser Long userId,
            @RequestBody @Valid CreateAnnouncementRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.createAnnouncement(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "공지 수정", description = "기존 운영 공지의 제목, 내용, 노출 상태 등을 수정합니다.")
    @PutMapping("/operations/announcements/{announcementId}")
    public ApiResponse<AnnouncementResponse> updateAnnouncement(
            @CurrentUser Long userId,
            @PathVariable Long announcementId,
            @RequestBody @Valid UpdateAnnouncementRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.updateAnnouncement(userId, announcementId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "공지 삭제", description = "운영 공지를 삭제하고 감사 로그를 남깁니다.")
    @DeleteMapping("/operations/announcements/{announcementId}")
    public ApiResponse<Void> deleteAnnouncement(
            @CurrentUser Long userId,
            @PathVariable Long announcementId,
            HttpServletRequest httpRequest
    ) {
        adminService.deleteAnnouncement(userId, announcementId, clientIp(httpRequest));
        return ApiResponse.success();
    }

    @Operation(summary = "정책 버전 목록", description = "서비스 약관, 개인정보처리방침 등 정책 버전 목록을 조회합니다.")
    @GetMapping("/operations/policies")
    public ApiResponse<PageResponse<PolicyVersionResponse>> getPolicies(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(adminService.getPolicies(userId, limit));
    }

    @Operation(summary = "점검 모드 토글", description = "서비스 점검 모드 활성화 여부를 변경하고 현재 상태를 반환합니다.")
    @PostMapping("/operations/maintenance")
    public ApiResponse<MaintenanceResponse> updateMaintenance(
            @CurrentUser Long userId,
            @RequestBody @Valid MaintenanceRequest request,
            HttpServletRequest httpRequest
    ) {
        return ApiResponse.success(adminService.updateMaintenance(userId, request, clientIp(httpRequest)));
    }

    @Operation(summary = "감사 로그 목록", description = "관리자 조치 감사 로그를 행위자, 액션, 대상, 기간 조건으로 조회합니다.")
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

    @Operation(summary = "관리자 전역 검색", description = "관리자 콘솔에서 사용자, 신고, 정산 등 여러 리소스를 통합 검색합니다.")
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
