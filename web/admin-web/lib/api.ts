"use client";

import { loadAuth, touchAuth } from "./auth";
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
  ReportItem,
  ReportSeverity,
  ReportStatus,
  ReportTargetType,
  SearchHit,
  SearchHitType,
  SystemHealth,
  UserHistoryItem,
  UserNote,
  UserPurchase,
  UserSpaceSnapshot,
  UserState,
  UserSummary,
  WithdrawalRequest,
  WithdrawalStatus,
} from "./types";

/**
 * Admin REST client.
 *
 * The UI was originally built against MSW payloads. The backend now exposes
 * `/v1/admin` with ApiResponse envelopes and PageResponse lists, so this layer
 * keeps page components stable while translating the current backend DTOs.
 */

const API_BASE = (
  process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL ?? "/api/admin"
).replace(/\/$/u, "");
const TEST_ADMIN_HEADER =
  process.env.NEXT_PUBLIC_ADMIN_TEST_AUTH_HEADER_NAME ?? "X-Test-Admin-User-Id";
const TEST_ADMIN_USER_ID = process.env.NEXT_PUBLIC_ADMIN_TEST_USER_ID ?? "1004";
const PAGE_TO_WON = 10;

export interface ResponseEnvelope<T> {
  httpStatus: string | number;
  isSuccess?: boolean;
  success?: boolean;
  message: string;
  code: string | number;
  result: T | null;
}

type JsonRecord = Record<string, unknown>;

class ApiError extends Error {
  status: number;
  code?: string | number;

  constructor(status: number, message: string, code?: string | number) {
    super(message);
    this.status = status;
    this.code = code;
    this.name = "ApiError";
  }
}

function isRecord(payload: unknown): payload is JsonRecord {
  return typeof payload === "object" && payload !== null && !Array.isArray(payload);
}

function isEnvelope(payload: unknown): payload is ResponseEnvelope<unknown> {
  if (!isRecord(payload)) return false;
  return ("isSuccess" in payload || "success" in payload) && "code" in payload && "result" in payload;
}

function envelopeSuccess(payload: ResponseEnvelope<unknown>): boolean {
  return payload.isSuccess ?? payload.success ?? false;
}

function methodOf(init: RequestInit): string {
  return (init.method ?? "GET").toUpperCase();
}

function pathOnly(path: string): string {
  const rawPath = path.startsWith("http") ? new URL(path).pathname : path;
  const withoutBase = rawPath.startsWith("/v1/admin")
    ? rawPath.slice("/v1/admin".length)
    : rawPath;
  return withoutBase.split("?")[0] || "/";
}

function withQuery(original: string, nextPath: string): string {
  const queryIndex = original.indexOf("?");
  return queryIndex >= 0 ? `${nextPath}${original.slice(queryIndex)}` : nextPath;
}

function value(source: unknown, key: string): unknown {
  return isRecord(source) ? source[key] : undefined;
}

function nested(source: unknown, key: string): JsonRecord {
  const child = value(source, key);
  return isRecord(child) ? child : {};
}

function text(source: unknown, fallback = ""): string {
  if (typeof source === "string") return source;
  if (source === null || source === undefined) return fallback;
  return String(source);
}

function numberValue(source: unknown, fallback = 0): number {
  const n = typeof source === "number" ? source : Number(source);
  return Number.isFinite(n) ? n : fallback;
}

function booleanValue(source: unknown, fallback = false): boolean {
  return typeof source === "boolean" ? source : fallback;
}

function dateValue(source: unknown, fallback = new Date().toISOString()): string {
  const raw = text(source);
  return raw.length > 0 ? raw : fallback;
}

function nullableDateValue(source: unknown): string | null {
  const raw = text(source);
  return raw.length > 0 ? raw : null;
}

function dateInputToInstant(source: unknown, endOfDay = false): string | null {
  const raw = text(source);
  if (!raw) return null;
  if (raw.includes("T")) return raw;
  return `${raw}T${endOfDay ? "23:59:59" : "00:00:00"}Z`;
}

function normalizeKey(source: unknown): string {
  return text(source).trim().toUpperCase().replace(/-/gu, "_");
}

function parseJsonBody(init: RequestInit): JsonRecord {
  if (typeof init.body !== "string") return {};
  try {
    const parsed = JSON.parse(init.body) as unknown;
    return isRecord(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function withJsonBody(init: RequestInit, body: JsonRecord): RequestInit {
  return { ...init, body: JSON.stringify(body) };
}

function announcementRequestBody(body: JsonRecord): JsonRecord {
  return {
    title: text(body.title),
    body: text(body.body),
    audience: text(body.audience, "all_users"),
    startsAt: dateInputToInstant(body.startsAt),
    endsAt: dateInputToInstant(body.endsAt, true),
    urgent: booleanValue(body.isUrgent ?? body.urgent),
  };
}

function pageItems(payload: unknown): unknown[] {
  if (Array.isArray(payload)) return payload;
  if (!isRecord(payload)) return [];
  const items = payload.items;
  return Array.isArray(items) ? items : [];
}

function pagesToWon(pages: unknown): number {
  return numberValue(pages) * PAGE_TO_WON;
}

function localAdminAccount(): AdminAccount {
  return {
    id: "local-admin",
    email: "admin@dotshelf.local",
    name: "로컬 관리자",
    role: "super_admin",
    team: "운영팀",
    ipAllowlisted: true,
    mfaEnrolled: true,
    lastSignInAt: new Date().toISOString(),
  };
}

function localResponse(path: string, init: RequestInit): { handled: boolean; value?: unknown } {
  const pathname = pathOnly(path);
  const method = methodOf(init);

  if (pathname === "/auth/sso/start" && method === "POST") {
    return {
      handled: true,
      value: {
        challengeId: `local-${Date.now()}`,
        email: localAdminAccount().email,
        ipAllowlisted: true,
      },
    };
  }

  if (pathname === "/auth/mfa/verify" && method === "POST") {
    const body = parseJsonBody(init);
    if (text(body.code) === "000000") {
      throw new ApiError(401, "인증 코드가 일치하지 않습니다.", "LOCAL_MFA_FAILED");
    }
    return {
      handled: true,
      value: {
        token: "admin-local-session",
        account: localAdminAccount(),
      },
    };
  }

  if (pathname === "/notifications" && method === "GET") {
    return { handled: true, value: [] satisfies AdminNotification[] };
  }

  if (pathname === "/notifications/read-all" && method === "POST") {
    return { handled: true, value: { ok: true } };
  }

  if (pathname === "/reviews" && method === "GET") {
    return { handled: true, value: [] };
  }

  if (/^\/reviews\/[^/]+\/notes$/u.test(pathname) && method === "GET") {
    return { handled: true, value: [] };
  }

  if (/^\/reviews\/[^/]+\/appeal$/u.test(pathname) && method === "GET") {
    return { handled: true, value: null };
  }

  if (pathname.startsWith("/reviews/") && method !== "GET") {
    return { handled: true, value: { ok: true } };
  }

  if (pathname === "/signups" && method === "GET") {
    return { handled: true, value: [] };
  }

  if (pathname.startsWith("/signups/") && method !== "GET") {
    return { handled: true, value: { ok: true } };
  }

  return { handled: false };
}

function reportAction(kind: unknown): JsonRecord {
  const normalized = normalizeKey(kind);
  if (normalized === "DISMISS" || normalized === "REJECT") {
    return { action: "REJECT", blindToReporter: false, globallyBlind: false };
  }
  if (normalized === "START_REVIEW") {
    return { action: "START_REVIEW" };
  }
  if (normalized === "UNBLIND") {
    return { action: "UNBLIND", globallyBlind: false };
  }
  return {
    action: "RESOLVE",
    blindToReporter: true,
    globallyBlind: ["HIDE", "DELETE", "FORCE_PRIVATE_SPACE"].includes(normalized),
  };
}

function userAction(kind: unknown): string {
  const normalized = normalizeKey(kind);
  if (normalized === "SUSPEND_7D" || normalized === "SUSPEND_TEMP") return "SUSPEND_TEMP";
  if (normalized === "SUSPEND_PERM") return "SUSPEND_PERM";
  if (normalized === "ANONYMIZE") return "ANONYMIZE";
  if (normalized === "FORCE_LOGOUT") return "FORCE_LOGOUT";
  if (normalized === "RENAME_NICKNAME") return "RENAME_NICKNAME";
  if (normalized === "REQUIRE_RELOGIN") return "REQUIRE_RELOGIN";
  return "ACTIVATE";
}

function backendAdminRole(role: unknown): string {
  return normalizeKey(role) === "READ_ONLY" ? "USER" : "ADMIN";
}

function adaptRequest(path: string, init: RequestInit): { path: string; init: RequestInit } {
  const pathname = pathOnly(path);
  const method = methodOf(init);
  let nextPath = path;
  let nextInit = init;

  if (pathname === "/dashboard/stats") {
    nextPath = withQuery(path, "/dashboard/summary");
  } else if (pathname === "/audit") {
    nextPath = withQuery(path, "/audit-logs");
  } else if (pathname === "/reports/bulk") {
    nextPath = withQuery(path, "/reports/bulk-action");
  }

  if (method === "POST" && /^\/reports\/[^/]+\/action$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      ...reportAction(body.kind ?? body.action),
      reason: text(body.reason, text(body.kind ?? body.action)),
    });
  }

  if (method === "POST" && pathname === "/reports/bulk") {
    const body = parseJsonBody(init);
    const ids = Array.isArray(body.ids)
      ? body.ids.map((id) => Number(id)).filter((id) => Number.isFinite(id))
      : [];
    nextInit = withJsonBody(init, {
      ids,
      action: text(reportAction(body.action).action, "RESOLVE"),
      reason: text(body.reason, text(body.action)),
    });
  }

  if (method === "POST" && /^\/users\/[^/]+\/action$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      action: userAction(body.kind ?? body.action),
      reason: text(body.reason, text(body.kind ?? body.action)),
      nickname: text(body.nickname),
    });
  }

  if (method === "POST" && /^\/users\/[^/]+\/adjust-balance$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      deltaPages: numberValue(body.pages ?? body.deltaPages),
      reason: text(body.reason, "manual_correction"),
      memo: text(body.comment ?? body.memo),
    });
  }

  if (method === "POST" && /^\/users\/[^/]+\/refund$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      purchaseId: text(body.purchaseId),
      reason: text(body.reason, "other"),
      comment: text(body.comment),
    });
  }

  if (method === "POST" && pathname === "/operations/announcements") {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, announcementRequestBody(body));
  }

  if (method === "PUT" && /^\/operations\/announcements\/[^/]+$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, announcementRequestBody(body));
  }

  if (method === "POST" && pathname === "/operations/admins" && nextPath === path) {
    nextInit = init;
  }

  if (method === "POST" && /^\/operations\/admins\/[^/]+\/role$/u.test(pathname)) {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      role: backendAdminRole(body.role),
      reason: text(body.reason, "role_changed_from_admin_web"),
    });
  }

  if (method === "POST" && pathname === "/operations/maintenance") {
    const body = parseJsonBody(init);
    nextInit = withJsonBody(init, {
      enabled: booleanValue(body.enabled, true),
      message: text(body.message, "관리자 콘솔에서 점검 모드를 변경했습니다."),
    });
  }

  return { path: nextPath, init: nextInit };
}

function adminRole(source: unknown): AdminAccount["role"] {
  const normalized = normalizeKey(source);
  if (normalized === "CONTENT_REVIEWER") return "content_reviewer";
  if (normalized === "MODERATOR") return "moderator";
  if (normalized === "SETTLEMENT") return "settlement";
  if (normalized === "CS") return "cs";
  if (normalized === "READ_ONLY" || normalized === "USER") return "read_only";
  return "super_admin";
}

function userState(source: unknown): UserState {
  const normalized = normalizeKey(source);
  if (normalized === "SUSPENDED_TEMP" || normalized === "SUSPEND_TEMP") return "suspended_temp";
  if (normalized === "SUSPENDED_PERM" || normalized === "SUSPEND_PERM") return "suspended_perm";
  return "active";
}

function socialProvider(source: unknown): UserSummary["socialProvider"] {
  const normalized = normalizeKey(source);
  if (normalized === "APPLE") return "apple";
  if (normalized === "GOOGLE") return "google";
  return "kakao";
}

function reportTargetType(source: unknown): ReportTargetType {
  const normalized = normalizeKey(source);
  if (normalized === "READING_RECORD") return "reading_log";
  if (normalized === "READING_RECORD_COMMENT" || normalized === "COMMENT") return "comment";
  if (normalized === "GUESTBOOK") return "guestbook";
  if (normalized === "USER" || normalized === "PROFILE") return "profile";
  if (normalized === "SPACE") return "space";
  return "content";
}

function reportStatus(source: unknown): ReportStatus {
  const normalized = normalizeKey(source);
  if (normalized === "IN_REVIEW") return "in_review";
  if (normalized === "RESOLVED" || normalized === "DONE") return "resolved";
  if (normalized === "REJECTED" || normalized === "DISMISSED") return "dismissed";
  return "open";
}

function reportSeverity(source: unknown): ReportSeverity {
  const normalized = normalizeKey(source);
  if (normalized === "P1") return "p1";
  if (normalized === "P2") return "p2";
  return "p3";
}

function withdrawalStatus(source: unknown): WithdrawalStatus {
  const normalized = normalizeKey(source);
  if (normalized === "FIRST_APPROVED") return "first_approved";
  if (normalized === "SETTLEMENT_APPROVED" || normalized === "APPROVED") return "settlement_approved";
  if (normalized === "REJECTED") return "rejected";
  if (normalized === "REMITTED" || normalized === "PAID" || normalized === "COMPLETED") return "remitted";
  return "pending";
}

function accountChangeStatus(source: unknown): BankAccountChangeRequest["status"] {
  const normalized = normalizeKey(source);
  if (normalized === "APPROVED") return "approved";
  if (normalized === "REJECTED") return "rejected";
  return "pending_review";
}

function healthStatus(source: unknown): SystemHealth["payment"] {
  const normalized = normalizeKey(source);
  if (normalized === "NOT_INTEGRATED" || normalized === "NOTINTEGRATED") return "not_integrated";
  if (normalized === "UNKNOWN" || normalized === "") return "unknown";
  if (normalized === "DOWN" || normalized === "FAIL" || normalized === "ERROR") return "down";
  if (normalized === "DEGRADED" || normalized === "WARN" || normalized === "SLOW") return "degraded";
  return "ok";
}

function searchHitType(source: unknown): SearchHitType {
  const normalized = normalizeKey(source);
  if (normalized === "SIGNUP") return "signup";
  if (normalized === "REVIEW") return "review";
  if (normalized === "REPORT") return "report";
  if (normalized === "SPACE") return "space";
  if (normalized === "BOOK") return "book";
  return "user";
}

function mapDashboardStats(source: unknown): DashboardStats {
  const moderation = nested(source, "moderation");
  const settlement = nested(source, "settlement");
  const ops = nested(source, "ops");
  return {
    moderationQueue: {
      open: numberValue(moderation.open),
      autoFlagged: numberValue(moderation.autoFlagged),
      p1: numberValue(moderation.p1),
    },
    settlement: {
      pendingPayoutWon: pagesToWon(settlement.pendingPayoutPages),
      reconciliationOk: booleanValue(settlement.reconciliationOk, true),
    },
    ops: {
      dau: numberValue(ops.todayVisits),
      prepaidBalanceWon: pagesToWon(settlement.walletBalancePages),
      apiErrorRate: healthStatus(ops.apiStatus) === "ok" ? 0 : 0.02,
    },
  };
}

function mapReport(source: unknown): ReportItem {
  const reasonType = text(value(source, "reasonType"));
  const reasonDetail = text(value(source, "reasonDetail"));
  return {
    id: text(value(source, "id")),
    targetType: reportTargetType(value(source, "targetType")),
    targetExcerpt: text(value(source, "targetExcerpt"), "대상 콘텐츠를 찾을 수 없습니다."),
    targetAuthorId: text(value(source, "targetAuthorId")),
    targetAuthorName: text(value(source, "targetAuthorName"), "알 수 없음"),
    reporterCount: numberValue(value(source, "reporterCount"), 1),
    reporterReputation: 0.84,
    keywords: [reasonType, reasonDetail].filter((item) => item.length > 0).slice(0, 3),
    nsfwScore: reasonType.toUpperCase().includes("NSFW") ? 0.82 : 0.12,
    status: reportStatus(value(source, "status")),
    severity: reportSeverity(value(source, "severity")),
    createdAt: dateValue(value(source, "createdAt")),
  };
}

function mapUser(source: unknown): UserSummary {
  return {
    id: text(value(source, "id")),
    nickname: text(value(source, "nickname"), "이름 없음"),
    email: text(value(source, "email")),
    joinedAt: dateValue(value(source, "joinedAt")),
    lastSeenAt: dateValue(value(source, "lastSeenAt"), dateValue(value(source, "joinedAt"))),
    state: userState(value(source, "state")),
    socialProvider: socialProvider(value(source, "socialProvider")),
    shelves: numberValue(value(source, "bookshelves") ?? value(source, "shelves")),
    books: numberValue(value(source, "books")),
    reportsAgainst: numberValue(value(source, "reportsAgainst")),
    pageBalance: numberValue(value(source, "pageBalance")),
  };
}

function mapPurchase(source: unknown): UserPurchase {
  const pages = numberValue(value(source, "pricePages") ?? value(source, "pages"));
  const status = normalizeKey(value(source, "status"));
  return {
    id: text(value(source, "id")),
    userId: text(value(source, "userId")),
    kind: "asset",
    itemName: `에셋 #${text(value(source, "assetId"), text(value(source, "id")))}`,
    amountWon: pagesToWon(pages),
    pages,
    purchasedAt: dateValue(value(source, "purchasedAt")),
    refunded: ["REFUNDED", "CANCELED", "CANCELLED"].includes(status),
  };
}

function mapUserNote(source: unknown): UserNote {
  return {
    id: text(value(source, "id")),
    userId: text(value(source, "userId")),
    authorName: text(value(source, "authorName"), "관리자"),
    authorRole: "super_admin",
    body: text(value(source, "body")),
    createdAt: dateValue(value(source, "createdAt")),
  };
}

function mapUserHistory(source: unknown): UserHistoryItem {
  const type = normalizeKey(value(source, "type"));
  return {
    id: text(value(source, "id")),
    userId: text(value(source, "userId")),
    kind: type === "BLOCK" ? "blocked_others" : "reported_by_others",
    excerpt: [text(value(source, "title")), text(value(source, "detail"))]
      .filter((item) => item.length > 0)
      .join(" · "),
    occurredAt: dateValue(value(source, "occurredAt")),
  };
}

function mapUserSpace(source: unknown): UserSpaceSnapshot {
  return {
    spaceId: text(value(source, "spaceId")),
    spaceName: text(value(source, "spaceName"), "이름 없는 공간"),
    visibilityScope: text(value(source, "visibilityScope"), "PUBLIC"),
    visitCount: numberValue(value(source, "visitCount")),
    likeCount: numberValue(value(source, "likeCount")),
    bookshelves: numberValue(value(source, "bookshelves")),
    books: numberValue(value(source, "books")),
  };
}

function mapReconciliation(source: unknown): AccountingReconciliation {
  const prepaid = pagesToWon(value(source, "walletBalancePages"));
  const pending = pagesToWon(value(source, "pendingPayoutPages"));
  return {
    prepaidBalanceWon: prepaid,
    assetPurchaseWon: pagesToWon(value(source, "assetPurchasePages")),
    pendingPayoutWon: pending,
    consistent: booleanValue(value(source, "consistent"), true),
    asOf: dateValue(value(source, "asOf")),
  };
}

function mapWithdrawal(source: unknown): WithdrawalRequest {
  return {
    id: text(value(source, "id")),
    creatorName: `Creator #${text(value(source, "creatorUserId"))}`,
    amountWon: pagesToWon(value(source, "requestedPages")),
    requestedAt: dateValue(value(source, "requestedAt")),
    status: withdrawalStatus(value(source, "status")),
    identityVerified: true,
  };
}

function mapPayoutBatch(source: unknown): AutoPayoutBatch {
  const status = normalizeKey(value(source, "status"));
  const count = numberValue(value(source, "withdrawalCount"));
  return {
    id: text(value(source, "batchCode"), text(value(source, "id"))),
    scheduledAt: dateValue(value(source, "scheduledAt")),
    totalWon: pagesToWon(value(source, "totalPages")),
    countSucceeded: status === "FAILED" ? 0 : count,
    countFailed: status === "FAILED" ? count : 0,
  };
}

function mapAccountChange(source: unknown): BankAccountChangeRequest {
  const userId = text(value(source, "userId"));
  return {
    id: text(value(source, "id")),
    creatorName: text(value(source, "accountHolder"), `User #${userId}`),
    creatorId: userId,
    oldBank: "등록 계좌",
    oldAccountMasked: "****",
    newBank: text(value(source, "bankName")),
    newAccountMasked: text(value(source, "accountNumberMasked")),
    bankbookFile: `bankbook_${text(value(source, "id"))}.pdf`,
    status: accountChangeStatus(value(source, "status")),
    requestedAt: dateValue(value(source, "requestedAt")),
  };
}

function mapCreator(source: unknown): CreatorPayoutSummary {
  const earnedWon = pagesToWon(value(source, "earnedPages"));
  return {
    creatorId: text(value(source, "userId"), text(value(source, "id"))),
    creatorName: text(value(source, "displayName"), "크리에이터"),
    pendingWon: earnedWon,
    paidThisMonthWon: 0,
    totalPaidWon: earnedWon,
    nextPayoutAt: new Date(Date.now() + 7 * 24 * 3_600_000).toISOString(),
    bank: "-",
    accountMasked: "-",
  };
}

function mapKpi(source: unknown): KpiSnapshot {
  const guestConversion = numberValue(value(source, "guestConversion"));
  return {
    dau: numberValue(value(source, "dau")),
    wau: numberValue(value(source, "wau")),
    mau: numberValue(value(source, "mau")),
    newSignups: numberValue(value(source, "newSignups")),
    guestConversion: guestConversion > 1 ? guestConversion / 100 : guestConversion,
    booksAdded: numberValue(value(source, "booksAdded")),
    openModerationQueue: numberValue(value(source, "openModerationQueue")),
    pageGmvWon: pagesToWon(value(source, "pageGmvPages")),
    bookSalesWon: pagesToWon(value(source, "assetPurchaseCount")),
    adRevenueWon: 0,
    apiErrorRate: numberValue(value(source, "apiErrorRate")),
  };
}

function mapDaily(source: unknown): KpiTimePoint {
  const visits = numberValue(value(source, "spaceVisits"));
  return {
    date: text(value(source, "date")),
    dau: visits,
    newSignups: numberValue(value(source, "newSignups")),
    pageGmv: 0,
    bookSales: pagesToWon(value(source, "assetPurchasePages")),
    adRevenue: 0,
    openModeration: numberValue(value(source, "openModeration")),
    apiErrorRate: 0,
  };
}

function mapTimeseries(source: unknown): KpiTimeseries {
  const daily = pageItems(value(source, "daily")).map(mapDaily);
  const latest = daily[daily.length - 1];
  return {
    daily,
    categoryRevenue: pageItems(value(source, "categoryRevenue")).map((item) => ({
      category: text(value(item, "category"), "기타"),
      won: pagesToWon(value(item, "pages")),
    })),
    funnel: pageItems(value(source, "funnel")).length > 0
      ? pageItems(value(source, "funnel")).map((item) => ({
          stage: text(value(item, "stage")),
          count: numberValue(value(item, "count")),
        }))
      : [
          { stage: "공간 진입", count: latest?.dau ?? 0 },
          { stage: "책 추가", count: latest?.newSignups ?? 0 },
          { stage: "꾸미기 구매", count: latest?.bookSales ?? 0 },
        ],
  };
}

function mapSystemHealth(source: unknown): SystemHealth {
  return {
    payment: healthStatus(value(source, "payment")),
    externalBookApi: healthStatus(value(source, "externalBookApi")),
    moderationBacklog: numberValue(value(source, "moderationBacklog")),
    maintenanceMode: booleanValue(value(source, "maintenanceMode")),
  };
}

function mapAdminMember(source: unknown): AdminMember {
  return {
    id: text(value(source, "userId"), text(value(source, "id"))),
    email: text(value(source, "email")),
    name: text(value(source, "name"), "관리자"),
    role: adminRole(value(source, "role")),
    team: text(value(source, "team"), "운영"),
    active: booleanValue(value(source, "active"), true),
    joinedAt: dateValue(value(source, "joinedAt"), dateValue(value(source, "lastSignInAt"))),
    lastSignInAt: text(value(source, "lastSignInAt")) || null,
  };
}

function mapAnnouncement(source: unknown): AnnouncementItem {
  const startsAt = nullableDateValue(value(source, "startsAt"));
  const publishedAt = startsAt ?? dateValue(value(source, "createdAt"));
  return {
    id: text(value(source, "id")),
    title: text(value(source, "title")),
    body: text(value(source, "body")),
    audience: text(value(source, "audience"), "all_users") as AnnouncementItem["audience"],
    startsAt,
    publishedAt,
    endsAt: nullableDateValue(value(source, "endsAt")),
    isUrgent: booleanValue(value(source, "urgent") ?? value(source, "isUrgent")),
  };
}

function mapPolicy(source: unknown): PolicyVersion {
  return {
    id: text(value(source, "id")),
    name: text(value(source, "title"), text(value(source, "policyType"), "정책")),
    version: text(value(source, "version")),
    effectiveAt: dateValue(value(source, "effectiveAt")),
    requiresReConsent: false,
  };
}

function mapAuditLog(source: unknown): AuditLogEntry {
  return {
    id: text(value(source, "id")),
    actorEmail: text(value(source, "actorEmail"), "admin@dotshelf.local"),
    actorRole: "super_admin",
    action: text(value(source, "action")),
    targetType: text(value(source, "targetType")),
    targetId: text(value(source, "targetId")),
    before: text(value(source, "before")) || null,
    after: text(value(source, "after")) || null,
    reason: text(value(source, "reason")) || null,
    occurredAt: dateValue(value(source, "occurredAt")),
    ip: text(value(source, "ip"), "-"),
  };
}

function mapSearchHit(source: unknown): SearchHit {
  return {
    id: text(value(source, "id")),
    type: searchHitType(value(source, "type")),
    title: text(value(source, "title")),
    subtitle: text(value(source, "subtitle")),
    href: text(value(source, "href"), "/dashboard"),
  };
}

function adaptPayload(path: string, method: string, payload: unknown): unknown {
  const pathname = pathOnly(path);

  if (method === "GET" && pathname === "/dashboard/stats") return mapDashboardStats(payload);
  if (method === "GET" && pathname === "/reports") return pageItems(payload).map(mapReport);
  if (method === "POST" && /^\/reports\/[^/]+\/action$/u.test(pathname)) return mapReport(payload);
  if (method === "GET" && pathname === "/users") return pageItems(payload).map(mapUser);
  if (method === "GET" && /^\/users\/[^/]+\/purchases$/u.test(pathname)) return pageItems(payload).map(mapPurchase);
  if (method === "GET" && /^\/users\/[^/]+\/notes$/u.test(pathname)) return pageItems(payload).map(mapUserNote);
  if (method === "POST" && /^\/users\/[^/]+\/notes$/u.test(pathname)) return mapUserNote(payload);
  if (method === "GET" && /^\/users\/[^/]+\/history$/u.test(pathname)) return pageItems(payload).map(mapUserHistory);
  if (method === "GET" && /^\/users\/[^/]+\/spaces$/u.test(pathname)) return pageItems(payload).map(mapUserSpace);
  if (method === "GET" && pathname === "/settlement/reconciliation") return mapReconciliation(payload);
  if (method === "GET" && pathname === "/settlement/withdrawals") return pageItems(payload).map(mapWithdrawal);
  if (method === "GET" && pathname === "/settlement/payout-batches") return pageItems(payload).map(mapPayoutBatch);
  if (method === "GET" && pathname === "/settlement/account-changes") return pageItems(payload).map(mapAccountChange);
  if (method === "GET" && pathname === "/settlement/creators") return pageItems(payload).map(mapCreator);
  if (method === "GET" && pathname === "/stats/kpi") return mapKpi(payload);
  if (method === "GET" && pathname === "/stats/timeseries") return mapTimeseries(payload);
  if (method === "GET" && pathname === "/operations/system-health") return mapSystemHealth(payload);
  if (method === "GET" && pathname === "/operations/admins") return pageItems(payload).map(mapAdminMember);
  if (method === "POST" && /^\/operations\/admins\/[^/]+\/role$/u.test(pathname)) return mapAdminMember(payload);
  if (method === "GET" && pathname === "/operations/announcements") return pageItems(payload).map(mapAnnouncement);
  if (method === "POST" && pathname === "/operations/announcements") return mapAnnouncement(payload);
  if (method === "PUT" && /^\/operations\/announcements\/[^/]+$/u.test(pathname)) return mapAnnouncement(payload);
  if (method === "GET" && pathname === "/operations/policies") return pageItems(payload).map(mapPolicy);
  if (method === "GET" && pathname === "/audit") return pageItems(payload).map(mapAuditLog);
  if (method === "GET" && pathname === "/search") return pageItems(payload).map(mapSearchHit);

  return payload;
}

function shouldSendToken(token: string): boolean {
  return token.split(".").length === 3;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const local = localResponse(path, init);
  if (local.handled) return local.value as T;

  const method = methodOf(init);
  const adapted = adaptRequest(path, init);
  const auth = loadAuth();
  const headers = new Headers(adapted.init.headers);

  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (auth?.token && shouldSendToken(auth.token)) {
    headers.set("Authorization", `Bearer ${auth.token}`);
    touchAuth();
  } else if (auth) {
    touchAuth();
  }
  if (!headers.has(TEST_ADMIN_HEADER)) {
    headers.set(TEST_ADMIN_HEADER, TEST_ADMIN_USER_ID);
  }

  const url = adapted.path.startsWith("http")
    ? adapted.path
    : `${API_BASE}${adapted.path}`;
  const res = await fetch(url, { ...adapted.init, headers });

  if (res.status === 204) return undefined as T;

  let payload: unknown = null;
  try {
    payload = await res.json();
  } catch {
    payload = null;
  }

  if (!res.ok) {
    if (isEnvelope(payload)) {
      throw new ApiError(res.status, payload.message, payload.code);
    }
    const message =
      payload && typeof (payload as { message?: unknown }).message === "string"
        ? (payload as { message: string }).message
        : res.statusText;
    throw new ApiError(res.status, message);
  }

  const unwrapped = isEnvelope(payload) ? payload.result : payload;
  if (isEnvelope(payload) && !envelopeSuccess(payload)) {
    throw new ApiError(res.status, payload.message, payload.code);
  }

  return adaptPayload(path, method, unwrapped) as T;
}

export { ApiError };
