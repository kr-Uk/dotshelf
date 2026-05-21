"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  CheckCircle2,
  Clock,
  PauseCircle,
  PlayCircle,
  RotateCcw,
  Truck,
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
import { Skeleton } from "@/components/ui/skeleton";
import { ResubmitDialog } from "@/components/common/resubmit-dialog";
import { api, ApiError } from "@/lib/api";
import { useNow } from "@/lib/use-now";
import { cn } from "@/lib/utils";
import { formatDate, formatPages, pagesToWon } from "@/lib/format";
import {
  AGE_RATING_LABEL,
  BOOK_CATEGORY_LABEL,
  BOOK_STATUS_LABEL,
  CONSIGNMENT_STAGE_LABEL,
  type Book,
  type ConsignmentStage,
} from "@/lib/types";

const CONSIGNMENT_FLOW: ConsignmentStage[] = [
  "requested",
  "shipping_label_issued",
  "received",
  "scanning",
  "review",
  "applied",
];

export default function BookDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id as string;
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ["books", id],
    queryFn: () => api<Book>(`/books/${id}`),
    enabled: !!id,
  });

  const toggleSale = useMutation({
    mutationFn: () =>
      api<Book>(`/books/${id}/toggle-sale`, { method: "POST" }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["books", id], updated);
      queryClient.invalidateQueries({ queryKey: ["books"] });
      toast.success(
        updated.status === "on_sale" ? "판매를 재개했어요." : "판매를 중지했어요.",
      );
    },
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="space-y-4">
        <Link
          href="/creator/books"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 책 목록으로
        </Link>
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            책을 찾지 못했어요.
          </CardContent>
        </Card>
      </div>
    );
  }

  const isOnSale = data.status === "on_sale";
  const canToggleSale = data.status === "on_sale" || data.status === "off_sale";

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/creator/books"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 책 목록으로
        </Link>
        <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {data.title}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {data.author} · {data.publisher} ·{" "}
              {data.isbn ? `ISBN ${data.isbn}` : "자가출판"}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={data.status} />
            {canToggleSale && (
              <Button
                variant={isOnSale ? "destructive" : "default"}
                onClick={() => toggleSale.mutate()}
                disabled={toggleSale.isPending}
              >
                {isOnSale ? (
                  <>
                    <PauseCircle className="size-4" /> 판매 중지
                  </>
                ) : (
                  <>
                    <PlayCircle className="size-4" /> 판매 재개
                  </>
                )}
              </Button>
            )}
          </div>
        </div>
      </div>

      {data.status === "rejected" && data.rejectionReason && (
        <Card className="border-destructive/30 bg-destructive/5">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base text-destructive">
              <RotateCcw className="size-4" /> 반려됐어요
            </CardTitle>
            <CardDescription className="text-destructive/80">
              사유를 확인하고 수정 후 재신청해주세요.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="rounded-md bg-background/60 p-3 text-sm">
              {data.rejectionReason}
            </p>
            <div className="mt-3">
              <ResubmitDialog<Book>
                rejectionReason={data.rejectionReason}
                guideTips={[
                  "표지·텍스처 해상도 가이드라인(2048px 이상) 충족 여부",
                  "메타데이터(제목·저자·줄거리)와 실제 콘텐츠 일치 여부",
                  "미리보기 PDF/EPUB 첨부 여부 — 청약철회 면책 1요건",
                  "권리 증빙 파일이 최신인지 (계약 만료 등)",
                ]}
                resubmit={async ({ changeNote, extra }) =>
                  api<Book>(`/books/${data.id}/resubmit`, {
                    method: "POST",
                    body: JSON.stringify({
                      changeNote,
                      ...extra,
                    }),
                  })
                }
                onSuccess={(updated) => {
                  queryClient.setQueryData(["books", id], updated);
                  queryClient.invalidateQueries({ queryKey: ["books"] });
                }}
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">기본 정보</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3 text-sm">
            <Row label="줄거리·소개">{data.synopsis || "—"}</Row>
            <Row label="카테고리">
              <Badge variant="secondary">
                {BOOK_CATEGORY_LABEL[data.category]}
              </Badge>
            </Row>
            <Row label="연령 등급">
              <Badge variant="outline">{AGE_RATING_LABEL[data.ageRating]}</Badge>
            </Row>
            <Row label="판매 시작일">
              {data.saleStartDate ? formatDate(data.saleStartDate) : "—"}
            </Row>
            <Row label="마케팅 카피">{data.marketingCopy ?? "—"}</Row>
            <Row label="미리보기">
              {data.previewSampleName ? (
                <div className="flex flex-wrap items-center gap-2">
                  <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
                    첨부됨
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {data.previewSampleName}
                    {data.previewSamplePageCount
                      ? ` · ${data.previewSamplePageCount}p`
                      : ""}
                  </span>
                </div>
              ) : (
                <div className="flex items-center gap-2">
                  <Badge variant="destructive">미첨부</Badge>
                  <span className="text-xs text-destructive/80">
                    청약철회 면책 1요건 미충족 — 검수 통과 후에도 노출 차단
                  </span>
                </div>
              )}
            </Row>
            <Row label="업데이트">{formatDate(data.updatedAt)}</Row>
          </CardContent>
        </Card>

        <PriceCard book={data} />
      </div>

      {data.coverMode === "consignment" && (
        <ConsignmentTracker stage={data.consignmentStage} />
      )}
    </div>
  );
}

function StatusBadge({ status }: { status: Book["status"] }) {
  if (status === "on_sale") {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {BOOK_STATUS_LABEL.on_sale}
      </Badge>
    );
  }
  if (status === "rejected") {
    return <Badge variant="destructive">{BOOK_STATUS_LABEL.rejected}</Badge>;
  }
  if (status === "in_review" || status === "submitted") {
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {BOOK_STATUS_LABEL[status]}
      </Badge>
    );
  }
  if (status === "off_sale") {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        {BOOK_STATUS_LABEL.off_sale}
      </Badge>
    );
  }
  return <Badge variant="secondary">{BOOK_STATUS_LABEL[status]}</Badge>;
}

function Row({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid grid-cols-[120px_1fr] items-start gap-3 border-b border-border/60 pb-3 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}

function PriceCard({ book }: { book: Book }) {
  const queryClient = useQueryClient();
  const [pricePages, setPricePages] = useState<number>(book.pricePages);
  const now = useNow(60_000);

  const cooldownMs = book.lastPriceChangedAt
    ? 24 * 60 * 60 * 1000 -
      (now - new Date(book.lastPriceChangedAt).getTime())
    : 0;
  const cooldownActive = cooldownMs > 0;
  const cooldownHours = Math.ceil(cooldownMs / (60 * 60 * 1000));

  const updatePrice = useMutation({
    mutationFn: () =>
      api<Book>(`/books/${book.id}/price`, {
        method: "POST",
        body: JSON.stringify({ pricePages }),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["books", book.id], updated);
      queryClient.invalidateQueries({ queryKey: ["books"] });
      toast.success("가격을 업데이트했어요.");
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error("가격은 24시간 쿨다운 후에만 변경할 수 있어요.");
      } else {
        toast.error("가격 변경 중 오류");
      }
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">가격</CardTitle>
        <CardDescription className="text-xs">
          1p = 10원. 변경 시 24시간 쿨다운 (덤핑 방지).
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="text-3xl font-semibold">
          {formatPages(book.pricePages)}
        </div>
        <div className="text-xs text-muted-foreground">
          ≈ {pagesToWon(book.pricePages).toLocaleString()}원
        </div>
        <div className="flex items-center gap-2 pt-2">
          <Input
            type="number"
            min={100}
            step={50}
            value={pricePages}
            onChange={(e) => setPricePages(Number(e.target.value))}
          />
          <Button
            size="sm"
            onClick={() => updatePrice.mutate()}
            disabled={
              cooldownActive ||
              updatePrice.isPending ||
              pricePages === book.pricePages
            }
          >
            저장
          </Button>
        </div>
        {cooldownActive && (
          <p className="flex items-center gap-1 text-xs text-amber-700">
            <Clock className="size-3" /> 약 {cooldownHours}시간 후 변경 가능
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function ConsignmentTracker({ stage }: { stage: ConsignmentStage }) {
  if (stage === "not_applicable") return null;
  const currentIdx = CONSIGNMENT_FLOW.indexOf(stage);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Truck className="size-4" /> 위탁 3D 제작 트래킹
        </CardTitle>
        <CardDescription>
          실물 책 1부 발송 → 스캐닝 → 검수 → 적용까지의 진행 상황입니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ol className="grid gap-3 md:grid-cols-6">
          {CONSIGNMENT_FLOW.map((s, i) => {
            const reached = i <= currentIdx;
            const current = i === currentIdx;
            return (
              <li
                key={s}
                className={cn(
                  "rounded-lg border p-3 text-xs transition",
                  current
                    ? "border-primary bg-primary/5 text-foreground"
                    : reached
                      ? "border-border bg-muted/30 text-foreground"
                      : "border-border/60 text-muted-foreground",
                )}
              >
                <div className="flex items-center gap-2">
                  <span
                    className={cn(
                      "inline-flex size-5 items-center justify-center rounded-full border text-[10px]",
                      reached
                        ? "border-primary bg-primary text-primary-foreground"
                        : "border-border",
                    )}
                  >
                    {reached ? <CheckCircle2 className="size-3" /> : i + 1}
                  </span>
                  <span className="font-medium">
                    {CONSIGNMENT_STAGE_LABEL[s]}
                  </span>
                </div>
              </li>
            );
          })}
        </ol>
      </CardContent>
    </Card>
  );
}
