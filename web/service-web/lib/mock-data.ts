import type {
  Account,
  AdCampaign,
  AdSection,
  Asset,
  BankAccountChangeRequest,
  Book,
  DashboardSummary,
  EscrowBalance,
  IsbnLookupResult,
  NotificationItem,
  NotificationPrefs,
  ReviewItem,
  SectionMarketStats,
  SettlementHistoryRow,
  SettlementSummary,
  StatsCategoryRow,
  StatsContentRow,
  StatsKpi,
  StatsPeriod,
  StatsScope,
  StatsTimeseriesPoint,
  SupportTicket,
  WithdrawRequest,
} from "./types";

export const mockAccount: Account = {
  id: "acc_001",
  email: "creator@example.com",
  supplierTypes: ["publisher", "designer"],
  businessNumber: "123-45-67890",
  businessName: "도트셀프 출판",
  representativeName: "김지호",
  status: "active",
  createdAt: "2026-02-14T09:00:00.000Z",
};

export const mockDashboard: DashboardSummary = {
  monthlyRevenuePages: 482_350,
  monthlyRevenueWon: 4_823_500,
  monthlySales: 1_204,
  pendingReviews: 3,
  upcomingPayoutWon: 1_620_000,
  nextPayoutDate: "2026-04-28T00:00:00.000Z",
  activeCampaigns: 5,
};

export const mockReviews: ReviewItem[] = [
  {
    id: "rv_01",
    type: "book",
    title: "느린 화요일의 정원",
    status: "in_review",
    submittedAt: "2026-04-24T11:30:00.000Z",
  },
  {
    id: "rv_02",
    type: "asset",
    title: "초저녁 책장 스킨 v2",
    status: "submitted",
    submittedAt: "2026-04-25T15:12:00.000Z",
  },
  {
    id: "rv_03",
    type: "ad",
    title: "에세이 섹션 - 04월 5주차",
    status: "rejected",
    submittedAt: "2026-04-23T09:45:00.000Z",
  },
];

export const mockNotifications: NotificationItem[] = [
  {
    id: "nt_01",
    category: "review",
    title: "「느린 화요일의 정원」 검토가 시작됐어요",
    body: "평균 SLA 1.5영업일 내 결과를 알려드릴게요.",
    createdAt: "2026-04-26T10:02:00.000Z",
    read: false,
  },
  {
    id: "nt_02",
    category: "payout",
    title: "정산 1,620,000원이 4/28(화) 입금 예정",
    body: "예치 은행 KB국민은행 *** 1234.",
    createdAt: "2026-04-25T18:00:00.000Z",
    read: false,
  },
  {
    id: "nt_03",
    category: "ad_guardrail",
    title: "에세이 섹션 점유율 28% 도달",
    body: "광고주당 점유 한도(30%)에 근접하여 노출이 일부 제한될 수 있어요.",
    createdAt: "2026-04-25T13:14:00.000Z",
    read: true,
  },
];

export const mockSettlement: SettlementSummary = {
  heldWon: 5_120_000,
  pendingWon: 1_620_000,
  paidWon: 12_480_000,
  bankName: "KB국민은행",
  accountMasked: "*** **** 1234",
  nextPayoutDate: "2026-04-28T00:00:00.000Z",
};

export const mockBooks: Book[] = [
  {
    id: "bk_001",
    isbn: "9788954682152",
    title: "느린 화요일의 정원",
    author: "한지민",
    publisher: "도트셀프 출판",
    synopsis:
      "도시의 가장자리에서 자란 한 식물학자가 매주 화요일마다 자신의 정원을 일기처럼 기록한다.",
    coverImageUrl: null,
    category: "essay",
    ageRating: "all",
    pricePages: 1200,
    saleStartDate: null,
    marketingCopy: "한 사람의 일주일을 식물의 시선으로 다시 쓴 에세이.",
    coverMode: "self_upload",
    consignmentStage: "not_applicable",
    rejectionReason: null,
    status: "in_review",
    submittedAt: "2026-04-24T11:30:00.000Z",
    updatedAt: "2026-04-24T11:30:00.000Z",
    lastPriceChangedAt: null,
    previewSampleName: "neulin_chapter1.pdf",
    previewSamplePageCount: 24,
  },
  {
    id: "bk_002",
    isbn: "9791168472334",
    title: "도시의 작은 도서관",
    author: "이서윤",
    publisher: "도트셀프 출판",
    synopsis:
      "마을 도서관에서 일하는 사서가 만난 사람들의 이야기를 엮은 단편집.",
    coverImageUrl: null,
    category: "novel",
    ageRating: "12+",
    pricePages: 1500,
    saleStartDate: "2026-03-01T00:00:00.000Z",
    marketingCopy: null,
    coverMode: "consignment",
    consignmentStage: "applied",
    rejectionReason: null,
    status: "on_sale",
    submittedAt: "2026-02-12T09:00:00.000Z",
    updatedAt: "2026-03-10T08:00:00.000Z",
    lastPriceChangedAt: null,
    previewSampleName: "city_library_preview.pdf",
    previewSamplePageCount: 18,
  },
  {
    id: "bk_003",
    isbn: null,
    title: "디자이너의 밤 — 자가출판",
    author: "김지호",
    publisher: "셀프퍼블리시",
    synopsis: "야근하는 디자이너의 자기 위로 만화.",
    coverImageUrl: null,
    category: "comics",
    ageRating: "15+",
    pricePages: 800,
    saleStartDate: null,
    marketingCopy: null,
    coverMode: "self_upload",
    consignmentStage: "not_applicable",
    rejectionReason: null,
    status: "draft",
    submittedAt: "2026-04-25T14:00:00.000Z",
    updatedAt: "2026-04-26T10:30:00.000Z",
    lastPriceChangedAt: null,
    previewSampleName: null,
    previewSamplePageCount: null,
  },
  {
    id: "bk_004",
    isbn: "9788937462931",
    title: "여름의 알리바이",
    author: "박해우",
    publisher: "도트셀프 출판",
    synopsis: "8월의 어느 사라진 사건을 좇는 미스터리 단편.",
    coverImageUrl: null,
    category: "novel",
    ageRating: "15+",
    pricePages: 1100,
    saleStartDate: null,
    marketingCopy: null,
    coverMode: "self_upload",
    consignmentStage: "not_applicable",
    rejectionReason:
      "표지 텍스처 해상도가 가이드라인(2048px) 미만입니다. 4096px 이상으로 재업로드 후 재신청해주세요.",
    status: "rejected",
    submittedAt: "2026-04-20T08:00:00.000Z",
    updatedAt: "2026-04-22T17:30:00.000Z",
    lastPriceChangedAt: null,
    previewSampleName: "summer_alibi_chapter1.pdf",
    previewSamplePageCount: 22,
  },
  {
    id: "bk_005",
    isbn: "9788966263400",
    title: "월요일의 우리들",
    author: "정세호",
    publisher: "도트셀프 출판",
    synopsis: "월요일이 두려운 직장인을 위한 짧은 위로 모음.",
    coverImageUrl: null,
    category: "essay",
    ageRating: "all",
    pricePages: 900,
    saleStartDate: "2026-01-15T00:00:00.000Z",
    marketingCopy: "주말이 끝나도 무너지지 않기 위해.",
    coverMode: "consignment",
    consignmentStage: "scanning",
    rejectionReason: null,
    status: "on_sale",
    submittedAt: "2026-01-02T10:00:00.000Z",
    updatedAt: "2026-04-15T12:00:00.000Z",
    lastPriceChangedAt: "2026-04-10T09:00:00.000Z",
    previewSampleName: "monday_us_preview.pdf",
    previewSamplePageCount: 16,
  },
];

/** ISBN 입력 → fake match. 실제론 알라딘/카카오 API. */
export const isbnFixtures: Record<string, IsbnLookupResult> = {
  "9788954682152": {
    isbn: "9788954682152",
    title: "느린 화요일의 정원",
    author: "한지민",
    publisher: "도트셀프 출판",
    coverImageUrl: "",
    synopsis:
      "도시의 가장자리에서 자란 한 식물학자가 매주 화요일마다 자신의 정원을 일기처럼 기록한다.",
  },
  "9791168472334": {
    isbn: "9791168472334",
    title: "도시의 작은 도서관",
    author: "이서윤",
    publisher: "도트셀프 출판",
    coverImageUrl: "",
    synopsis:
      "마을 도서관에서 일하는 사서가 만난 사람들의 이야기를 엮은 단편집.",
  },
  "9788937462931": {
    isbn: "9788937462931",
    title: "여름의 알리바이",
    author: "박해우",
    publisher: "도트셀프 출판",
    coverImageUrl: "",
    synopsis: "8월의 어느 사라진 사건을 좇는 미스터리 단편.",
  },
};

// ---------------------------------------------------------------------------
// Ads
// ---------------------------------------------------------------------------

export const mockEscrow: EscrowBalance = {
  balanceWon: 480_000,
  custodianBankName: "KB국민은행 (분리 보관)",
  lastChargedAt: "2026-04-22T10:30:00.000Z",
};

/**
 * 섹션(category × ageRating) 별 시장 정보. 광고주당 익명 집계만 노출.
 * key: `${category}|${ageRating}`
 */
export const mockSectionMarket: Record<string, SectionMarketStats> = {
  "essay|all": {
    avgWeeklyBidWon: 80_000,
    minBidWon: 30_000,
    currentPoolWon: 320_000,
    avgCtr: 0.034,
    advertiserCount: 6,
  },
  "novel|12+": {
    avgWeeklyBidWon: 110_000,
    minBidWon: 40_000,
    currentPoolWon: 540_000,
    avgCtr: 0.041,
    advertiserCount: 9,
  },
  "novel|15+": {
    avgWeeklyBidWon: 95_000,
    minBidWon: 35_000,
    currentPoolWon: 410_000,
    avgCtr: 0.038,
    advertiserCount: 7,
  },
  "self_dev|all": {
    avgWeeklyBidWon: 130_000,
    minBidWon: 50_000,
    currentPoolWon: 720_000,
    avgCtr: 0.028,
    advertiserCount: 11,
  },
  "comics|15+": {
    avgWeeklyBidWon: 70_000,
    minBidWon: 25_000,
    currentPoolWon: 230_000,
    avgCtr: 0.052,
    advertiserCount: 4,
  },
};

export function sectionKey(s: AdSection) {
  return `${s.category}|${s.ageRating}`;
}

export const mockCampaigns: AdCampaign[] = [
  {
    id: "ad_001",
    bookId: "bk_002",
    bookTitle: "도시의 작은 도서관",
    bookCoverUrl: null,
    section: { category: "novel", ageRating: "12+" },
    weeks: [
      "2026-04-20",
      "2026-04-27",
      "2026-05-04",
      "2026-05-11",
    ],
    weeklyBidWon: 90_000,
    copy: "사서가 만난 사람들의 이야기, 이번 주.",
    status: "running",
    rejectionReason: null,
    submittedAt: "2026-04-15T09:00:00.000Z",
    updatedAt: "2026-04-22T10:00:00.000Z",
    weekStats: [
      {
        weekStart: "2026-04-20",
        weeklyBidWon: 90_000,
        impressions: 18_240,
        clicks: 712,
        conversions: 96,
        ctr: 0.039,
        shareOfVoice: 0.21,
        guardrails: [],
      },
      {
        weekStart: "2026-04-27",
        weeklyBidWon: 90_000,
        impressions: 6_120,
        clicks: 248,
        conversions: 31,
        ctr: 0.041,
        shareOfVoice: 0.28,
        guardrails: ["share_threshold"],
      },
    ],
  },
  {
    id: "ad_002",
    bookId: "bk_005",
    bookTitle: "월요일의 우리들",
    bookCoverUrl: null,
    section: { category: "essay", ageRating: "all" },
    weeks: ["2026-04-27", "2026-05-04"],
    weeklyBidWon: 60_000,
    copy: null,
    status: "in_review",
    rejectionReason: null,
    submittedAt: "2026-04-25T11:20:00.000Z",
    updatedAt: "2026-04-25T11:20:00.000Z",
    weekStats: [],
  },
  {
    id: "ad_003",
    bookId: "bk_002",
    bookTitle: "도시의 작은 도서관",
    bookCoverUrl: null,
    section: { category: "essay", ageRating: "all" },
    weeks: ["2026-04-13", "2026-04-20"],
    weeklyBidWon: 50_000,
    copy: "에세이 섹션 — 04월 5주차",
    status: "rejected",
    rejectionReason:
      "섹션 매칭 위반: 책의 카테고리(소설)와 광고 섹션(에세이)이 일치하지 않습니다.",
    submittedAt: "2026-04-23T09:45:00.000Z",
    updatedAt: "2026-04-23T17:00:00.000Z",
    weekStats: [],
  },
];

// ---------------------------------------------------------------------------
// Assets
// ---------------------------------------------------------------------------

export const mockAssets: Asset[] = [
  {
    id: "as_001",
    name: "초저녁 책장 스킨",
    nameEn: "Twilight Shelf Skin",
    category: "shelf_skin",
    compatTargetIds: ["shelf_basic_5", "shelf_basic_3"],
    themeSlots: null,
    description:
      "해질녘의 따뜻한 톤으로 책장을 감싸는 우드 텍스처. 5단·3단 기본 책장에 적용.",
    pricePages: 600,
    previewFrontUrl: null,
    previewSideUrl: null,
    motionPreviewName: "twilight_rotation.mp4",
    modelFileName: "twilight_shelf.usdz",
    texturesFileName: "twilight_textures.zip",
    licenses: [{ source: "자체 제작", type: "self" }],
    status: "submitted",
    rejectionReason: null,
    submittedAt: "2026-04-25T15:12:00.000Z",
    updatedAt: "2026-04-25T15:12:00.000Z",
    lastPriceChangedAt: null,
  },
  {
    id: "as_002",
    name: "포스트잇 — 일상 시리즈",
    nameEn: "Postit — Daily Series",
    category: "postit",
    compatTargetIds: ["postit_panel"],
    themeSlots: null,
    description: "출근길에 떠오르는 짧은 메모용 포스트잇 12종 세트.",
    pricePages: 200,
    previewFrontUrl: null,
    previewSideUrl: null,
    motionPreviewName: "postit_demo.gif",
    modelFileName: "postit_pack.glb",
    texturesFileName: "postit_textures.zip",
    licenses: [{ source: "자체 제작", type: "self" }],
    status: "on_sale",
    rejectionReason: null,
    submittedAt: "2026-03-10T10:00:00.000Z",
    updatedAt: "2026-04-12T11:00:00.000Z",
    lastPriceChangedAt: null,
  },
  {
    id: "as_003",
    name: "Linen 룸 (전체 테마)",
    nameEn: "Linen Room — Full Theme",
    category: "full_theme",
    compatTargetIds: [],
    themeSlots: {
      shelf_skin: "as_th_shelf_linen",
      dashboard_skin: "as_th_dash_linen",
      wallpaper: "as_th_wall_linen",
      floor: "as_th_floor_linen",
    },
    description:
      "리넨 텍스처로 통일한 차분한 룸 테마. 책장·대시보드·벽지·바닥 4개 슬롯 패키지.",
    pricePages: 2500,
    previewFrontUrl: null,
    previewSideUrl: null,
    motionPreviewName: null,
    modelFileName: "linen_room_bundle.usdz",
    texturesFileName: "linen_textures.zip",
    licenses: [
      { source: "자체 제작", type: "self" },
      {
        source: "linen_pattern.com — Standard License",
        type: "paid",
      },
    ],
    status: "in_review",
    rejectionReason: null,
    submittedAt: "2026-04-22T14:00:00.000Z",
    updatedAt: "2026-04-22T14:00:00.000Z",
    lastPriceChangedAt: null,
  },
  {
    id: "as_004",
    name: "노란 펜던트 조명",
    nameEn: "Yellow Pendant Light",
    category: "lighting",
    compatTargetIds: ["light_pendant"],
    themeSlots: null,
    description: "따뜻한 색온도(2700K)의 펜던트 조명. 야간 모드에서 강조 적용.",
    pricePages: 350,
    previewFrontUrl: null,
    previewSideUrl: null,
    motionPreviewName: null,
    modelFileName: "pendant_yellow.glb",
    texturesFileName: "pendant_textures.zip",
    licenses: [{ source: "자체 제작", type: "self" }],
    status: "rejected",
    rejectionReason:
      "정면·측면 미리보기 이미지가 첨부되지 않았습니다. 두 각도 모두 PNG로 재첨부 후 재신청해주세요.",
    submittedAt: "2026-04-18T09:00:00.000Z",
    updatedAt: "2026-04-19T17:00:00.000Z",
    lastPriceChangedAt: null,
  },
];

// ---------------------------------------------------------------------------
// Stats — 30 daily points, ~12 weekly, 6 monthly
// ---------------------------------------------------------------------------

function ymd(d: Date) {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}

function seedRandom(seed: number) {
  return () => {
    seed = (seed * 9301 + 49297) % 233280;
    return seed / 233280;
  };
}

/** 결정적 mock 시계열을 만든다(seed 고정). */
function buildTimeseries(
  period: StatsPeriod,
  scope: StatsScope,
): StatsTimeseriesPoint[] {
  const len = period === "daily" ? 30 : period === "weekly" ? 12 : 6;
  const stride = period === "daily" ? 1 : period === "weekly" ? 7 : 30;
  const baseRevenue =
    scope === "asset" ? 8_000 : scope === "book" ? 12_000 : 18_000;
  const rand = seedRandom(period === "daily" ? 42 : period === "weekly" ? 17 : 9);
  const today = new Date("2026-04-26T00:00:00.000Z");
  const result: StatsTimeseriesPoint[] = [];
  for (let i = len - 1; i >= 0; i--) {
    const d = new Date(today);
    d.setUTCDate(today.getUTCDate() - i * stride);
    const wave =
      0.7 + 0.5 * Math.sin((i / len) * Math.PI * 2) + (rand() - 0.5) * 0.4;
    const revenuePages = Math.max(
      0,
      Math.round(baseRevenue * wave * (period === "monthly" ? 30 : period === "weekly" ? 7 : 1)),
    );
    const adShare = 0.35 + (rand() - 0.5) * 0.2;
    const adRevenuePages = Math.round(revenuePages * adShare);
    const organicRevenuePages = revenuePages - adRevenuePages;
    const impressions = Math.round(revenuePages * (4 + rand() * 4));
    const conversions = Math.round(revenuePages / (8 + rand() * 4));
    result.push({
      date: ymd(d),
      revenuePages,
      adRevenuePages,
      organicRevenuePages,
      impressions,
      conversions,
    });
  }
  return result;
}

export function getMockTimeseries(
  period: StatsPeriod,
  scope: StatsScope,
): StatsTimeseriesPoint[] {
  return buildTimeseries(period, scope);
}

export function getMockKpi(scope: StatsScope): StatsKpi {
  if (scope === "book") {
    return {
      revenuePages: 312_400,
      revenueWon: 3_124_000,
      sales: 798,
      adRoi: 2.4,
      activeCampaigns: 4,
      categoryPercentile: 0.74,
      prevRevenueChange: 0.18,
    };
  }
  if (scope === "asset") {
    return {
      revenuePages: 169_950,
      revenueWon: 1_699_500,
      sales: 406,
      adRoi: 1.6,
      activeCampaigns: 1,
      categoryPercentile: 0.62,
      prevRevenueChange: 0.09,
    };
  }
  return {
    revenuePages: 482_350,
    revenueWon: 4_823_500,
    sales: 1_204,
    adRoi: 2.1,
    activeCampaigns: 5,
    categoryPercentile: 0.71,
    prevRevenueChange: 0.15,
  };
}

export const mockCategoryBreakdown: StatsCategoryRow[] = [
  { category: "에세이", scope: "book", revenuePages: 142_000, sales: 360, percentile: 0.78 },
  { category: "소설", scope: "book", revenuePages: 128_000, sales: 312, percentile: 0.7 },
  { category: "만화", scope: "book", revenuePages: 42_400, sales: 126, percentile: 0.55 },
  { category: "책장 스킨", scope: "asset", revenuePages: 78_500, sales: 198, percentile: 0.81 },
  { category: "포스트잇", scope: "asset", revenuePages: 36_200, sales: 124, percentile: 0.49 },
  { category: "전체 테마", scope: "asset", revenuePages: 55_250, sales: 84, percentile: 0.68 },
];

export const mockContentRows: StatsContentRow[] = [
  {
    id: "bk_002",
    title: "도시의 작은 도서관",
    scope: "book",
    category: "소설",
    revenuePages: 92_400,
    sales: 232,
    adRoi: 2.8,
  },
  {
    id: "bk_005",
    title: "월요일의 우리들",
    scope: "book",
    category: "에세이",
    revenuePages: 84_200,
    sales: 220,
    adRoi: 2.1,
  },
  {
    id: "as_002",
    title: "포스트잇 — 일상 시리즈",
    scope: "asset",
    category: "포스트잇",
    revenuePages: 36_200,
    sales: 124,
    adRoi: null,
  },
  {
    id: "as_th",
    title: "Linen 룸 — Full Theme",
    scope: "asset",
    category: "전체 테마",
    revenuePages: 55_250,
    sales: 84,
    adRoi: 1.4,
  },
];

// ---------------------------------------------------------------------------
// Settlement: withdraw history, change-account requests
// ---------------------------------------------------------------------------

export const mockWithdrawRequests: WithdrawRequest[] = [
  {
    id: "wd_001",
    amountWon: 1_200_000,
    requestedAt: "2026-04-15T09:00:00.000Z",
    status: "paid",
    expectedPayoutDate: "2026-04-16T00:00:00.000Z",
  },
];

export const mockSettlementHistory: SettlementHistoryRow[] = [
  {
    id: "sh_2026_03",
    period: "2026-03",
    amountWon: 3_840_000,
    paidAt: "2026-04-01T10:00:00.000Z",
    taxInvoiceIssued: true,
  },
  {
    id: "sh_2026_02",
    period: "2026-02",
    amountWon: 2_910_000,
    paidAt: "2026-03-04T10:00:00.000Z",
    taxInvoiceIssued: true,
  },
  {
    id: "sh_2026_01",
    period: "2026-01",
    amountWon: 2_180_000,
    paidAt: "2026-02-03T10:00:00.000Z",
    taxInvoiceIssued: true,
  },
];

export const mockBankChangeRequests: BankAccountChangeRequest[] = [];

// ---------------------------------------------------------------------------
// Support tickets
// ---------------------------------------------------------------------------

export const mockTickets: SupportTicket[] = [
  {
    id: "tk_001",
    category: "ad",
    subject: "에세이 섹션 매칭 위반 반려에 대해",
    body:
      "책 카테고리(에세이)와 광고 섹션이 일치한다고 생각했는데 반려가 났습니다. 어떤 기준인지 알려주실 수 있나요?",
    attachmentNames: [],
    status: "answered",
    createdAt: "2026-04-23T14:00:00.000Z",
    updatedAt: "2026-04-24T11:00:00.000Z",
    replies: [
      {
        id: "rp_001",
        author: "admin",
        body: "광고 섹션은 책 메타의 카테고리·연령과 정확히 일치해야 합니다. 해당 책은 데이터상 '소설'로 등록되어 있어 에세이 섹션 입찰이 차단됐습니다. 메타를 변경하려면 별도 검수가 필요합니다.",
        createdAt: "2026-04-24T11:00:00.000Z",
      },
    ],
  },
  {
    id: "tk_002",
    category: "settlement",
    subject: "이번 주 화요일 자동 정산이 누락된 것 같아요",
    body: "4/22(화) 정산이 입금되지 않았습니다. 확인 부탁드립니다.",
    attachmentNames: ["transfer_check.pdf"],
    status: "open",
    createdAt: "2026-04-23T09:00:00.000Z",
    updatedAt: "2026-04-23T09:00:00.000Z",
    replies: [],
  },
];

// ---------------------------------------------------------------------------
// Notification preferences
// ---------------------------------------------------------------------------

export const defaultNotificationPrefs: NotificationPrefs = {
  review: { inapp: true, email: true, sms: false },
  payout: { inapp: true, email: true, sms: true },
  ad_guardrail: { inapp: true, email: false, sms: false },
  report: { inapp: true, email: true, sms: false },
  system: { inapp: true, email: false, sms: false },
};
