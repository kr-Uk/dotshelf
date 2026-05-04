package com.dotshelf.domain.admin.service;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.data.jpa.domain.Specification;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

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
import com.dotshelf.domain.admin.dto.AdminDtos.CategoryRevenueResponse;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAdminUserNoteRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.CreateAnnouncementRequest;
import com.dotshelf.domain.admin.dto.AdminDtos.DailyStatsResponse;
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
import com.dotshelf.domain.admin.dto.AdminDtos.SettlementAdjustmentResponse;
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
import com.dotshelf.domain.admin.entity.AdminAnnouncement;
import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.entity.AdminUserNote;
import com.dotshelf.domain.admin.entity.CreatorProfile;
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
import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.UserSocialAccount;
import com.dotshelf.domain.auth.repository.AuthRefreshTokenRepository;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
import com.dotshelf.domain.book.entity.UserBook;
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
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.report.entity.Report;
import com.dotshelf.domain.report.repository.ReportRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import jakarta.persistence.criteria.Predicate;
import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminService {

    private static final int DEFAULT_LIMIT = 20;
    private static final int MAX_LIMIT = 100;
    private static final String DELETED = "DELETED";
    private static final String ROLE_ADMIN = "ADMIN";
    private static final String ROLE_USER = "USER";
    private static final String STATUS_PENDING = "PENDING";
    private static final Duration EXTERNAL_BOOK_API_HEALTH_TTL = Duration.ofMinutes(5);

    private final UserService userService;
    private final UserRepository userRepository;
    private final UserSocialAccountRepository userSocialAccountRepository;
    private final AuthRefreshTokenRepository authRefreshTokenRepository;
    private final KakaoBookSearchClient kakaoBookSearchClient;
    private final PageWalletRepository pageWalletRepository;
    private final SpaceRepository spaceRepository;
    private final BookshelfRepository bookshelfRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final UserBookRepository userBookRepository;
    private final BookRepository bookRepository;
    private final ReportRepository reportRepository;
    private final ReadingRecordRepository readingRecordRepository;
    private final ReadingRecordCommentRepository readingRecordCommentRepository;
    private final GuestbookRepository guestbookRepository;
    private final UserBlockRepository userBlockRepository;
    private final AssetRepository assetRepository;
    private final AssetPurchaseRepository assetPurchaseRepository;
    private final SpaceVisitRepository spaceVisitRepository;
    private final AdminAuditLogRepository adminAuditLogRepository;
    private final AdminUserNoteRepository adminUserNoteRepository;
    private final AdminAnnouncementRepository adminAnnouncementRepository;
    private final MaintenanceSettingRepository maintenanceSettingRepository;
    private final PolicyVersionRepository policyVersionRepository;
    private final SettlementWithdrawalRepository settlementWithdrawalRepository;
    private final SettlementPayoutBatchRepository settlementPayoutBatchRepository;
    private final SettlementAccountChangeRequestRepository settlementAccountChangeRequestRepository;
    private final CreatorProfileRepository creatorProfileRepository;
    private final SettlementAdjustmentRepository settlementAdjustmentRepository;

    private volatile String cachedExternalBookApiStatus = "UNKNOWN";
    private volatile Instant externalBookApiStatusCheckedAt;

    public AdminMeResponse getMe(Long userId) {
        return AdminMeResponse.from(requireAdminUser(userId));
    }

    public DashboardSummaryResponse getDashboardSummary(Long userId) {
        requireAdminUser(userId);
        long walletBalancePages = walletBalancePages();
        long assetPurchasePages = assetPurchasePages();
        long pendingPayoutPages = pendingPayoutPages();
        return new DashboardSummaryResponse(
                new DashboardContentSummary(bookRepository.count(), assetRepository.count(), 0),
                new DashboardModerationSummary(reportRepository.countByStatus(STATUS_PENDING), 0, 0),
                new DashboardSettlementSummary(pendingPayoutPages, walletBalancePages, walletBalancePages >= 0),
                new DashboardOpsSummary(userRepository.count(), spaceRepository.countByDeletedAtIsNull(),
                        spaceVisitRepository.countByVisitedDate(LocalDate.now()), "UP")
        );
    }

    public PageResponse<ReportListItemResponse> getReports(Long userId, String status, String targetType,
                                                           String q, String cursor, int limit) {
        requireAdminUser(userId);
        return page(reportRepository.findAll(reportSpec(status, targetType, q), pageRequest(cursor, limit)),
                this::toReportListItem);
    }

    public ReportDetailResponse getReport(Long userId, Long reportId) {
        requireAdminUser(userId);
        Report report = getReportEntity(reportId);
        ReportTargetResponse target = resolveTarget(report.getTargetType(), report.getTargetId());
        List<ReportReporterResponse> reporters = reportRepository
                .findByTargetTypeAndTargetIdOrderByIdDesc(report.getTargetType(), report.getTargetId())
                .stream()
                .map(this::toReportReporter)
                .toList();
        return new ReportDetailResponse(String.valueOf(report.getId()), target, reporters, report.getStatus());
    }

    @Transactional
    public ReportListItemResponse actReport(Long userId, Long reportId, ReportActionRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        Report report = getReportEntity(reportId);
        String before = reportState(report);
        applyReportAction(report, admin.getId(), request.action(), request.blindToReporter(), request.globallyBlind());
        writeAudit(admin, "REPORT_" + normalizeAction(request.action()), "REPORT", report.getId(),
                before, reportState(report), request.reason(), ipAddress);
        return toReportListItem(report);
    }

    @Transactional
    public OkResponse bulkActReports(Long userId, BulkReportActionRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        List<AdminAuditLog> auditLogs = new ArrayList<>();
        for (Long reportId : request.ids()) {
            Report report = getReportEntity(reportId);
            String before = reportState(report);
            applyReportAction(report, admin.getId(), request.action(), null, null);
            auditLogs.add(buildAudit(admin, "REPORT_" + normalizeAction(request.action()), "REPORT", report.getId(),
                    before, reportState(report), request.reason(), ipAddress));
        }
        adminAuditLogRepository.saveAll(auditLogs);
        return OkResponse.success();
    }

    public PageResponse<UserListItemResponse> getUsers(Long userId, String q, String status,
                                                       String provider, String cursor, int limit) {
        requireAdminUser(userId);
        Page<User> users = userRepository.findAll(userSpec(q, status, provider), pageRequest(cursor, limit));
        Map<Long, List<UserSocialAccount>> socialAccounts = socialAccountsByUserId(users.getContent().stream()
                .map(User::getId)
                .toList());
        return page(users, user -> toUserListItem(user, socialAccounts.get(user.getId())));
    }

    public AdminUserDetailResponse getUser(Long userId, Long targetUserId) {
        requireAdminUser(userId);
        User user = userService.getUser(targetUserId);
        List<UserSocialAccount> socialAccounts = userSocialAccountRepository.findByUserId(targetUserId);
        return new AdminUserDetailResponse(
                String.valueOf(user.getId()),
                user.getNickname(),
                user.getEmail(),
                user.getRole(),
                user.getStatus(),
                user.isOnboarded(),
                user.getProfileImageUrl(),
                user.getIntroduction(),
                user.getCreatedAt(),
                user.getLastLoginAt(),
                socialAccounts.stream().map(account -> account.getProvider().name()).toList(),
                walletBalance(targetUserId),
                spacesOf(targetUserId).size(),
                bookshelfCount(targetUserId),
                userBookRepository.countByUserIdAndStorageStatusNot(targetUserId, DELETED),
                reportsAgainst(targetUserId),
                adminUserNoteRepository.findByUserIdOrderByIdDesc(targetUserId).size()
        );
    }

    @Transactional
    public AdminUserDetailResponse actUser(Long userId, Long targetUserId, UserActionRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        User target = userService.getUser(targetUserId);
        String before = userState(target);
        String action = normalizeAction(request.action());
        if ("ACTIVATE".equals(action)) {
            target.changeStatus("ACTIVE");
        } else if ("SUSPEND_TEMP".equals(action)) {
            target.changeStatus("SUSPENDED_TEMP");
        } else if ("SUSPEND_PERM".equals(action)) {
            target.changeStatus("SUSPENDED_PERM");
        } else if ("ANONYMIZE".equals(action)) {
            target.anonymize();
        } else if ("FORCE_LOGOUT".equals(action) || "REQUIRE_RELOGIN".equals(action)) {
            target.invalidateSessions();
            revokeActiveRefreshTokens(target.getId());
        } else if ("RENAME_NICKNAME".equals(action)) {
            String nickname = normalizeNickname(request.nickname());
            if (!target.getNickname().equals(nickname) && userRepository.existsByNickname(nickname)) {
                throw new CustomException(ErrorCode.DUPLICATE_NICKNAME);
            }
            target.updateProfile(nickname, target.getProfileImageUrl(), target.getIntroduction());
        } else {
            throw new CustomException(ErrorCode.INVALID_INPUT, "지원하지 않는 사용자 액션입니다.");
        }
        writeAudit(admin, "USER_" + action, "USER", target.getId(), before, userState(target), request.reason(), ipAddress);
        return getUser(admin.getId(), targetUserId);
    }

    public PageResponse<UserSpaceResponse> getUserSpaces(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        return page(spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(targetUserId, firstPageRequest(limit)),
                this::toUserSpaceResponse);
    }

    public PageResponse<UserPurchaseResponse> getUserPurchases(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        return page(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDesc(targetUserId, firstPageRequest(limit)),
                UserPurchaseResponse::from);
    }

    public PageResponse<UserHistoryResponse> getUserHistory(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        List<UserHistoryResponse> history = new ArrayList<>();
        reportRepository.findByReporterUserIdOrReportedUserIdOrderByIdDesc(targetUserId, targetUserId, firstPageRequest(limit))
                .forEach(report -> history.add(new UserHistoryResponse(
                        String.valueOf(report.getId()),
                        "REPORT",
                        report.getTargetType(),
                        report.getReasonType(),
                        report.getCreatedAt()
                )));
        userBlockRepository.findByBlockerUserIdOrBlockedUserIdOrderByIdDesc(targetUserId, targetUserId, firstPageRequest(limit))
                .forEach(block -> history.add(new UserHistoryResponse(
                        String.valueOf(block.getId()),
                        "BLOCK",
                        String.valueOf(block.getBlockedUserId()),
                        "blocked by " + block.getBlockerUserId(),
                        block.getCreatedAt()
                )));
        history.sort(Comparator.comparing(UserHistoryResponse::occurredAt, Comparator.nullsLast(Comparator.naturalOrder())).reversed());
        return page(history, limit);
    }

    public PageResponse<AdminUserNoteResponse> getUserNotes(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        Page<AdminUserNote> notes = adminUserNoteRepository.findByUserIdOrderByIdDesc(targetUserId, firstPageRequest(limit));
        Map<Long, String> authorNames = userRepository.findAllById(notes.getContent().stream()
                        .map(AdminUserNote::getAuthorUserId)
                        .collect(Collectors.toSet()))
                .stream()
                .collect(Collectors.toMap(User::getId, User::getNickname));
        return page(notes, note -> AdminUserNoteResponse.from(note, authorNames.get(note.getAuthorUserId())));
    }

    @Transactional
    public AdminUserNoteResponse createUserNote(Long userId, Long targetUserId, CreateAdminUserNoteRequest request,
                                                String ipAddress) {
        User admin = requireAdminUser(userId);
        userService.getUser(targetUserId);
        AdminUserNote note = adminUserNoteRepository.save(AdminUserNote.builder()
                .userId(targetUserId)
                .authorUserId(admin.getId())
                .body(request.body())
                .build());
        writeAudit(admin, "USER_NOTE_CREATE", "USER", targetUserId, null, request.body(), null, ipAddress);
        return AdminUserNoteResponse.from(note, admin.getNickname());
    }

    @Transactional
    public BalanceAdjustResponse adjustBalance(Long userId, Long targetUserId, BalanceAdjustRequest request,
                                               String ipAddress) {
        User admin = requireAdminUser(userId);
        userService.getUser(targetUserId);
        PageWallet wallet = pageWalletRepository.findByUserId(targetUserId)
                .orElseGet(() -> pageWalletRepository.save(PageWallet.builder()
                        .userId(targetUserId)
                        .balancePages(0)
                        .build()));
        String before = "balancePages=" + wallet.getBalancePages();
        if (request.deltaPages() >= 0) {
            wallet.charge(request.deltaPages());
        } else {
            wallet.use(Math.abs(request.deltaPages()));
        }
        SettlementAdjustment adjustment = settlementAdjustmentRepository.save(SettlementAdjustment.builder()
                .userId(targetUserId)
                .deltaPages(request.deltaPages())
                .reason(request.reason())
                .memo(request.memo())
                .adjustedByUserId(admin.getId())
                .build());
        writeAudit(admin, "BALANCE_ADJUST", "USER", targetUserId,
                before, "balancePages=" + wallet.getBalancePages(), request.memo(), ipAddress);
        return new BalanceAdjustResponse(
                String.valueOf(targetUserId),
                wallet.getBalancePages(),
                SettlementAdjustmentResponse.from(adjustment)
        );
    }

    @Transactional
    public RefundResponse refundPurchase(Long userId, Long targetUserId, RefundRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        if (admin.getId().equals(targetUserId)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "관리자는 본인 구매를 환불할 수 없습니다.");
        }
        userService.getUser(targetUserId);
        Long purchaseId = parseLongId(request.purchaseId(), "purchaseId가 올바르지 않습니다.");
        AssetPurchase purchase = assetPurchaseRepository.findByIdAndUserIdForUpdate(purchaseId, targetUserId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        if ("REFUNDED".equalsIgnoreCase(purchase.getPurchaseStatus())) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "이미 환불된 구매입니다.");
        }
        PageWallet wallet = pageWalletRepository.findByUserId(targetUserId)
                .orElseGet(() -> pageWalletRepository.save(PageWallet.builder()
                        .userId(targetUserId)
                        .balancePages(0)
                        .build()));
        String before = "purchaseStatus=" + purchase.getPurchaseStatus()
                + ", balancePages=" + wallet.getBalancePages();
        purchase.refund();
        wallet.charge(purchase.getPricePages());
        settlementAdjustmentRepository.save(SettlementAdjustment.builder()
                .userId(targetUserId)
                .deltaPages(purchase.getPricePages())
                .reason(request.reason())
                .memo(request.comment())
                .adjustedByUserId(admin.getId())
                .build());
        writeAudit(admin, "USER_REFUND", "ASSET_PURCHASE", purchase.getId(),
                before,
                "purchaseStatus=" + purchase.getPurchaseStatus() + ", balancePages=" + wallet.getBalancePages(),
                request.comment(),
                ipAddress);
        return new RefundResponse(
                String.valueOf(targetUserId),
                String.valueOf(purchase.getId()),
                purchase.getPricePages(),
                wallet.getBalancePages()
        );
    }

    public SettlementReconciliationResponse getSettlementReconciliation(Long userId) {
        requireAdminUser(userId);
        long walletBalancePages = walletBalancePages();
        long assetPurchasePages = assetPurchasePages();
        long pendingPayoutPages = pendingPayoutPages();
        return new SettlementReconciliationResponse(
                walletBalancePages,
                assetPurchasePages,
                pendingPayoutPages,
                walletBalancePages >= 0,
                Instant.now()
        );
    }

    public PageResponse<SettlementWithdrawalResponse> getSettlementWithdrawals(Long userId, int limit) {
        requireAdminUser(userId);
        return page(settlementWithdrawalRepository.findAllByOrderByIdDesc(firstPageRequest(limit)),
                SettlementWithdrawalResponse::from);
    }

    public PageResponse<SettlementPayoutBatchResponse> getSettlementPayoutBatches(Long userId, int limit) {
        requireAdminUser(userId);
        return page(settlementPayoutBatchRepository.findAllByOrderByIdDesc(firstPageRequest(limit)),
                SettlementPayoutBatchResponse::from);
    }

    public PageResponse<SettlementAccountChangeRequestResponse> getSettlementAccountChanges(Long userId, int limit) {
        requireAdminUser(userId);
        return page(settlementAccountChangeRequestRepository.findAllByOrderByIdDesc(firstPageRequest(limit)),
                SettlementAccountChangeRequestResponse::from);
    }

    public PageResponse<SettlementCreatorResponse> getSettlementCreators(Long userId, int limit) {
        requireAdminUser(userId);
        Page<CreatorProfile> creators = creatorProfileRepository.findAllByOrderByIdDesc(firstPageRequest(limit));
        Map<Long, PurchaseSummary> purchaseSummary = purchaseSummaryByUserId(creators.getContent().stream()
                .map(CreatorProfile::getUserId)
                .toList());
        return page(creators, creator -> {
            PurchaseSummary summary = purchaseSummary.getOrDefault(creator.getUserId(), PurchaseSummary.empty());
            return SettlementCreatorResponse.from(creator, summary.count(), summary.pages());
        });
    }

    public StatsKpiResponse getStatsKpi(Long userId) {
        requireAdminUser(userId);
        Instant now = Instant.now();
        return new StatsKpiResponse(
                activeUsersSince(now.minus(1, ChronoUnit.DAYS)),
                activeUsersSince(now.minus(7, ChronoUnit.DAYS)),
                activeUsersSince(now.minus(30, ChronoUnit.DAYS)),
                usersCreatedSince(now.minus(1, ChronoUnit.DAYS)),
                0,
                userBookRepository.count(),
                reportRepository.countByStatus(STATUS_PENDING),
                assetPurchasePages(),
                assetPurchaseRepository.count(),
                spaceVisitRepository.count(),
                0
        );
    }

    public StatsTimeseriesResponse getStatsTimeseries(Long userId, LocalDate from, LocalDate to, String granularity) {
        requireAdminUser(userId);
        LocalDate end = to == null ? LocalDate.now() : to;
        LocalDate start = from == null ? end.minusDays(6) : from;
        List<DailyStatsResponse> daily = new ArrayList<>();
        for (LocalDate date = start; !date.isAfter(end); date = date.plusDays(1)) {
            LocalDate current = date;
            daily.add(new DailyStatsResponse(
                    current,
                usersCreatedOn(current),
                userBooksCreatedOn(current),
                assetPurchasePagesOn(current),
                reportRepository.countByStatus(STATUS_PENDING),
                spaceVisitRepository.countByVisitedDate(current)
            ));
        }
        return new StatsTimeseriesResponse(daily, categoryRevenue(), List.of());
    }

    public SystemHealthResponse getSystemHealth(Long userId) {
        requireAdminUser(userId);
        String dbStatus = "UP";
        try {
            userRepository.count();
        } catch (RuntimeException exception) {
            dbStatus = "DOWN";
        }
        boolean maintenanceMode = maintenanceSettingRepository.findFirstByOrderByIdDesc()
                .map(MaintenanceSetting::isEnabled)
                .orElse(false);
        return new SystemHealthResponse(
                "UP",
                dbStatus,
                "UNKNOWN",
                paymentStatus(),
                externalBookApiStatus(),
                reportRepository.countByStatus(STATUS_PENDING),
                maintenanceMode
        );
    }

    public PageResponse<AdminMemberResponse> getAdmins(Long userId, int limit) {
        requireAdminUser(userId);
        return page(userRepository.findByRoleIgnoreCase(ROLE_ADMIN, firstPageRequest(limit)),
                AdminMemberResponse::from);
    }

    @Transactional
    public AdminMemberResponse updateAdminRole(Long userId, Long targetUserId, AdminRoleRequest request,
                                               String ipAddress) {
        User admin = requireAdminUser(userId);
        User target = userService.getUser(targetUserId);
        String role = request.role().trim().toUpperCase(Locale.ROOT);
        if (!ROLE_ADMIN.equals(role) && !ROLE_USER.equals(role)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "role은 ADMIN 또는 USER만 가능합니다.");
        }
        String before = "role=" + target.getRole();
        target.changeRole(role);
        writeAudit(admin, "ADMIN_ROLE_UPDATE", "USER", targetUserId, before, "role=" + role,
                request.reason(), ipAddress);
        return AdminMemberResponse.from(target);
    }

    public PageResponse<AnnouncementResponse> getAnnouncements(Long userId, int limit) {
        requireAdminUser(userId);
        return page(adminAnnouncementRepository.findAllByOrderByIdDesc(firstPageRequest(limit)),
                AnnouncementResponse::from);
    }

    @Transactional
    public AnnouncementResponse createAnnouncement(Long userId, CreateAnnouncementRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        AdminAnnouncement announcement = adminAnnouncementRepository.save(AdminAnnouncement.builder()
                .title(request.title())
                .body(request.body())
                .audience(request.audience())
                .startsAt(request.startsAt())
                .endsAt(request.endsAt())
                .isUrgent(request.urgent())
                .createdByUserId(admin.getId())
                .build());
        writeAudit(admin, "ANNOUNCEMENT_CREATE", "ANNOUNCEMENT", announcement.getId(), null,
                announcementSnapshot(announcement), null, ipAddress);
        return AnnouncementResponse.from(announcement);
    }

    @Transactional
    public AnnouncementResponse updateAnnouncement(Long userId, Long announcementId,
                                                   UpdateAnnouncementRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        AdminAnnouncement announcement = getAnnouncement(announcementId);
        String before = announcementSnapshot(announcement);
        announcement.update(
                request.title(),
                request.body(),
                request.audience(),
                request.startsAt(),
                request.endsAt(),
                request.urgent()
        );
        String after = announcementSnapshot(announcement);
        writeAudit(admin, "ANNOUNCEMENT_UPDATE", "ANNOUNCEMENT", announcement.getId(),
                before, after, null, ipAddress);
        return AnnouncementResponse.from(announcement);
    }

    @Transactional
    public void deleteAnnouncement(Long userId, Long announcementId, String ipAddress) {
        User admin = requireAdminUser(userId);
        AdminAnnouncement announcement = getAnnouncement(announcementId);
        String before = announcementSnapshot(announcement);
        adminAnnouncementRepository.delete(announcement);
        writeAudit(admin, "ANNOUNCEMENT_DELETE", "ANNOUNCEMENT", announcement.getId(),
                before, null, null, ipAddress);
    }

    public PageResponse<PolicyVersionResponse> getPolicies(Long userId, int limit) {
        requireAdminUser(userId);
        return page(policyVersionRepository.findAllByOrderByIdDesc(firstPageRequest(limit)),
                PolicyVersionResponse::from);
    }

    @Transactional
    public MaintenanceResponse updateMaintenance(Long userId, MaintenanceRequest request, String ipAddress) {
        User admin = requireAdminUser(userId);
        MaintenanceSetting setting = maintenanceSettingRepository.save(MaintenanceSetting.builder()
                .enabled(request.enabled())
                .message(request.message())
                .updatedByUserId(admin.getId())
                .build());
        writeAudit(admin, "MAINTENANCE_UPDATE", "MAINTENANCE", setting.getId(),
                null, "enabled=" + request.enabled(), request.message(), ipAddress);
        return MaintenanceResponse.from(setting);
    }

    public PageResponse<AuditLogResponse> getAuditLogs(Long userId, Long actorUserId, String action, String targetType,
                                                       Instant from, Instant to, String cursor, int limit) {
        requireAdminUser(userId);
        return page(adminAuditLogRepository.findAll(auditLogSpec(actorUserId, action, targetType, from, to),
                        pageRequest(cursor, limit)),
                log -> AuditLogResponse.from(log, email(log.getActorUserId())));
    }

    public PageResponse<SearchItemResponse> search(Long userId, String q, String types, int limit) {
        requireAdminUser(userId);
        String keyword = normalize(q);
        Set<String> requestedTypes = parseTypes(types);
        List<SearchItemResponse> items = new ArrayList<>();
        if (requestedTypes.contains("USER")) {
            userRepository.findAll(userSpec(keyword, null, null), firstPageRequest(limit))
                    .forEach(user -> items.add(new SearchItemResponse(
                            String.valueOf(user.getId()),
                            "USER",
                            user.getNickname(),
                            nullToEmpty(user.getEmail()) + " · " + user.getId(),
                            "/users?selected=" + user.getId()
                    )));
        }
        if (requestedTypes.contains("REPORT")) {
            reportRepository.findAll(reportSpec(null, null, keyword), firstPageRequest(limit))
                    .forEach(report -> items.add(new SearchItemResponse(
                            String.valueOf(report.getId()),
                            "REPORT",
                            report.getTargetType() + " 신고",
                            report.getStatus() + " · " + report.getReasonType(),
                            "/moderation?selected=" + report.getId()
                    )));
        }
        if (requestedTypes.contains("SPACE")) {
            (keyword.isBlank()
                    ? spaceRepository.findAll(firstPageRequest(limit))
                    : spaceRepository.findBySpaceNameContainingIgnoreCase(keyword, firstPageRequest(limit)))
                    .forEach(space -> items.add(new SearchItemResponse(
                            String.valueOf(space.getId()),
                            "SPACE",
                            space.getSpaceName(),
                            "owner " + space.getOwnerUserId(),
                            "/users?space=" + space.getId()
                    )));
        }
        if (requestedTypes.contains("BOOK")) {
            (keyword.isBlank()
                    ? bookRepository.findAll(firstPageRequest(limit))
                    : bookRepository.findByTitleContainingIgnoreCaseOrAuthorContainingIgnoreCase(
                            keyword, keyword, firstPageRequest(limit)))
                    .forEach(book -> items.add(new SearchItemResponse(
                            String.valueOf(book.getId()),
                            "BOOK",
                            book.getTitle(),
                            book.getAuthor(),
                            "/search?book=" + book.getId()
                    )));
        }
        return page(items, limit);
    }

    private ReportListItemResponse toReportListItem(Report report) {
        ReportTargetResponse target = resolveTarget(report.getTargetType(), report.getTargetId());
        return new ReportListItemResponse(
                String.valueOf(report.getId()),
                report.getTargetType(),
                String.valueOf(report.getTargetId()),
                target.excerpt(),
                target.authorId(),
                target.authorName(),
                reportRepository.findByTargetTypeAndTargetIdOrderByIdDesc(report.getTargetType(), report.getTargetId()).size(),
                report.getReasonType(),
                report.getReasonDetail(),
                report.getStatus(),
                severity(report),
                report.isBlindedToReporter(),
                report.isGloballyBlinded(),
                report.getCreatedAt(),
                report.getReviewedByUserId() == null ? null : String.valueOf(report.getReviewedByUserId()),
                report.getReviewedAt()
        );
    }

    private ReportReporterResponse toReportReporter(Report report) {
        return new ReportReporterResponse(
                String.valueOf(report.getReporterUserId()),
                nickname(report.getReporterUserId()),
                report.getReasonType(),
                report.getReasonDetail(),
                report.getCreatedAt()
        );
    }

    private ReportTargetResponse resolveTarget(String targetType, Long targetId) {
        if (targetId == null || targetType == null) {
            return new ReportTargetResponse(targetType, targetId == null ? null : String.valueOf(targetId), null, null, null);
        }
        String normalizedType = targetType.toUpperCase(Locale.ROOT);
        if ("USER".equals(normalizedType)) {
            User user = userRepository.findById(targetId).orElse(null);
            return new ReportTargetResponse(targetType, String.valueOf(targetId),
                    user == null ? "사용자 없음" : user.getNickname(), String.valueOf(targetId),
                    user == null ? null : user.getNickname());
        }
        if ("SPACE".equals(normalizedType)) {
            Space space = spaceRepository.findById(targetId).orElse(null);
            return new ReportTargetResponse(targetType, String.valueOf(targetId),
                    space == null ? "공간 없음" : space.getSpaceName(),
                    space == null ? null : String.valueOf(space.getOwnerUserId()),
                    space == null ? null : nickname(space.getOwnerUserId()));
        }
        if ("GUESTBOOK".equals(normalizedType)) {
            Guestbook guestbook = guestbookRepository.findById(targetId).orElse(null);
            return new ReportTargetResponse(targetType, String.valueOf(targetId),
                    guestbook == null ? "방명록 없음" : truncate(guestbook.getContent(), 80),
                    guestbook == null ? null : String.valueOf(guestbook.getWriterUserId()),
                    guestbook == null ? null : nickname(guestbook.getWriterUserId()));
        }
        if ("READING_RECORD_COMMENT".equals(normalizedType)) {
            ReadingRecordComment comment = readingRecordCommentRepository.findById(targetId).orElse(null);
            return new ReportTargetResponse(targetType, String.valueOf(targetId),
                    comment == null ? "댓글 없음" : truncate(comment.getContent(), 80),
                    comment == null ? null : String.valueOf(comment.getUserId()),
                    comment == null ? null : nickname(comment.getUserId()));
        }
        if ("READING_RECORD".equals(normalizedType)) {
            ReadingRecord record = readingRecordRepository.findById(targetId).orElse(null);
            Long authorId = record == null ? null : userBookRepository.findById(record.getUserBookId())
                    .map(UserBook::getUserId)
                    .orElse(null);
            return new ReportTargetResponse(targetType, String.valueOf(targetId),
                    record == null ? "독서기록 없음" : truncate(firstNonBlank(record.getTitle(), record.getContent()), 80),
                    authorId == null ? null : String.valueOf(authorId),
                    authorId == null ? null : nickname(authorId));
        }
        return new ReportTargetResponse(targetType, String.valueOf(targetId), targetType + " #" + targetId, null, null);
    }

    private UserListItemResponse toUserListItem(User user, List<UserSocialAccount> socialAccounts) {
        return new UserListItemResponse(
                String.valueOf(user.getId()),
                user.getNickname(),
                user.getEmail(),
                user.getCreatedAt(),
                user.getLastLoginAt(),
                user.getStatus(),
                firstProvider(socialAccounts),
                spacesOf(user.getId()).size(),
                bookshelfCount(user.getId()),
                userBookRepository.countByUserIdAndStorageStatusNot(user.getId(), DELETED),
                reportsAgainst(user.getId()),
                walletBalance(user.getId())
        );
    }

    private UserSpaceResponse toUserSpaceResponse(Space space) {
        List<Bookshelf> bookshelves = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE");
        long bookCount = bookshelves.stream()
                .mapToLong(bookshelf -> bookshelfItemRepository.countByBookshelfIdAndStatus(bookshelf.getId(), "ACTIVE"))
                .sum();
        return new UserSpaceResponse(
                String.valueOf(space.getId()),
                space.getSpaceName(),
                space.getVisibilityScope(),
                space.getVisitCount(),
                space.getLikeCount(),
                bookshelves.size(),
                bookCount
        );
    }

    private void applyReportAction(Report report, Long reviewerUserId, String action,
                                   Boolean blindToReporter, Boolean globallyBlind) {
        String normalizedAction = normalizeAction(action);
        if ("START_REVIEW".equals(normalizedAction)) {
            report.startReview(reviewerUserId);
            return;
        }
        if ("RESOLVE".equals(normalizedAction)) {
            report.resolve(reviewerUserId, blindToReporter, globallyBlind);
            return;
        }
        if ("REJECT".equals(normalizedAction)) {
            report.reject(reviewerUserId);
            return;
        }
        if ("GLOBAL_BLIND".equals(normalizedAction)) {
            report.changeGlobalBlind(reviewerUserId, true);
            return;
        }
        if ("UNBLIND".equals(normalizedAction)) {
            report.changeGlobalBlind(reviewerUserId, false);
            return;
        }
        throw new CustomException(ErrorCode.INVALID_INPUT, "지원하지 않는 신고 액션입니다.");
    }

    private void revokeActiveRefreshTokens(Long targetUserId) {
        List<AuthRefreshToken> tokens = authRefreshTokenRepository
                .findByUserIdAndRevokedAtIsNullAndExpiresAtAfter(targetUserId, Instant.now());
        tokens.forEach(AuthRefreshToken::revoke);
    }

    private String normalizeNickname(String nickname) {
        if (nickname == null || nickname.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "새 닉네임은 필수입니다.");
        }
        return nickname.trim();
    }

    private Long parseLongId(String value, String message) {
        try {
            return Long.parseLong(value);
        } catch (NumberFormatException exception) {
            throw new CustomException(ErrorCode.INVALID_INPUT, message);
        }
    }

    private User requireAdminUser(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        User user = userService.getUser(currentUserId);
        if (!ROLE_ADMIN.equalsIgnoreCase(user.getRole())) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return user;
    }

    private Report getReportEntity(Long reportId) {
        return reportRepository.findById(reportId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
    }

    private AdminAnnouncement getAnnouncement(Long announcementId) {
        return adminAnnouncementRepository.findById(announcementId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
    }

    private void writeAudit(User actor, String action, String targetType, Long targetId,
                            String beforePayload, String afterPayload, String reason, String ipAddress) {
        adminAuditLogRepository.save(buildAudit(actor, action, targetType, targetId,
                beforePayload, afterPayload, reason, ipAddress));
    }

    private AdminAuditLog buildAudit(User actor, String action, String targetType, Long targetId,
                                     String beforePayload, String afterPayload, String reason, String ipAddress) {
        return AdminAuditLog.builder()
                .actorUserId(actor.getId())
                .action(action)
                .targetType(targetType)
                .targetId(targetId)
                .beforePayload(beforePayload)
                .afterPayload(afterPayload)
                .reason(reason)
                .ipAddress(ipAddress)
                .build();
    }

    private long walletBalancePages() {
        return pageWalletRepository.sumBalancePages();
    }

    private long assetPurchasePages() {
        return assetPurchaseRepository.sumPricePages();
    }

    private long pendingPayoutPages() {
        return settlementWithdrawalRepository.sumRequestedPagesByStatus("REQUESTED");
    }

    private String paymentStatus() {
        return "NOT_INTEGRATED";
    }

    private String externalBookApiStatus() {
        Instant now = Instant.now();
        if (externalBookApiStatusCheckedAt != null
                && now.isBefore(externalBookApiStatusCheckedAt.plus(EXTERNAL_BOOK_API_HEALTH_TTL))) {
            return cachedExternalBookApiStatus;
        }
        synchronized (this) {
            Instant checkedAt = externalBookApiStatusCheckedAt;
            if (checkedAt != null && now.isBefore(checkedAt.plus(EXTERNAL_BOOK_API_HEALTH_TTL))) {
                return cachedExternalBookApiStatus;
            }
            cachedExternalBookApiStatus = resolveExternalBookApiStatus();
            externalBookApiStatusCheckedAt = now;
            return cachedExternalBookApiStatus;
        }
    }

    private String resolveExternalBookApiStatus() {
        try {
            kakaoBookSearchClient.search("도서", 1, 1);
            return "UP";
        } catch (RuntimeException exception) {
            return "DOWN";
        }
    }

    private int walletBalance(Long userId) {
        return pageWalletRepository.findByUserId(userId)
                .map(PageWallet::getBalancePages)
                .orElse(0);
    }

    private long reportsAgainst(Long userId) {
        return reportRepository.countByReportedUserIdOrTargetTypeIgnoreCaseAndTargetId(userId, ROLE_USER, userId);
    }

    private long bookshelfCount(Long userId) {
        return spacesOf(userId).stream()
                .mapToLong(space -> bookshelfRepository.countBySpaceIdAndStatus(space.getId(), "ACTIVE"))
                .sum();
    }

    private List<Space> spacesOf(Long userId) {
        return spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(userId);
    }

    private long activeUsersSince(Instant since) {
        return userRepository.countByLastLoginAtGreaterThanEqual(since);
    }

    private long usersCreatedSince(Instant since) {
        return userRepository.countByCreatedAtGreaterThanEqual(since);
    }

    private long usersCreatedOn(LocalDate date) {
        return userRepository.countByCreatedAtBetween(startOfDay(date), startOfNextDay(date));
    }

    private long userBooksCreatedOn(LocalDate date) {
        return userBookRepository.countByCreatedAtBetween(startOfDay(date), startOfNextDay(date));
    }

    private long assetPurchasePagesOn(LocalDate date) {
        return assetPurchaseRepository.sumPricePagesByPurchasedAtBetween(date.atStartOfDay(), date.plusDays(1).atStartOfDay());
    }

    private List<CategoryRevenueResponse> categoryRevenue() {
        return assetPurchaseRepository.sumPricePagesByCategory().stream()
                .map(row -> new CategoryRevenueResponse(row.getCategory(), row.getPages()))
                .sorted(Comparator.comparing(CategoryRevenueResponse::pages).reversed())
                .toList();
    }

    private Map<Long, List<UserSocialAccount>> socialAccountsByUserId(Collection<Long> userIds) {
        if (userIds.isEmpty()) {
            return Map.of();
        }
        return userSocialAccountRepository.findByUserIdIn(userIds).stream()
                .collect(Collectors.groupingBy(UserSocialAccount::getUserId));
    }

    private Specification<Report> reportSpec(String status, String targetType, String q) {
        return (root, query, criteriaBuilder) -> {
            List<Predicate> predicates = new ArrayList<>();
            if (status != null && !status.isBlank()) {
                predicates.add(criteriaBuilder.equal(criteriaBuilder.upper(root.get("status")),
                        status.trim().toUpperCase(Locale.ROOT)));
            }
            if (targetType != null && !targetType.isBlank()) {
                predicates.add(criteriaBuilder.equal(criteriaBuilder.upper(root.get("targetType")),
                        targetType.trim().toUpperCase(Locale.ROOT)));
            }
            String keyword = normalize(q);
            if (!keyword.isBlank()) {
                String like = "%" + keyword + "%";
                predicates.add(criteriaBuilder.or(
                        criteriaBuilder.like(criteriaBuilder.lower(root.get("targetType")), like),
                        criteriaBuilder.like(criteriaBuilder.lower(root.get("reasonType")), like),
                        criteriaBuilder.like(criteriaBuilder.lower(root.get("reasonDetail")), like)
                ));
            }
            return criteriaBuilder.and(predicates.toArray(Predicate[]::new));
        };
    }

    private Specification<User> userSpec(String q, String status, String provider) {
        return (root, query, criteriaBuilder) -> {
            List<Predicate> predicates = new ArrayList<>();
            if (status != null && !status.isBlank()) {
                predicates.add(criteriaBuilder.equal(criteriaBuilder.upper(root.get("status")),
                        status.trim().toUpperCase(Locale.ROOT)));
            }
            String keyword = normalize(q);
            if (!keyword.isBlank()) {
                String like = "%" + keyword + "%";
                predicates.add(criteriaBuilder.or(
                        criteriaBuilder.like(criteriaBuilder.lower(root.get("email")), like),
                        criteriaBuilder.like(criteriaBuilder.lower(root.get("nickname")), like),
                        criteriaBuilder.like(root.get("id").as(String.class), like)
                ));
            }
            SocialProvider socialProvider = parseSocialProvider(provider);
            if (provider != null && !provider.isBlank() && socialProvider == null) {
                predicates.add(criteriaBuilder.disjunction());
            } else if (socialProvider != null) {
                var subquery = query.subquery(Long.class);
                var account = subquery.from(UserSocialAccount.class);
                subquery.select(account.get("userId"))
                        .where(criteriaBuilder.equal(account.get("provider"), socialProvider));
                predicates.add(root.get("id").in(subquery));
            }
            return criteriaBuilder.and(predicates.toArray(Predicate[]::new));
        };
    }

    private Specification<AdminAuditLog> auditLogSpec(Long actorUserId, String action, String targetType,
                                                      Instant from, Instant to) {
        return (root, query, criteriaBuilder) -> {
            List<Predicate> predicates = new ArrayList<>();
            if (actorUserId != null) {
                predicates.add(criteriaBuilder.equal(root.get("actorUserId"), actorUserId));
            }
            if (action != null && !action.isBlank()) {
                predicates.add(criteriaBuilder.equal(criteriaBuilder.upper(root.get("action")),
                        action.trim().toUpperCase(Locale.ROOT)));
            }
            if (targetType != null && !targetType.isBlank()) {
                predicates.add(criteriaBuilder.equal(criteriaBuilder.upper(root.get("targetType")),
                        targetType.trim().toUpperCase(Locale.ROOT)));
            }
            if (from != null) {
                predicates.add(criteriaBuilder.greaterThanOrEqualTo(root.get("createdAt"), from));
            }
            if (to != null) {
                predicates.add(criteriaBuilder.lessThanOrEqualTo(root.get("createdAt"), to));
            }
            return criteriaBuilder.and(predicates.toArray(Predicate[]::new));
        };
    }

    private SocialProvider parseSocialProvider(String provider) {
        if (provider == null || provider.isBlank()) {
            return null;
        }
        try {
            return SocialProvider.valueOf(provider.trim().toUpperCase(Locale.ROOT));
        } catch (IllegalArgumentException exception) {
            return null;
        }
    }

    private <T> PageResponse<T> page(List<T> source, int limit) {
        return page(source, null, limit);
    }

    private <S, T> PageResponse<T> page(Page<S> source, Function<S, T> mapper) {
        List<T> items = source.getContent().stream()
                .map(mapper)
                .toList();
        String nextCursor = source.hasNext() ? String.valueOf((source.getNumber() + 1) * source.getSize()) : null;
        return new PageResponse<>(items, nextCursor, source.getTotalElements());
    }

    private <T> PageResponse<T> page(List<T> source, String cursor, int limit) {
        int validLimit = validateLimit(limit);
        int start = parseCursor(cursor);
        List<T> items = source.stream()
                .skip(start)
                .limit(validLimit)
                .toList();
        String nextCursor = source.size() > start + validLimit ? String.valueOf(start + validLimit) : null;
        return new PageResponse<>(items, nextCursor, source.size());
    }

    private PageRequest pageRequest(String cursor, int limit) {
        int validLimit = validateLimit(limit);
        int start = parseCursor(cursor);
        return PageRequest.of(start / validLimit, validLimit, Sort.by(Sort.Direction.DESC, "id"));
    }

    private PageRequest firstPageRequest(int limit) {
        return PageRequest.of(0, validateLimit(limit), Sort.by(Sort.Direction.DESC, "id"));
    }

    private int parseCursor(String cursor) {
        if (cursor == null || cursor.isBlank()) {
            return 0;
        }
        try {
            return Math.max(0, Integer.parseInt(cursor));
        } catch (NumberFormatException exception) {
            return 0;
        }
    }

    private int validateLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_LIMIT;
        }
        return Math.min(limit, MAX_LIMIT);
    }

    private Instant startOfDay(LocalDate date) {
        return date.atStartOfDay().toInstant(ZoneOffset.UTC);
    }

    private Instant startOfNextDay(LocalDate date) {
        return date.plusDays(1).atStartOfDay().toInstant(ZoneOffset.UTC);
    }

    private String reportState(Report report) {
        return "status=" + report.getStatus()
                + ", blindedToReporter=" + report.isBlindedToReporter()
                + ", globallyBlinded=" + report.isGloballyBlinded();
    }

    private String userState(User user) {
        return "status=" + user.getStatus()
                + ", email=" + user.getEmail()
                + ", nickname=" + user.getNickname()
                + ", profileImageUrl=" + user.getProfileImageUrl()
                + ", introduction=" + user.getIntroduction()
                + ", sessionInvalidatedAt=" + user.getSessionInvalidatedAt();
    }

    private String announcementSnapshot(AdminAnnouncement announcement) {
        return "{"
                + jsonField("id", announcement.getId()) + ","
                + jsonField("title", announcement.getTitle()) + ","
                + jsonField("body", announcement.getBody()) + ","
                + jsonField("audience", announcement.getAudience()) + ","
                + jsonField("startsAt", announcement.getStartsAt()) + ","
                + jsonField("endsAt", announcement.getEndsAt()) + ","
                + jsonField("urgent", announcement.isUrgent()) + ","
                + jsonField("createdByUserId", announcement.getCreatedByUserId()) + ","
                + jsonField("createdAt", announcement.getCreatedAt()) + ","
                + jsonField("updatedAt", announcement.getUpdatedAt())
                + "}";
    }

    private String jsonField(String name, Object value) {
        return jsonString(name) + ":" + jsonValue(value);
    }

    private String jsonValue(Object value) {
        if (value == null) {
            return "null";
        }
        if (value instanceof Number || value instanceof Boolean) {
            return value.toString();
        }
        return jsonString(value.toString());
    }

    private String jsonString(String value) {
        return "\"" + escapeJson(value) + "\"";
    }

    private String escapeJson(String value) {
        StringBuilder escaped = new StringBuilder(value.length());
        for (int index = 0; index < value.length(); index++) {
            char character = value.charAt(index);
            switch (character) {
                case '"' -> escaped.append("\\\"");
                case '\\' -> escaped.append("\\\\");
                case '\b' -> escaped.append("\\b");
                case '\f' -> escaped.append("\\f");
                case '\n' -> escaped.append("\\n");
                case '\r' -> escaped.append("\\r");
                case '\t' -> escaped.append("\\t");
                default -> appendJsonCharacter(escaped, character);
            }
        }
        return escaped.toString();
    }

    private void appendJsonCharacter(StringBuilder escaped, char character) {
        if (character < 0x20) {
            escaped.append(String.format("\\u%04x", (int) character));
            return;
        }
        escaped.append(character);
    }

    private String severity(Report report) {
        if (report.isGloballyBlinded()) {
            return "P1";
        }
        if ("HARASSMENT".equalsIgnoreCase(report.getReasonType()) || "ABUSE".equalsIgnoreCase(report.getReasonType())) {
            return "P2";
        }
        return "P3";
    }

    private String firstProvider(List<UserSocialAccount> socialAccounts) {
        if (socialAccounts == null || socialAccounts.isEmpty()) {
            return null;
        }
        return socialAccounts.get(0).getProvider().name();
    }

    private String email(Long userId) {
        return userRepository.findById(userId)
                .map(User::getEmail)
                .orElse(null);
    }

    private String nickname(Long userId) {
        return userRepository.findById(userId)
                .map(User::getNickname)
                .orElse(null);
    }

    private String normalizeAction(String action) {
        return action == null ? "" : action.trim().toUpperCase(Locale.ROOT);
    }

    private String normalize(String value) {
        return value == null ? "" : value.trim().toLowerCase(Locale.ROOT);
    }

    private Set<String> parseTypes(String types) {
        if (types == null || types.isBlank()) {
            return new HashSet<>(Set.of("USER", "REPORT", "SPACE", "BOOK"));
        }
        Set<String> result = new HashSet<>();
        for (String type : types.split(",")) {
            String normalized = type.trim().toUpperCase(Locale.ROOT);
            if (!normalized.isBlank()) {
                result.add(normalized);
            }
        }
        return result.isEmpty() ? new HashSet<>(Set.of("USER", "REPORT", "SPACE", "BOOK")) : result;
    }

    private String firstNonBlank(String first, String second) {
        if (first != null && !first.isBlank()) {
            return first;
        }
        return second;
    }

    private String truncate(String value, int maxLength) {
        if (value == null || value.length() <= maxLength) {
            return value;
        }
        return value.substring(0, maxLength);
    }

    private String nullToEmpty(String value) {
        return value == null ? "" : value;
    }

    private Map<Long, PurchaseSummary> purchaseSummaryByUserId(Collection<Long> userIds) {
        if (userIds.isEmpty()) {
            return Map.of();
        }
        return assetPurchaseRepository.summarizeByUserIds(userIds).stream()
                .map(row -> new PurchaseSummary(row.getUserId(), row.getCount(), Math.toIntExact(row.getPages())))
                .collect(Collectors.toMap(PurchaseSummary::userId, Function.identity()));
    }

    private record PurchaseSummary(Long userId, long count, int pages) {

        private static PurchaseSummary empty() {
            return new PurchaseSummary(null, 0, 0);
        }
    }
}
