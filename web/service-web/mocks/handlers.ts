import { http, HttpResponse, delay } from "msw";

import {
  defaultNotificationPrefs,
  getMockKpi,
  getMockTimeseries,
  isbnFixtures,
  mockAccount,
  mockAssets,
  mockBankChangeRequests,
  mockBooks,
  mockCampaigns,
  mockCategoryBreakdown,
  mockContentRows,
  mockDashboard,
  mockEscrow,
  mockNotifications,
  mockReviews,
  mockSectionMarket,
  mockSettlement,
  mockSettlementHistory,
  mockTickets,
  mockWithdrawRequests,
} from "@/lib/mock-data";
import type {
  AdCampaign,
  AdCampaignStatus,
  AdSection,
  Asset,
  AssetSaleStatus,
  BankAccountChangeRequest,
  Book,
  BookSaleStatus,
  EscrowBalance,
  NotificationItem,
  NotificationPrefs,
  SectionMarketStats,
  StatsPeriod,
  StatsScope,
  SupportTicket,
  SupportTicketCategory,
  WithdrawRequest,
} from "@/lib/types";

// in-memory mutable stores (mock only). 핸들러보다 먼저 선언.
let booksStore: Book[] = [...mockBooks];
let campaignsStore: AdCampaign[] = [...mockCampaigns];
let assetsStore: Asset[] = [...mockAssets];
let escrowStore: EscrowBalance = { ...mockEscrow };
let withdrawStore: WithdrawRequest[] = [...mockWithdrawRequests];
let bankChangeStore: BankAccountChangeRequest[] = [...mockBankChangeRequests];
let ticketsStore: SupportTicket[] = [...mockTickets];
let notificationsStore: NotificationItem[] = [...mockNotifications];
let notificationPrefsStore: NotificationPrefs = {
  ...defaultNotificationPrefs,
};

const SIGNUP_DELAY_MS = 600;
const LOGIN_DELAY_MS = 500;

export const handlers = [
  // --- Auth -----------------------------------------------------------------
  http.post("/api/auth/login", async ({ request }) => {
    const body = (await request.json()) as { email?: string; password?: string };
    await delay(LOGIN_DELAY_MS);
    if (!body?.email || !body?.password) {
      return HttpResponse.json(
        { message: "이메일과 비밀번호를 입력해주세요." },
        { status: 400 },
      );
    }
    if (body.password === "wrong") {
      return HttpResponse.json(
        { message: "이메일 또는 비밀번호가 올바르지 않습니다." },
        { status: 401 },
      );
    }
    return HttpResponse.json({
      token: "mock-token-" + Date.now(),
      account: { ...mockAccount, email: body.email },
    });
  }),

  http.post("/api/auth/forgot-password", async () => {
    await delay(400);
    return HttpResponse.json({ ok: true });
  }),

  http.post("/api/auth/business-status", async ({ request }) => {
    const body = (await request.json()) as { businessNumber?: string };
    await delay(450);
    const digits = (body?.businessNumber ?? "").replace(/\D/g, "");
    if (digits.length !== 10) {
      return HttpResponse.json(
        { status: "invalid", message: "사업자 번호 10자리를 정확히 입력해주세요." },
        { status: 400 },
      );
    }
    if (digits.startsWith("000")) {
      return HttpResponse.json({
        status: "closed",
        message: "휴업 또는 폐업 상태입니다. 가입이 제한됩니다.",
      });
    }
    return HttpResponse.json({
      status: "active",
      businessName: "도트셀프 출판",
      representativeName: "김지호",
    });
  }),

  http.post("/api/auth/signup", async () => {
    await delay(SIGNUP_DELAY_MS);
    return HttpResponse.json({ status: "pending" });
  }),

  // --- Dashboard ------------------------------------------------------------
  http.get("/api/dashboard/summary", async () => {
    await delay(250);
    return HttpResponse.json(mockDashboard);
  }),

  http.get("/api/reviews", async () => {
    await delay(200);
    return HttpResponse.json(mockReviews);
  }),

  http.get("/api/notifications", async () => {
    await delay(200);
    return HttpResponse.json(notificationsStore);
  }),

  http.post("/api/notifications/:id/read", async ({ params }) => {
    await delay(120);
    notificationsStore = notificationsStore.map((n) =>
      n.id === params.id ? { ...n, read: true } : n,
    );
    return HttpResponse.json({ ok: true });
  }),

  http.post("/api/notifications/mark-all-read", async () => {
    await delay(150);
    notificationsStore = notificationsStore.map((n) => ({ ...n, read: true }));
    return HttpResponse.json({ ok: true });
  }),

  http.get("/api/notifications/prefs", async () => {
    await delay(120);
    return HttpResponse.json(notificationPrefsStore);
  }),

  http.put("/api/notifications/prefs", async ({ request }) => {
    const body = (await request.json()) as NotificationPrefs;
    await delay(180);
    notificationPrefsStore = body;
    return HttpResponse.json(notificationPrefsStore);
  }),

  http.get("/api/settlement/summary", async () => {
    await delay(200);
    return HttpResponse.json(mockSettlement);
  }),

  http.get("/api/settlement/withdraws", async () => {
    await delay(150);
    return HttpResponse.json(withdrawStore);
  }),

  http.post("/api/settlement/withdraws", async ({ request }) => {
    const body = (await request.json()) as { amountWon?: number };
    await delay(450);
    const amount = Number(body?.amountWon ?? 0);
    if (!Number.isFinite(amount) || amount < 50_000) {
      return HttpResponse.json(
        { message: "최소 출금 금액은 50,000원입니다." },
        { status: 400 },
      );
    }
    if (amount > mockSettlement.heldWon) {
      return HttpResponse.json(
        { message: "예치 보관 잔액을 초과했어요." },
        { status: 402 },
      );
    }
    const id = `wd_${Date.now()}`;
    const now = new Date();
    const expected = new Date(now);
    expected.setDate(now.getDate() + 2);
    const created: WithdrawRequest = {
      id,
      amountWon: amount,
      requestedAt: now.toISOString(),
      status: "pending",
      expectedPayoutDate: expected.toISOString(),
    };
    withdrawStore = [created, ...withdrawStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  http.get("/api/settlement/history", async () => {
    await delay(200);
    return HttpResponse.json(mockSettlementHistory);
  }),

  http.get("/api/settlement/bank-change", async () => {
    await delay(120);
    return HttpResponse.json(bankChangeStore);
  }),

  http.post("/api/settlement/bank-change", async ({ request }) => {
    const body = (await request.json()) as {
      bankName?: string;
      accountNumber?: string;
      accountHolder?: string;
    };
    await delay(450);
    if (!body.bankName || !body.accountNumber || !body.accountHolder) {
      return HttpResponse.json(
        { message: "은행·계좌번호·예금주를 모두 입력해주세요." },
        { status: 400 },
      );
    }
    const masked =
      body.accountNumber.length > 4
        ? `*** *** ${body.accountNumber.slice(-4)}`
        : body.accountNumber;
    const id = `bc_${Date.now()}`;
    const created: BankAccountChangeRequest = {
      id,
      bankName: body.bankName,
      accountNumber: masked,
      accountHolder: body.accountHolder,
      status: "pending",
      requestedAt: new Date().toISOString(),
    };
    bankChangeStore = [created, ...bankChangeStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  // --- Support tickets ------------------------------------------------------
  http.get("/api/support/tickets", async ({ request }) => {
    await delay(150);
    const url = new URL(request.url);
    const cat = url.searchParams.get("category") as
      | SupportTicketCategory
      | "all"
      | null;
    let rows = ticketsStore;
    if (cat && cat !== "all") {
      rows = rows.filter((t) => t.category === cat);
    }
    return HttpResponse.json(rows);
  }),

  http.get("/api/support/tickets/:id", async ({ params }) => {
    await delay(150);
    const t = ticketsStore.find((x) => x.id === params.id);
    if (!t) return HttpResponse.json({ message: "없음" }, { status: 404 });
    return HttpResponse.json(t);
  }),

  http.post("/api/support/tickets", async ({ request }) => {
    const body = (await request.json()) as {
      category?: SupportTicketCategory;
      subject?: string;
      body?: string;
      attachmentNames?: string[];
    };
    await delay(450);
    if (!body.category || !body.subject || !body.body) {
      return HttpResponse.json(
        { message: "카테고리·제목·본문을 모두 입력해주세요." },
        { status: 400 },
      );
    }
    const now = new Date().toISOString();
    const created: SupportTicket = {
      id: `tk_${Date.now()}`,
      category: body.category,
      subject: body.subject,
      body: body.body,
      attachmentNames: body.attachmentNames ?? [],
      status: "open",
      createdAt: now,
      updatedAt: now,
      replies: [],
    };
    ticketsStore = [created, ...ticketsStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  // --- Account withdraw -----------------------------------------------------
  http.post("/api/account/withdraw", async () => {
    await delay(450);
    const activeCampaigns = campaignsStore.filter(
      (c) => c.status === "running" || c.status === "in_review",
    ).length;
    const pendingPayout = mockSettlement.pendingWon + mockSettlement.heldWon;
    if (activeCampaigns > 0) {
      return HttpResponse.json(
        {
          message: `진행 중·검토 중 광고 캠페인 ${activeCampaigns}건이 있어 탈퇴할 수 없어요. 모두 종료 후 다시 시도해주세요.`,
        },
        { status: 409 },
      );
    }
    if (pendingPayout > 0) {
      return HttpResponse.json(
        {
          message: `미정산 잔액 ${pendingPayout.toLocaleString()}원이 있어 탈퇴할 수 없어요. 정산 완료 후 다시 시도해주세요.`,
        },
        { status: 409 },
      );
    }
    return HttpResponse.json({
      ok: true,
      retentionDays: 60,
    });
  }),

  // --- Books ----------------------------------------------------------------
  http.get("/api/books", async ({ request }) => {
    await delay(200);
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as
      | BookSaleStatus
      | "all"
      | null;
    const q = url.searchParams.get("q")?.toLowerCase() ?? "";
    let result = booksStore;
    if (status && status !== "all") {
      result = result.filter((b) => b.status === status);
    }
    if (q) {
      result = result.filter(
        (b) =>
          b.title.toLowerCase().includes(q) ||
          b.author.toLowerCase().includes(q) ||
          (b.isbn ?? "").includes(q),
      );
    }
    return HttpResponse.json(result);
  }),

  http.get("/api/books/:id", async ({ params }) => {
    await delay(150);
    const book = booksStore.find((b) => b.id === params.id);
    if (!book) {
      return HttpResponse.json({ message: "책을 찾지 못했어요." }, { status: 404 });
    }
    return HttpResponse.json(book);
  }),

  http.post("/api/books/isbn-lookup", async ({ request }) => {
    const body = (await request.json()) as { isbn?: string };
    await delay(450);
    const digits = (body?.isbn ?? "").replace(/\D/g, "");
    const fixture = isbnFixtures[digits];
    if (!fixture) {
      return HttpResponse.json(
        {
          status: "not_found",
          message:
            "ISBN 매칭 결과가 없습니다. 자가출판이라면 [수동 입력]으로 진행해주세요.",
        },
        { status: 404 },
      );
    }
    return HttpResponse.json({ status: "found", ...fixture });
  }),

  http.post("/api/books", async ({ request }) => {
    const body = (await request.json()) as Partial<Book>;
    await delay(500);
    const id = `bk_${Date.now()}`;
    const now = new Date().toISOString();
    const created: Book = {
      id,
      isbn: body.isbn ?? null,
      title: body.title ?? "제목 없음",
      author: body.author ?? "",
      publisher: body.publisher ?? "",
      synopsis: body.synopsis ?? "",
      coverImageUrl: body.coverImageUrl ?? null,
      category: body.category ?? "etc",
      ageRating: body.ageRating ?? "all",
      pricePages: body.pricePages ?? 0,
      saleStartDate: body.saleStartDate ?? null,
      marketingCopy: body.marketingCopy ?? null,
      coverMode: body.coverMode ?? "self_upload",
      consignmentStage:
        body.coverMode === "consignment" ? "requested" : "not_applicable",
      rejectionReason: null,
      status: body.status ?? "draft",
      submittedAt: now,
      updatedAt: now,
      lastPriceChangedAt: null,
      previewSampleName: body.previewSampleName ?? null,
      previewSamplePageCount: body.previewSamplePageCount ?? null,
    };
    booksStore = [created, ...booksStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  http.post("/api/books/:id/submit", async ({ params }) => {
    await delay(300);
    booksStore = booksStore.map((b) =>
      b.id === params.id
        ? { ...b, status: "submitted", updatedAt: new Date().toISOString() }
        : b,
    );
    const updated = booksStore.find((b) => b.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/books/:id/resubmit", async ({ params, request }) => {
    const body = (await request.json()) as {
      changeNote?: string;
      pricePages?: number;
      marketingCopy?: string | null;
    };
    await delay(350);
    const found = booksStore.find((b) => b.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "책 없음" }, { status: 404 });
    }
    if (found.status !== "rejected") {
      return HttpResponse.json(
        { message: "반려 상태에서만 재신청할 수 있어요." },
        { status: 409 },
      );
    }
    if (!body.changeNote || body.changeNote.trim().length < 5) {
      return HttpResponse.json(
        {
          message:
            "변경 사항 메모를 5자 이상 작성해주세요. 검수자가 빠르게 확인합니다.",
        },
        { status: 400 },
      );
    }
    booksStore = booksStore.map((b) =>
      b.id === params.id
        ? {
            ...b,
            status: "submitted",
            rejectionReason: null,
            pricePages: body.pricePages ?? b.pricePages,
            marketingCopy:
              body.marketingCopy !== undefined
                ? body.marketingCopy
                : b.marketingCopy,
            updatedAt: new Date().toISOString(),
          }
        : b,
    );
    const updated = booksStore.find((b) => b.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/books/:id/toggle-sale", async ({ params }) => {
    await delay(250);
    booksStore = booksStore.map((b) => {
      if (b.id !== params.id) return b;
      const next: BookSaleStatus = b.status === "on_sale" ? "off_sale" : "on_sale";
      return { ...b, status: next, updatedAt: new Date().toISOString() };
    });
    const updated = booksStore.find((b) => b.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/books/:id/price", async ({ params, request }) => {
    const body = (await request.json()) as { pricePages?: number };
    await delay(250);
    const found = booksStore.find((b) => b.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "책 없음" }, { status: 404 });
    }
    if (found.lastPriceChangedAt) {
      const since = Date.now() - new Date(found.lastPriceChangedAt).getTime();
      if (since < 24 * 60 * 60 * 1000) {
        return HttpResponse.json(
          {
            message:
              "가격은 24시간 쿨다운 후 변경할 수 있어요 (덤핑 방지).",
          },
          { status: 409 },
        );
      }
    }
    booksStore = booksStore.map((b) =>
      b.id === params.id
        ? {
            ...b,
            pricePages: body.pricePages ?? b.pricePages,
            lastPriceChangedAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          }
        : b,
    );
    const updated = booksStore.find((b) => b.id === params.id);
    return HttpResponse.json(updated);
  }),

  // --- Ads ------------------------------------------------------------------
  http.get("/api/escrow", async () => {
    await delay(150);
    return HttpResponse.json(escrowStore);
  }),

  http.post("/api/escrow/charge", async ({ request }) => {
    const body = (await request.json()) as { amountWon?: number };
    await delay(400);
    const amount = Number(body?.amountWon ?? 0);
    if (!Number.isFinite(amount) || amount <= 0) {
      return HttpResponse.json(
        { message: "충전 금액을 확인해주세요." },
        { status: 400 },
      );
    }
    escrowStore = {
      ...escrowStore,
      balanceWon: escrowStore.balanceWon + amount,
      lastChargedAt: new Date().toISOString(),
    };
    return HttpResponse.json(escrowStore);
  }),

  http.get("/api/ads/section-market", async ({ request }) => {
    await delay(150);
    const url = new URL(request.url);
    const cat = url.searchParams.get("category");
    const age = url.searchParams.get("ageRating");
    if (!cat || !age) {
      return HttpResponse.json(mockSectionMarket);
    }
    const key = `${cat}|${age}`;
    const stats: SectionMarketStats = mockSectionMarket[key] ?? {
      avgWeeklyBidWon: 60_000,
      minBidWon: 25_000,
      currentPoolWon: 180_000,
      avgCtr: 0.03,
      advertiserCount: 3,
    };
    return HttpResponse.json(stats);
  }),

  http.get("/api/ads/campaigns", async ({ request }) => {
    await delay(180);
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as
      | AdCampaignStatus
      | "all"
      | null;
    let list = campaignsStore;
    if (status && status !== "all") {
      list = list.filter((c) => c.status === status);
    }
    return HttpResponse.json(list);
  }),

  http.get("/api/ads/campaigns/:id", async ({ params }) => {
    await delay(150);
    const c = campaignsStore.find((c) => c.id === params.id);
    if (!c) {
      return HttpResponse.json({ message: "캠페인 없음" }, { status: 404 });
    }
    return HttpResponse.json(c);
  }),

  http.post("/api/ads/campaigns", async ({ request }) => {
    await delay(450);
    const body = (await request.json()) as {
      bookId: string;
      bookTitle: string;
      section: AdSection;
      weeks: string[];
      weeklyBidWon: number;
      copy?: string | null;
    };
    const totalCost = body.weeks.length * body.weeklyBidWon;
    if (escrowStore.balanceWon < totalCost) {
      return HttpResponse.json(
        {
          message: `예치금 잔액(${escrowStore.balanceWon.toLocaleString()}원)이 부족해요. ${totalCost.toLocaleString()}원 이상 충전 후 다시 신청해주세요.`,
        },
        { status: 402 },
      );
    }
    const id = `ad_${Date.now()}`;
    const now = new Date().toISOString();
    const created: AdCampaign = {
      id,
      bookId: body.bookId,
      bookTitle: body.bookTitle,
      bookCoverUrl: null,
      section: body.section,
      weeks: body.weeks,
      weeklyBidWon: body.weeklyBidWon,
      copy: body.copy ?? null,
      status: "in_review",
      rejectionReason: null,
      submittedAt: now,
      updatedAt: now,
      weekStats: [],
    };
    // 예치금에서 차감
    escrowStore = {
      ...escrowStore,
      balanceWon: escrowStore.balanceWon - totalCost,
    };
    campaignsStore = [created, ...campaignsStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  http.post("/api/ads/campaigns/:id/pause", async ({ params }) => {
    await delay(250);
    const found = campaignsStore.find((c) => c.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "캠페인 없음" }, { status: 404 });
    }
    if (found.status !== "running") {
      return HttpResponse.json(
        {
          message: "진행 중 상태에서만 일시정지할 수 있어요.",
        },
        { status: 409 },
      );
    }
    // 잔여 주차 환불
    const now = new Date();
    const remainingWeeks = found.weeks.filter((w) => new Date(w) > now);
    const refund = remainingWeeks.length * found.weeklyBidWon;
    escrowStore = {
      ...escrowStore,
      balanceWon: escrowStore.balanceWon + refund,
    };
    campaignsStore = campaignsStore.map((c) =>
      c.id === params.id
        ? { ...c, status: "paused", updatedAt: new Date().toISOString() }
        : c,
    );
    const updated = campaignsStore.find((c) => c.id === params.id);
    return HttpResponse.json({ campaign: updated, refund });
  }),

  http.post(
    "/api/ads/campaigns/:id/resubmit",
    async ({ params, request }) => {
      const body = (await request.json()) as {
        changeNote?: string;
        copy?: string | null;
        weeklyBidWon?: number;
      };
      await delay(350);
      const found = campaignsStore.find((c) => c.id === params.id);
      if (!found) {
        return HttpResponse.json({ message: "캠페인 없음" }, { status: 404 });
      }
      if (found.status !== "rejected") {
        return HttpResponse.json(
          { message: "반려 상태에서만 재신청할 수 있어요." },
          { status: 409 },
        );
      }
      if (!body.changeNote || body.changeNote.trim().length < 5) {
        return HttpResponse.json(
          {
            message:
              "변경 사항 메모를 5자 이상 작성해주세요. 검수자가 빠르게 확인합니다.",
          },
          { status: 400 },
        );
      }
      campaignsStore = campaignsStore.map((c) =>
        c.id === params.id
          ? {
              ...c,
              status: "in_review",
              rejectionReason: null,
              copy: body.copy !== undefined ? body.copy : c.copy,
              weeklyBidWon: body.weeklyBidWon ?? c.weeklyBidWon,
              updatedAt: new Date().toISOString(),
            }
          : c,
      );
      const updated = campaignsStore.find((c) => c.id === params.id);
      return HttpResponse.json(updated);
    },
  ),

  http.post(
    "/api/ads/campaigns/:id/update-bid",
    async ({ params, request }) => {
      const body = (await request.json()) as { weeklyBidWon?: number };
      await delay(250);
      const found = campaignsStore.find((c) => c.id === params.id);
      if (!found) {
        return HttpResponse.json({ message: "캠페인 없음" }, { status: 404 });
      }
      // 다음 주 시작 24시간 전까지만 변경 가능 — mock에서는 항상 OK로 두지만,
      // running 상태가 아닐 때는 거절.
      if (found.status !== "running" && found.status !== "in_review") {
        return HttpResponse.json(
          { message: "진행 중 또는 검토 중 캠페인만 입찰을 변경할 수 있어요." },
          { status: 409 },
        );
      }
      campaignsStore = campaignsStore.map((c) =>
        c.id === params.id
          ? {
              ...c,
              weeklyBidWon: body.weeklyBidWon ?? c.weeklyBidWon,
              updatedAt: new Date().toISOString(),
            }
          : c,
      );
      const updated = campaignsStore.find((c) => c.id === params.id);
      return HttpResponse.json(updated);
    },
  ),

  // --- Assets ---------------------------------------------------------------
  http.get("/api/assets", async ({ request }) => {
    await delay(200);
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as
      | AssetSaleStatus
      | "all"
      | null;
    const category = url.searchParams.get("category");
    const q = url.searchParams.get("q")?.toLowerCase() ?? "";
    let result = assetsStore;
    if (status && status !== "all") {
      result = result.filter((a) => a.status === status);
    }
    if (category && category !== "all") {
      result = result.filter((a) => a.category === category);
    }
    if (q) {
      result = result.filter(
        (a) =>
          a.name.toLowerCase().includes(q) ||
          (a.nameEn ?? "").toLowerCase().includes(q),
      );
    }
    return HttpResponse.json(result);
  }),

  http.get("/api/assets/:id", async ({ params }) => {
    await delay(150);
    const found = assetsStore.find((a) => a.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "에셋 없음" }, { status: 404 });
    }
    return HttpResponse.json(found);
  }),

  http.post("/api/assets", async ({ request }) => {
    const body = (await request.json()) as Partial<Asset>;
    await delay(450);
    const id = `as_${Date.now()}`;
    const now = new Date().toISOString();
    const created: Asset = {
      id,
      name: body.name ?? "이름 없음",
      nameEn: body.nameEn ?? null,
      category: body.category ?? "shelf_object",
      compatTargetIds: body.compatTargetIds ?? [],
      themeSlots: body.themeSlots ?? null,
      description: body.description ?? "",
      pricePages: body.pricePages ?? 0,
      previewFrontUrl: body.previewFrontUrl ?? null,
      previewSideUrl: body.previewSideUrl ?? null,
      motionPreviewName: body.motionPreviewName ?? null,
      modelFileName: body.modelFileName ?? null,
      texturesFileName: body.texturesFileName ?? null,
      licenses: body.licenses ?? [{ source: "자체 제작", type: "self" }],
      status: body.status ?? "draft",
      rejectionReason: null,
      submittedAt: now,
      updatedAt: now,
      lastPriceChangedAt: null,
    };
    assetsStore = [created, ...assetsStore];
    return HttpResponse.json(created, { status: 201 });
  }),

  http.post("/api/assets/:id/submit", async ({ params }) => {
    await delay(300);
    assetsStore = assetsStore.map((a) =>
      a.id === params.id
        ? { ...a, status: "submitted", updatedAt: new Date().toISOString() }
        : a,
    );
    const updated = assetsStore.find((a) => a.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/assets/:id/resubmit", async ({ params, request }) => {
    const body = (await request.json()) as {
      changeNote?: string;
      newPreviewFront?: string | null;
      newPreviewSide?: string | null;
      pricePages?: number;
    };
    await delay(350);
    const found = assetsStore.find((a) => a.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "에셋 없음" }, { status: 404 });
    }
    if (found.status !== "rejected") {
      return HttpResponse.json(
        { message: "반려 상태에서만 재신청할 수 있어요." },
        { status: 409 },
      );
    }
    if (!body.changeNote || body.changeNote.trim().length < 5) {
      return HttpResponse.json(
        {
          message:
            "변경 사항 메모를 5자 이상 작성해주세요. 검수자가 빠르게 확인합니다.",
        },
        { status: 400 },
      );
    }
    assetsStore = assetsStore.map((a) =>
      a.id === params.id
        ? {
            ...a,
            status: "submitted",
            rejectionReason: null,
            previewFrontUrl:
              body.newPreviewFront !== undefined
                ? body.newPreviewFront
                : a.previewFrontUrl,
            previewSideUrl:
              body.newPreviewSide !== undefined
                ? body.newPreviewSide
                : a.previewSideUrl,
            pricePages: body.pricePages ?? a.pricePages,
            updatedAt: new Date().toISOString(),
          }
        : a,
    );
    const updated = assetsStore.find((a) => a.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/assets/:id/toggle-sale", async ({ params }) => {
    await delay(250);
    assetsStore = assetsStore.map((a) => {
      if (a.id !== params.id) return a;
      const next: AssetSaleStatus = a.status === "on_sale" ? "off_sale" : "on_sale";
      return { ...a, status: next, updatedAt: new Date().toISOString() };
    });
    const updated = assetsStore.find((a) => a.id === params.id);
    return HttpResponse.json(updated);
  }),

  http.post("/api/assets/:id/price", async ({ params, request }) => {
    const body = (await request.json()) as { pricePages?: number };
    await delay(250);
    const found = assetsStore.find((a) => a.id === params.id);
    if (!found) {
      return HttpResponse.json({ message: "에셋 없음" }, { status: 404 });
    }
    if (found.lastPriceChangedAt) {
      const since = Date.now() - new Date(found.lastPriceChangedAt).getTime();
      if (since < 24 * 60 * 60 * 1000) {
        return HttpResponse.json(
          {
            message: "가격은 24시간 쿨다운 후 변경할 수 있어요 (덤핑 방지).",
          },
          { status: 409 },
        );
      }
    }
    assetsStore = assetsStore.map((a) =>
      a.id === params.id
        ? {
            ...a,
            pricePages: body.pricePages ?? a.pricePages,
            lastPriceChangedAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          }
        : a,
    );
    const updated = assetsStore.find((a) => a.id === params.id);
    return HttpResponse.json(updated);
  }),

  // --- Stats ----------------------------------------------------------------
  http.get("/api/stats/kpi", async ({ request }) => {
    await delay(150);
    const url = new URL(request.url);
    const scope = (url.searchParams.get("scope") ?? "all") as StatsScope;
    return HttpResponse.json(getMockKpi(scope));
  }),

  http.get("/api/stats/timeseries", async ({ request }) => {
    await delay(180);
    const url = new URL(request.url);
    const scope = (url.searchParams.get("scope") ?? "all") as StatsScope;
    const period = (url.searchParams.get("period") ?? "daily") as StatsPeriod;
    return HttpResponse.json(getMockTimeseries(period, scope));
  }),

  http.get("/api/stats/by-category", async ({ request }) => {
    await delay(150);
    const url = new URL(request.url);
    const scope = url.searchParams.get("scope") as StatsScope | null;
    let rows = mockCategoryBreakdown;
    if (scope && scope !== "all") {
      rows = rows.filter((r) => r.scope === scope);
    }
    return HttpResponse.json(rows);
  }),

  http.get("/api/stats/content", async ({ request }) => {
    await delay(150);
    const url = new URL(request.url);
    const scope = url.searchParams.get("scope");
    let rows = mockContentRows;
    if (scope === "book" || scope === "asset") {
      rows = rows.filter((r) => r.scope === scope);
    }
    return HttpResponse.json(rows);
  }),
];
