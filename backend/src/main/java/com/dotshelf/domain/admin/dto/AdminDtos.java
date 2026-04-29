package com.dotshelf.domain.admin.dto;

import java.time.Instant;
import java.time.LocalDate;
import java.util.List;

import com.dotshelf.domain.admin.entity.AdminAnnouncement;
import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.entity.AdminUserNote;
import com.dotshelf.domain.admin.entity.CreatorProfile;
import com.dotshelf.domain.admin.entity.MaintenanceSetting;
import com.dotshelf.domain.admin.entity.PolicyVersion;
import com.dotshelf.domain.admin.entity.SettlementAccountChangeRequest;
import com.dotshelf.domain.admin.entity.SettlementAdjustment;
import com.dotshelf.domain.admin.entity.SettlementPayoutBatch;
import com.dotshelf.domain.admin.entity.SettlementWithdrawal;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.user.entity.User;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public final class AdminDtos {

    private AdminDtos() {
    }

    public record PageResponse<T>(
            List<T> items,
            String nextCursor,
            long totalCount
    ) {
    }

    public record OkResponse(boolean ok) {

        public static OkResponse success() {
            return new OkResponse(true);
        }
    }

    public record AdminMeResponse(
            String id,
            String email,
            String name,
            String role,
            String team,
            boolean ipAllowlisted,
            boolean mfaEnrolled,
            Instant lastSignInAt
    ) {

        public static AdminMeResponse from(User user) {
            return new AdminMeResponse(
                    String.valueOf(user.getId()),
                    user.getEmail(),
                    user.getNickname(),
                    "admin",
                    "운영",
                    true,
                    false,
                    user.getLastLoginAt()
            );
        }
    }

    public record DashboardSummaryResponse(
            DashboardContentSummary content,
            DashboardModerationSummary moderation,
            DashboardSettlementSummary settlement,
            DashboardOpsSummary ops
    ) {
    }

    public record DashboardContentSummary(long books, long assets, long activePromotions) {
    }

    public record DashboardModerationSummary(long open, long autoFlagged, long p1) {
    }

    public record DashboardSettlementSummary(long pendingPayoutPages, long walletBalancePages, boolean reconciliationOk) {
    }

    public record DashboardOpsSummary(long users, long spaces, long todayVisits, String apiStatus) {
    }

    public record ReportListItemResponse(
            String id,
            String targetType,
            String targetId,
            String targetExcerpt,
            String targetAuthorId,
            String targetAuthorName,
            long reporterCount,
            String reasonType,
            String reasonDetail,
            String status,
            String severity,
            boolean isBlindedToReporter,
            boolean isGloballyBlinded,
            Instant createdAt,
            String reviewedByUserId,
            Object reviewedAt
    ) {
    }

    public record ReportTargetResponse(
            String type,
            String id,
            String excerpt,
            String authorId,
            String authorName
    ) {
    }

    public record ReportReporterResponse(
            String userId,
            String nickname,
            String reasonType,
            String reasonDetail,
            Instant reportedAt
    ) {
    }

    public record ReportDetailResponse(
            String id,
            ReportTargetResponse target,
            List<ReportReporterResponse> reporters,
            String status
    ) {
    }

    public record ReportActionRequest(
            @NotBlank(message = "action은 필수입니다.")
            String action,
            Boolean blindToReporter,
            Boolean globallyBlind,
            @Size(max = 500, message = "reason은 최대 500자입니다.")
            String reason
    ) {
    }

    public record BulkReportActionRequest(
            @NotEmpty(message = "ids는 1개 이상 필요합니다.")
            List<Long> ids,
            @NotBlank(message = "action은 필수입니다.")
            String action,
            @Size(max = 500, message = "reason은 최대 500자입니다.")
            String reason
    ) {
    }

    public record UserListItemResponse(
            String id,
            String nickname,
            String email,
            Instant joinedAt,
            Instant lastSeenAt,
            String state,
            String socialProvider,
            long spaces,
            long bookshelves,
            long books,
            long reportsAgainst,
            int pageBalance
    ) {
    }

    public record AdminUserDetailResponse(
            String id,
            String nickname,
            String email,
            String role,
            String state,
            boolean onboarded,
            String profileImageUrl,
            String introduction,
            Instant joinedAt,
            Instant lastSeenAt,
            List<String> socialProviders,
            int pageBalance,
            long spaces,
            long bookshelves,
            long books,
            long reportsAgainst,
            long notes
    ) {
    }

    public record UserActionRequest(
            @NotBlank(message = "action은 필수입니다.")
            String action,
            @Size(max = 500, message = "reason은 최대 500자입니다.")
            String reason,
            Instant until
    ) {
    }

    public record UserSpaceResponse(
            String spaceId,
            String spaceName,
            String visibilityScope,
            long visitCount,
            long likeCount,
            long bookshelves,
            long books
    ) {
    }

    public record UserPurchaseResponse(
            String id,
            String assetId,
            int pricePages,
            String status,
            Object purchasedAt
    ) {

        public static UserPurchaseResponse from(AssetPurchase purchase) {
            return new UserPurchaseResponse(
                    String.valueOf(purchase.getId()),
                    String.valueOf(purchase.getAssetId()),
                    purchase.getPricePages(),
                    purchase.getPurchaseStatus(),
                    purchase.getPurchasedAt()
            );
        }
    }

    public record UserHistoryResponse(
            String id,
            String type,
            String title,
            String detail,
            Instant occurredAt
    ) {
    }

    public record AdminUserNoteResponse(
            String id,
            String userId,
            String authorUserId,
            String authorName,
            String body,
            Instant createdAt
    ) {

        public static AdminUserNoteResponse from(AdminUserNote note, String authorName) {
            return new AdminUserNoteResponse(
                    String.valueOf(note.getId()),
                    String.valueOf(note.getUserId()),
                    String.valueOf(note.getAuthorUserId()),
                    authorName,
                    note.getBody(),
                    note.getCreatedAt()
            );
        }
    }

    public record CreateAdminUserNoteRequest(
            @NotBlank(message = "body는 필수입니다.")
            @Size(max = 1000, message = "body는 최대 1000자입니다.")
            String body
    ) {
    }

    public record BalanceAdjustRequest(
            int deltaPages,
            @NotBlank(message = "reason은 필수입니다.")
            @Size(max = 80, message = "reason은 최대 80자입니다.")
            String reason,
            @Size(max = 500, message = "memo는 최대 500자입니다.")
            String memo
    ) {
    }

    public record BalanceAdjustResponse(String userId, int balancePages, SettlementAdjustmentResponse adjustment) {
    }

    public record SettlementAdjustmentResponse(
            String id,
            String userId,
            int deltaPages,
            String reason,
            String memo,
            String adjustedByUserId,
            Instant createdAt
    ) {

        public static SettlementAdjustmentResponse from(SettlementAdjustment adjustment) {
            return new SettlementAdjustmentResponse(
                    String.valueOf(adjustment.getId()),
                    String.valueOf(adjustment.getUserId()),
                    adjustment.getDeltaPages(),
                    adjustment.getReason(),
                    adjustment.getMemo(),
                    String.valueOf(adjustment.getAdjustedByUserId()),
                    adjustment.getCreatedAt()
            );
        }
    }

    public record SettlementReconciliationResponse(
            long walletBalancePages,
            long assetPurchasePages,
            long pendingPayoutPages,
            boolean consistent,
            Instant asOf
    ) {
    }

    public record SettlementWithdrawalResponse(
            String id,
            String creatorUserId,
            int requestedPages,
            String status,
            Instant requestedAt,
            String memo
    ) {

        public static SettlementWithdrawalResponse from(SettlementWithdrawal withdrawal) {
            return new SettlementWithdrawalResponse(
                    String.valueOf(withdrawal.getId()),
                    String.valueOf(withdrawal.getCreatorUserId()),
                    withdrawal.getRequestedPages(),
                    withdrawal.getStatus(),
                    withdrawal.getRequestedAt(),
                    withdrawal.getMemo()
            );
        }
    }

    public record SettlementPayoutBatchResponse(
            String id,
            String batchCode,
            int totalPages,
            int withdrawalCount,
            String status,
            Instant scheduledAt,
            Instant completedAt
    ) {

        public static SettlementPayoutBatchResponse from(SettlementPayoutBatch batch) {
            return new SettlementPayoutBatchResponse(
                    String.valueOf(batch.getId()),
                    batch.getBatchCode(),
                    batch.getTotalPages(),
                    batch.getWithdrawalCount(),
                    batch.getStatus(),
                    batch.getScheduledAt(),
                    batch.getCompletedAt()
            );
        }
    }

    public record SettlementAccountChangeRequestResponse(
            String id,
            String userId,
            String bankName,
            String accountNumberMasked,
            String accountHolder,
            String status,
            Instant requestedAt
    ) {

        public static SettlementAccountChangeRequestResponse from(SettlementAccountChangeRequest request) {
            return new SettlementAccountChangeRequestResponse(
                    String.valueOf(request.getId()),
                    String.valueOf(request.getUserId()),
                    request.getBankName(),
                    request.getAccountNumberMasked(),
                    request.getAccountHolder(),
                    request.getStatus(),
                    request.getRequestedAt()
            );
        }
    }

    public record SettlementCreatorResponse(
            String id,
            String userId,
            String displayName,
            String status,
            long assetPurchaseCount,
            long earnedPages
    ) {

        public static SettlementCreatorResponse from(CreatorProfile creator, long purchaseCount, long earnedPages) {
            return new SettlementCreatorResponse(
                    String.valueOf(creator.getId()),
                    String.valueOf(creator.getUserId()),
                    creator.getDisplayName(),
                    creator.getStatus(),
                    purchaseCount,
                    earnedPages
            );
        }
    }

    public record StatsKpiResponse(
            long dau,
            long wau,
            long mau,
            long newSignups,
            long guestConversion,
            long booksAdded,
            long openModerationQueue,
            long pageGmvPages,
            long assetPurchaseCount,
            long spaceVisits,
            double apiErrorRate,
            Double cdnHitRate
    ) {
    }

    public record StatsTimeseriesResponse(
            List<DailyStatsResponse> daily,
            List<CategoryRevenueResponse> categoryRevenue,
            List<Object> funnel
    ) {
    }

    public record DailyStatsResponse(
            LocalDate date,
            long newSignups,
            long booksAdded,
            long assetPurchasePages,
            long openModeration,
            long spaceVisits
    ) {
    }

    public record CategoryRevenueResponse(String category, long pages) {
    }

    public record SystemHealthResponse(
            String api,
            String db,
            String redis,
            String externalBookApi,
            String cdn,
            long reviewQueueBacklog,
            long moderationBacklog,
            boolean maintenanceMode
    ) {
    }

    public record AdminMemberResponse(
            String userId,
            String email,
            String name,
            String role,
            String team,
            boolean active,
            boolean mfaEnrolled,
            Instant lastSignInAt
    ) {

        public static AdminMemberResponse from(User user) {
            return new AdminMemberResponse(
                    String.valueOf(user.getId()),
                    user.getEmail(),
                    user.getNickname(),
                    user.getRole(),
                    "운영",
                    "ACTIVE".equalsIgnoreCase(user.getStatus()),
                    false,
                    user.getLastLoginAt()
            );
        }
    }

    public record AdminRoleRequest(
            @NotBlank(message = "role은 필수입니다.")
            String role,
            @Size(max = 500, message = "reason은 최대 500자입니다.")
            String reason
    ) {
    }

    public record AnnouncementResponse(
            String id,
            String title,
            String body,
            String audience,
            Instant startsAt,
            Instant endsAt,
            boolean urgent,
            String createdByUserId,
            Instant createdAt
    ) {

        public static AnnouncementResponse from(AdminAnnouncement announcement) {
            return new AnnouncementResponse(
                    String.valueOf(announcement.getId()),
                    announcement.getTitle(),
                    announcement.getBody(),
                    announcement.getAudience(),
                    announcement.getStartsAt(),
                    announcement.getEndsAt(),
                    announcement.isUrgent(),
                    String.valueOf(announcement.getCreatedByUserId()),
                    announcement.getCreatedAt()
            );
        }
    }

    public record CreateAnnouncementRequest(
            @NotBlank(message = "title은 필수입니다.")
            @Size(max = 150, message = "title은 최대 150자입니다.")
            String title,
            @NotBlank(message = "body는 필수입니다.")
            String body,
            @NotBlank(message = "audience는 필수입니다.")
            String audience,
            Instant startsAt,
            Instant endsAt,
            boolean urgent
    ) {
    }

    public record PolicyVersionResponse(
            String id,
            String policyType,
            String version,
            String title,
            String body,
            Instant effectiveAt,
            boolean active,
            String publishedByUserId,
            Instant createdAt
    ) {

        public static PolicyVersionResponse from(PolicyVersion policyVersion) {
            return new PolicyVersionResponse(
                    String.valueOf(policyVersion.getId()),
                    policyVersion.getPolicyType(),
                    policyVersion.getVersion(),
                    policyVersion.getTitle(),
                    policyVersion.getBody(),
                    policyVersion.getEffectiveAt(),
                    policyVersion.isActive(),
                    policyVersion.getPublishedByUserId() == null ? null : String.valueOf(policyVersion.getPublishedByUserId()),
                    policyVersion.getCreatedAt()
            );
        }
    }

    public record MaintenanceRequest(
            boolean enabled,
            @Size(max = 500, message = "message는 최대 500자입니다.")
            String message
    ) {
    }

    public record MaintenanceResponse(
            String id,
            boolean enabled,
            String message,
            String updatedByUserId,
            Instant updatedAt
    ) {

        public static MaintenanceResponse from(MaintenanceSetting setting) {
            return new MaintenanceResponse(
                    String.valueOf(setting.getId()),
                    setting.isEnabled(),
                    setting.getMessage(),
                    String.valueOf(setting.getUpdatedByUserId()),
                    setting.getUpdatedAt()
            );
        }
    }

    public record AuditLogResponse(
            String id,
            String actorUserId,
            String actorEmail,
            String action,
            String targetType,
            String targetId,
            String before,
            String after,
            String reason,
            Instant occurredAt,
            String ip
    ) {

        public static AuditLogResponse from(AdminAuditLog log, String actorEmail) {
            return new AuditLogResponse(
                    String.valueOf(log.getId()),
                    String.valueOf(log.getActorUserId()),
                    actorEmail,
                    log.getAction(),
                    log.getTargetType(),
                    log.getTargetId() == null ? null : String.valueOf(log.getTargetId()),
                    log.getBeforePayload(),
                    log.getAfterPayload(),
                    log.getReason(),
                    log.getCreatedAt(),
                    log.getIpAddress()
            );
        }
    }

    public record SearchItemResponse(
            String id,
            String type,
            String title,
            String subtitle,
            String href
    ) {
    }

    public record LimitRequest(
            @Min(value = 1, message = "limit은 1 이상입니다.")
            int limit
    ) {
    }
}
