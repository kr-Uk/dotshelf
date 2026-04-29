export type SupplierType = "publisher" | "author" | "designer";

export const SUPPLIER_TYPE_LABEL: Record<SupplierType, string> = {
  publisher: "출판사",
  author: "작가",
  designer: "꾸미기 디자이너",
};

export type AccountStatus = "pending" | "active" | "rejected" | "suspended";

export interface Account {
  id: string;
  email: string;
  supplierTypes: SupplierType[];
  businessNumber: string;
  businessName: string;
  representativeName: string;
  status: AccountStatus;
  createdAt: string;
}

export type ReviewStatus =
  | "draft"
  | "submitted"
  | "in_review"
  | "approved"
  | "rejected";

export const REVIEW_STATUS_LABEL: Record<ReviewStatus, string> = {
  draft: "초안",
  submitted: "접수",
  in_review: "검토 중",
  approved: "승인",
  rejected: "반려",
};

export type BookSaleStatus =
  | ReviewStatus
  | "on_sale"
  | "off_sale";

export const BOOK_STATUS_LABEL: Record<BookSaleStatus, string> = {
  draft: "초안",
  submitted: "접수",
  in_review: "검토 중",
  approved: "승인",
  rejected: "반려",
  on_sale: "판매 중",
  off_sale: "판매 중지",
};

export type BookCategory =
  | "novel"
  | "essay"
  | "self_dev"
  | "comics"
  | "humanities"
  | "kids"
  | "etc";

export const BOOK_CATEGORY_LABEL: Record<BookCategory, string> = {
  novel: "소설",
  essay: "에세이",
  self_dev: "자기계발",
  comics: "만화",
  humanities: "인문",
  kids: "아동",
  etc: "기타",
};

export type AgeRating = "all" | "12+" | "15+" | "19+";

export const AGE_RATING_LABEL: Record<AgeRating, string> = {
  all: "전 연령",
  "12+": "12+",
  "15+": "15+",
  "19+": "19+",
};

/**
 * 위탁 3D 제작 단계.
 * `not_applicable`: 직접 업로드(Option A).
 * 나머지는 노션 §3 4 (B) 위탁 제작 트래킹 단계.
 */
export type ConsignmentStage =
  | "not_applicable"
  | "requested"
  | "shipping_label_issued"
  | "received"
  | "scanning"
  | "review"
  | "applied";

export const CONSIGNMENT_STAGE_LABEL: Record<ConsignmentStage, string> = {
  not_applicable: "—",
  requested: "신청 접수",
  shipping_label_issued: "송장 발급",
  received: "수령",
  scanning: "스캐닝",
  review: "검수",
  applied: "적용 완료",
};

export type CoverMode = "self_upload" | "consignment";

export interface Book {
  id: string;
  isbn: string | null; // null = 자가출판(수동 입력)
  title: string;
  author: string;
  publisher: string;
  synopsis: string;
  coverImageUrl: string | null;
  category: BookCategory;
  ageRating: AgeRating;
  pricePages: number;
  saleStartDate: string | null;
  marketingCopy: string | null;
  coverMode: CoverMode;
  consignmentStage: ConsignmentStage;
  rejectionReason: string | null;
  status: BookSaleStatus;
  submittedAt: string;
  updatedAt: string;
  // 마지막 가격 변경 시각 — 24시간 쿨다운 계산용
  lastPriceChangedAt: string | null;
  /**
   * 청약철회 면책 1요건 — 미리보기 N페이지 PDF/EPUB.
   * 등록 시 첨부, 검수 통과 시 사용자 앱에 노출되어 청약철회 면책 근거가 된다.
   */
  previewSampleName: string | null;
  /** 미리보기 페이지 수(약). */
  previewSamplePageCount: number | null;
}

export interface IsbnLookupResult {
  isbn: string;
  title: string;
  author: string;
  publisher: string;
  coverImageUrl: string;
  synopsis: string;
}

export interface BookListFilter {
  status?: BookSaleStatus | "all";
  q?: string;
  from?: string;
  to?: string;
}

// ---------------------------------------------------------------------------
// Ads
// ---------------------------------------------------------------------------

export type AdCampaignStatus =
  | "draft"
  | "in_review"
  | "running"
  | "ended"
  | "rejected"
  | "paused";

export const AD_CAMPAIGN_STATUS_LABEL: Record<AdCampaignStatus, string> = {
  draft: "초안",
  in_review: "검토 중",
  running: "진행 중",
  ended: "종료",
  rejected: "반려",
  paused: "일시정지",
};

export interface AdSection {
  category: BookCategory;
  ageRating: AgeRating;
}

export interface SectionMarketStats {
  /** 지난 4주 평균 주간 입찰금(원). */
  avgWeeklyBidWon: number;
  /** 섹션 최소 입찰가(원). */
  minBidWon: number;
  /** 현재 섹션 풀(주간 누적 입찰 합산, 원). */
  currentPoolWon: number;
  /** 지난 4주 평균 CTR (0~1). */
  avgCtr: number;
  /** 풀에 참여 중인 광고주 수(익명 집계). */
  advertiserCount: number;
}

export type GuardrailKind =
  | "share_threshold"
  | "weekly_spend_cap"
  | "frequency_cap"
  | "low_ctr";

export const GUARDRAIL_LABEL: Record<GuardrailKind, string> = {
  share_threshold: "섹션 점유율 25~30% 도달",
  weekly_spend_cap: "주간 지출 상한 도달",
  frequency_cap: "반복 노출 상한",
  low_ctr: "CTR 낮음 — 다른 섹션 추천",
};

export interface CampaignWeekStats {
  /** ISO 주의 월요일 (YYYY-MM-DD). */
  weekStart: string;
  weeklyBidWon: number;
  impressions: number;
  clicks: number;
  conversions: number;
  /** 0~1. */
  ctr: number;
  /** 0~1. 광고주당 섹션 점유 비율 (가드레일과 비교). */
  shareOfVoice: number;
  guardrails: GuardrailKind[];
}

export interface AdCampaign {
  id: string;
  bookId: string;
  bookTitle: string;
  bookCoverUrl: string | null;
  section: AdSection;
  /** ISO 주의 월요일들 (YYYY-MM-DD), 최대 8주. */
  weeks: string[];
  /** 주간 입찰 금액 (원, 다음 주차에 적용될 base 입찰가). */
  weeklyBidWon: number;
  /** 1줄 카피 (선택). */
  copy: string | null;
  status: AdCampaignStatus;
  rejectionReason: string | null;
  weekStats: CampaignWeekStats[];
  submittedAt: string;
  updatedAt: string;
}

export interface EscrowBalance {
  balanceWon: number;
  /** 분리 보관 은행 표시 (전자금융 컴플라이언스 안내용). */
  custodianBankName: string;
  /** 마지막 충전 시각. */
  lastChargedAt: string | null;
}

// ---------------------------------------------------------------------------
// Assets (커스텀 꾸미기 에셋)
// ---------------------------------------------------------------------------

/** 사용자 앱 §에셋 정의의 카테고리 키와 동일. */
export type AssetCategory =
  | "shelf_skin"
  | "dashboard_skin"
  | "laptop_skin"
  | "desk_skin"
  | "lighting"
  | "wallpaper"
  | "floor"
  | "wall_decor"
  | "floor_decor"
  | "postit"
  | "full_theme"
  | "shelf_object";

export const ASSET_CATEGORY_LABEL: Record<AssetCategory, string> = {
  shelf_skin: "책장 스킨",
  dashboard_skin: "대시보드 스킨",
  laptop_skin: "노트북 스킨",
  desk_skin: "책상 스킨",
  lighting: "내·외부 조명",
  wallpaper: "벽지",
  floor: "바닥",
  wall_decor: "벽 걸이",
  floor_decor: "바닥 놓기",
  postit: "포스트잇",
  full_theme: "전체 테마",
  shelf_object: "책장 내 오브젝트",
};

/** 다중 슬롯 의존이 있는 카테고리. 전체 테마는 여러 카테고리 결합. */
export const ASSET_THEME_REQUIRED_SLOTS: AssetCategory[] = [
  "shelf_skin",
  "dashboard_skin",
  "wallpaper",
  "floor",
];

/** 호환 타겟 (책장 본체 모델 ID 등). v1.0은 mock id로 표시. */
export interface CompatTarget {
  id: string;
  label: string;
}

export const COMPAT_TARGETS: Record<AssetCategory, CompatTarget[]> = {
  shelf_skin: [
    { id: "shelf_basic_5", label: "기본 책장 5단" },
    { id: "shelf_basic_3", label: "기본 책장 3단" },
    { id: "shelf_corner", label: "코너 책장" },
  ],
  dashboard_skin: [
    { id: "dash_classic", label: "클래식 대시보드" },
    { id: "dash_minimal", label: "미니멀 대시보드" },
  ],
  laptop_skin: [{ id: "laptop_13", label: "노트북 13인치 (기본)" }],
  desk_skin: [{ id: "desk_default", label: "기본 책상" }],
  lighting: [
    { id: "light_pendant", label: "펜던트 라이트" },
    { id: "light_floor", label: "플로어 램프" },
  ],
  wallpaper: [{ id: "room_default", label: "기본 룸 벽" }],
  floor: [{ id: "room_default", label: "기본 룸 바닥" }],
  wall_decor: [{ id: "wall_default", label: "기본 벽" }],
  floor_decor: [{ id: "floor_default", label: "기본 바닥" }],
  postit: [{ id: "postit_panel", label: "포스트잇 패널" }],
  full_theme: [],
  shelf_object: [
    { id: "shelf_basic_5", label: "기본 책장 5단" },
    { id: "shelf_basic_3", label: "기본 책장 3단" },
  ],
};

export type AssetSaleStatus = BookSaleStatus;

export interface ExternalLicense {
  /** 참조한 외부 에셋 출처 / 라이선스 종류. */
  source: string;
  type: "cc0" | "paid" | "self";
}

// ---------------------------------------------------------------------------
// Stats
// ---------------------------------------------------------------------------

export type StatsPeriod = "daily" | "weekly" | "monthly";

export type StatsScope = "all" | "book" | "asset";

export interface StatsKpi {
  /** 선택 기간 매출 (페이지 단위). */
  revenuePages: number;
  /** 원화 환산. */
  revenueWon: number;
  /** 선택 기간 판매 건수. */
  sales: number;
  /** 광고 ROI = (광고 유입 매출) / (광고 지출). */
  adRoi: number;
  /** 활성 캠페인 수. */
  activeCampaigns: number;
  /** 동일 카테고리 내 백분위(0~1). */
  categoryPercentile: number;
  /** 전기 대비 매출 변화율 (예: 0.18 = +18%). */
  prevRevenueChange: number;
}

export interface StatsTimeseriesPoint {
  /** YYYY-MM-DD 또는 주/월 시작일. */
  date: string;
  /** 페이지 단위 매출. */
  revenuePages: number;
  /** 노출 (광고 합산). */
  impressions: number;
  /** 전환 (책 추가). */
  conversions: number;
  /** 광고 유입 매출 (페이지). */
  adRevenuePages: number;
  /** 오가닉 매출 (페이지). */
  organicRevenuePages: number;
}

export interface StatsCategoryRow {
  category: string;
  /** 책/에셋 모두 가능. */
  scope: StatsScope;
  revenuePages: number;
  sales: number;
  /** 동일 카테고리 광고주 평균 대비 백분위. */
  percentile: number;
}

export interface StatsContentRow {
  id: string;
  title: string;
  scope: "book" | "asset";
  category: string;
  revenuePages: number;
  sales: number;
  adRoi: number | null;
}

// ---------------------------------------------------------------------------
// Assets (커스텀 꾸미기 에셋)
// ---------------------------------------------------------------------------

export interface Asset {
  id: string;
  name: string; // 표시명 (한국어 기본)
  nameEn: string | null; // 다국어 (영어)
  category: AssetCategory;
  /** 호환 타겟 ID 목록. */
  compatTargetIds: string[];
  /** full_theme일 경우 의존 카테고리별 에셋 ID. */
  themeSlots: Partial<Record<AssetCategory, string>> | null;
  description: string;
  pricePages: number;
  previewFrontUrl: string | null;
  previewSideUrl: string | null;
  /** 모션 프리뷰 (GIF/MP4) — 청약철회 면책 강화용. 회전 또는 짧은 데모 영상. */
  motionPreviewName: string | null;
  modelFileName: string | null;
  texturesFileName: string | null;
  licenses: ExternalLicense[];
  status: AssetSaleStatus;
  rejectionReason: string | null;
  submittedAt: string;
  updatedAt: string;
  lastPriceChangedAt: string | null;
}

export interface DashboardSummary {
  monthlyRevenuePages: number;
  monthlyRevenueWon: number;
  monthlySales: number;
  pendingReviews: number;
  upcomingPayoutWon: number;
  nextPayoutDate: string;
  activeCampaigns: number;
}

export interface ReviewItem {
  id: string;
  type: "book" | "asset" | "ad";
  title: string;
  status: ReviewStatus;
  submittedAt: string;
}

export interface NotificationItem {
  id: string;
  category:
    | "review"
    | "payout"
    | "ad_guardrail"
    | "report"
    | "system";
  title: string;
  body: string;
  createdAt: string;
  read: boolean;
}

export interface SettlementSummary {
  heldWon: number;
  pendingWon: number;
  paidWon: number;
  bankName: string;
  accountMasked: string;
  nextPayoutDate: string;
}

export type WithdrawRequestStatus =
  | "pending"
  | "processing"
  | "paid"
  | "failed";

export const WITHDRAW_STATUS_LABEL: Record<WithdrawRequestStatus, string> = {
  pending: "신청 접수",
  processing: "처리 중",
  paid: "입금 완료",
  failed: "실패",
};

export interface WithdrawRequest {
  id: string;
  amountWon: number;
  requestedAt: string;
  status: WithdrawRequestStatus;
  expectedPayoutDate: string;
}

export interface SettlementHistoryRow {
  id: string;
  /** 정산 회차 (월별). YYYY-MM. */
  period: string;
  amountWon: number;
  paidAt: string;
  taxInvoiceIssued: boolean;
}

export type BankAccountChangeStatus = "pending" | "approved" | "rejected";

export interface BankAccountChangeRequest {
  id: string;
  bankName: string;
  accountNumber: string; // masked
  accountHolder: string;
  status: BankAccountChangeStatus;
  requestedAt: string;
}

// ---------------------------------------------------------------------------
// Support tickets (1:1 문의)
// ---------------------------------------------------------------------------

export type SupportTicketCategory =
  | "signup"
  | "registration"
  | "ad"
  | "settlement"
  | "etc";

export const SUPPORT_TICKET_CATEGORY_LABEL: Record<
  SupportTicketCategory,
  string
> = {
  signup: "가입",
  registration: "등록",
  ad: "광고",
  settlement: "정산",
  etc: "기타",
};

export type SupportTicketStatus = "open" | "answered" | "closed";

export const SUPPORT_TICKET_STATUS_LABEL: Record<
  SupportTicketStatus,
  string
> = {
  open: "답변 대기",
  answered: "답변 완료",
  closed: "종료",
};

export interface SupportTicketReply {
  id: string;
  /** "user" 또는 "admin". */
  author: "user" | "admin";
  body: string;
  createdAt: string;
}

export interface SupportTicket {
  id: string;
  category: SupportTicketCategory;
  subject: string;
  body: string;
  attachmentNames: string[];
  status: SupportTicketStatus;
  createdAt: string;
  updatedAt: string;
  replies: SupportTicketReply[];
}

// ---------------------------------------------------------------------------
// Notification preferences (카테고리 × 채널 토글)
// ---------------------------------------------------------------------------

export type NotificationChannel = "inapp" | "email" | "sms";

export const NOTIFICATION_CHANNEL_LABEL: Record<NotificationChannel, string> = {
  inapp: "인앱",
  email: "이메일",
  sms: "SMS",
};

export type NotificationCategoryKey = NotificationItem["category"];

export const NOTIFICATION_CATEGORY_LABEL: Record<
  NotificationCategoryKey,
  string
> = {
  review: "검수 결과",
  payout: "정산",
  ad_guardrail: "광고 가드레일",
  report: "신고",
  system: "시스템 공지",
};

export type NotificationPrefs = Record<
  NotificationCategoryKey,
  Record<NotificationChannel, boolean>
>;
