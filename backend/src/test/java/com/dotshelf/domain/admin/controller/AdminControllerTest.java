package com.dotshelf.domain.admin.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.Instant;
import java.time.LocalDate;
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

import com.dotshelf.domain.admin.dto.AdminDtos.AdminMeResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminMemberResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AnnouncementResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AuditLogResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.BalanceAdjustRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.BalanceAdjustResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.BulkReportActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAdminUserNoteRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.DashboardContentSummary;
import com.dotshelf.domain.admin.dto.AdminDtos.DashboardModerationSummary;
import com.dotshelf.domain.admin.dto.AdminDtos.DashboardOpsSummary;
import com.dotshelf.domain.admin.dto.AdminDtos.DashboardSettlementSummary;
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
import com.dotshelf.domain.admin.dto.AdminDtos.ReportReporterResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.ReportTargetResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SearchItemResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementAccountChangeRequestResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementCreatorResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementPayoutBatchResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementReconciliationResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementWithdrawalResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.StatsKpiResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.SystemHealthResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UpdateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.UserHistoryResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.UserPurchaseResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserSpaceResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminUserDetailResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.AdminUserNoteResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.UserListItemResponse;
import com.dotshelf.domain.admin.service.AdminService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class AdminControllerTest {

    @Mock
    private AdminService adminService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new AdminController(adminService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("관리자 내 계정과 대시보드 요약은 current userId를 전달한다")
    void getMeAndDashboard_passCurrentUser() throws Exception {
        given(adminService.getMe(1L)).willReturn(new AdminMeResponse("1", "admin@example.com", "admin", "admin", "운영", true, false, null));
        given(adminService.getDashboardSummary(1L)).willReturn(new DashboardSummaryResponse(
                new DashboardContentSummary(1, 2, 3),
                new DashboardModerationSummary(4, 5, 6),
                new DashboardSettlementSummary(7, 8, true),
                new DashboardOpsSummary(9, 10, 11, "UP")
        ));

        mockMvc.perform(get("/v1/admin/me").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.email").value("admin@example.com"));

        mockMvc.perform(get("/v1/admin/dashboard/summary").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.ops.apiStatus").value("UP"));
    }

    @Test
    @DisplayName("신고 목록 조회는 필터와 페이징 파라미터를 전달한다")
    void getReports_passesFilters() throws Exception {
        given(adminService.getReports(1L, "OPEN", "BOOK", "spam", "c1", 5))
                .willReturn(new PageResponse<>(List.of(reportItem("9")), "next", 1));

        mockMvc.perform(get("/v1/admin/reports")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("status", "OPEN")
                        .param("targetType", "BOOK")
                        .param("q", "spam")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].id").value("9"))
                .andExpect(jsonPath("$.result.totalCount").value(1));
    }

    @Test
    @DisplayName("신고 조치는 X-Forwarded-For 첫 IP를 감사 로그 IP로 전달한다")
    void actReport_usesForwardedForFirstIp() throws Exception {
        given(adminService.actReport(eq(1L), eq(9L), any(ReportActionRequest.class), eq("203.0.113.1")))
                .willReturn(reportItem("9"));

        mockMvc.perform(post("/v1/admin/reports/9/action")
                        .header("X-Test-Admin-User-Id", "1")
                        .header("X-Forwarded-For", "203.0.113.1, 10.0.0.1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"action":"WARN","reason":"spam"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value("9"));
    }

    @Test
    @DisplayName("신고 일괄 조치는 ids와 action 필수값을 검증한다")
    void bulkActReports_validatesBody() throws Exception {
        given(adminService.bulkActReports(eq(1L), any(BulkReportActionRequest.class), any()))
                .willReturn(OkResponse.success());

        mockMvc.perform(post("/v1/admin/reports/bulk-action")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"ids":[1,2],"action":"DISMISS","reason":"ok"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.ok").value(true));

        mockMvc.perform(post("/v1/admin/reports/bulk-action")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"ids":[],"action":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("사용자 목록과 조치는 current userId와 targetUserId를 전달한다")
    void userQueriesAndAction_passIds() throws Exception {
        given(adminService.getUsers(1L, "kim", "ACTIVE", "GOOGLE", "c1", 5))
                .willReturn(new PageResponse<>(List.of(new UserListItemResponse("2", "kim", "kim@example.com", null, null, "ACTIVE", "GOOGLE", 0, 0, 0, 0, 0)), null, 1));
        given(adminService.actUser(eq(1L), eq(2L), any(UserActionRequest.class), any()))
                .willReturn(null);

        mockMvc.perform(get("/v1/admin/users")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("q", "kim")
                        .param("status", "ACTIVE")
                        .param("provider", "GOOGLE")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].nickname").value("kim"));

        mockMvc.perform(post("/v1/admin/users/2/action")
                        .header("X-Test-Admin-User-Id", "1")
                        .header("X-Real-IP", "198.51.100.7")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"action":"SUSPEND","reason":"abuse"}
                                """))
                .andExpect(status().isOk());

        then(adminService).should().actUser(eq(1L), eq(2L), any(UserActionRequest.class), eq("198.51.100.7"));
    }

    @Test
    @DisplayName("관리자 메모 작성은 body 필수값을 검증한다")
    void createUserNote_validatesBody() throws Exception {
        mockMvc.perform(post("/v1/admin/users/2/notes")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"body":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("body")));

        then(adminService).shouldHaveNoMoreInteractions();
    }

    @Test
    @DisplayName("잔액 조정과 환불은 reason 필수값을 검증한다")
    void balanceAdjustAndRefund_validateReason() throws Exception {
        given(adminService.adjustBalance(eq(1L), eq(2L), any(BalanceAdjustRequest.class), any()))
                .willReturn(new BalanceAdjustResponse("2", 100, null));
        given(adminService.refundPurchase(eq(1L), eq(2L), any(RefundRequest.class), any()))
                .willReturn(new RefundResponse("2", "9", 10, 90));

        mockMvc.perform(post("/v1/admin/users/2/adjust-balance")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"deltaPages":100,"reason":"event","memo":"memo"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.balancePages").value(100));

        mockMvc.perform(post("/v1/admin/users/2/refund")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"purchaseId":"9","reason":"refund","comment":"memo"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.refundedPages").value(10));

        mockMvc.perform(post("/v1/admin/users/2/refund")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"purchaseId":"9","reason":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("정산/통계/검색 조회 API는 query parameter를 전달한다")
    void settlementStatsAndSearch_passParameters() throws Exception {
        given(adminService.getSettlementReconciliation(1L))
                .willReturn(new SettlementReconciliationResponse(100, 90, 10, true, Instant.now()));
        given(adminService.getStatsKpi(1L))
                .willReturn(new StatsKpiResponse(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0.1));
        given(adminService.search(1L, "kim", "users", 5))
                .willReturn(new PageResponse<>(List.of(new SearchItemResponse("2", "USER", "kim", "user", "/users/2")), null, 1));

        mockMvc.perform(get("/v1/admin/settlement/reconciliation").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.consistent").value(true));

        mockMvc.perform(get("/v1/admin/stats/kpi").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.dau").value(1));

        mockMvc.perform(get("/v1/admin/search")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("q", "kim")
                        .param("types", "users")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].type").value("USER"));
    }

    @Test
    @DisplayName("시계열 통계는 LocalDate query parameter를 바인딩한다")
    void statsTimeseries_bindsDates() throws Exception {
        given(adminService.getStatsTimeseries(eq(1L), eq(LocalDate.parse("2026-05-01")), eq(LocalDate.parse("2026-05-07")), eq("day")))
                .willReturn(null);

        mockMvc.perform(get("/v1/admin/stats/timeseries")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("from", "2026-05-01")
                        .param("to", "2026-05-07")
                        .param("granularity", "day"))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("공지 생성/수정은 audience 패턴과 필수값을 검증한다")
    void announcements_validateBody() throws Exception {
        given(adminService.createAnnouncement(eq(1L), any(CreateAnnouncementRequest.class), any()))
                .willReturn(announcement("9"));
        given(adminService.updateAnnouncement(eq(1L), eq(9L), any(UpdateAnnouncementRequest.class), any()))
                .willReturn(announcement("9"));

        String body = """
                {"title":"공지","body":"내용","audience":"all_users","urgent":true}
                """;
        mockMvc.perform(post("/v1/admin/operations/announcements")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value("9"));

        mockMvc.perform(put("/v1/admin/operations/announcements/9")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.urgent").value(true));

        mockMvc.perform(post("/v1/admin/operations/announcements")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"공지","body":"내용","audience":"bad"}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("공지 삭제와 점검 모드 변경은 성공 응답을 반환한다")
    void deleteAnnouncementAndMaintenance_returnEnvelope() throws Exception {
        given(adminService.updateMaintenance(eq(1L), any(MaintenanceRequest.class), any()))
                .willReturn(new MaintenanceResponse("1", true, "점검", "1", Instant.now()));

        mockMvc.perform(delete("/v1/admin/operations/announcements/9")
                        .header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        mockMvc.perform(post("/v1/admin/operations/maintenance")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"enabled":true,"message":"점검"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.enabled").value(true));
    }

    @Test
    @DisplayName("감사 로그 조회는 필터 파라미터를 서비스에 전달한다")
    void auditLogs_passFilters() throws Exception {
        given(adminService.getAuditLogs(eq(1L), eq(2L), eq("ACTION"), eq("USER"), any(), any(), eq("c1"), eq(5)))
                .willReturn(new PageResponse<>(List.of(new AuditLogResponse("1", "2", "admin@example.com", "ACTION", "USER", "2", null, null, "reason", Instant.now(), "127.0.0.1")), null, 1));

        mockMvc.perform(get("/v1/admin/audit-logs")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("actorUserId", "2")
                        .param("action", "ACTION")
                        .param("targetType", "USER")
                        .param("from", "2026-05-01T00:00:00Z")
                        .param("to", "2026-05-07T00:00:00Z")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].action").value("ACTION"));
    }

    @Test
    @DisplayName("신고/사용자 상세 하위 조회 API는 path variable과 limit을 전달한다")
    void detailAndUserNestedQueries_passPathVariables() throws Exception {
        given(adminService.getReport(1L, 9L))
                .willReturn(new ReportDetailResponse(
                        "9",
                        new ReportTargetResponse("USER", "2", "target", "2", "target"),
                        List.of(new ReportReporterResponse("3", "reporter", "SPAM", "spam", Instant.now())),
                        "PENDING"
                ));
        given(adminService.getUser(1L, 2L))
                .willReturn(new AdminUserDetailResponse("2", "target", "target@example.com", "USER", "ACTIVE", true, null, null, Instant.now(), null, List.of("KAKAO"), 10, 1, 2, 3, 4, 5));
        given(adminService.getUserSpaces(1L, 2L, 3))
                .willReturn(new PageResponse<>(List.of(new UserSpaceResponse("10", "space", "PUBLIC", 7, 8, 1, 2)), null, 1));
        given(adminService.getUserPurchases(1L, 2L, 4))
                .willReturn(new PageResponse<>(List.of(new UserPurchaseResponse("11", "20", 100, "COMPLETED", Instant.now())), null, 1));
        given(adminService.getUserHistory(1L, 2L, 5))
                .willReturn(new PageResponse<>(List.of(new UserHistoryResponse("12", "REPORT", "USER", "SPAM", Instant.now())), null, 1));
        given(adminService.getUserNotes(1L, 2L, 6))
                .willReturn(new PageResponse<>(List.of(new AdminUserNoteResponse("13", "2", "1", "admin", "memo", Instant.now())), null, 1));

        mockMvc.perform(get("/v1/admin/reports/9").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.target.type").value("USER"));

        mockMvc.perform(get("/v1/admin/users/2").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.socialProviders[0]").value("KAKAO"));

        mockMvc.perform(get("/v1/admin/users/2/spaces")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "3"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].visitCount").value(7));

        mockMvc.perform(get("/v1/admin/users/2/purchases")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "4"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].pricePages").value(100));

        mockMvc.perform(get("/v1/admin/users/2/history")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].type").value("REPORT"));

        mockMvc.perform(get("/v1/admin/users/2/notes")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "6"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].authorName").value("admin"));
    }

    @Test
    @DisplayName("정산 목록 API와 운영 조회 API는 기본 envelope와 limit을 보존한다")
    void settlementAndOperationsQueries_returnEnvelope() throws Exception {
        given(adminService.getSettlementWithdrawals(1L, 3))
                .willReturn(new PageResponse<>(List.of(new SettlementWithdrawalResponse("1", "2", 100, "REQUESTED", Instant.now(), "memo")), null, 1));
        given(adminService.getSettlementPayoutBatches(1L, 4))
                .willReturn(new PageResponse<>(List.of(new SettlementPayoutBatchResponse("2", "batch-1", 300, 3, "READY", Instant.now(), null)), null, 1));
        given(adminService.getSettlementAccountChanges(1L, 5))
                .willReturn(new PageResponse<>(List.of(new SettlementAccountChangeRequestResponse("3", "2", "bank", "1234****", "holder", "REQUESTED", Instant.now())), null, 1));
        given(adminService.getSettlementCreators(1L, 6))
                .willReturn(new PageResponse<>(List.of(new SettlementCreatorResponse("4", "2", "creator", "ACTIVE", 7, 800)), null, 1));
        given(adminService.getSystemHealth(1L))
                .willReturn(new SystemHealthResponse("UP", "UP", "UNKNOWN", "READY", "UP", 2, false));
        given(adminService.getAdmins(1L, 7))
                .willReturn(new PageResponse<>(List.of(new AdminMemberResponse("1", "admin@example.com", "admin", "ADMIN", "운영", true, false, Instant.now())), null, 1));
        given(adminService.getAnnouncements(1L, 8))
                .willReturn(new PageResponse<>(List.of(announcement("9")), null, 1));
        given(adminService.getPolicies(1L, 9))
                .willReturn(new PageResponse<>(List.of(new PolicyVersionResponse("10", "terms", "v1", "terms", "body", Instant.now(), true, "1", Instant.now())), null, 1));

        mockMvc.perform(get("/v1/admin/settlement/withdrawals")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "3"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].requestedPages").value(100));

        mockMvc.perform(get("/v1/admin/settlement/payout-batches")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "4"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].batchCode").value("batch-1"));

        mockMvc.perform(get("/v1/admin/settlement/account-changes")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].accountNumberMasked").value("1234****"));

        mockMvc.perform(get("/v1/admin/settlement/creators")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "6"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].earnedPages").value(800));

        mockMvc.perform(get("/v1/admin/operations/system-health").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.payment").value("READY"));

        mockMvc.perform(get("/v1/admin/operations/admins")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "7"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].role").value("ADMIN"));

        mockMvc.perform(get("/v1/admin/operations/announcements")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "8"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].id").value("9"));

        mockMvc.perform(get("/v1/admin/operations/policies")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("limit", "9"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].version").value("v1"));
    }

    @Test
    @DisplayName("관리자 role 변경은 role 필수값을 검증하고 Real-IP를 전달한다")
    void updateAdminRole_validatesRoleAndPassesRealIp() throws Exception {
        given(adminService.updateAdminRole(eq(1L), eq(2L), any(), eq("198.51.100.9")))
                .willReturn(new AdminMemberResponse("2", "target@example.com", "target", "ADMIN", "운영", true, false, null));

        mockMvc.perform(post("/v1/admin/operations/admins/2/role")
                        .header("X-Test-Admin-User-Id", "1")
                        .header("X-Real-IP", "198.51.100.9")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"role":"ADMIN","reason":"승격"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.role").value("ADMIN"));

        mockMvc.perform(post("/v1/admin/operations/admins/2/role")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"role":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    private ReportListItemResponse reportItem(String id) {
        return new ReportListItemResponse(id, "BOOK", "10", "excerpt", "2", "target", 1,
                "SPAM", "detail", "OPEN", "P2", false, false, Instant.now(), null, null);
    }

    private AnnouncementResponse announcement(String id) {
        return new AnnouncementResponse(id, "공지", "내용", "all_users", null, null, true, "1", Instant.now());
    }
}
