import type {
  AccountingReconciliation,
  AdminAccount,
  AdminMember,
  AdminNotification,
  AnnouncementItem,
  AuditLogEntry,
  AutoPayoutBatch,
  BankAccountChangeRequest,
  CreatorPayoutSummary,
  DashboardStats,
  KpiSnapshot,
  KpiTimePoint,
  KpiTimeseries,
  PolicyVersion,
  QuarterlyAttestation,
  ReportItem,
  ReportSeverity,
  ReviewAppeal,
  ReviewItem,
  ReviewNote,
  SignupApplication,
  SystemHealth,
  UserHistoryItem,
  UserNote,
  UserPurchase,
  UserSummary,
  WithdrawalRequest,
} from "./types";

export const mockAdminAccount: AdminAccount = {
  id: "u_admin_001",
  email: "kim.jiho@dotshelf.kr",
  name: "김지호",
  role: "super_admin",
  team: "운영팀",
  ipAllowlisted: true,
  mfaEnrolled: true,
  lastSignInAt: "2026-04-26T08:14:00.000Z",
};

export const mockDashboardStats: DashboardStats = {
  moderationQueue: { open: 27, autoFlagged: 4, p1: 1 },
  settlement: {
    pendingPayoutWon: 38_420_000,
    reconciliationOk: true,
  },
  ops: {
    dau: 12_482,
    prepaidBalanceWon: 184_320_000,
    apiErrorRate: 0.0042,
  },
};

export const mockReviews: ReviewItem[] = [
  {
    id: "rv_book_01",
    type: "book",
    title: "느린 화요일의 정원",
    applicantName: "도트셀프 출판",
    applicantId: "cr_001",
    status: "submitted",
    submittedAt: "2026-04-25T03:14:00.000Z",
    slaDueAt: "2026-04-28T03:14:00.000Z",
    claimedBy: null,
    autoChecks: [
      { name: "ISBN 진위", result: "pass" },
      { name: "표지 해상도", result: "pass" },
      { name: "라이선스 증빙", result: "warn", note: "계약서 서명일 누락" },
      { name: "중복 ISBN", result: "pass" },
    ],
    detail: {
      kind: "book",
      isbn: "9788956609959",
      publisher: "도트셀프 출판",
      licenseProof: "license_2026_q2.pdf",
      consignmentStage: null,
    },
  },
  {
    id: "rv_book_02",
    type: "book",
    title: "마음이 바람을 닮을 때",
    applicantName: "초저녁 작가스튜디오",
    applicantId: "cr_005",
    status: "in_review",
    submittedAt: "2026-04-24T22:31:00.000Z",
    slaDueAt: "2026-04-27T22:31:00.000Z",
    claimedBy: "박서연",
    autoChecks: [
      { name: "ISBN 진위", result: "pass" },
      { name: "표지 해상도", result: "pass" },
      { name: "라이선스 증빙", result: "pass" },
      { name: "중복 ISBN", result: "pass" },
    ],
    detail: {
      kind: "book",
      isbn: "9791168472341",
      publisher: "초저녁 출판",
      licenseProof: "self_publishing_grant.pdf",
      consignmentStage: "scanning",
    },
  },
  {
    id: "rv_asset_01",
    type: "asset",
    title: "초저녁 책장 스킨 v2",
    applicantName: "스튜디오 모로",
    applicantId: "cr_022",
    status: "in_review",
    submittedAt: "2026-04-25T15:20:00.000Z",
    slaDueAt: "2026-04-28T15:20:00.000Z",
    claimedBy: "이도현",
    autoChecks: [
      { name: "폴리곤 수", result: "pass" },
      { name: "텍스처 해상도", result: "pass" },
      { name: "NSFW 스캔", result: "pass" },
      { name: "카테고리 호환성", result: "pass" },
    ],
    detail: {
      kind: "asset",
      category: "책장 스킨",
      compatibleTargets: ["bookshelf-basic-01", "bookshelf-pine-02"],
      preview: "preview_dusk_skin_v2.png",
    },
  },
  {
    id: "rv_asset_02",
    type: "asset",
    title: "노을빛 벽지",
    applicantName: "디자이너 김여울",
    applicantId: "cr_018",
    status: "submitted",
    submittedAt: "2026-04-26T08:02:00.000Z",
    slaDueAt: "2026-04-29T08:02:00.000Z",
    claimedBy: null,
    autoChecks: [
      { name: "텍스처 해상도", result: "fail", note: "1024px 미만" },
      { name: "NSFW 스캔", result: "pass" },
    ],
    detail: {
      kind: "asset",
      category: "벽지",
      compatibleTargets: ["wall-default"],
      preview: "preview_dusk_wallpaper.png",
    },
  },
  {
    id: "rv_ad_01",
    type: "ad",
    title: "에세이 04월 5주차 - 느린 화요일의 정원",
    applicantName: "도트셀프 출판",
    applicantId: "cr_001",
    status: "submitted",
    submittedAt: "2026-04-25T11:08:00.000Z",
    slaDueAt: "2026-04-26T11:08:00.000Z",
    claimedBy: null,
    autoChecks: [
      { name: "카피 부적절", result: "pass" },
      { name: "장르·연령 매칭", result: "pass" },
      { name: "평점 미달", result: "warn", note: "평점 데이터 없음(신간)" },
    ],
    detail: {
      kind: "ad",
      bookTitle: "느린 화요일의 정원",
      section: "에세이 · 전 연령",
      weeklyBidWon: 480_000,
      copy: "느린 화요일, 당신의 책장에서.",
      guardrailWarnings: [],
    },
  },
];

export const mockReviewNotes: ReviewNote[] = [
  {
    id: "rn_001",
    reviewId: "rv_book_02",
    authorName: "박서연",
    authorRole: "content_reviewer",
    body: "위탁 스캔 1차 결과는 깔끔. 마감 처리만 한 번 더 확인해 드릴게요.",
    createdAt: "2026-04-26T01:14:00.000Z",
  },
  {
    id: "rn_002",
    reviewId: "rv_book_02",
    authorName: "김지호",
    authorRole: "super_admin",
    body: "출판사 라이선스 만료일이 2026-09-30이라 자동 알림 등록 부탁.",
    createdAt: "2026-04-26T05:02:00.000Z",
  },
  {
    id: "rn_003",
    reviewId: "rv_asset_02",
    authorName: "이도현",
    authorRole: "content_reviewer",
    body: "텍스처 1024px 미만 — 재업로드 가이드와 함께 반려 추천.",
    createdAt: "2026-04-26T08:21:00.000Z",
  },
];

export const mockReviewAppeals: ReviewAppeal[] = [
  {
    id: "ra_001",
    reviewId: "rv_ad_01",
    reason: "신간이라 평점 데이터가 없을 뿐 가드레일 위반은 아닙니다. 재검토 부탁드립니다.",
    submittedAt: "2026-04-26T07:05:00.000Z",
    status: "reassigned",
    reassignedTo: "박서연",
  },
];

export const mockSignups: SignupApplication[] = [
  {
    id: "su_001",
    businessNumber: "123-45-67890",
    businessName: "초저녁 출판",
    representativeName: "이수민",
    supplierTypes: ["publisher", "author"],
    email: "soomin@dusk.kr",
    status: "pending",
    submittedAt: "2026-04-26T01:32:00.000Z",
    ntsStatus: "active",
    riskFlags: [],
  },
  {
    id: "su_002",
    businessNumber: "987-65-43210",
    businessName: "스튜디오 모로",
    representativeName: "박지원",
    supplierTypes: ["designer"],
    email: "jiwon@moro.studio",
    status: "info_requested",
    submittedAt: "2026-04-24T08:11:00.000Z",
    ntsStatus: "active",
    riskFlags: ["통장 사본 사업자명 불일치"],
  },
  {
    id: "su_003",
    businessNumber: "555-12-22220",
    businessName: "오랜낮 출판",
    representativeName: "한도윤",
    supplierTypes: ["publisher"],
    email: "doyoon@oraennat.kr",
    status: "pending",
    submittedAt: "2026-04-26T05:18:00.000Z",
    ntsStatus: "closed",
    riskFlags: ["국세청 휴업 상태", "동일 사업자번호 재신청"],
  },
];

// SLA 타이머가 의미를 갖도록 — '지금'을 기준으로 한 상대 시각으로 만든 mock 헬퍼.
function isoMinutesAgo(min: number): string {
  return new Date(Date.now() - min * 60_000).toISOString();
}

export const mockReports: ReportItem[] = [
  {
    id: "rp_001",
    targetType: "comment",
    targetExcerpt: "아니 그딴 책을 읽어요? ㅋ 진짜 어이없네…",
    targetAuthorId: "u_2841",
    targetAuthorName: "moonbear92",
    reporterCount: 7,
    reporterReputation: 0.78,
    keywords: ["혐오", "조롱"],
    nsfwScore: 0.04,
    status: "open",
    severity: "p2",
    createdAt: isoMinutesAgo(60 * 18 + 12), // ≈ 5h 48m 남음
  },
  {
    id: "rp_002",
    targetType: "guestbook",
    targetExcerpt: "[연락처] 010-****-**** 으로 연락주세요",
    targetAuthorId: "u_5520",
    targetAuthorName: "mintsoda",
    reporterCount: 3,
    reporterReputation: 0.66,
    keywords: ["개인정보 노출"],
    nsfwScore: 0.0,
    status: "in_review",
    severity: "p1",
    createdAt: isoMinutesAgo(48), // ≈ -12m, 지남 표시 테스트
  },
  {
    id: "rp_003",
    targetType: "space",
    targetExcerpt: "공간 전체를 광고 콘텐츠로 도배",
    targetAuthorId: "u_8190",
    targetAuthorName: "promo_books",
    reporterCount: 12,
    reporterReputation: 0.51,
    keywords: ["스팸", "광고"],
    nsfwScore: 0.0,
    status: "open",
    severity: "p3",
    createdAt: isoMinutesAgo(60 * 32), // ≈ 40h 남음 (P3)
  },
  {
    id: "rp_004",
    targetType: "reading_log",
    targetExcerpt: "(자해 묘사 포함)",
    targetAuthorId: "u_7011",
    targetAuthorName: "leaf_journal",
    reporterCount: 5,
    reporterReputation: 0.81,
    keywords: ["자해", "안전"],
    nsfwScore: 0.34,
    status: "open",
    severity: "p1",
    createdAt: isoMinutesAgo(18), // ≈ 42m 남음 (P1)
  },
  {
    id: "rp_005",
    targetType: "comment",
    targetExcerpt: "이 사람 댓글 다 신고 가능. 보면 같은 패턴.",
    targetAuthorId: "u_8190",
    targetAuthorName: "promo_books",
    reporterCount: 8,
    reporterReputation: 0.6,
    keywords: ["스팸"],
    nsfwScore: 0.0,
    status: "open",
    severity: "p3",
    createdAt: isoMinutesAgo(60 * 6),
  },
  {
    id: "rp_006",
    targetType: "guestbook",
    targetExcerpt: "[연락처] 010-****-**** 으로 디엠 주세요!",
    targetAuthorId: "u_5520",
    targetAuthorName: "mintsoda",
    reporterCount: 2,
    reporterReputation: 0.7,
    keywords: ["개인정보 노출"],
    nsfwScore: 0.0,
    status: "open",
    severity: "p2",
    createdAt: isoMinutesAgo(60 * 8),
  },
];

// 모더레이션 SLA — P1 1h / P2 24h / P3 72h 정책
export function reportSlaDueAt(severity: ReportSeverity, createdAt: string): string {
  const offsetH = severity === "p1" ? 1 : severity === "p2" ? 24 : 72;
  return new Date(new Date(createdAt).getTime() + offsetH * 3_600_000).toISOString();
}

export const mockNotifications: AdminNotification[] = [
  {
    id: "no_001",
    kind: "moderation_p1",
    title: "P1 신고 1건 접수 — 자해 묘사 가능",
    body: "독서기록 신고. 1시간 SLA — 즉시 검토 권장.",
    href: "/moderation",
    createdAt: "2026-04-26T11:01:00.000Z",
    read: false,
  },
  {
    id: "no_002",
    kind: "review_sla",
    title: "광고 심사 SLA 임박 (3건)",
    body: "에세이 04월 5주차 캠페인 외 2건이 12시간 내 응답 필요.",
    href: "/reviews",
    createdAt: "2026-04-26T09:30:00.000Z",
    read: false,
  },
  {
    id: "no_003",
    kind: "settlement_mismatch",
    title: "회계 대조 — 분기 1회 외부 검증 일정",
    body: "다음 분기 회계사무소 확인서 일정 등록 필요.",
    href: "/settlements",
    createdAt: "2026-04-26T07:00:00.000Z",
    read: true,
  },
  {
    id: "no_004",
    kind: "ip_violation",
    title: "외부 IP 로그인 시도 — admin@dotshelf.kr",
    body: "사무실/VPN 외부에서 SSO 시도. 자동 차단됨.",
    href: "/audit",
    createdAt: "2026-04-26T05:42:00.000Z",
    read: true,
  },
  {
    id: "no_005",
    kind: "report_spike",
    title: "공간 신고 급증 — 최근 3시간",
    body: "promo_books 사용자 공간에 12건 누적. 패턴 검토 필요.",
    href: "/moderation",
    createdAt: "2026-04-25T18:42:00.000Z",
    read: true,
  },
  {
    id: "no_006",
    kind: "system_health",
    title: "외부 도서 API(Aladin) 응답 지연",
    body: "최근 30분 평균 응답 1.8s — degraded 상태로 표기됨.",
    href: "/operations",
    createdAt: "2026-04-25T16:10:00.000Z",
    read: true,
  },
];

export const mockUsers: UserSummary[] = [
  {
    id: "u_2841",
    nickname: "moonbear92",
    email: "moonbear92@example.com",
    joinedAt: "2025-11-09T11:00:00.000Z",
    lastSeenAt: "2026-04-26T07:14:00.000Z",
    state: "active",
    socialProvider: "google",
    shelves: 3,
    books: 84,
    reportsAgainst: 4,
    pageBalance: 1240,
  },
  {
    id: "u_5520",
    nickname: "mintsoda",
    email: "mint@dotshelf.example",
    joinedAt: "2026-02-04T02:30:00.000Z",
    lastSeenAt: "2026-04-26T10:01:00.000Z",
    state: "suspended_temp",
    socialProvider: "kakao",
    shelves: 2,
    books: 41,
    reportsAgainst: 6,
    pageBalance: 320,
  },
  {
    id: "u_7011",
    nickname: "leaf_journal",
    email: "leaf@example.com",
    joinedAt: "2025-08-22T22:11:00.000Z",
    lastSeenAt: "2026-04-25T23:50:00.000Z",
    state: "active",
    socialProvider: "apple",
    shelves: 5,
    books: 162,
    reportsAgainst: 1,
    pageBalance: 0,
  },
];

// 사용자별 CS 메모 / 구매 / 신고·차단 이력
export const mockUserNotes: UserNote[] = [
  {
    id: "un_001",
    userId: "u_5520",
    authorName: "강현수",
    authorRole: "cs",
    body: "방명록 개인정보 노출 반복. 1차 경고 후 7일 정지 진행 중.",
    createdAt: "2026-04-26T10:25:00.000Z",
  },
  {
    id: "un_002",
    userId: "u_5520",
    authorName: "박서연",
    authorRole: "moderator",
    body: "재발 시 영구 정지 후보. 같은 패턴 신고 6건 누적.",
    createdAt: "2026-04-26T10:32:00.000Z",
  },
  {
    id: "un_003",
    userId: "u_2841",
    authorName: "최민서",
    authorRole: "moderator",
    body: "혐오·조롱 키워드. 가림 처리 후 경고 1회 발송.",
    createdAt: "2026-04-26T10:40:00.000Z",
  },
];

export const mockUserPurchases: UserPurchase[] = [
  {
    id: "up_001",
    userId: "u_2841",
    kind: "page_charge",
    itemName: "페이지 충전 50,000p",
    amountWon: 500_000,
    pages: 50_000,
    purchasedAt: "2026-04-20T11:14:00.000Z",
    refunded: false,
  },
  {
    id: "up_002",
    userId: "u_2841",
    kind: "book_cover",
    itemName: "「느린 화요일의 정원」 유료 표지",
    amountWon: 8_000,
    pages: 800,
    purchasedAt: "2026-04-22T18:42:00.000Z",
    refunded: false,
  },
  {
    id: "up_003",
    userId: "u_2841",
    kind: "asset",
    itemName: "초저녁 책장 스킨 v2",
    amountWon: 12_000,
    pages: 1_200,
    purchasedAt: "2026-04-25T20:01:00.000Z",
    refunded: true,
  },
  {
    id: "up_004",
    userId: "u_5520",
    kind: "page_charge",
    itemName: "페이지 충전 10,000p",
    amountWon: 100_000,
    pages: 10_000,
    purchasedAt: "2026-04-18T08:30:00.000Z",
    refunded: false,
  },
];

export const mockUserHistory: UserHistoryItem[] = [
  {
    id: "uh_001",
    userId: "u_2841",
    kind: "reported_by_others",
    excerpt: "댓글 — 혐오·조롱 표현으로 7건 신고 누적",
    occurredAt: "2026-04-26T09:42:00.000Z",
  },
  {
    id: "uh_002",
    userId: "u_2841",
    kind: "blocked_others",
    excerpt: "@noisy_user 차단",
    occurredAt: "2026-04-22T13:01:00.000Z",
  },
  {
    id: "uh_003",
    userId: "u_5520",
    kind: "reported_by_others",
    excerpt: "방명록 — 개인정보 노출로 6건 신고 누적",
    occurredAt: "2026-04-26T10:11:00.000Z",
  },
  {
    id: "uh_004",
    userId: "u_5520",
    kind: "reported_others",
    excerpt: "@spam_bot — 스팸 신고",
    occurredAt: "2026-04-20T19:21:00.000Z",
  },
];

export const mockBankAccountChanges: BankAccountChangeRequest[] = [
  {
    id: "bc_001",
    creatorName: "초저녁 작가스튜디오",
    creatorId: "cr_005",
    oldBank: "신한은행",
    oldAccountMasked: "*** *** 9921",
    newBank: "KB국민은행",
    newAccountMasked: "*** **** 4408",
    bankbookFile: "bankbook_2026q2.pdf",
    status: "pending_review",
    requestedAt: "2026-04-26T03:14:00.000Z",
  },
  {
    id: "bc_002",
    creatorName: "디자이너 김여울",
    creatorId: "cr_018",
    oldBank: "토스뱅크",
    oldAccountMasked: "*** ** 1140",
    newBank: "토스뱅크",
    newAccountMasked: "*** ** 7782",
    bankbookFile: "bankbook_kim.pdf",
    status: "pending_review",
    requestedAt: "2026-04-25T20:55:00.000Z",
  },
  {
    id: "bc_003",
    creatorName: "스튜디오 모로",
    creatorId: "cr_022",
    oldBank: "우리은행",
    oldAccountMasked: "*** *** 2210",
    newBank: "우리은행",
    newAccountMasked: "*** *** 6603",
    bankbookFile: "moro_bankbook_v2.pdf",
    status: "approved",
    requestedAt: "2026-04-22T10:18:00.000Z",
  },
];

export const mockCreatorPayouts: CreatorPayoutSummary[] = [
  {
    creatorId: "cr_001",
    creatorName: "도트셀프 출판",
    pendingWon: 4_820_000,
    paidThisMonthWon: 18_400_000,
    totalPaidWon: 168_320_000,
    nextPayoutAt: "2026-04-28T01:00:00.000Z",
    bank: "KB국민은행",
    accountMasked: "*** **** 1234",
  },
  {
    creatorId: "cr_005",
    creatorName: "초저녁 작가스튜디오",
    pendingWon: 1_240_000,
    paidThisMonthWon: 4_920_000,
    totalPaidWon: 22_180_000,
    nextPayoutAt: "2026-04-28T01:00:00.000Z",
    bank: "신한은행",
    accountMasked: "*** *** 9921",
  },
  {
    creatorId: "cr_018",
    creatorName: "디자이너 김여울",
    pendingWon: 530_000,
    paidThisMonthWon: 1_180_000,
    totalPaidWon: 6_840_000,
    nextPayoutAt: "2026-04-28T01:00:00.000Z",
    bank: "토스뱅크",
    accountMasked: "*** ** 1140",
  },
  {
    creatorId: "cr_022",
    creatorName: "스튜디오 모로",
    pendingWon: 2_180_000,
    paidThisMonthWon: 3_400_000,
    totalPaidWon: 9_220_000,
    nextPayoutAt: "2026-04-28T01:00:00.000Z",
    bank: "우리은행",
    accountMasked: "*** *** 2210",
  },
];

export const mockQuarterlyAttestations: QuarterlyAttestation[] = [
  {
    id: "qa_2026q1",
    quarter: "2026-Q1",
    attestor: "삼정회계법인",
    publishedAt: "2026-04-15T00:00:00.000Z",
    fileUrl: "attestation_2026q1.pdf",
  },
  {
    id: "qa_2025q4",
    quarter: "2025-Q4",
    attestor: "삼정회계법인",
    publishedAt: "2026-01-12T00:00:00.000Z",
    fileUrl: "attestation_2025q4.pdf",
  },
];

export const mockReconciliation: AccountingReconciliation = {
  prepaidBalanceWon: 184_320_000,
  assetPurchaseWon: 142_900_000,
  pendingPayoutWon: 41_420_000,
  consistent: true,
  asOf: "2026-04-26T07:00:00.000Z",
};

export const mockWithdrawals: WithdrawalRequest[] = [
  {
    id: "wd_001",
    creatorName: "도트셀프 출판",
    amountWon: 4_820_000,
    requestedAt: "2026-04-26T01:14:00.000Z",
    status: "pending",
    identityVerified: true,
  },
  {
    id: "wd_002",
    creatorName: "초저녁 작가스튜디오",
    amountWon: 1_240_000,
    requestedAt: "2026-04-25T22:08:00.000Z",
    status: "first_approved",
    identityVerified: true,
  },
  {
    id: "wd_003",
    creatorName: "디자이너 김여울",
    amountWon: 530_000,
    requestedAt: "2026-04-25T19:55:00.000Z",
    status: "rejected",
    identityVerified: false,
  },
  {
    id: "wd_004",
    creatorName: "스튜디오 모로",
    amountWon: 2_180_000,
    requestedAt: "2026-04-24T13:21:00.000Z",
    status: "remitted",
    identityVerified: true,
  },
];

export const mockPayoutBatches: AutoPayoutBatch[] = [
  {
    id: "pb_2026_w16",
    scheduledAt: "2026-04-21T01:00:00.000Z",
    totalWon: 38_400_000,
    countSucceeded: 132,
    countFailed: 2,
  },
  {
    id: "pb_2026_w17",
    scheduledAt: "2026-04-28T01:00:00.000Z",
    totalWon: 41_420_000,
    countSucceeded: 0,
    countFailed: 0,
  },
];

// 결정적 시계열 — 시드 기반 노이즈로 동일한 데이터를 매번 만든다.
function seededNoise(seed: number): number {
  const x = Math.sin(seed) * 10_000;
  return x - Math.floor(x);
}

function buildDailyKpi(days = 30, base = "2026-04-26"): KpiTimePoint[] {
  const baseDate = new Date(base + "T00:00:00.000Z");
  const points: KpiTimePoint[] = [];
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(baseDate);
    d.setUTCDate(baseDate.getUTCDate() - i);
    const dow = d.getUTCDay(); // 0=일
    const weekend = dow === 0 || dow === 6 ? 1.18 : 1; // 주말 살짝 상승
    const trend = 1 + (days - i) * 0.006; // 30일 누적 약 +18%
    const n = (s: number) => seededNoise(d.getTime() / 1e7 + s);

    const dau = Math.round(8_400 * weekend * trend * (0.92 + n(1) * 0.16));
    const newSignups = Math.round(280 * weekend * trend * (0.85 + n(2) * 0.3));
    const pageGmv = Math.round(6_400_000 * weekend * trend * (0.88 + n(3) * 0.2));
    const bookSales = Math.round(4_900_000 * trend * (0.9 + n(4) * 0.18));
    const adRevenue = Math.round(380_000 * trend * (0.85 + n(5) * 0.3));
    const openModeration = Math.max(
      6,
      Math.round(22 + Math.sin((days - i) / 4) * 6 + n(6) * 8),
    );
    const apiErrorRate = +(0.0035 + n(8) * 0.0035).toFixed(4);

    points.push({
      date: d.toISOString().slice(0, 10),
      dau,
      newSignups,
      pageGmv,
      bookSales,
      adRevenue,
      openModeration,
      apiErrorRate,
    });
  }
  return points;
}

export const mockKpiTimeseries: KpiTimeseries = {
  daily: buildDailyKpi(30),
  categoryRevenue: [
    { category: "소설", won: 84_320_000 },
    { category: "에세이", won: 52_140_000 },
    { category: "자기계발", won: 41_980_000 },
    { category: "만화·웹툰", won: 36_220_000 },
    { category: "인문·사회", won: 18_640_000 },
    { category: "기타", won: 14_980_000 },
  ],
  funnel: [
    { stage: "공간 진입", count: 184_320 },
    { stage: "책장에 책 1권 이상", count: 92_140 },
    { stage: "독서기록 작성", count: 38_220 },
    { stage: "유료 표지 구매", count: 14_980 },
    { stage: "광고/꾸미기 구매", count: 6_420 },
  ],
};

export const mockKpi: KpiSnapshot = {
  dau: 12_482,
  wau: 51_209,
  mau: 184_320,
  newSignups: 482,
  guestConversion: 0.18,
  booksAdded: 3_204,
  openModerationQueue: 27,
  pageGmvWon: 248_320_000,
  bookSalesWon: 184_290_000,
  adRevenueWon: 12_180_000,
  apiErrorRate: 0.0042,
};

export const mockAnnouncements: AnnouncementItem[] = [
  {
    id: "an_001",
    title: "5/2(토) 03:00~04:00 정기 점검 안내",
    body: "정기 점검 시간 동안 일부 서비스 이용이 제한될 수 있습니다.",
    audience: "all_users",
    startsAt: "2026-04-25T05:00:00.000Z",
    publishedAt: "2026-04-25T05:00:00.000Z",
    endsAt: "2026-05-02T04:00:00.000Z",
    isUrgent: false,
  },
  {
    id: "an_002",
    title: "광고 가드레일 정책 일부 개정",
    body: "광고 심사 기준 일부가 개정되었습니다. 상세 내용은 정책 문서를 확인해주세요.",
    audience: "all_users",
    startsAt: "2026-04-22T07:00:00.000Z",
    publishedAt: "2026-04-22T07:00:00.000Z",
    endsAt: "2026-05-22T07:00:00.000Z",
    isUrgent: false,
  },
];

export const mockPolicies: PolicyVersion[] = [
  {
    id: "pl_terms",
    name: "이용약관",
    version: "2026.04.01",
    effectiveAt: "2026-04-01T00:00:00.000Z",
    requiresReConsent: false,
  },
  {
    id: "pl_privacy",
    name: "개인정보 처리방침",
    version: "2026.04.01",
    effectiveAt: "2026-04-01T00:00:00.000Z",
    requiresReConsent: false,
  },
  {
    id: "pl_payout",
    name: "정산 약관",
    version: "2026.05.01",
    effectiveAt: "2026-05-01T00:00:00.000Z",
    requiresReConsent: true,
  },
];

export const mockSystemHealth: SystemHealth = {
  payment: "ok",
  externalBookApi: "degraded",
  moderationBacklog: 29,
  maintenanceMode: false,
};

export const mockAdminMembers: AdminMember[] = [
  {
    id: "u_admin_001",
    email: "kim.jiho@dotshelf.kr",
    name: "김지호",
    role: "super_admin",
    team: "운영팀",
    active: true,
    joinedAt: "2025-09-01T00:00:00.000Z",
    lastSignInAt: "2026-04-26T08:14:00.000Z",
  },
  {
    id: "u_admin_002",
    email: "park.seoyeon@dotshelf.kr",
    name: "박서연",
    role: "content_reviewer",
    team: "콘텐츠팀",
    active: true,
    joinedAt: "2025-11-12T00:00:00.000Z",
    lastSignInAt: "2026-04-26T07:55:00.000Z",
  },
  {
    id: "u_admin_003",
    email: "lee.dohyun@dotshelf.kr",
    name: "이도현",
    role: "settlement",
    team: "재무팀",
    active: true,
    joinedAt: "2025-12-02T00:00:00.000Z",
    lastSignInAt: "2026-04-26T08:42:00.000Z",
  },
  {
    id: "u_admin_004",
    email: "choi.minseo@dotshelf.kr",
    name: "최민서",
    role: "moderator",
    team: "운영팀",
    active: true,
    joinedAt: "2026-01-08T00:00:00.000Z",
    lastSignInAt: "2026-04-25T22:11:00.000Z",
  },
  {
    id: "u_admin_005",
    email: "kang.hyunsoo@dotshelf.kr",
    name: "강현수",
    role: "cs",
    team: "CS팀",
    active: true,
    joinedAt: "2026-02-14T00:00:00.000Z",
    lastSignInAt: "2026-04-26T01:32:00.000Z",
  },
  {
    id: "u_admin_006",
    email: "yoo.taeha@dotshelf.kr",
    name: "유태하",
    role: "read_only",
    team: "법무팀",
    active: true,
    joinedAt: "2026-03-03T00:00:00.000Z",
    lastSignInAt: "2026-04-22T11:44:00.000Z",
  },
];

export const mockAuditLog: AuditLogEntry[] = [
  {
    id: "al_001",
    actorEmail: "kim.jiho@dotshelf.kr",
    actorRole: "super_admin",
    action: "approve_review",
    targetType: "review.book",
    targetId: "rv_book_03",
    before: "in_review",
    after: "approved",
    reason: "라이선스 증빙 보강 확인",
    occurredAt: "2026-04-26T07:55:00.000Z",
    ip: "10.0.12.42",
  },
  {
    id: "al_002",
    actorEmail: "park.seoyeon@dotshelf.kr",
    actorRole: "moderator",
    action: "suspend_user",
    targetType: "user",
    targetId: "u_5520",
    before: "active",
    after: "suspended_temp:7d",
    reason: "방명록 개인정보 노출 반복 신고",
    occurredAt: "2026-04-26T10:18:00.000Z",
    ip: "10.0.12.18",
  },
  {
    id: "al_003",
    actorEmail: "lee.dohyun@dotshelf.kr",
    actorRole: "settlement",
    action: "approve_withdrawal",
    targetType: "withdrawal",
    targetId: "wd_002",
    before: "pending",
    after: "first_approved",
    reason: "본인 인증 통과",
    occurredAt: "2026-04-26T08:42:00.000Z",
    ip: "10.0.12.7",
  },
  {
    id: "al_004",
    actorEmail: "kim.jiho@dotshelf.kr",
    actorRole: "super_admin",
    action: "reject_review",
    targetType: "review.asset",
    targetId: "rv_asset_02",
    before: "in_review",
    after: "rejected:texture_low_resolution",
    reason: "텍스처 1024px 미만 — 재업로드 안내",
    occurredAt: "2026-04-26T09:14:00.000Z",
    ip: "10.0.12.42",
  },
  {
    id: "al_005",
    actorEmail: "choi.minseo@dotshelf.kr",
    actorRole: "moderator",
    action: "hide_content",
    targetType: "report",
    targetId: "rp_001",
    before: "open",
    after: "resolved:hide",
    reason: "혐오·조롱 키워드 매칭 + 7건 누적",
    occurredAt: "2026-04-26T10:32:00.000Z",
    ip: "10.0.12.21",
  },
  {
    id: "al_006",
    actorEmail: "kim.jiho@dotshelf.kr",
    actorRole: "super_admin",
    action: "toggle_maintenance",
    targetType: "system",
    targetId: "frontend",
    before: "maintenance:false",
    after: "maintenance:true",
    reason: "긴급 패치 배포",
    occurredAt: "2026-04-25T22:10:00.000Z",
    ip: "10.0.12.42",
  },
  {
    id: "al_007",
    actorEmail: "kim.jiho@dotshelf.kr",
    actorRole: "super_admin",
    action: "toggle_maintenance",
    targetType: "system",
    targetId: "frontend",
    before: "maintenance:true",
    after: "maintenance:false",
    reason: "패치 완료",
    occurredAt: "2026-04-25T22:48:00.000Z",
    ip: "10.0.12.42",
  },
  {
    id: "al_008",
    actorEmail: "park.seoyeon@dotshelf.kr",
    actorRole: "content_reviewer",
    action: "claim_review",
    targetType: "review.book",
    targetId: "rv_book_02",
    before: "submitted",
    after: "in_review:박서연",
    reason: null,
    occurredAt: "2026-04-25T08:14:00.000Z",
    ip: "10.0.12.18",
  },
  {
    id: "al_009",
    actorEmail: "kang.hyunsoo@dotshelf.kr",
    actorRole: "cs",
    action: "force_logout",
    targetType: "user",
    targetId: "u_7011",
    before: "active",
    after: "active",
    reason: "사용자 보안 사고 신고",
    occurredAt: "2026-04-25T14:02:00.000Z",
    ip: "10.0.12.33",
  },
  {
    id: "al_010",
    actorEmail: "kim.jiho@dotshelf.kr",
    actorRole: "super_admin",
    action: "assign_role",
    targetType: "admin_member",
    targetId: "u_admin_004",
    before: "cs",
    after: "moderator",
    reason: "신규 모더레이션 인력 충원",
    occurredAt: "2026-04-24T03:00:00.000Z",
    ip: "10.0.12.42",
  },
  {
    id: "al_011",
    actorEmail: "park.seoyeon@dotshelf.kr",
    actorRole: "content_reviewer",
    action: "info_request_review",
    targetType: "review.ad",
    targetId: "rv_ad_01",
    before: "in_review",
    after: "info_requested",
    reason: "신간 평점 미달 — 가드레일 시뮬 결과 첨부 요청",
    occurredAt: "2026-04-26T07:00:00.000Z",
    ip: "10.0.12.18",
  },
  {
    id: "al_012",
    actorEmail: "lee.dohyun@dotshelf.kr",
    actorRole: "settlement",
    action: "reject_withdrawal",
    targetType: "withdrawal",
    targetId: "wd_003",
    before: "pending",
    after: "rejected",
    reason: "본인 인증 미통과 — 통장 사본 사업자명 불일치",
    occurredAt: "2026-04-25T20:14:00.000Z",
    ip: "10.0.12.7",
  },
];
