package com.dotshelf.domain.admin.service;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

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
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.repository.AssetCategoryRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.auth.entity.UserSocialAccount;
import com.dotshelf.domain.auth.repository.UserSocialAccountRepository;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
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
import com.dotshelf.domain.user.entity.UserBlock;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminService {

    private static final int DEFAULT_LIMIT = 20;
    private static final int MAX_LIMIT = 100;
    private static final String DELETED = "DELETED";

    private final UserService userService;
    private final UserRepository userRepository;
    private final UserSocialAccountRepository userSocialAccountRepository;
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
    private final AssetCategoryRepository assetCategoryRepository;
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
                new DashboardModerationSummary(reportRepository.countByStatus("PENDING"), 0, 0),
                new DashboardSettlementSummary(pendingPayoutPages, walletBalancePages, walletBalancePages >= 0),
                new DashboardOpsSummary(userRepository.count(), spaceRepository.countByDeletedAtIsNull(),
                        spaceVisitRepository.countByVisitedDate(LocalDate.now()), "UP")
        );
    }

    public PageResponse<ReportListItemResponse> getReports(Long userId, String status, String targetType,
                                                           String q, String cursor, int limit) {
        requireAdminUser(userId);
        List<ReportListItemResponse> reports = reportRepository.findAll().stream()
                .filter(report -> matches(status, report.getStatus()))
                .filter(report -> matches(targetType, report.getTargetType()))
                .map(this::toReportListItem)
                .filter(response -> containsReportQuery(response, q))
                .sorted(Comparator.comparing(ReportListItemResponse::id, AdminService::compareNumericString).reversed())
                .toList();
        return page(reports, cursor, limit);
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
    public ReportListItemResponse actReport(Long userId, Long reportId, ReportActionRequest request) {
        User admin = requireAdminUser(userId);
        Report report = getReportEntity(reportId);
        String before = reportState(report);
        applyReportAction(report, admin.getId(), request.action(), request.blindToReporter(), request.globallyBlind());
        writeAudit(admin, "REPORT_" + normalizeAction(request.action()), "REPORT", report.getId(),
                before, reportState(report), request.reason());
        return toReportListItem(report);
    }

    @Transactional
    public OkResponse bulkActReports(Long userId, BulkReportActionRequest request) {
        User admin = requireAdminUser(userId);
        for (Long reportId : request.ids()) {
            Report report = getReportEntity(reportId);
            String before = reportState(report);
            applyReportAction(report, admin.getId(), request.action(), null, null);
            writeAudit(admin, "REPORT_" + normalizeAction(request.action()), "REPORT", report.getId(),
                    before, reportState(report), request.reason());
        }
        return OkResponse.success();
    }

    public PageResponse<UserListItemResponse> getUsers(Long userId, String q, String status,
                                                       String provider, String cursor, int limit) {
        requireAdminUser(userId);
        Map<Long, List<UserSocialAccount>> socialAccounts = socialAccountsByUserId();
        List<UserListItemResponse> users = userRepository.findAll().stream()
                .filter(user -> matches(status, user.getStatus()))
                .filter(user -> containsUserQuery(user, q))
                .filter(user -> hasProvider(socialAccounts.get(user.getId()), provider))
                .map(user -> toUserListItem(user, socialAccounts.get(user.getId())))
                .sorted(Comparator.comparing(UserListItemResponse::id, AdminService::compareNumericString).reversed())
                .toList();
        return page(users, cursor, limit);
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
    public AdminUserDetailResponse actUser(Long userId, Long targetUserId, UserActionRequest request) {
        User admin = requireAdminUser(userId);
        User target = userService.getUser(targetUserId);
        String before = "status=" + target.getStatus();
        String action = normalizeAction(request.action());
        if ("ACTIVATE".equals(action)) {
            target.changeStatus("ACTIVE");
        } else if ("SUSPEND_TEMP".equals(action)) {
            target.changeStatus("SUSPENDED_TEMP");
        } else if ("SUSPEND_PERM".equals(action)) {
            target.changeStatus("SUSPENDED_PERM");
        } else if ("ANONYMIZE".equals(action)) {
            target.anonymize();
        } else {
            throw new CustomException(ErrorCode.INVALID_INPUT, "지원하지 않는 사용자 액션입니다.");
        }
        writeAudit(admin, "USER_" + action, "USER", target.getId(), before, "status=" + target.getStatus(), request.reason());
        return getUser(admin.getId(), targetUserId);
    }

    public PageResponse<UserSpaceResponse> getUserSpaces(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        List<UserSpaceResponse> spaces = spacesOf(targetUserId).stream()
                .map(this::toUserSpaceResponse)
                .toList();
        return page(spaces, limit);
    }

    public PageResponse<UserPurchaseResponse> getUserPurchases(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        return page(assetPurchaseRepository.findByUserIdOrderByPurchasedAtDesc(targetUserId).stream()
                .map(UserPurchaseResponse::from)
                .toList(), limit);
    }

    public PageResponse<UserHistoryResponse> getUserHistory(Long userId, Long targetUserId, int limit) {
        requireAdminUser(userId);
        userService.getUser(targetUserId);
        List<UserHistoryResponse> history = new ArrayList<>();
        reportRepository.findAll().stream()
                .filter(report -> targetUserId.equals(report.getReporterUserId()) || targetUserId.equals(report.getReportedUserId()))
                .forEach(report -> history.add(new UserHistoryResponse(
                        String.valueOf(report.getId()),
                        "REPORT",
                        report.getTargetType(),
                        report.getReasonType(),
                        report.getCreatedAt()
                )));
        userBlockRepository.findAll().stream()
                .filter(block -> targetUserId.equals(block.getBlockerUserId()) || targetUserId.equals(block.getBlockedUserId()))
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
        return page(adminUserNoteRepository.findByUserIdOrderByIdDesc(targetUserId).stream()
                .map(note -> AdminUserNoteResponse.from(note, nickname(note.getAuthorUserId())))
                .toList(), limit);
    }

    @Transactional
    public AdminUserNoteResponse createUserNote(Long userId, Long targetUserId, CreateAdminUserNoteRequest request) {
        User admin = requireAdminUser(userId);
        userService.getUser(targetUserId);
        AdminUserNote note = adminUserNoteRepository.save(AdminUserNote.builder()
                .userId(targetUserId)
                .authorUserId(admin.getId())
                .body(request.body())
                .build());
        writeAudit(admin, "USER_NOTE_CREATE", "USER", targetUserId, null, request.body(), null);
        return AdminUserNoteResponse.from(note, admin.getNickname());
    }

    @Transactional
    public BalanceAdjustResponse adjustBalance(Long userId, Long targetUserId, BalanceAdjustRequest request) {
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
                before, "balancePages=" + wallet.getBalancePages(), request.memo());
        return new BalanceAdjustResponse(
                String.valueOf(targetUserId),
                wallet.getBalancePages(),
                SettlementAdjustmentResponse.from(adjustment)
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
        return page(settlementWithdrawalRepository.findAllByOrderByIdDesc().stream()
                .map(SettlementWithdrawalResponse::from)
                .toList(), limit);
    }

    public PageResponse<SettlementPayoutBatchResponse> getSettlementPayoutBatches(Long userId, int limit) {
        requireAdminUser(userId);
        return page(settlementPayoutBatchRepository.findAllByOrderByIdDesc().stream()
                .map(SettlementPayoutBatchResponse::from)
                .toList(), limit);
    }

    public PageResponse<SettlementAccountChangeRequestResponse> getSettlementAccountChanges(Long userId, int limit) {
        requireAdminUser(userId);
        return page(settlementAccountChangeRequestRepository.findAllByOrderByIdDesc().stream()
                .map(SettlementAccountChangeRequestResponse::from)
                .toList(), limit);
    }

    public PageResponse<SettlementCreatorResponse> getSettlementCreators(Long userId, int limit) {
        requireAdminUser(userId);
        List<AssetPurchase> purchases = assetPurchaseRepository.findAll();
        Map<Long, Long> purchaseCount = purchases.stream()
                .collect(Collectors.groupingBy(AssetPurchase::getUserId, Collectors.counting()));
        Map<Long, Integer> earnedPages = purchases.stream()
                .collect(Collectors.groupingBy(AssetPurchase::getUserId, Collectors.summingInt(AssetPurchase::getPricePages)));
        return page(creatorProfileRepository.findAllByOrderByIdDesc().stream()
                .map(creator -> SettlementCreatorResponse.from(
                        creator,
                        purchaseCount.getOrDefault(creator.getUserId(), 0L),
                        earnedPages.getOrDefault(creator.getUserId(), 0)
                ))
                .toList(), limit);
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
                userBookRepository.findAll().size(),
                reportRepository.countByStatus("PENDING"),
                assetPurchasePages(),
                assetPurchaseRepository.count(),
                spaceVisitRepository.count(),
                0,
                null
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
                    reportRepository.countByStatus("PENDING"),
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
                "UNKNOWN",
                "UNKNOWN",
                0,
                reportRepository.countByStatus("PENDING"),
                maintenanceMode
        );
    }

    public PageResponse<AdminMemberResponse> getAdmins(Long userId, int limit) {
        requireAdminUser(userId);
        return page(userRepository.findByRoleIgnoreCase("ADMIN").stream()
                .map(AdminMemberResponse::from)
                .toList(), limit);
    }

    @Transactional
    public AdminMemberResponse updateAdminRole(Long userId, Long targetUserId, AdminRoleRequest request) {
        User admin = requireAdminUser(userId);
        User target = userService.getUser(targetUserId);
        String role = request.role().trim().toUpperCase(Locale.ROOT);
        if (!"ADMIN".equals(role) && !"USER".equals(role)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "role은 ADMIN 또는 USER만 가능합니다.");
        }
        String before = "role=" + target.getRole();
        target.changeRole(role);
        writeAudit(admin, "ADMIN_ROLE_UPDATE", "USER", targetUserId, before, "role=" + role, request.reason());
        return AdminMemberResponse.from(target);
    }

    public PageResponse<AnnouncementResponse> getAnnouncements(Long userId, int limit) {
        requireAdminUser(userId);
        return page(adminAnnouncementRepository.findAllByOrderByIdDesc().stream()
                .map(AnnouncementResponse::from)
                .toList(), limit);
    }

    @Transactional
    public AnnouncementResponse createAnnouncement(Long userId, CreateAnnouncementRequest request) {
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
        writeAudit(admin, "ANNOUNCEMENT_CREATE", "ANNOUNCEMENT", announcement.getId(), null, announcement.getTitle(), null);
        return AnnouncementResponse.from(announcement);
    }

    public PageResponse<PolicyVersionResponse> getPolicies(Long userId, int limit) {
        requireAdminUser(userId);
        return page(policyVersionRepository.findAllByOrderByIdDesc().stream()
                .map(PolicyVersionResponse::from)
                .toList(), limit);
    }

    @Transactional
    public MaintenanceResponse updateMaintenance(Long userId, MaintenanceRequest request) {
        User admin = requireAdminUser(userId);
        MaintenanceSetting setting = maintenanceSettingRepository.save(MaintenanceSetting.builder()
                .enabled(request.enabled())
                .message(request.message())
                .updatedByUserId(admin.getId())
                .build());
        writeAudit(admin, "MAINTENANCE_UPDATE", "MAINTENANCE", setting.getId(),
                null, "enabled=" + request.enabled(), request.message());
        return MaintenanceResponse.from(setting);
    }

    public PageResponse<AuditLogResponse> getAuditLogs(Long userId, Long actorUserId, String action, String targetType,
                                                       Instant from, Instant to, String cursor, int limit) {
        requireAdminUser(userId);
        List<AuditLogResponse> logs = adminAuditLogRepository.findAll().stream()
                .filter(log -> actorUserId == null || actorUserId.equals(log.getActorUserId()))
                .filter(log -> matches(action, log.getAction()))
                .filter(log -> matches(targetType, log.getTargetType()))
                .filter(log -> from == null || !log.getCreatedAt().isBefore(from))
                .filter(log -> to == null || !log.getCreatedAt().isAfter(to))
                .sorted(Comparator.comparing(AdminAuditLog::getId).reversed())
                .map(log -> AuditLogResponse.from(log, email(log.getActorUserId())))
                .toList();
        return page(logs, cursor, limit);
    }

    public PageResponse<SearchItemResponse> search(Long userId, String q, String types, int limit) {
        requireAdminUser(userId);
        String keyword = normalize(q);
        Set<String> requestedTypes = parseTypes(types);
        List<SearchItemResponse> items = new ArrayList<>();
        if (requestedTypes.contains("USER")) {
            userRepository.findAll().stream()
                    .filter(user -> containsUserQuery(user, keyword))
                    .forEach(user -> items.add(new SearchItemResponse(
                            String.valueOf(user.getId()),
                            "USER",
                            user.getNickname(),
                            nullToEmpty(user.getEmail()) + " · " + user.getId(),
                            "/users?selected=" + user.getId()
                    )));
        }
        if (requestedTypes.contains("REPORT")) {
            reportRepository.findAll().stream()
                    .filter(report -> keyword.isBlank()
                            || normalize(report.getTargetType()).contains(keyword)
                            || normalize(report.getReasonType()).contains(keyword)
                            || normalize(report.getReasonDetail()).contains(keyword))
                    .forEach(report -> items.add(new SearchItemResponse(
                            String.valueOf(report.getId()),
                            "REPORT",
                            report.getTargetType() + " 신고",
                            report.getStatus() + " · " + report.getReasonType(),
                            "/moderation?selected=" + report.getId()
                    )));
        }
        if (requestedTypes.contains("SPACE")) {
            spaceRepository.findAll().stream()
                    .filter(space -> normalize(space.getSpaceName()).contains(keyword))
                    .forEach(space -> items.add(new SearchItemResponse(
                            String.valueOf(space.getId()),
                            "SPACE",
                            space.getSpaceName(),
                            "owner " + space.getOwnerUserId(),
                            "/users?space=" + space.getId()
                    )));
        }
        if (requestedTypes.contains("BOOK")) {
            bookRepository.findAll().stream()
                    .filter(book -> normalize(book.getTitle()).contains(keyword) || normalize(book.getAuthor()).contains(keyword))
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

    private User requireAdminUser(Long userId) {
        if (userId == null) {
            return userRepository.findByRoleIgnoreCase("ADMIN").stream()
                    .findFirst()
                    .orElseThrow(() -> new CustomException(ErrorCode.UNAUTHORIZED));
        }
        Long currentUserId = userService.requireUserId(userId);
        User user = userService.getUser(currentUserId);
        if (!"ADMIN".equalsIgnoreCase(user.getRole())) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return user;
    }

    private Report getReportEntity(Long reportId) {
        return reportRepository.findById(reportId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
    }

    private void writeAudit(User actor, String action, String targetType, Long targetId,
                            String beforePayload, String afterPayload, String reason) {
        adminAuditLogRepository.save(AdminAuditLog.builder()
                .actorUserId(actor.getId())
                .action(action)
                .targetType(targetType)
                .targetId(targetId)
                .beforePayload(beforePayload)
                .afterPayload(afterPayload)
                .reason(reason)
                .build());
    }

    private long walletBalancePages() {
        return pageWalletRepository.findAll().stream()
                .mapToLong(PageWallet::getBalancePages)
                .sum();
    }

    private long assetPurchasePages() {
        return assetPurchaseRepository.findAll().stream()
                .mapToLong(AssetPurchase::getPricePages)
                .sum();
    }

    private long pendingPayoutPages() {
        return settlementWithdrawalRepository.findAll().stream()
                .filter(withdrawal -> "REQUESTED".equalsIgnoreCase(withdrawal.getStatus()))
                .mapToLong(withdrawal -> withdrawal.getRequestedPages())
                .sum();
    }

    private int walletBalance(Long userId) {
        return pageWalletRepository.findByUserId(userId)
                .map(PageWallet::getBalancePages)
                .orElse(0);
    }

    private long reportsAgainst(Long userId) {
        return reportRepository.findAll().stream()
                .filter(report -> userId.equals(report.getReportedUserId())
                        || ("USER".equalsIgnoreCase(report.getTargetType()) && userId.equals(report.getTargetId())))
                .count();
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
        return userRepository.findAll().stream()
                .filter(user -> user.getLastLoginAt() != null && !user.getLastLoginAt().isBefore(since))
                .count();
    }

    private long usersCreatedSince(Instant since) {
        return userRepository.findAll().stream()
                .filter(user -> user.getCreatedAt() != null && !user.getCreatedAt().isBefore(since))
                .count();
    }

    private long usersCreatedOn(LocalDate date) {
        return userRepository.findAll().stream()
                .filter(user -> sameDate(user.getCreatedAt(), date))
                .count();
    }

    private long userBooksCreatedOn(LocalDate date) {
        return userBookRepository.findAll().stream()
                .filter(userBook -> sameDate(userBook.getCreatedAt(), date))
                .count();
    }

    private long assetPurchasePagesOn(LocalDate date) {
        return assetPurchaseRepository.findAll().stream()
                .filter(purchase -> purchase.getPurchasedAt() != null
                        && purchase.getPurchasedAt().toLocalDate().equals(date))
                .mapToLong(AssetPurchase::getPricePages)
                .sum();
    }

    private List<CategoryRevenueResponse> categoryRevenue() {
        Map<Long, Asset> assets = assetRepository.findAll().stream()
                .collect(Collectors.toMap(Asset::getId, Function.identity()));
        Map<Long, String> categoryCodes = assetCategoryRepository.findAll().stream()
                .collect(Collectors.toMap(AssetCategory::getId, AssetCategory::getCode));
        Map<String, Long> pagesByCategory = new HashMap<>();
        for (AssetPurchase purchase : assetPurchaseRepository.findAll()) {
            Asset asset = assets.get(purchase.getAssetId());
            String category = asset == null ? "UNKNOWN" : categoryCodes.getOrDefault(asset.getCategoryId(), "UNKNOWN");
            pagesByCategory.merge(category, (long) purchase.getPricePages(), Long::sum);
        }
        return pagesByCategory.entrySet().stream()
                .map(entry -> new CategoryRevenueResponse(entry.getKey(), entry.getValue()))
                .sorted(Comparator.comparing(CategoryRevenueResponse::pages).reversed())
                .toList();
    }

    private Map<Long, List<UserSocialAccount>> socialAccountsByUserId() {
        return userSocialAccountRepository.findAll().stream()
                .collect(Collectors.groupingBy(UserSocialAccount::getUserId));
    }

    private boolean containsReportQuery(ReportListItemResponse report, String q) {
        String keyword = normalize(q);
        return keyword.isBlank()
                || normalize(report.targetExcerpt()).contains(keyword)
                || normalize(report.targetAuthorName()).contains(keyword)
                || normalize(report.reasonDetail()).contains(keyword);
    }

    private boolean containsUserQuery(User user, String q) {
        String keyword = normalize(q);
        return keyword.isBlank()
                || String.valueOf(user.getId()).contains(keyword)
                || normalize(user.getEmail()).contains(keyword)
                || normalize(user.getNickname()).contains(keyword);
    }

    private boolean hasProvider(List<UserSocialAccount> socialAccounts, String provider) {
        if (provider == null || provider.isBlank()) {
            return true;
        }
        if (socialAccounts == null) {
            return false;
        }
        return socialAccounts.stream()
                .anyMatch(account -> provider.equalsIgnoreCase(account.getProvider().name()));
    }

    private boolean sameDate(Instant instant, LocalDate date) {
        return instant != null && instant.atZone(ZoneOffset.UTC).toLocalDate().equals(date);
    }

    private boolean matches(String expected, String actual) {
        return expected == null || expected.isBlank() || expected.equalsIgnoreCase(actual);
    }

    private <T> PageResponse<T> page(List<T> source, int limit) {
        return page(source, null, limit);
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

    private String reportState(Report report) {
        return "status=" + report.getStatus()
                + ", blindedToReporter=" + report.isBlindedToReporter()
                + ", globallyBlinded=" + report.isGloballyBlinded();
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

    private static int compareNumericString(String left, String right) {
        return Long.compare(Long.parseLong(left), Long.parseLong(right));
    }
}
