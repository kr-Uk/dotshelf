"use client";

import Link from "next/link";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CreditCard, Megaphone, Wallet } from "lucide-react";
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
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { formatDate, formatWon } from "@/lib/format";
import {
  AD_CAMPAIGN_STATUS_LABEL,
  AGE_RATING_LABEL,
  BOOK_CATEGORY_LABEL,
  type AdCampaign,
  type AdCampaignStatus,
  type EscrowBalance,
} from "@/lib/types";

type StatusFilter = AdCampaignStatus | "all";

const STATUS_OPTIONS: Array<{ value: StatusFilter; label: string }> = [
  { value: "all", label: "전체" },
  { value: "draft", label: AD_CAMPAIGN_STATUS_LABEL.draft },
  { value: "in_review", label: AD_CAMPAIGN_STATUS_LABEL.in_review },
  { value: "running", label: AD_CAMPAIGN_STATUS_LABEL.running },
  { value: "paused", label: AD_CAMPAIGN_STATUS_LABEL.paused },
  { value: "ended", label: AD_CAMPAIGN_STATUS_LABEL.ended },
  { value: "rejected", label: AD_CAMPAIGN_STATUS_LABEL.rejected },
];

function statusBadge(status: AdCampaignStatus) {
  if (status === "running") {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {AD_CAMPAIGN_STATUS_LABEL.running}
      </Badge>
    );
  }
  if (status === "rejected") {
    return (
      <Badge variant="destructive">{AD_CAMPAIGN_STATUS_LABEL.rejected}</Badge>
    );
  }
  if (status === "in_review") {
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {AD_CAMPAIGN_STATUS_LABEL.in_review}
      </Badge>
    );
  }
  if (status === "paused") {
    return (
      <Badge className="bg-zinc-100 text-zinc-700 hover:bg-zinc-100/80">
        {AD_CAMPAIGN_STATUS_LABEL.paused}
      </Badge>
    );
  }
  if (status === "ended") {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        {AD_CAMPAIGN_STATUS_LABEL.ended}
      </Badge>
    );
  }
  return <Badge variant="secondary">{AD_CAMPAIGN_STATUS_LABEL[status]}</Badge>;
}

export default function AdsPage() {
  const [status, setStatus] = useState<StatusFilter>("all");

  const escrow = useQuery({
    queryKey: ["escrow"],
    queryFn: () => api<EscrowBalance>("/escrow"),
  });

  const campaigns = useQuery({
    queryKey: ["ads", "campaigns", status],
    queryFn: () => {
      const qs = status === "all" ? "" : `?status=${status}`;
      return api<AdCampaign[]>(`/ads/campaigns${qs}`);
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="text-xs font-medium text-muted-foreground">
            5. 책 광고 신청
          </div>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">광고</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            주간 SoV 입찰 + 가드레일. 캠페인 = 책 × 섹션(카테고리×연령) × 주차.
          </p>
        </div>
        <Button size="lg" render={<Link href="/creator/ads/new" />}>
          <Megaphone className="size-4" /> 새 캠페인
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <EscrowCard escrow={escrow.data} loading={escrow.isLoading} />
        <Card>
          <CardHeader>
            <CardDescription>진행 중 캠페인</CardDescription>
            <CardTitle className="text-2xl">
              {campaigns.data
                ? campaigns.data.filter((c) => c.status === "running").length +
                  "개"
                : "—"}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs text-muted-foreground">
            가드레일: 광고주당 섹션 점유 25~30% 도달 시 분산
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>검토 중</CardDescription>
            <CardTitle className="text-2xl">
              {campaigns.data
                ? campaigns.data.filter((c) => c.status === "in_review")
                    .length + "개"
                : "—"}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs text-muted-foreground">
            반려 시 사유 확인 후 수정·재신청
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="text-base">캠페인 목록</CardTitle>
            <Select
              value={status}
              onValueChange={(v) => setStatus(v as StatusFilter)}
            >
              <SelectTrigger className="w-44">
                <SelectValue placeholder="상태">
                  {(v: StatusFilter) =>
                    STATUS_OPTIONS.find((o) => o.value === v)?.label ?? "상태"
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-6">책 / 섹션</TableHead>
                <TableHead>주차</TableHead>
                <TableHead>주간 입찰</TableHead>
                <TableHead>최근 노출 / CTR</TableHead>
                <TableHead className="pr-6 text-right">상태</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {campaigns.isLoading &&
                [1, 2, 3].map((i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={5} className="px-6">
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                  </TableRow>
                ))}
              {campaigns.data?.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className="px-6 py-12 text-center text-sm text-muted-foreground"
                  >
                    조건에 맞는 캠페인이 없어요.
                  </TableCell>
                </TableRow>
              )}
              {campaigns.data?.map((c) => {
                const last = c.weekStats.at(-1);
                return (
                  <TableRow key={c.id}>
                    <TableCell className="pl-6">
                      <Link
                        href={`/creator/ads/${c.id}`}
                        className="font-medium hover:underline"
                      >
                        {c.bookTitle}
                      </Link>
                      <div className="mt-0.5 flex flex-wrap gap-1 text-xs">
                        <Badge variant="secondary" className="text-[10px]">
                          {BOOK_CATEGORY_LABEL[c.section.category]}
                        </Badge>
                        <Badge variant="outline" className="text-[10px]">
                          {AGE_RATING_LABEL[c.section.ageRating]}
                        </Badge>
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">
                      {c.weeks.length}주차
                      <div className="text-xs text-muted-foreground">
                        {formatDate(c.weeks[0])} 시작
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">
                      {formatWon(c.weeklyBidWon)}
                      <div className="text-xs text-muted-foreground">
                        주당
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">
                      {last ? (
                        <>
                          {last.impressions.toLocaleString()}회
                          <div className="text-xs text-muted-foreground">
                            CTR {(last.ctr * 100).toFixed(1)}%
                          </div>
                        </>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="pr-6 text-right">
                      {statusBadge(c.status)}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function EscrowCard({
  escrow,
  loading,
}: {
  escrow: EscrowBalance | undefined;
  loading: boolean;
}) {
  return (
    <Card>
      <CardHeader>
        <CardDescription className="flex items-center gap-2">
          <Wallet className="size-3.5" /> 광고 예치금
          <Badge variant="outline" className="text-[10px]">
            분리 보관
          </Badge>
        </CardDescription>
        <CardTitle className="text-2xl">
          {loading ? (
            <Skeleton className="h-7 w-32" />
          ) : (
            formatWon(escrow?.balanceWon ?? 0)
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2 text-xs text-muted-foreground">
        {escrow ? (
          <>
            <div>{escrow.custodianBankName}</div>
            <div>
              마지막 충전:{" "}
              {escrow.lastChargedAt
                ? formatDate(escrow.lastChargedAt)
                : "—"}
            </div>
          </>
        ) : (
          <Skeleton className="h-4 w-full" />
        )}
        <ChargeDialog />
      </CardContent>
    </Card>
  );
}

function ChargeDialog() {
  const queryClient = useQueryClient();
  const [amount, setAmount] = useState(100_000);

  const charge = useMutation({
    mutationFn: () =>
      api<EscrowBalance>("/escrow/charge", {
        method: "POST",
        body: JSON.stringify({ amountWon: amount }),
      }),
    onSuccess: (next) => {
      queryClient.setQueryData(["escrow"], next);
      toast.success(`${formatWon(amount)} 충전됐어요.`);
    },
    onError: () => toast.error("충전 중 오류"),
  });

  return (
    <Dialog>
      <DialogTrigger
        render={
          <Button variant="outline" size="sm" className={cn("mt-3 w-full")}>
            <CreditCard className="size-3.5" /> 예치금 충전
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>광고 예치금 충전</DialogTitle>
          <DialogDescription>
            카드 / 계좌이체. 충전금은 운영 계좌와 분리 보관(전자금융 컴플라이언스)됩니다.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid grid-cols-3 gap-2">
            {[100_000, 300_000, 500_000].map((v) => (
              <Button
                key={v}
                variant={amount === v ? "default" : "outline"}
                onClick={() => setAmount(v)}
              >
                {formatWon(v)}
              </Button>
            ))}
          </div>
          <Input
            type="number"
            value={amount}
            onChange={(e) => setAmount(Number(e.target.value))}
            min={10_000}
            step={10_000}
          />
        </div>
        <DialogFooter>
          <DialogClose
            render={
              <Button
                onClick={() => charge.mutate()}
                disabled={charge.isPending}
              >
                {charge.isPending ? "충전 중…" : `${formatWon(amount)} 충전`}
              </Button>
            }
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

