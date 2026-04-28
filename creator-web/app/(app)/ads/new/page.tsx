"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Megaphone,
  TrendingUp,
} from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { formatDate, formatWon } from "@/lib/format";
import {
  AGE_RATING_LABEL,
  BOOK_CATEGORY_LABEL,
  type AdCampaign,
  type AdSection,
  type AgeRating,
  type Book,
  type BookCategory,
  type EscrowBalance,
  type SectionMarketStats,
} from "@/lib/types";

const STEPS = [
  { id: 1, title: "책 선택" },
  { id: 2, title: "섹션" },
  { id: 3, title: "기간" },
  { id: 4, title: "입찰" },
  { id: 5, title: "광고 카피" },
  { id: 6, title: "결제·검수 신청" },
] as const;

interface CampaignDraft {
  book: Book | null;
  section: AdSection | null;
  weeks: string[];
  weeklyBidWon: number;
  copy: string;
}

const initialDraft: CampaignDraft = {
  book: null,
  section: null,
  weeks: [],
  weeklyBidWon: 0,
  copy: "",
};

export default function NewAdPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<CampaignDraft>(initialDraft);

  const goNext = () => setStep((s) => Math.min(STEPS.length, s + 1));
  const goPrev = () => setStep((s) => Math.max(1, s - 1));

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/ads"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 광고 목록으로
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">
          새 캠페인
        </h1>
        <p className="text-sm text-muted-foreground">
          캠페인 = 책 × 섹션(카테고리×연령) × 주차. 주간 SoV 입찰로 노출이
          분배됩니다.
        </p>
      </div>

      <Stepper step={step} />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div>
          {step === 1 && (
            <BookStep
              draft={draft}
              onNext={(book) => {
                setDraft({ ...draft, book });
                goNext();
              }}
            />
          )}
          {step === 2 && (
            <SectionStep
              draft={draft}
              onPrev={goPrev}
              onNext={(section) => {
                setDraft({ ...draft, section });
                goNext();
              }}
            />
          )}
          {step === 3 && (
            <WeeksStep
              draft={draft}
              onPrev={goPrev}
              onNext={(weeks) => {
                setDraft({ ...draft, weeks });
                goNext();
              }}
            />
          )}
          {step === 4 && (
            <BidStep
              draft={draft}
              onPrev={goPrev}
              onNext={(weeklyBidWon) => {
                setDraft({ ...draft, weeklyBidWon });
                goNext();
              }}
            />
          )}
          {step === 5 && (
            <CopyStep
              draft={draft}
              onPrev={goPrev}
              onNext={(copy) => {
                setDraft({ ...draft, copy });
                goNext();
              }}
            />
          )}
          {step === 6 && (
            <PaymentStep
              draft={draft}
              onPrev={goPrev}
              onSubmitted={(camp) => {
                toast.success("검수 신청이 접수됐어요.");
                router.push(`/ads/${camp.id}`);
              }}
            />
          )}
        </div>

        <DraftSidebar draft={draft} step={step} />
      </div>
    </div>
  );
}

function Stepper({ step }: { step: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
      {STEPS.map((s, i) => {
        const active = step === s.id;
        const done = step > s.id;
        return (
          <li key={s.id} className="flex items-center gap-2">
            <span
              className={cn(
                "inline-flex size-5 items-center justify-center rounded-full border text-[10px] font-medium",
                done
                  ? "border-primary bg-primary text-primary-foreground"
                  : active
                    ? "border-primary text-primary"
                    : "border-border text-muted-foreground",
              )}
            >
              {done ? <CheckCircle2 className="size-3" /> : s.id}
            </span>
            <span
              className={cn(
                active
                  ? "text-foreground font-medium"
                  : "text-muted-foreground",
              )}
            >
              {s.title}
            </span>
            {i < STEPS.length - 1 && (
              <span className="text-muted-foreground/50">›</span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

function DraftSidebar({
  draft,
  step,
}: {
  draft: CampaignDraft;
  step: number;
}) {
  const total = draft.weeks.length * draft.weeklyBidWon;
  return (
    <Card className="h-fit">
      <CardHeader>
        <CardTitle className="text-sm">진행 요약</CardTitle>
        <CardDescription className="text-xs">
          단계 {step}/{STEPS.length}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 text-xs">
        <Row label="책">{draft.book?.title ?? "—"}</Row>
        <Row label="섹션">
          {draft.section
            ? `${BOOK_CATEGORY_LABEL[draft.section.category]} · ${AGE_RATING_LABEL[draft.section.ageRating]}`
            : "—"}
        </Row>
        <Row label="주차">
          {draft.weeks.length > 0 ? `${draft.weeks.length}주` : "—"}
        </Row>
        <Row label="주간 입찰">
          {draft.weeklyBidWon ? formatWon(draft.weeklyBidWon) : "—"}
        </Row>
        <div className="border-t border-border/60 pt-2">
          <Row label="총 예상 비용">
            <strong>{total ? formatWon(total) : "—"}</strong>
          </Row>
        </div>
      </CardContent>
    </Card>
  );
}

function Row({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-words text-right font-medium">
        {children}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 1: Book
// ---------------------------------------------------------------------------

function BookStep({
  draft,
  onNext,
}: {
  draft: CampaignDraft;
  onNext: (book: Book) => void;
}) {
  const { data, isLoading } = useQuery({
    queryKey: ["books", "on_sale"],
    queryFn: () => api<Book[]>("/books?status=on_sale"),
  });

  const [selectedId, setSelectedId] = useState<string | null>(
    draft.book?.id ?? null,
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">광고할 책 선택</CardTitle>
        <CardDescription>
          판매 중인 책만 광고할 수 있어요. 동일 책에 여러 섹션 캠페인을 동시에
          운영할 수도 있습니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading && <Skeleton className="h-32 w-full" />}
        {data?.length === 0 && (
          <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
            판매 중인 책이 없어요. 책 목록에서 먼저 등록·검수 후 판매 시작해주세요.
          </div>
        )}
        <div className="grid gap-3 md:grid-cols-2">
          {data?.map((b) => (
            <button
              key={b.id}
              type="button"
              onClick={() => setSelectedId(b.id)}
              className={cn(
                "flex items-start gap-3 rounded-lg border p-4 text-left transition",
                selectedId === b.id
                  ? "border-primary bg-primary/5"
                  : "border-border hover:bg-muted/40",
              )}
            >
              <div className="flex size-12 shrink-0 items-center justify-center rounded bg-muted text-xs">
                {b.title.slice(0, 2)}
              </div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-semibold">{b.title}</div>
                <div className="text-xs text-muted-foreground">{b.author}</div>
                <div className="mt-1 flex flex-wrap gap-1 text-[10px]">
                  <Badge variant="secondary">
                    {BOOK_CATEGORY_LABEL[b.category]}
                  </Badge>
                  <Badge variant="outline">
                    {AGE_RATING_LABEL[b.ageRating]}
                  </Badge>
                </div>
              </div>
            </button>
          ))}
        </div>

        <div className="flex justify-end pt-2">
          <Button
            size="lg"
            disabled={!selectedId}
            onClick={() => {
              const book = data?.find((b) => b.id === selectedId);
              if (book) onNext(book);
            }}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 2: Section
// ---------------------------------------------------------------------------

function SectionStep({
  draft,
  onNext,
  onPrev,
}: {
  draft: CampaignDraft;
  onNext: (section: AdSection) => void;
  onPrev: () => void;
}) {
  const [category, setCategory] = useState<BookCategory>(
    draft.section?.category ?? draft.book?.category ?? "novel",
  );
  const [ageRating, setAgeRating] = useState<AgeRating>(
    draft.section?.ageRating ?? draft.book?.ageRating ?? "all",
  );

  const matchOk = useMemo(() => {
    if (!draft.book) return false;
    return (
      draft.book.category === category && draft.book.ageRating === ageRating
    );
  }, [category, ageRating, draft.book]);

  const market = useQuery({
    queryKey: ["section-market", category, ageRating],
    queryFn: () =>
      api<SectionMarketStats>(
        `/ads/section-market?category=${category}&ageRating=${ageRating}`,
      ),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">섹션 선택</CardTitle>
        <CardDescription>
          섹션은 카테고리(소설/에세이/…) × 연령대 조합. 책의 메타와 일치해야
          노출됩니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="grid gap-2">
            <Label>카테고리</Label>
            <Select
              value={category}
              onValueChange={(v) => setCategory(v as BookCategory)}
            >
              <SelectTrigger>
                <SelectValue>
                  {(v: BookCategory) => BOOK_CATEGORY_LABEL[v]}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {Object.entries(BOOK_CATEGORY_LABEL).map(([v, l]) => (
                  <SelectItem key={v} value={v}>
                    {l}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>연령 등급</Label>
            <Select
              value={ageRating}
              onValueChange={(v) => setAgeRating(v as AgeRating)}
            >
              <SelectTrigger>
                <SelectValue>
                  {(v: AgeRating) => AGE_RATING_LABEL[v]}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {Object.entries(AGE_RATING_LABEL).map(([v, l]) => (
                  <SelectItem key={v} value={v}>
                    {l}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {!matchOk && draft.book && (
          <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-xs">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-destructive" />
            <div>
              <div className="font-medium text-destructive">
                매칭 위반 — 입찰이 차단됩니다
              </div>
              <div className="mt-1 text-destructive/80">
                선택한 책 「{draft.book.title}」의 카테고리/연령은{" "}
                <strong>
                  {BOOK_CATEGORY_LABEL[draft.book.category]} ·{" "}
                  {AGE_RATING_LABEL[draft.book.ageRating]}
                </strong>
                인데, 광고 섹션은{" "}
                <strong>
                  {BOOK_CATEGORY_LABEL[category]} · {AGE_RATING_LABEL[ageRating]}
                </strong>{" "}
                입니다.
              </div>
            </div>
          </div>
        )}

        {market.data && (
          <div className="rounded-lg border border-border bg-muted/30 p-4 text-xs">
            <div className="mb-2 font-medium">
              섹션 시장 정보 (지난 4주, 광고주당 익명 집계)
            </div>
            <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
              <Stat
                label="평균 주간 입찰"
                value={formatWon(market.data.avgWeeklyBidWon)}
              />
              <Stat
                label="최소 입찰"
                value={formatWon(market.data.minBidWon)}
              />
              <Stat
                label="현재 풀"
                value={formatWon(market.data.currentPoolWon)}
              />
              <Stat
                label="평균 CTR"
                value={`${(market.data.avgCtr * 100).toFixed(1)}%`}
              />
            </div>
            <div className="mt-2 text-muted-foreground">
              참여 광고주 {market.data.advertiserCount}곳
            </div>
          </div>
        )}

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={!matchOk}
            onClick={() => onNext({ category, ageRating })}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md bg-background p-2">
      <div className="text-[10px] text-muted-foreground">{label}</div>
      <div className="font-semibold">{value}</div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 3: Weeks
// ---------------------------------------------------------------------------

/** 주어진 날짜의 주(월요일 기준)을 YYYY-MM-DD로 변환. */
function mondayOf(date: Date) {
  const d = new Date(date);
  const day = d.getDay(); // 0=일, 1=월, ..., 6=토
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function ymd(d: Date) {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}

function WeeksStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: CampaignDraft;
  onPrev: () => void;
  onNext: (weeks: string[]) => void;
}) {
  // 다음 주 월요일부터 8주 후보
  const weekCandidates = useMemo(() => {
    const result: string[] = [];
    const start = mondayOf(new Date());
    start.setDate(start.getDate() + 7);
    for (let i = 0; i < 8; i++) {
      const d = new Date(start);
      d.setDate(start.getDate() + i * 7);
      result.push(ymd(d));
    }
    return result;
  }, []);

  const [selected, setSelected] = useState<string[]>(
    draft.weeks.length > 0 ? draft.weeks : weekCandidates.slice(0, 4),
  );

  const toggle = (w: string) =>
    setSelected((prev) =>
      prev.includes(w) ? prev.filter((x) => x !== w) : [...prev, w],
    );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">기간 (주 단위)</CardTitle>
        <CardDescription>
          월~일을 한 주로 셉니다. 최대 8주까지 예약할 수 있어요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
          {weekCandidates.map((w) => {
            const active = selected.includes(w);
            return (
              <button
                key={w}
                type="button"
                onClick={() => toggle(w)}
                className={cn(
                  "rounded-md border p-3 text-left transition",
                  active
                    ? "border-primary bg-primary/5"
                    : "border-border hover:bg-muted/40",
                )}
              >
                <div className="text-xs text-muted-foreground">
                  {formatDate(w)} 주
                </div>
                <div className="mt-1 text-sm font-medium">
                  {active ? "선택됨" : "선택"}
                </div>
              </button>
            );
          })}
        </div>
        <div className="text-xs text-muted-foreground">
          {selected.length}주 선택됨
        </div>

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={selected.length === 0}
            onClick={() => onNext([...selected].sort())}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 4: Bid
// ---------------------------------------------------------------------------

function BidStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: CampaignDraft;
  onPrev: () => void;
  onNext: (weeklyBidWon: number) => void;
}) {
  const market = useQuery({
    queryKey: [
      "section-market",
      draft.section?.category,
      draft.section?.ageRating,
    ],
    queryFn: () =>
      api<SectionMarketStats>(
        `/ads/section-market?category=${draft.section?.category}&ageRating=${draft.section?.ageRating}`,
      ),
    enabled: !!draft.section,
  });

  const [bid, setBid] = useState<number>(
    draft.weeklyBidWon || market.data?.minBidWon || 30_000,
  );

  // 시장 정보가 늦게 도착해도 초기값을 채워주기 — 외부 fetch 결과 동기화라 effect가 정당.
  useEffect(() => {
    if (!draft.weeklyBidWon && market.data && bid === 30_000) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setBid(market.data.minBidWon);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [market.data]);

  const expectedShare = market.data
    ? bid / (market.data.currentPoolWon + bid)
    : 0;
  const minBid = market.data?.minBidWon ?? 0;
  const valid = bid >= minBid;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">주간 입찰 금액</CardTitle>
        <CardDescription>
          섹션 최소 입찰가 이상으로 입력하세요. 노출 비율 = 내 입찰금 ÷ (현재 섹션 풀 +
          내 입찰금).
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {market.isLoading && <Skeleton className="h-24 w-full" />}
        {market.data && (
          <>
            <div className="grid gap-2">
              <Label>주간 입찰 (원)</Label>
              <Input
                type="number"
                min={market.data.minBidWon}
                step={5_000}
                value={bid}
                onChange={(e) => setBid(Number(e.target.value))}
              />
              {!valid && (
                <p className="text-xs text-destructive">
                  최소 입찰가 {formatWon(minBid)} 이상 필요해요.
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                최소 {formatWon(minBid)} · 평균{" "}
                {formatWon(market.data.avgWeeklyBidWon)} · 풀{" "}
                {formatWon(market.data.currentPoolWon)}
              </p>
            </div>

            <div className="rounded-lg border border-emerald-200 bg-emerald-50/50 p-4">
              <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
                <TrendingUp className="size-4" /> 예상 노출 비율
              </div>
              <div className="mt-2 text-2xl font-semibold">
                {(expectedShare * 100).toFixed(1)}%
              </div>
              <p className="mt-1 text-xs text-emerald-700/80">
                광고주당 섹션 점유 25~30% 도달 시 가드레일이 분산을 적용해요.
              </p>
            </div>
          </>
        )}

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={!valid}
            onClick={() => onNext(bid)}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 5: Copy
// ---------------------------------------------------------------------------

function CopyStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: CampaignDraft;
  onPrev: () => void;
  onNext: (copy: string) => void;
}) {
  const [copy, setCopy] = useState(draft.copy);
  const exceeded = copy.length > 40;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">광고 카피 (선택)</CardTitle>
        <CardDescription>
          책 표지·제목은 등록 정보를 그대로 사용합니다. 한 줄 카피(40자 이내)를
          더하면 검색·광고 노출 시 활용돼요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <Textarea
          value={copy}
          onChange={(e) => setCopy(e.target.value)}
          rows={3}
          placeholder="예: 이번 주, 사서가 만난 사람들의 이야기."
        />
        <div className="flex justify-between text-xs">
          <span className="text-muted-foreground">
            반려 사유 예: 카피 부적절·매칭 위반·평점 미달·소재 가독성 낮음
          </span>
          <span className={cn(exceeded && "text-destructive")}>
            {copy.length}/40
          </span>
        </div>

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={exceeded}
            onClick={() => onNext(copy.trim())}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 6: Payment & submit
// ---------------------------------------------------------------------------

function PaymentStep({
  draft,
  onPrev,
  onSubmitted,
}: {
  draft: CampaignDraft;
  onPrev: () => void;
  onSubmitted: (camp: AdCampaign) => void;
}) {
  const escrow = useQuery({
    queryKey: ["escrow"],
    queryFn: () => api<EscrowBalance>("/escrow"),
  });

  const [submitting, setSubmitting] = useState(false);

  const totalCost = draft.weeks.length * draft.weeklyBidWon;
  const insufficient =
    !!escrow.data && escrow.data.balanceWon < totalCost;

  async function submit() {
    if (!draft.book || !draft.section) return;
    setSubmitting(true);
    try {
      const created = await api<AdCampaign>("/ads/campaigns", {
        method: "POST",
        body: JSON.stringify({
          bookId: draft.book.id,
          bookTitle: draft.book.title,
          section: draft.section,
          weeks: draft.weeks,
          weeklyBidWon: draft.weeklyBidWon,
          copy: draft.copy || null,
        }),
      });
      onSubmitted(created);
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        toast.error(
          "예치금이 부족해요. 예치금 충전 후 다시 시도해주세요.",
        );
      } else {
        toast.error("검수 신청 중 오류");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">결제 · 검수 신청</CardTitle>
        <CardDescription>
          예치금에서 차감 후 관리자 큐로 진입합니다. 일시정지 시 잔여 주차는
          자동 환불돼요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-3 text-sm">
          <Summary label="책" value={draft.book?.title ?? "—"} />
          <Summary
            label="섹션"
            value={
              draft.section
                ? `${BOOK_CATEGORY_LABEL[draft.section.category]} · ${AGE_RATING_LABEL[draft.section.ageRating]}`
                : "—"
            }
          />
          <Summary
            label="기간"
            value={`${draft.weeks.length}주 (${formatDate(draft.weeks[0])}~)`}
          />
          <Summary label="주간 입찰" value={formatWon(draft.weeklyBidWon)} />
        </div>

        <div className="rounded-lg border border-border bg-muted/30 p-4 text-sm">
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">총 예상 비용</span>
            <strong className="text-base">{formatWon(totalCost)}</strong>
          </div>
          <div className="mt-2 flex items-center justify-between text-xs">
            <span className="text-muted-foreground">현재 예치금 잔액</span>
            <span>
              {escrow.data ? formatWon(escrow.data.balanceWon) : "—"}
            </span>
          </div>
          {insufficient && (
            <div className="mt-3 flex items-center gap-2 rounded-md bg-destructive/10 p-2 text-xs text-destructive">
              <AlertTriangle className="size-3.5" /> 예치금이 부족해요. [광고
              목록 → 예치금 충전]에서 충전 후 다시 시도해주세요.
            </div>
          )}
        </div>

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
            disabled={submitting}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={insufficient || submitting}
            onClick={submit}
          >
            {submitting ? (
              "신청 중…"
            ) : (
              <>
                <Megaphone className="size-4" /> 검수 신청
              </>
            )}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function Summary({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-1 font-medium">{value}</div>
    </div>
  );
}
