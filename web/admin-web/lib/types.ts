// 관리자 백오피스 도메인 타입 — v1.0 명세 기준.

// --- RBAC ----------------------------------------------------------------
export type AdminRole =
  | "super_admin"
  | "content_reviewer"
  | "moderator"
  | "settlement"
  | "cs"
  | "read_only";

export const ADMIN_ROLE_LABEL: Record<AdminRole, string> = {
  super_admin: "슈퍼 관리자",
  content_reviewer: "콘텐츠 심사자",
  moderator: "모더레이터",
  settlement: "정산 담당",
  cs: "CS 담당",
  read_only: "읽기 전용",
};

export interface AdminAccount {
  id: string;
  email: string;
  name: string;
  role: AdminRole;
  team: string;
  ipAllowlisted: boolean;
  mfaEnrolled: boolean;
  lastSignInAt: string;
}

// --- Dashboard -----------------------------------------------------------
export interface DashboardStats {
  reviewQueue: {
    book: number;
    asset: number;
    ad: number;
    slaAtRisk: number;
  };
  moderationQueue: {
    open: number;
    autoFlagged: number;
    p1: number;
  };
  settlement: {
    pendingPayoutWon: number;
    reconciliationOk: boolean;
    withdrawalRequests: number;
  };
  ops: {
    dau: number;
    prepaidBalanceWon: number;
    apiErrorRate: number;
  };
}

// --- Content Review ------------------------------------------------------
export type ReviewType = "book" | "asset" | "ad";

export const REVIEW_TYPE_LABEL: Record<ReviewType, string> = {
  book: "책",
  asset: "에셋",
  ad: "광고",
};

export type ReviewStatus =
  | "submitted"
  | "in_review"
  | "approved"
  | "rejected"
  | "info_requested"
  | "appealed";

export const REVIEW_STATUS_LABEL: Record<ReviewStatus, string> = {
  submitted: "접수",
  in_review: "검토 중",
  approved: "승인",
  rejected: "반려",
  info_requested: "추가 정보 요청",
  appealed: "이의 제기",
};

export interface ReviewItem {
  id: string;
  type: ReviewType;
  title: string;
  applicantName: string;
  applicantId: string;
  status: ReviewStatus;
  submittedAt: string;
  slaDueAt: string;
  claimedBy: string | null;
  autoChecks: AutoCheckResult[];
  detail: ReviewDetail;
}

export interface AutoCheckResult {
  name: string;
  result: "pass" | "warn" | "fail";
  note?: string;
}

export type ReviewDetail =
  | BookReviewDetail
  | AssetReviewDetail
  | AdReviewDetail;

export interface BookReviewDetail {
  kind: "book";
  isbn: string | null;
  publisher: string;
  licenseProof: string;
  consignmentStage:
    | null
    | "received"
    | "scanning"
    | "post_processing"
    | "applied";
}

export interface AssetReviewDetail {
  kind: "asset";
  category: string;
  compatibleTargets: string[];
  preview: string;
}

export interface AdReviewDetail {
  kind: "ad";
  bookTitle: string;
  section: string;
  weeklyBidWon: number;
  copy: string;
  guardrailWarnings: string[];
}

// --- Review notes / appeals ---------------------------------------------
export interface ReviewNote {
  id: string;
  reviewId: string;
  authorName: string;
  authorRole: AdminRole;
  body: string;
  createdAt: string;
}

export interface ReviewAppeal {
  id: string;
  reviewId: string;
  reason: string;
  submittedAt: string;
  status: "pending" | "reassigned" | "upheld" | "overturned";
  reassignedTo?: string;
}

export type RejectionReason =
  | "license_proof_missing"
  | "duplicate_isbn"
  | "polygon_over_budget"
  | "texture_low_resolution"
  | "nsfw_violation"
  | "category_mismatch"
  | "ad_copy_inappropriate"
  | "ad_genre_age_mismatch"
  | "ad_rating_below_threshold"
  | "other";

export const REJECTION_REASON_LABEL: Record<RejectionReason, string> = {
  license_proof_missing: "라이선스 증빙 미비",
  duplicate_isbn: "ISBN 중복",
  polygon_over_budget: "폴리곤 한도 초과",
  texture_low_resolution: "텍스처 해상도 부족",
  nsfw_violation: "NSFW 정책 위반",
  category_mismatch: "카테고리·연령 매칭 불일치",
  ad_copy_inappropriate: "광고 카피 부적절",
  ad_genre_age_mismatch: "광고 장르·연령 매칭 위반",
  ad_rating_below_threshold: "평점 미달",
  other: "기타 (코멘트 참조)",
};

// --- 가입 거절·자료요청 사유 ---------------------------------------------
export type SignupRejectReason =
  | "business_inactive"
  | "duplicate_application"
  | "documents_invalid"
  | "bankbook_name_mismatch"
  | "blacklist_match"
  | "other";

export const SIGNUP_REJECT_REASON_LABEL: Record<SignupRejectReason, string> = {
  business_inactive: "휴업·폐업 사업자",
  duplicate_application: "동일 사업자 재신청",
  documents_invalid: "서류 검증 실패",
  bankbook_name_mismatch: "통장 사본 사업자명 불일치",
  blacklist_match: "블랙리스트 매칭",
  other: "기타 (코멘트 참조)",
};

// --- 환불 / 잔액 조정 사유 -----------------------------------------------
export type RefundReason =
  | "duplicate_charge"
  | "service_outage"
  | "user_dispute"
  | "fraud_chargeback"
  | "goodwill"
  | "other";

export const REFUND_REASON_LABEL: Record<RefundReason, string> = {
  duplicate_charge: "중복 결제",
  service_outage: "서비스 장애",
  user_dispute: "구매자 분쟁",
  fraud_chargeback: "사기·차지백",
  goodwill: "선의의 환불",
  other: "기타",
};

export type BalanceAdjustReason =
  | "incident_compensation"
  | "promotion_grant"
  | "fraud_clawback"
  | "manual_correction"
  | "other";

export const BALANCE_ADJUST_REASON_LABEL: Record<BalanceAdjustReason, string> = {
  incident_compensation: "장애 보상",
  promotion_grant: "프로모션 지급",
  fraud_clawback: "사기 회수",
  manual_correction: "수기 정정",
  other: "기타",
};

// --- 사용자 부가 정보 (CS 메모 / 구매 / 신고·차단 이력) -------------------
export interface UserNote {
  id: string;
  userId: string;
  authorName: string;
  authorRole: AdminRole;
  body: string;
  createdAt: string;
}

export type UserPurchaseKind = "page_charge" | "book_cover" | "asset" | "ad_deposit";

export const USER_PURCHASE_KIND_LABEL: Record<UserPurchaseKind, string> = {
  page_charge: "페이지 충전",
  book_cover: "유료 표지",
  asset: "꾸미기 에셋",
  ad_deposit: "광고 예치금",
};

export interface UserPurchase {
  id: string;
  userId: string;
  kind: UserPurchaseKind;
  itemName: string;
  amountWon: number;
  pages: number;
  purchasedAt: string;
  refunded: boolean;
}

export type UserHistoryKind = "reported_by_others" | "reported_others" | "blocked_others";

export const USER_HISTORY_KIND_LABEL: Record<UserHistoryKind, string> = {
  reported_by_others: "타인이 신고",
  reported_others: "내가 신고",
  blocked_others: "내가 차단",
};

export interface UserHistoryItem {
  id: string;
  userId: string;
  kind: UserHistoryKind;
  excerpt: string;
  occurredAt: string;
}

// --- 정산 보강 ----------------------------------------------------------
export interface BankAccountChangeRequest {
  id: string;
  creatorName: string;
  creatorId: string;
  oldBank: string;
  oldAccountMasked: string;
  newBank: string;
  newAccountMasked: string;
  bankbookFile: string;
  status: "pending_review" | "approved" | "rejected";
  requestedAt: string;
}

export const BANK_ACCOUNT_CHANGE_STATUS_LABEL: Record<
  BankAccountChangeRequest["status"],
  string
> = {
  pending_review: "검토 대기",
  approved: "승인",
  rejected: "거절",
};

export interface CreatorPayoutSummary {
  creatorId: string;
  creatorName: string;
  pendingWon: number;
  paidThisMonthWon: number;
  totalPaidWon: number;
  nextPayoutAt: string;
  bank: string;
  accountMasked: string;
}

export interface QuarterlyAttestation {
  id: string;
  quarter: string; // "2026-Q1"
  attestor: string;
  publishedAt: string;
  fileUrl: string;
}

// --- Creator Signup ------------------------------------------------------
export type SignupStatus =
  | "pending"
  | "info_requested"
  | "approved"
  | "rejected";

export const SIGNUP_STATUS_LABEL: Record<SignupStatus, string> = {
  pending: "대기",
  info_requested: "추가 자료 요청",
  approved: "승인",
  rejected: "거절",
};

export interface SignupApplication {
  id: string;
  businessNumber: string;
  businessName: string;
  representativeName: string;
  supplierTypes: ("publisher" | "author" | "designer")[];
  email: string;
  status: SignupStatus;
  submittedAt: string;
  ntsStatus: "active" | "closed" | "unknown";
  riskFlags: string[];
}

// --- Moderation ----------------------------------------------------------
export type ReportTargetType =
  | "reading_log"
  | "comment"
  | "guestbook"
  | "profile"
  | "space"
  | "content";

export const REPORT_TARGET_LABEL: Record<ReportTargetType, string> = {
  reading_log: "독서기록",
  comment: "댓글",
  guestbook: "방명록",
  profile: "프로필",
  space: "공간",
  content: "콘텐츠",
};

export type ReportStatus = "open" | "in_review" | "resolved" | "dismissed";

export const REPORT_STATUS_LABEL: Record<ReportStatus, string> = {
  open: "미처리",
  in_review: "검토 중",
  resolved: "조치 완료",
  dismissed: "기각",
};

export type ReportSeverity = "p1" | "p2" | "p3";

export const REPORT_SEVERITY_LABEL: Record<ReportSeverity, string> = {
  p1: "P1 · 1시간 SLA",
  p2: "P2 · 24시간 SLA",
  p3: "P3 · 72시간 SLA",
};

export interface ReportItem {
  id: string;
  targetType: ReportTargetType;
  targetExcerpt: string;
  targetAuthorId: string;
  targetAuthorName: string;
  reporterCount: number;
  reporterReputation: number;
  keywords: string[];
  nsfwScore: number;
  status: ReportStatus;
  severity: ReportSeverity;
  createdAt: string;
}

// --- Users / Spaces ------------------------------------------------------
export type UserState = "active" | "suspended_temp" | "suspended_perm";

export const USER_STATE_LABEL: Record<UserState, string> = {
  active: "정상",
  suspended_temp: "일시 정지",
  suspended_perm: "영구 정지",
};

export interface UserSummary {
  id: string;
  nickname: string;
  email: string;
  joinedAt: string;
  lastSeenAt: string;
  state: UserState;
  socialProvider: "apple" | "google" | "kakao";
  shelves: number;
  books: number;
  reportsAgainst: number;
  pageBalance: number;
}

// --- Settlement ----------------------------------------------------------
export interface AccountingReconciliation {
  prepaidBalanceWon: number;
  bankDepositWon: number;
  pendingPayoutWon: number;
  consistent: boolean;
  asOf: string;
}

export type WithdrawalStatus =
  | "pending"
  | "first_approved"
  | "settlement_approved"
  | "rejected"
  | "remitted";

export const WITHDRAWAL_STATUS_LABEL: Record<WithdrawalStatus, string> = {
  pending: "본인 인증 검증",
  first_approved: "1차 승인",
  settlement_approved: "2차 승인",
  rejected: "거절",
  remitted: "송금 완료",
};

export interface WithdrawalRequest {
  id: string;
  creatorName: string;
  amountWon: number;
  requestedAt: string;
  status: WithdrawalStatus;
  identityVerified: boolean;
}

export interface AutoPayoutBatch {
  id: string;
  scheduledAt: string;
  totalWon: number;
  countSucceeded: number;
  countFailed: number;
}

// --- Stats / KPI ---------------------------------------------------------
export interface KpiTimePoint {
  date: string; // YYYY-MM-DD
  dau: number;
  newSignups: number;
  pageGmv: number;
  bookSales: number;
  adRevenue: number;
  openModeration: number;
  reviewSlaHours: number;
  apiErrorRate: number; // 0~1
  cdnHitRate: number; // 0~1
}

export interface CategoryRevenueSlice {
  category: string;
  won: number;
}

export interface FunnelStage {
  stage: string;
  count: number;
}

export interface KpiTimeseries {
  daily: KpiTimePoint[];
  categoryRevenue: CategoryRevenueSlice[];
  funnel: FunnelStage[];
}

export interface KpiSnapshot {
  dau: number;
  wau: number;
  mau: number;
  newSignups: number;
  guestConversion: number; // 0~1
  booksAdded: number;
  reviewsAvgSlaHours: number;
  openModerationQueue: number;
  pageGmvWon: number;
  bookSalesWon: number;
  adRevenueWon: number;
  apiErrorRate: number; // 0~1
  cdnHitRate: number; // 0~1
}

// --- Operations ----------------------------------------------------------
export interface AnnouncementItem {
  id: string;
  title: string;
  audience: "all_users" | "creators" | "internal";
  publishedAt: string;
  endsAt: string;
  isUrgent: boolean;
}

export interface PolicyVersion {
  id: string;
  name: string;
  version: string;
  effectiveAt: string;
  requiresReConsent: boolean;
}

export interface SystemHealth {
  payment: "ok" | "degraded" | "down";
  externalBookApi: "ok" | "degraded" | "down";
  cdn: "ok" | "degraded" | "down";
  reviewQueueBacklog: number;
  maintenanceMode: boolean;
}

// --- Audit Log -----------------------------------------------------------
// --- Notifications (관리자 본인용) ---------------------------------------
export type AdminNotificationKind =
  | "review_sla"
  | "moderation_p1"
  | "settlement_mismatch"
  | "ip_violation"
  | "report_spike"
  | "system_health";

export const ADMIN_NOTIFICATION_LABEL: Record<AdminNotificationKind, string> = {
  review_sla: "심사 SLA",
  moderation_p1: "P1 신고",
  settlement_mismatch: "회계 불일치",
  ip_violation: "IP 정책 위반",
  report_spike: "신고 급증",
  system_health: "시스템 헬스",
};

export interface AdminNotification {
  id: string;
  kind: AdminNotificationKind;
  title: string;
  body: string;
  href?: string;
  createdAt: string;
  read: boolean;
}

// --- Global Search -------------------------------------------------------
export type SearchHitType = "user" | "signup" | "review" | "report" | "space" | "book";

export const SEARCH_HIT_LABEL: Record<SearchHitType, string> = {
  user: "사용자",
  signup: "가입 신청",
  review: "심사",
  report: "신고",
  space: "공간",
  book: "도서",
};

export interface SearchHit {
  id: string;
  type: SearchHitType;
  title: string;
  subtitle: string;
  href: string;
}

export interface AdminMember {
  id: string;
  email: string;
  name: string;
  role: AdminRole;
  team: string;
  active: boolean;
  joinedAt: string;
  lastSignInAt: string | null;
}

export interface AuditLogEntry {
  id: string;
  actorEmail: string;
  actorRole: AdminRole;
  action: string;
  targetType: string;
  targetId: string;
  before: string | null;
  after: string | null;
  reason: string | null;
  occurredAt: string;
  ip: string;
}
