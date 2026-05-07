package com.dotshelf.domain.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.Instant;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.domain.PageImpl;

import com.dotshelf.domain.admin.dto.AdminDtos.AdminRoleRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.BalanceAdjustRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.BulkReportActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAdminUserNoteRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.MaintenanceRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.RefundRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.ReportActionRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.UpdateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.UserActionRequest;
import com.dotshelf.domain.admin.entity.AdminAnnouncement;
import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.entity.MaintenanceSetting;
import com.dotshelf.domain.admin.entity.SettlementAdjustment;
import com.dotshelf.domain.admin.repository.AdminAnnouncementRepository;
import com.dotshelf.domain.admin.repository.AdminAuditLogRepository;
import com.dotshelf.domain.admin.repository.AdminUserNoteRepository;
import com.dotshelf.domain.admin.repository.CreatorProfileRepository;
import com.dotshelf.domain.admin.repository.MaintenanceSettingRepository;
import com.dotshelf.domain.admin.repository.PolicyVersionRepository;
import com.dotshelf.domain.admin.repository.SettlementAccountChangeRequestRepository;
import com.dotshelf.domain.admin.repository.SettlementAdjustmentRepository;
import com.dotshelf.domain.admin.repository.SettlementPayoutBatchRepository;
import com.dotshelf.domain.admin.repository.SettlementWithdrawalRepository;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.auth.entity.AuthRefreshToken;
import com.dotshelf.domain.auth.repository.AuthRefreshTokenRepository;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.book.service.KakaoBookSearchClient;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.repository.GuestbookRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.report.entity.Report;
import com.dotshelf.domain.report.repository.ReportRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserBlock;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
@SuppressWarnings({"unchecked", "rawtypes"})
class AdminServiceTest {

    @Mock
    private UserService userService;

    @Mock
    private UserRepository userRepository;

    @Mock
    private UserSocialAccountRepository userSocialAccountRepository;

    @Mock
    private AuthRefreshTokenRepository authRefreshTokenRepository;

    @Mock
    private KakaoBookSearchClient kakaoBookSearchClient;

    @Mock
    private PageWalletRepository pageWalletRepository;

    @Mock
    private SpaceRepository spaceRepository;

    @Mock
    private BookshelfRepository bookshelfRepository;

    @Mock
    private BookshelfItemRepository bookshelfItemRepository;

    @Mock
    private UserBookRepository userBookRepository;

    @Mock
    private BookRepository bookRepository;

    @Mock
    private ReportRepository reportRepository;

    @Mock
    private ReadingRecordRepository readingRecordRepository;

    @Mock
    private ReadingRecordCommentRepository readingRecordCommentRepository;

    @Mock
    private GuestbookRepository guestbookRepository;

    @Mock
    private UserBlockRepository userBlockRepository;

    @Mock
    private AssetRepository assetRepository;

    @Mock
    private AssetPurchaseRepository assetPurchaseRepository;

    @Mock
    private SpaceVisitRepository spaceVisitRepository;

    @Mock
    private AdminAuditLogRepository adminAuditLogRepository;

    @Mock
    private AdminUserNoteRepository adminUserNoteRepository;

    @Mock
    private AdminAnnouncementRepository adminAnnouncementRepository;

    @Mock
    private MaintenanceSettingRepository maintenanceSettingRepository;

    @Mock
    private PolicyVersionRepository policyVersionRepository;

    @Mock
    private SettlementWithdrawalRepository settlementWithdrawalRepository;

    @Mock
    private SettlementPayoutBatchRepository settlementPayoutBatchRepository;

    @Mock
    private SettlementAccountChangeRequestRepository settlementAccountChangeRequestRepository;

    @Mock
    private CreatorProfileRepository creatorProfileRepository;

    @Mock
    private SettlementAdjustmentRepository settlementAdjustmentRepository;

    @InjectMocks
    private AdminService adminService;

    @Test
    @DisplayName("관리자 내 정보 조회는 ADMIN role만 허용한다")
    void getMe_requiresAdminRole() {
        User admin = adminUser();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userService.getUser(1L)).willReturn(admin);

        var response = adminService.getMe(1L);

        assertThat(response.id()).isEqualTo("1");
        assertThat(response.role()).isEqualTo("admin");
    }

    @Test
    @DisplayName("일반 사용자가 관리자 기능을 호출하면 FORBIDDEN을 던진다")
    void getMe_nonAdmin_throwsForbidden() {
        User user = TestFixtures.user(2L, "user");
        given(userService.requireUserId(2L)).willReturn(2L);
        given(userService.getUser(2L)).willReturn(user);

        assertThatThrownBy(() -> adminService.getMe(2L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("공지 생성은 생성자와 감사로그를 남긴다")
    void createAnnouncement_savesAnnouncementAndAuditLog() {
        User admin = stubAdmin();
        given(adminAnnouncementRepository.save(any(AdminAnnouncement.class))).willAnswer(invocation -> {
            AdminAnnouncement announcement = invocation.getArgument(0);
            TestFixtures.setId(announcement, 10L);
            return announcement;
        });
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.createAnnouncement(
                1L,
                new CreateAnnouncementRequest("공지", "본문", "all_users", null, null, true),
                "127.0.0.1"
        );

        assertThat(response.id()).isEqualTo("10");
        assertThat(response.createdByUserId()).isEqualTo(String.valueOf(admin.getId()));
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("ANNOUNCEMENT_CREATE");
        assertThat(auditCaptor.getValue().getActorUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("점검 모드 변경은 설정을 저장하고 감사로그를 남긴다")
    void updateMaintenance_savesSettingAndAuditLog() {
        stubAdmin();
        given(maintenanceSettingRepository.save(any(MaintenanceSetting.class))).willAnswer(invocation -> {
            MaintenanceSetting setting = invocation.getArgument(0);
            TestFixtures.setId(setting, 20L);
            return setting;
        });
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.updateMaintenance(
                1L,
                new MaintenanceRequest(true, "점검 중"),
                "127.0.0.1"
        );

        assertThat(response.id()).isEqualTo("20");
        assertThat(response.enabled()).isTrue();
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("MAINTENANCE_UPDATE");
        assertThat(auditCaptor.getValue().getReason()).isEqualTo("점검 중");
    }

    @Test
    @DisplayName("잔액 조정은 지갑을 변경하고 정산 조정 이력과 감사로그를 만든다")
    void adjustBalance_updatesWalletAndCreatesAdjustment() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        PageWallet wallet = PageWallet.builder()
                .userId(2L)
                .balancePages(100)
                .build();
        given(userService.getUser(2L)).willReturn(target);
        given(pageWalletRepository.findByUserId(2L)).willReturn(Optional.of(wallet));
        given(settlementAdjustmentRepository.save(any(SettlementAdjustment.class))).willAnswer(invocation -> {
            SettlementAdjustment adjustment = invocation.getArgument(0);
            TestFixtures.setId(adjustment, 30L);
            return adjustment;
        });

        var response = adminService.adjustBalance(
                1L,
                2L,
                new BalanceAdjustRequest(-40, "운영 조정", "차감"),
                "127.0.0.1"
        );

        assertThat(response.userId()).isEqualTo("2");
        assertThat(response.balancePages()).isEqualTo(60);
        assertThat(response.adjustment().id()).isEqualTo("30");
        then(adminAuditLogRepository).should().save(any(AdminAuditLog.class));
    }

    @Test
    @DisplayName("지원하지 않는 사용자 액션은 INVALID_INPUT을 던지고 감사로그를 쓰지 않는다")
    void actUser_unsupportedAction_throwsInvalidInput() {
        stubAdmin();
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));

        assertThatThrownBy(() -> adminService.actUser(
                1L,
                2L,
                new UserActionRequest("unknown", "reason", null, null),
                "127.0.0.1"
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("신고 RESOLVE 액션은 블라인드 플래그와 리뷰어를 반영하고 감사로그를 남긴다")
    void actReport_resolve_updatesReportAndWritesAuditLog() {
        stubAdmin();
        Report report = report(40L, "USER", 2L);
        given(reportRepository.findById(40L)).willReturn(Optional.of(report));
        given(reportRepository.findByTargetTypeAndTargetIdOrderByIdDesc("USER", 2L)).willReturn(List.of(report));
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.actReport(
                1L,
                40L,
                new ReportActionRequest(" resolve ", false, true, "처리 완료"),
                "127.0.0.1"
        );

        assertThat(response.status()).isEqualTo("RESOLVED");
        assertThat(response.isBlindedToReporter()).isFalse();
        assertThat(response.isGloballyBlinded()).isTrue();
        assertThat(response.reviewedByUserId()).isEqualTo("1");
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("REPORT_RESOLVE");
        assertThat(auditCaptor.getValue().getBeforePayload()).contains("status=PENDING");
        assertThat(auditCaptor.getValue().getAfterPayload()).contains("status=RESOLVED");
    }

    @Test
    @DisplayName("신고 상세는 신고 대상과 동일 대상 신고자 목록을 함께 반환한다")
    void getReport_guestbookTarget_mapsTargetAndReporters() {
        stubAdmin();
        Report report = report(40L, "GUESTBOOK", 20L);
        Guestbook guestbook = Guestbook.builder()
                .spaceId(10L)
                .writerUserId(2L)
                .postItInventoryId(100L)
                .content("신고 대상 방명록")
                .visibilityScope("PUBLIC")
                .anonymous(false)
                .build();
        TestFixtures.setId(guestbook, 20L);
        given(reportRepository.findById(40L)).willReturn(Optional.of(report));
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "writer")));
        given(reportRepository.findByTargetTypeAndTargetIdOrderByIdDesc("GUESTBOOK", 20L))
                .willReturn(List.of(report));
        given(userRepository.findById(3L)).willReturn(Optional.of(TestFixtures.user(3L, "reporter")));

        var response = adminService.getReport(1L, 40L);

        assertThat(response.id()).isEqualTo("40");
        assertThat(response.target().excerpt()).isEqualTo("신고 대상 방명록");
        assertThat(response.target().authorName()).isEqualTo("writer");
        assertThat(response.reporters()).singleElement()
                .satisfies(reporter -> {
                    assertThat(reporter.userId()).isEqualTo("3");
                    assertThat(reporter.nickname()).isEqualTo("reporter");
                });
    }

    @Test
    @DisplayName("존재하지 않는 신고 상세 조회는 REPORT_NOT_FOUND를 던진다")
    void getReport_missingReport_throwsNotFound() {
        stubAdmin();
        given(reportRepository.findById(40L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> adminService.getReport(1L, 40L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.RESOURCE_NOT_FOUND);
    }

    @Test
    @DisplayName("신고 START_REVIEW/GLOBAL_BLIND/UNBLIND 액션은 상태와 전역 블라인드를 변경한다")
    void actReport_reviewAndBlindActions_updateReport() {
        stubAdmin();
        Report report = report(40L, "USER", 2L);
        given(reportRepository.findById(40L)).willReturn(Optional.of(report));
        given(reportRepository.findByTargetTypeAndTargetIdOrderByIdDesc("USER", 2L)).willReturn(List.of(report));
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));

        var reviewing = adminService.actReport(
                1L,
                40L,
                new ReportActionRequest("start_review", null, null, "검토"),
                "127.0.0.1"
        );
        var blinded = adminService.actReport(
                1L,
                40L,
                new ReportActionRequest("global_blind", null, null, "블라인드"),
                "127.0.0.1"
        );
        var unblinded = adminService.actReport(
                1L,
                40L,
                new ReportActionRequest("unblind", null, null, "해제"),
                "127.0.0.1"
        );

        assertThat(reviewing.status()).isEqualTo("IN_REVIEW");
        assertThat(blinded.isGloballyBlinded()).isTrue();
        assertThat(unblinded.isGloballyBlinded()).isFalse();
        then(adminAuditLogRepository).should(org.mockito.Mockito.times(3)).save(any(AdminAuditLog.class));
    }

    @SuppressWarnings({"rawtypes", "unchecked"})
    @Test
    @DisplayName("신고 일괄 액션은 각 신고를 변경하고 감사로그를 saveAll로 한 번에 저장한다")
    void bulkActReports_rejectsReportsAndSavesAuditBatch() {
        stubAdmin();
        Report first = report(40L, "SPACE", 10L);
        Report second = report(41L, "SPACE", 10L);
        given(reportRepository.findById(40L)).willReturn(Optional.of(first));
        given(reportRepository.findById(41L)).willReturn(Optional.of(second));
        ArgumentCaptor<Iterable> auditCaptor = ArgumentCaptor.forClass(Iterable.class);

        var response = adminService.bulkActReports(
                1L,
                new BulkReportActionRequest(List.of(40L, 41L), "reject", "오신고"),
                "127.0.0.1"
        );

        assertThat(response.ok()).isTrue();
        assertThat(first.getStatus()).isEqualTo("REJECTED");
        assertThat(second.getStatus()).isEqualTo("REJECTED");
        then(adminAuditLogRepository).should().saveAll(auditCaptor.capture());
        List<?> logs = iterableToList(auditCaptor.getValue());
        assertThat(logs).hasSize(2);
        assertThat(logs)
                .allSatisfy(log -> assertThat((AdminAuditLog) log)
                        .extracting(AdminAuditLog::getAction)
                        .isEqualTo("REPORT_REJECT"));
    }

    @Test
    @DisplayName("강제 로그아웃 액션은 세션 무효화와 활성 refresh token revoke를 수행한다")
    void actUser_forceLogout_invalidatesSessionsAndRevokesRefreshTokens() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        AuthRefreshToken first = TestFixtures.refreshToken(10L, 2L, "hash-1");
        AuthRefreshToken second = TestFixtures.refreshToken(11L, 2L, "hash-2");
        given(userService.getUser(2L)).willReturn(target);
        given(authRefreshTokenRepository.findByUserIdAndRevokedAtIsNullAndExpiresAtAfter(eq(2L), any(Instant.class)))
                .willReturn(List.of(first, second));
        stubUserDetailDependencies(2L);

        var response = adminService.actUser(
                1L,
                2L,
                new UserActionRequest("force_logout", "보안 조치", null, null),
                "127.0.0.1"
        );

        assertThat(response.id()).isEqualTo("2");
        assertThat(target.getSessionInvalidatedAt()).isNotNull();
        assertThat(first.getRevokedAt()).isNotNull();
        assertThat(second.getRevokedAt()).isNotNull();
        then(adminAuditLogRepository).should().save(any(AdminAuditLog.class));
    }

    @Test
    @DisplayName("사용자 상태 조치와 닉네임 변경 성공은 대상 사용자 상태를 반영한다")
    void actUser_statusAndRenameActions_mutateTarget() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        given(userService.getUser(2L)).willReturn(target);
        given(userRepository.existsByNickname("renamed")).willReturn(false);
        stubUserDetailDependencies(2L);

        var suspended = adminService.actUser(
                1L,
                2L,
                new UserActionRequest("suspend_temp", "임시 정지", null, null),
                "127.0.0.1"
        );
        var activated = adminService.actUser(
                1L,
                2L,
                new UserActionRequest("activate", "복구", null, null),
                "127.0.0.1"
        );
        var renamed = adminService.actUser(
                1L,
                2L,
                new UserActionRequest("rename_nickname", "닉네임 정리", null, " renamed "),
                "127.0.0.1"
        );

        assertThat(suspended.state()).isEqualTo("SUSPENDED_TEMP");
        assertThat(activated.state()).isEqualTo("ACTIVE");
        assertThat(renamed.nickname()).isEqualTo("renamed");
        assertThat(target.getNickname()).isEqualTo("renamed");
        then(adminAuditLogRepository).should(org.mockito.Mockito.times(3)).save(any(AdminAuditLog.class));
    }

    @Test
    @DisplayName("ANONYMIZE 액션은 사용자 개인정보를 지우고 DELETED 상태로 응답한다")
    void actUser_anonymize_deletesPersonalFields() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        given(userService.getUser(2L)).willReturn(target);
        stubUserDetailDependencies(2L);

        var response = adminService.actUser(
                1L,
                2L,
                new UserActionRequest("anonymize", "탈퇴 처리", null, null),
                "127.0.0.1"
        );

        assertThat(response.state()).isEqualTo("DELETED");
        assertThat(target.getEmail()).isNull();
        assertThat(target.getProfileImageUrl()).isNull();
        assertThat(target.getDeletedAt()).isNotNull();
    }

    @Test
    @DisplayName("닉네임 변경 액션은 중복 닉네임이면 DUPLICATE_NICKNAME을 던지고 감사로그를 남기지 않는다")
    void actUser_renameDuplicateNickname_throwsDuplicateNickname() {
        stubAdmin();
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));
        given(userRepository.existsByNickname("taken")).willReturn(true);

        assertThatThrownBy(() -> adminService.actUser(
                1L,
                2L,
                new UserActionRequest("rename_nickname", "중복 테스트", null, " taken "),
                "127.0.0.1"
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.DUPLICATE_NICKNAME);
        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("구매 환불은 구매 상태를 REFUNDED로 바꾸고 지갑에 페이지를 되돌린다")
    void refundPurchase_success_refundsPurchaseAndChargesWallet() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        AssetPurchase purchase = TestFixtures.assetPurchase(50L, 2L, 20L, 30L, 120);
        PageWallet wallet = PageWallet.builder()
                .userId(2L)
                .balancePages(10)
                .build();
        given(userService.getUser(2L)).willReturn(target);
        given(assetPurchaseRepository.findByIdAndUserIdForUpdate(50L, 2L)).willReturn(Optional.of(purchase));
        given(pageWalletRepository.findByUserId(2L)).willReturn(Optional.of(wallet));
        given(settlementAdjustmentRepository.save(any(SettlementAdjustment.class))).willAnswer(invocation -> invocation.getArgument(0));
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.refundPurchase(
                1L,
                2L,
                new RefundRequest("50", "환불", "중복 구매"),
                "127.0.0.1"
        );

        assertThat(response.purchaseId()).isEqualTo("50");
        assertThat(response.refundedPages()).isEqualTo(120);
        assertThat(response.balancePages()).isEqualTo(130);
        assertThat(purchase.getPurchaseStatus()).isEqualTo("REFUNDED");
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("USER_REFUND");
        assertThat(auditCaptor.getValue().getBeforePayload()).contains("purchaseStatus=COMPLETED");
        assertThat(auditCaptor.getValue().getAfterPayload()).contains("balancePages=130");
    }

    @Test
    @DisplayName("이미 환불된 구매를 다시 환불하면 INVALID_INPUT을 던진다")
    void refundPurchase_alreadyRefunded_throwsInvalidInput() {
        stubAdmin();
        AssetPurchase purchase = TestFixtures.assetPurchase(50L, 2L, 20L, 30L, 120);
        purchase.refund();
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));
        given(assetPurchaseRepository.findByIdAndUserIdForUpdate(50L, 2L)).willReturn(Optional.of(purchase));

        assertThatThrownBy(() -> adminService.refundPurchase(
                1L,
                2L,
                new RefundRequest("50", "환불", "중복"),
                "127.0.0.1"
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("관리자는 본인 구매를 환불할 수 없다")
    void refundPurchase_selfRefund_throwsInvalidInput() {
        stubAdmin();

        assertThatThrownBy(() -> adminService.refundPurchase(
                1L,
                1L,
                new RefundRequest("50", "환불", null),
                "127.0.0.1"
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(assetPurchaseRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("관리자 role 변경은 ADMIN/USER만 허용하고 감사로그를 남긴다")
    void updateAdminRole_validRole_changesRoleAndWritesAudit() {
        stubAdmin();
        User target = TestFixtures.user(2L, "target");
        target.changeRole("USER");
        given(userService.getUser(2L)).willReturn(target);
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.updateAdminRole(
                1L,
                2L,
                new AdminRoleRequest(" admin ", "운영자 승격"),
                "127.0.0.1"
        );

        assertThat(response.role()).isEqualTo("ADMIN");
        assertThat(target.getRole()).isEqualTo("ADMIN");
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getBeforePayload()).isEqualTo("role=USER");
        assertThat(auditCaptor.getValue().getAfterPayload()).isEqualTo("role=ADMIN");
    }

    @Test
    @DisplayName("관리자 role 변경에서 지원하지 않는 role은 INVALID_INPUT을 던진다")
    void updateAdminRole_invalidRole_throwsInvalidInput() {
        stubAdmin();
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));

        assertThatThrownBy(() -> adminService.updateAdminRole(
                1L,
                2L,
                new AdminRoleRequest("OWNER", "잘못된 role"),
                "127.0.0.1"
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(adminAuditLogRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("공지 수정은 변경 전후 snapshot을 감사로그에 남긴다")
    void updateAnnouncement_updatesFieldsAndWritesSnapshotAudit() {
        stubAdmin();
        AdminAnnouncement announcement = TestFixtures.announcement(10L, "old");
        given(adminAnnouncementRepository.findById(10L)).willReturn(Optional.of(announcement));
        ArgumentCaptor<AdminAuditLog> auditCaptor = ArgumentCaptor.forClass(AdminAuditLog.class);

        var response = adminService.updateAnnouncement(
                1L,
                10L,
                new UpdateAnnouncementRequest("new", "새 본문", "internal", null, null, false),
                "127.0.0.1"
        );

        assertThat(response.title()).isEqualTo("new");
        assertThat(response.audience()).isEqualTo("internal");
        then(adminAuditLogRepository).should().save(auditCaptor.capture());
        assertThat(auditCaptor.getValue().getAction()).isEqualTo("ANNOUNCEMENT_UPDATE");
        assertThat(auditCaptor.getValue().getBeforePayload()).contains("\"title\":\"old\"");
        assertThat(auditCaptor.getValue().getAfterPayload()).contains("\"title\":\"new\"");
    }

    @Test
    @DisplayName("공지 삭제는 repository delete와 감사로그를 수행한다")
    void deleteAnnouncement_deletesAnnouncementAndWritesAudit() {
        stubAdmin();
        AdminAnnouncement announcement = TestFixtures.announcement(10L, "delete");
        given(adminAnnouncementRepository.findById(10L)).willReturn(Optional.of(announcement));

        adminService.deleteAnnouncement(1L, 10L, "127.0.0.1");

        then(adminAnnouncementRepository).should().delete(announcement);
        then(adminAuditLogRepository).should().save(any(AdminAuditLog.class));
    }

    @Test
    @DisplayName("관리자 유저 메모 생성은 대상 사용자 검증 후 작성자 이름과 함께 응답한다")
    void createUserNote_savesNoteAndReturnsAuthorName() {
        stubAdmin();
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));
        given(adminUserNoteRepository.save(any())).willAnswer(invocation -> {
            var note = invocation.<com.dotshelf.domain.admin.entity.AdminUserNote>getArgument(0);
            TestFixtures.setId(note, 70L);
            return note;
        });

        var response = adminService.createUserNote(
                1L,
                2L,
                new CreateAdminUserNoteRequest("운영 메모"),
                "127.0.0.1"
        );

        assertThat(response.id()).isEqualTo("70");
        assertThat(response.authorName()).isEqualTo("admin");
        assertThat(response.body()).isEqualTo("운영 메모");
        then(adminAuditLogRepository).should().save(any(AdminAuditLog.class));
    }

    @Test
    @DisplayName("정산 크리에이터 목록은 구매 요약이 없는 사용자를 0으로 채운다")
    void getSettlementCreators_fillsMissingPurchaseSummaryWithZero() {
        stubAdmin();
        var first = TestFixtures.creatorProfile(10L, 2L);
        var second = TestFixtures.creatorProfile(11L, 3L);
        given(creatorProfileRepository.findAllByOrderByIdDesc(any()))
                .willReturn(new PageImpl<>(List.of(first, second)));
        given(assetPurchaseRepository.summarizeByUserIds(List.of(2L, 3L)))
                .willReturn(List.of(purchaseSummaryRow(2L, 3L, 450L)));

        var response = adminService.getSettlementCreators(1L, 20);

        assertThat(response.items()).hasSize(2);
        assertThat(response.items().get(0).assetPurchaseCount()).isEqualTo(3L);
        assertThat(response.items().get(0).earnedPages()).isEqualTo(450L);
        assertThat(response.items().get(1).assetPurchaseCount()).isZero();
        assertThat(response.items().get(1).earnedPages()).isZero();
    }

    @Test
    @DisplayName("시스템 헬스 조회는 DB 예외와 점검 모드, 신고 backlog를 반영한다")
    void getSystemHealth_reflectsDbFailureAndMaintenanceMode() {
        stubAdmin();
        given(userRepository.count()).willThrow(new RuntimeException("db down"));
        given(maintenanceSettingRepository.findFirstByOrderByIdDesc())
                .willReturn(Optional.of(TestFixtures.maintenanceSetting(1L, true)));
        given(reportRepository.countByStatus("PENDING")).willReturn(7L);

        var response = adminService.getSystemHealth(1L);

        assertThat(response.api()).isEqualTo("UP");
        assertThat(response.db()).isEqualTo("DOWN");
        assertThat(response.moderationBacklog()).isEqualTo(7L);
        assertThat(response.maintenanceMode()).isTrue();
    }

    @Test
    @DisplayName("대시보드 요약은 콘텐츠/신고/정산/운영 지표를 repository 집계로 구성한다")
    void getDashboardSummary_aggregatesRepositoryCounts() {
        stubAdmin();
        given(bookRepository.count()).willReturn(10L);
        given(assetRepository.count()).willReturn(20L);
        given(reportRepository.countByStatus("PENDING")).willReturn(3L);
        given(pageWalletRepository.sumBalancePages()).willReturn(1_000L);
        given(assetPurchaseRepository.sumPricePages()).willReturn(700L);
        given(settlementWithdrawalRepository.sumRequestedPagesByStatus("REQUESTED")).willReturn(200L);
        given(userRepository.count()).willReturn(5L);
        given(spaceRepository.countByDeletedAtIsNull()).willReturn(4L);
        given(spaceVisitRepository.countByVisitedDate(any(LocalDate.class))).willReturn(9L);

        var response = adminService.getDashboardSummary(1L);

        assertThat(response.content().books()).isEqualTo(10L);
        assertThat(response.content().assets()).isEqualTo(20L);
        assertThat(response.moderation().open()).isEqualTo(3L);
        assertThat(response.settlement().walletBalancePages()).isEqualTo(1_000L);
        assertThat(response.settlement().pendingPayoutPages()).isEqualTo(200L);
        assertThat(response.ops().users()).isEqualTo(5L);
        assertThat(response.ops().spaces()).isEqualTo(4L);
        assertThat(response.ops().todayVisits()).isEqualTo(9L);
    }

    @Test
    @DisplayName("통계 KPI는 최근 활성/가입/책추가/신고/GMV/방문 수를 집계한다")
    void getStatsKpi_aggregatesOperationalMetrics() {
        stubAdmin();
        given(userRepository.countByLastLoginAtGreaterThanEqual(any())).willReturn(1L, 7L, 30L);
        given(userRepository.countByCreatedAtGreaterThanEqual(any())).willReturn(4L);
        given(userBookRepository.count()).willReturn(12L);
        given(reportRepository.countByStatus("PENDING")).willReturn(2L);
        given(assetPurchaseRepository.sumPricePages()).willReturn(900L);
        given(assetPurchaseRepository.count()).willReturn(6L);
        given(spaceVisitRepository.count()).willReturn(33L);

        var response = adminService.getStatsKpi(1L);

        assertThat(response.dau()).isEqualTo(1L);
        assertThat(response.wau()).isEqualTo(7L);
        assertThat(response.mau()).isEqualTo(30L);
        assertThat(response.newSignups()).isEqualTo(4L);
        assertThat(response.booksAdded()).isEqualTo(12L);
        assertThat(response.openModerationQueue()).isEqualTo(2L);
        assertThat(response.pageGmvPages()).isEqualTo(900L);
        assertThat(response.assetPurchaseCount()).isEqualTo(6L);
        assertThat(response.spaceVisits()).isEqualTo(33L);
    }

    @Test
    @DisplayName("일별 통계는 지정 기간의 날짜별 지표와 카테고리 매출을 반환한다")
    void getStatsTimeseries_returnsDailyMetricsAndCategoryRevenue() {
        stubAdmin();
        given(userRepository.countByCreatedAtBetween(any(), any())).willReturn(1L, 2L);
        given(userBookRepository.countByCreatedAtBetween(any(), any())).willReturn(3L, 4L);
        given(assetPurchaseRepository.sumPricePagesByPurchasedAtBetween(any(), any())).willReturn(100L, 200L);
        given(reportRepository.countByStatus("PENDING")).willReturn(5L, 6L);
        given(spaceVisitRepository.countByVisitedDate(any(LocalDate.class))).willReturn(7L, 8L);
        given(assetPurchaseRepository.sumPricePagesByCategory())
                .willReturn(List.of(categoryRevenueRow("THEME", 300L), categoryRevenueRow("POSTIT", 100L)));

        var response = adminService.getStatsTimeseries(
                1L,
                LocalDate.of(2026, 5, 6),
                LocalDate.of(2026, 5, 7),
                "DAY"
        );

        assertThat(response.daily()).hasSize(2);
        assertThat(response.daily().get(0).date()).isEqualTo(LocalDate.of(2026, 5, 6));
        assertThat(response.daily().get(0).newSignups()).isEqualTo(1L);
        assertThat(response.daily().get(1).assetPurchasePages()).isEqualTo(200L);
        assertThat(response.categoryRevenue()).extracting(item -> item.category()).containsExactly("THEME", "POSTIT");
    }

    @Test
    @DisplayName("관리자 감사 로그 조회는 actor email을 함께 매핑한다")
    void getAuditLogs_mapsActorEmail() {
        stubAdmin();
        AdminAuditLog log = TestFixtures.auditLog(80L, 1L, "USER_FORCE_LOGOUT");
        given(adminAuditLogRepository.findAll(
                any(org.springframework.data.jpa.domain.Specification.class),
                any(org.springframework.data.domain.Pageable.class)
        ))
                .willReturn(new PageImpl<>(List.of(log)));
        given(userRepository.findById(1L)).willReturn(Optional.of(adminUser()));

        var response = adminService.getAuditLogs(1L, 1L, "USER_FORCE_LOGOUT", "USER", null, null, null, 20);

        assertThat(response.items()).hasSize(1);
        assertThat(response.items().getFirst().id()).isEqualTo("80");
        assertThat(response.items().getFirst().actorEmail()).isEqualTo("admin@example.com");
        assertThat(response.items().getFirst().action()).isEqualTo("USER_FORCE_LOGOUT");
    }

    @Test
    @DisplayName("관리자 전역 검색은 요청한 SPACE/BOOK 타입만 검색하고 limit으로 잘라낸다")
    void search_spaceAndBookTypes_returnsTypedItems() {
        stubAdmin();
        Space space = TestFixtures.space(10L, 2L);
        Book book = TestFixtures.book(20L, "데미안");
        given(spaceRepository.findBySpaceNameContainingIgnoreCase(eq("de"), any()))
                .willReturn(new PageImpl<>(List.of(space)));
        given(bookRepository.findByTitleContainingIgnoreCaseOrAuthorContainingIgnoreCase(eq("de"), eq("de"), any()))
                .willReturn(new PageImpl<>(List.of(book)));

        var response = adminService.search(1L, " de ", "SPACE,BOOK", 10);

        assertThat(response.items()).hasSize(2);
        assertThat(response.items()).extracting(item -> item.type()).containsExactly("SPACE", "BOOK");
        assertThat(response.items().get(0).href()).isEqualTo("/users?space=10");
        assertThat(response.items().get(1).href()).isEqualTo("/search?book=20");
    }

    @Test
    @DisplayName("관리자 전역 검색 types가 비어 있으면 모든 타입을 검색하고 limit으로 자른다")
    void search_defaultTypes_searchesAllAndAppliesLimit() {
        stubAdmin();
        User user = TestFixtures.user(2L, "kim");
        Report report = report(40L, "USER", 2L);
        Space space = TestFixtures.space(10L, 2L);
        Book book = TestFixtures.book(20L, "데미안");
        given(userRepository.findAll(any(org.springframework.data.jpa.domain.Specification.class), any(org.springframework.data.domain.Pageable.class)))
                .willReturn(new PageImpl<>(List.of(user)));
        given(reportRepository.findAll(any(org.springframework.data.jpa.domain.Specification.class), any(org.springframework.data.domain.Pageable.class)))
                .willReturn(new PageImpl<>(List.of(report)));
        given(spaceRepository.findAll(any(org.springframework.data.domain.Pageable.class)))
                .willReturn(new PageImpl<>(List.of(space)));
        given(bookRepository.findAll(any(org.springframework.data.domain.Pageable.class)))
                .willReturn(new PageImpl<>(List.of(book)));

        var response = adminService.search(1L, " ", null, 3);

        assertThat(response.items()).hasSize(3);
        assertThat(response.nextCursor()).isEqualTo("3");
        assertThat(response.items()).extracting(item -> item.type()).containsExactly("USER", "REPORT", "SPACE");
        assertThat(response.totalCount()).isEqualTo(4);
    }

    @Test
    @DisplayName("사용자 공간 목록은 활성 책장 수와 활성 아이템 수를 함께 계산한다")
    void getUserSpaces_countsActiveBookshelvesAndItems() {
        stubAdmin();
        Space space = TestFixtures.space(10L, 2L);
        Bookshelf first = TestFixtures.bookshelf(100L, 10L, 1);
        Bookshelf second = TestFixtures.bookshelf(101L, 10L, 2);
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));
        given(spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(eq(2L), any()))
                .willReturn(new PageImpl<>(List.of(space)));
        given(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(10L, "ACTIVE"))
                .willReturn(List.of(first, second));
        given(bookshelfItemRepository.countByBookshelfIdAndStatus(100L, "ACTIVE")).willReturn(3L);
        given(bookshelfItemRepository.countByBookshelfIdAndStatus(101L, "ACTIVE")).willReturn(4L);

        var response = adminService.getUserSpaces(1L, 2L, 20);

        assertThat(response.items()).hasSize(1);
        assertThat(response.items().getFirst().spaceId()).isEqualTo("10");
        assertThat(response.items().getFirst().bookshelves()).isEqualTo(2L);
        assertThat(response.items().getFirst().books()).isEqualTo(7L);
    }

    @Test
    @DisplayName("사용자 히스토리는 신고와 차단 이력을 합쳐 반환한다")
    void getUserHistory_combinesReportsAndBlocks() {
        stubAdmin();
        Report report = report(40L, "USER", 2L);
        UserBlock block = TestFixtures.userBlock(2L, 3L);
        TestFixtures.setId(block, 50L);
        given(userService.getUser(2L)).willReturn(TestFixtures.user(2L, "target"));
        given(reportRepository.findByReporterUserIdOrReportedUserIdOrderByIdDesc(eq(2L), eq(2L), any()))
                .willReturn(List.of(report));
        given(userBlockRepository.findByBlockerUserIdOrBlockedUserIdOrderByIdDesc(eq(2L), eq(2L), any()))
                .willReturn(List.of(block));

        var response = adminService.getUserHistory(1L, 2L, 20);

        assertThat(response.items()).hasSize(2);
        assertThat(response.items()).extracting(item -> item.type()).containsExactly("REPORT", "BLOCK");
    }

    private User stubAdmin() {
        User admin = adminUser();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userService.getUser(1L)).willReturn(admin);
        return admin;
    }

    private User adminUser() {
        User admin = TestFixtures.user(1L, "admin");
        admin.changeRole("ADMIN");
        return admin;
    }

    private Report report(Long id, String targetType, Long targetId) {
        Report report = TestFixtures.report(3L, targetType, targetId);
        TestFixtures.setId(report, id);
        return report;
    }

    private List<?> iterableToList(Iterable<?> iterable) {
        List<Object> items = new ArrayList<>();
        iterable.forEach(items::add);
        return items;
    }

    private void stubUserDetailDependencies(Long targetUserId) {
        given(userSocialAccountRepository.findByUserId(targetUserId)).willReturn(List.of());
        given(pageWalletRepository.findByUserId(targetUserId)).willReturn(Optional.empty());
        given(spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(targetUserId)).willReturn(List.of());
        given(userBookRepository.countByUserIdAndStorageStatusNot(targetUserId, "DELETED")).willReturn(0L);
        given(reportRepository.countByReportedUserIdOrTargetTypeIgnoreCaseAndTargetId(targetUserId, "USER", targetUserId))
                .willReturn(0L);
        given(adminUserNoteRepository.findByUserIdOrderByIdDesc(targetUserId)).willReturn(List.of());
    }

    private AssetPurchaseRepository.PurchaseSummaryRow purchaseSummaryRow(Long userId, long count, long pages) {
        return new AssetPurchaseRepository.PurchaseSummaryRow() {
            @Override
            public Long getUserId() {
                return userId;
            }

            @Override
            public long getCount() {
                return count;
            }

            @Override
            public long getPages() {
                return pages;
            }
        };
    }

    private AssetPurchaseRepository.CategoryRevenueRow categoryRevenueRow(String category, long pages) {
        return new AssetPurchaseRepository.CategoryRevenueRow() {
            @Override
            public String getCategory() {
                return category;
            }

            @Override
            public long getPages() {
                return pages;
            }
        };
    }
}
