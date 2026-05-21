import { http, HttpResponse, delay } from "msw";

import {
  mockAdminAccount,
  mockAdminMembers,
  mockAnnouncements,
  mockAuditLog,
  mockBankAccountChanges,
  mockCreatorPayouts,
  mockDashboardStats,
  mockKpi,
  mockKpiTimeseries,
  mockNotifications,
  mockPayoutBatches,
  mockPolicies,
  mockQuarterlyAttestations,
  mockReconciliation,
  mockReports,
  mockReviewAppeals,
  mockReviewNotes,
  mockReviews,
  mockSignups,
  mockSystemHealth,
  mockUserHistory,
  mockUserNotes,
  mockUserPurchases,
  mockUsers,
  mockWithdrawals,
} from "@/lib/mock-data";

const SSO_DELAY_MS = 600;
const MFA_DELAY_MS = 400;

function normalizeReportTarget(target: string | null): string | null {
  if (!target) return null;
  const normalized = target.trim().toUpperCase();
  if (normalized === "READING_RECORD" || normalized === "READING_LOG") {
    return "reading_log";
  }
  if (normalized === "READING_RECORD_COMMENT" || normalized === "COMMENT") {
    return "comment";
  }
  if (normalized === "USER" || normalized === "PROFILE") {
    return "profile";
  }
  if (normalized === "GUESTBOOK") return "guestbook";
  if (normalized === "SPACE") return "space";
  if (normalized === "CONTENT") return "content";
  return target;
}

function normalizeReportStatus(status: string | null): string | null {
  if (!status) return null;
  const normalized = status.trim().toUpperCase();
  if (normalized === "PENDING" || normalized === "OPEN") return "open";
  if (normalized === "IN_REVIEW") return "in_review";
  if (normalized === "RESOLVED" || normalized === "DONE") return "resolved";
  if (normalized === "REJECTED" || normalized === "DISMISSED") return "dismissed";
  return status;
}

export const handlers = [
  // --- Auth (SSO + MFA, 사내 IP 화이트리스트 가정) -------------------------
  http.post("/api/auth/sso/start", async () => {
    await delay(SSO_DELAY_MS);
    return HttpResponse.json({
      challengeId: "mfa-" + Date.now().toString(36),
      email: mockAdminAccount.email,
      ipAllowlisted: mockAdminAccount.ipAllowlisted,
    });
  }),

  http.post("/api/auth/mfa/verify", async ({ request }) => {
    const body = (await request.json()) as { code?: string; challengeId?: string };
    await delay(MFA_DELAY_MS);
    if (!body.code || body.code.length !== 6) {
      return HttpResponse.json(
        { message: "TOTP 6자리를 입력해주세요." },
        { status: 400 },
      );
    }
    if (body.code === "000000") {
      return HttpResponse.json(
        { message: "코드가 일치하지 않습니다." },
        { status: 401 },
      );
    }
    return HttpResponse.json({
      token: "admin-mock-" + Date.now().toString(36),
      account: mockAdminAccount,
    });
  }),

  // --- Dashboard ----------------------------------------------------------
  http.get("/api/dashboard/stats", async () => {
    await delay(180);
    return HttpResponse.json(mockDashboardStats);
  }),

  // --- Reviews ------------------------------------------------------------
  http.get("/api/reviews", async ({ request }) => {
    const url = new URL(request.url);
    const type = url.searchParams.get("type");
    await delay(180);
    const items = type ? mockReviews.filter((r) => r.type === type) : mockReviews;
    return HttpResponse.json(items);
  }),

  http.post("/api/reviews/:id/approve", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/reviews/:id/reject", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/reviews/:id/claim", async () => {
    await delay(180);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/reviews/bulk", async ({ request }) => {
    const body = (await request.json()) as { ids?: string[]; decision?: string };
    await delay(260);
    return HttpResponse.json({ ok: true, count: body.ids?.length ?? 0 });
  }),

  http.get("/api/reviews/:id/notes", async ({ params }) => {
    await delay(120);
    const id = params.id as string;
    return HttpResponse.json(mockReviewNotes.filter((n) => n.reviewId === id));
  }),
  http.post("/api/reviews/:id/notes", async () => {
    await delay(180);
    return HttpResponse.json({ ok: true });
  }),

  http.get("/api/reviews/:id/appeal", async ({ params }) => {
    await delay(120);
    const id = params.id as string;
    const appeal = mockReviewAppeals.find((a) => a.reviewId === id) ?? null;
    return HttpResponse.json(appeal);
  }),

  // --- Signups ------------------------------------------------------------
  http.get("/api/signups", async () => {
    await delay(180);
    return HttpResponse.json(mockSignups);
  }),
  http.post("/api/signups/:id/decision", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),

  // --- Reports / Moderation ----------------------------------------------
  http.get("/api/reports", async ({ request }) => {
    const url = new URL(request.url);
    const target = url.searchParams.get("targetType") ?? url.searchParams.get("target");
    const status = url.searchParams.get("status");
    const normalizedTarget = normalizeReportTarget(target);
    const normalizedStatus = normalizeReportStatus(status);
    await delay(180);
    const items = mockReports.filter((r) => {
      const targetMatched = normalizedTarget === null || r.targetType === normalizedTarget;
      const statusMatched = normalizedStatus === null || r.status === normalizedStatus;
      return targetMatched && statusMatched;
    });
    return HttpResponse.json(items);
  }),
  http.post("/api/reports/:id/action", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/reports/bulk", async ({ request }) => {
    const body = (await request.json()) as { ids?: string[]; action?: string };
    await delay(280);
    return HttpResponse.json({ ok: true, count: body.ids?.length ?? 0 });
  }),

  // --- Notifications (관리자 본인) ----------------------------------------
  http.get("/api/notifications", async () => {
    await delay(160);
    return HttpResponse.json(mockNotifications);
  }),
  http.post("/api/notifications/read-all", async () => {
    await delay(120);
    return HttpResponse.json({ ok: true });
  }),

  // --- Global Search ------------------------------------------------------
  http.get("/api/search", async ({ request }) => {
    const url = new URL(request.url);
    const q = (url.searchParams.get("q") ?? "").trim().toLowerCase();
    await delay(140);
    if (!q) return HttpResponse.json([]);
    const inc = (s: string) => s.toLowerCase().includes(q);
    const hits = [
      ...mockUsers
        .filter((u) => inc(u.nickname) || inc(u.email) || inc(u.id))
        .map((u) => ({
          id: u.id,
          type: "user" as const,
          title: u.nickname,
          subtitle: `${u.email} · ${u.id}`,
          href: `/users`,
        })),
      ...mockSignups
        .filter(
          (s) =>
            inc(s.businessName) ||
            inc(s.businessNumber.replace(/\D/g, "")) ||
            inc(s.email) ||
            inc(s.representativeName),
        )
        .map((s) => ({
          id: s.id,
          type: "signup" as const,
          title: s.businessName,
          subtitle: `${s.businessNumber} · ${s.representativeName}`,
          href: `/signups`,
        })),
      ...mockReviews
        .filter(
          (r) =>
            inc(r.title) || inc(r.applicantName) || inc(r.applicantId) || inc(r.id),
        )
        .map((r) => ({
          id: r.id,
          type: "review" as const,
          title: r.title,
          subtitle: `${r.type === "book" ? "책" : r.type === "asset" ? "에셋" : "광고"} · ${r.applicantName}`,
          href: `/reviews`,
        })),
      ...mockReports
        .filter(
          (rp) => inc(rp.targetExcerpt) || inc(rp.targetAuthorName) || inc(rp.id),
        )
        .map((rp) => ({
          id: rp.id,
          type: "report" as const,
          title: rp.targetExcerpt.slice(0, 40),
          subtitle: `${rp.targetAuthorName} · 신고 ${rp.reporterCount}건`,
          href: `/moderation`,
        })),
    ];
    return HttpResponse.json(hits.slice(0, 12));
  }),

  // --- Users --------------------------------------------------------------
  http.get("/api/users", async ({ request }) => {
    const url = new URL(request.url);
    const q = url.searchParams.get("q")?.toLowerCase() ?? "";
    await delay(180);
    const items = q
      ? mockUsers.filter(
          (u) =>
            u.nickname.toLowerCase().includes(q) ||
            u.email.toLowerCase().includes(q) ||
            u.id.toLowerCase().includes(q),
        )
      : mockUsers;
    return HttpResponse.json(items);
  }),
  http.post("/api/users/:id/action", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/users/:id/notes", async ({ params }) => {
    await delay(120);
    return HttpResponse.json(
      mockUserNotes.filter((n) => n.userId === (params.id as string)),
    );
  }),
  http.post("/api/users/:id/notes", async () => {
    await delay(180);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/users/:id/purchases", async ({ params }) => {
    await delay(150);
    return HttpResponse.json(
      mockUserPurchases.filter((p) => p.userId === (params.id as string)),
    );
  }),
  http.get("/api/users/:id/history", async ({ params }) => {
    await delay(150);
    return HttpResponse.json(
      mockUserHistory.filter((h) => h.userId === (params.id as string)),
    );
  }),
  http.post("/api/users/:id/refund", async () => {
    await delay(260);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/users/:id/adjust-balance", async () => {
    await delay(260);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/users/:id/export", async () => {
    await delay(400);
    return HttpResponse.json({ ok: true, ticketId: "exp-" + Date.now().toString(36) });
  }),

  // --- Settlement ---------------------------------------------------------
  http.get("/api/settlement/reconciliation", async () => {
    await delay(180);
    return HttpResponse.json(mockReconciliation);
  }),
  http.get("/api/settlement/withdrawals", async () => {
    await delay(180);
    return HttpResponse.json(mockWithdrawals);
  }),
  http.get("/api/settlement/payout-batches", async () => {
    await delay(180);
    return HttpResponse.json(mockPayoutBatches);
  }),
  http.post("/api/settlement/withdrawals/:id/approve", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/settlement/withdrawals/:id/reject", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/settlement/account-changes", async () => {
    await delay(180);
    return HttpResponse.json(mockBankAccountChanges);
  }),
  http.post("/api/settlement/account-changes/:id/decision", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/settlement/creators", async () => {
    await delay(180);
    return HttpResponse.json(mockCreatorPayouts);
  }),
  http.post("/api/settlement/adjust", async () => {
    await delay(280);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/settlement/attestations", async () => {
    await delay(140);
    return HttpResponse.json(mockQuarterlyAttestations);
  }),
  http.post("/api/settlement/attestations", async () => {
    await delay(280);
    return HttpResponse.json({ ok: true });
  }),

  // --- KPI ----------------------------------------------------------------
  http.get("/api/stats/kpi", async () => {
    await delay(180);
    return HttpResponse.json(mockKpi);
  }),
  http.get("/api/stats/timeseries", async () => {
    await delay(220);
    return HttpResponse.json(mockKpiTimeseries);
  }),

  // --- Operations ---------------------------------------------------------
  http.get("/api/operations/announcements", async () => {
    await delay(180);
    return HttpResponse.json(mockAnnouncements);
  }),
  http.post("/api/operations/announcements", async ({ request }) => {
    await delay(280);
    const body = (await request.json()) as {
      title: string;
      body: string;
      audience: "all_users" | "internal";
      startsAt: string | null;
      endsAt: string | null;
      urgent?: boolean;
      isUrgent?: boolean;
    };
    const publishedAt = body.startsAt ?? new Date().toISOString();
    const announcement = {
      id: "an_" + Date.now().toString(36),
      title: body.title,
      body: body.body,
      audience: body.audience,
      startsAt: body.startsAt,
      publishedAt,
      endsAt: body.endsAt,
      isUrgent: body.urgent ?? body.isUrgent ?? false,
    };
    mockAnnouncements.unshift(announcement);
    return HttpResponse.json(announcement);
  }),
  http.put("/api/operations/announcements/:id", async ({ params, request }) => {
    await delay(220);
    const id = params.id as string;
    const body = (await request.json()) as {
      title: string;
      body: string;
      audience: "all_users" | "internal";
      startsAt: string | null;
      endsAt: string | null;
      urgent?: boolean;
      isUrgent?: boolean;
    };
    const index = mockAnnouncements.findIndex((a) => a.id === id);
    if (index < 0) {
      return HttpResponse.json({ message: "공지사항을 찾을 수 없습니다." }, { status: 404 });
    }
    const announcement = {
      ...mockAnnouncements[index],
      title: body.title,
      body: body.body,
      audience: body.audience,
      startsAt: body.startsAt,
      publishedAt: body.startsAt ?? mockAnnouncements[index].publishedAt,
      endsAt: body.endsAt,
      isUrgent: body.urgent ?? body.isUrgent ?? false,
    };
    mockAnnouncements[index] = announcement;
    return HttpResponse.json(announcement);
  }),
  http.delete("/api/operations/announcements/:id", async ({ params }) => {
    await delay(200);
    const id = params.id as string;
    const index = mockAnnouncements.findIndex((a) => a.id === id);
    if (index < 0) {
      return HttpResponse.json({ message: "공지사항을 찾을 수 없습니다." }, { status: 404 });
    }
    mockAnnouncements.splice(index, 1);
    return new HttpResponse(null, { status: 204 });
  }),
  http.post("/api/operations/policies/publish", async () => {
    await delay(280);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/operations/policies", async () => {
    await delay(180);
    return HttpResponse.json(mockPolicies);
  }),
  http.get("/api/operations/system-health", async () => {
    await delay(180);
    return HttpResponse.json(mockSystemHealth);
  }),
  http.post("/api/operations/maintenance", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.get("/api/operations/admins", async () => {
    await delay(180);
    return HttpResponse.json(mockAdminMembers);
  }),
  http.post("/api/operations/admins/:id/role", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/operations/admins/:id/active", async () => {
    await delay(220);
    return HttpResponse.json({ ok: true });
  }),
  http.post("/api/operations/admins/invite", async () => {
    await delay(260);
    return HttpResponse.json({ ok: true });
  }),

  // --- Audit Log ----------------------------------------------------------
  http.get("/api/audit", async () => {
    await delay(180);
    return HttpResponse.json(mockAuditLog);
  }),
];
