"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowLeft,
  Clock,
  Pause,
  RotateCcw,
  ShieldAlert,
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ResubmitDialog } from "@/components/common/resubmit-dialog";
import { api, ApiError } from "@/lib/api";
import { formatDate, formatWon } from "@/lib/format";
import {
  AD_CAMPAIGN_STATUS_LABEL,
  AGE_RATING_LABEL,
  BOOK_CATEGORY_LABEL,
  GUARDRAIL_LABEL,
  type AdCampaign,
} from "@/lib/types";

export default function CampaignDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id as string;
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ["ads", "campaigns", id],
    queryFn: () => api<AdCampaign>(`/ads/campaigns/${id}`),
    enabled: !!id,
  });

  const pause = useMutation({
    mutationFn: () =>
      api<{ campaign: AdCampaign; refund: number }>(
        `/ads/campaigns/${id}/pause`,
        { method: "POST" },
      ),
    onSuccess: ({ campaign, refund }) => {
      queryClient.setQueryData(["ads", "campaigns", id], campaign);
      queryClient.invalidateQueries({ queryKey: ["ads", "campaigns"] });
      queryClient.invalidateQueries({ queryKey: ["escrow"] });
      toast.success(
        refund > 0
          ? `일시정지했어요. ${formatWon(refund)} 환불됐어요.`
          : "일시정지했어요.",
      );
    },
    onError: () => toast.error("일시정지 처리 중 오류"),
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
          href="/creator/ads"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 광고 목록으로
        </Link>
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            캠페인을 찾지 못했어요.
          </CardContent>
        </Card>
      </div>
    );
  }

  const totalImpressions = data.weekStats.reduce(
    (a, w) => a + w.impressions,
    0,
  );
  const totalClicks = data.weekStats.reduce((a, w) => a + w.clicks, 0);
  const totalConversions = data.weekStats.reduce(
    (a, w) => a + w.conversions,
    0,
  );
  const overallCtr =
    totalImpressions > 0 ? totalClicks / totalImpressions : 0;
  const triggeredGuardrails = Array.from(
    new Set(data.weekStats.flatMap((w) => w.guardrails)),
  );

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/creator/ads"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 광고 목록으로
        </Link>
        <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {data.bookTitle}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {BOOK_CATEGORY_LABEL[data.section.category]} ·{" "}
              {AGE_RATING_LABEL[data.section.ageRating]} · {data.weeks.length}주
              · {formatDate(data.weeks[0])} 시작
            </p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={data.status} />
            {data.status === "running" && (
              <Button
                variant="destructive"
                onClick={() => pause.mutate()}
                disabled={pause.isPending}
              >
                <Pause className="size-4" /> 일시정지
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
          </CardHeader>
          <CardContent>
            <p className="rounded-md bg-background/60 p-3 text-sm">
              {data.rejectionReason}
            </p>
            <div className="mt-3">
              <ResubmitDialog<AdCampaign>
                rejectionReason={data.rejectionReason}
                guideTips={[
                  "책 카테고리·연령과 광고 섹션이 일치하는지",
                  "광고 카피 40자 이내 + 표시광고법 위반 표현(거짓·과장·기만) 제거",
                  "주간 입찰가가 섹션 최소 입찰가 이상인지",
                  "예치금 잔액이 캠페인 총 비용 이상인지",
                ]}
                inlineEditor={({ onChange }) => (
                  <CampaignResubmitInline
                    initialCopy={data.copy ?? ""}
                    initialBid={data.weeklyBidWon}
                    onChange={onChange}
                  />
                )}
                resubmit={async ({ changeNote, extra }) =>
                  api<AdCampaign>(`/ads/campaigns/${data.id}/resubmit`, {
                    method: "POST",
                    body: JSON.stringify({
                      changeNote,
                      ...extra,
                    }),
                  })
                }
                onSuccess={(updated) => {
                  queryClient.setQueryData(
                    ["ads", "campaigns", id],
                    updated,
                  );
                  queryClient.invalidateQueries({
                    queryKey: ["ads", "campaigns"],
                  });
                }}
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 md:grid-cols-4">
        <KPI label="누적 노출" value={totalImpressions.toLocaleString()} />
        <KPI label="누적 클릭" value={totalClicks.toLocaleString()} />
        <KPI
          label="평균 CTR"
          value={`${(overallCtr * 100).toFixed(2)}%`}
        />
        <KPI
          label="전환 (책 추가)"
          value={totalConversions.toLocaleString()}
        />
      </div>

      {triggeredGuardrails.length > 0 && (
        <Card className="border-amber-200 bg-amber-50/50">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base text-amber-900">
              <ShieldAlert className="size-4" /> 가드레일 트리거
            </CardTitle>
            <CardDescription className="text-amber-900/70">
              플랫폼이 자동으로 노출을 분산하거나 제한했어요.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {triggeredGuardrails.map((g) => (
              <div
                key={g}
                className="rounded-md bg-background/60 px-3 py-2 text-sm"
              >
                <Badge
                  variant="outline"
                  className="mr-2 border-amber-300 text-amber-800"
                >
                  알림
                </Badge>
                {GUARDRAIL_LABEL[g]}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">주차별 운영 정보</CardTitle>
          <CardDescription>
            진행 중 캠페인의 실시간 노출 / CTR / 전환 (책 추가). SoV는 이 광고주의
            섹션 점유 비율.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-6">주차</TableHead>
                <TableHead>입찰</TableHead>
                <TableHead>노출</TableHead>
                <TableHead>CTR</TableHead>
                <TableHead>전환</TableHead>
                <TableHead className="pr-6 text-right">점유율</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.weekStats.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="px-6 py-10 text-center text-sm text-muted-foreground"
                  >
                    아직 운영 데이터가 없어요. 검수 통과 후 첫 주차부터 데이터가
                    쌓여요.
                  </TableCell>
                </TableRow>
              )}
              {data.weekStats.map((w) => (
                <TableRow key={w.weekStart}>
                  <TableCell className="pl-6">
                    <div className="font-medium">{formatDate(w.weekStart)}</div>
                    {w.guardrails.length > 0 && (
                      <div className="mt-1 flex flex-wrap gap-1">
                        {w.guardrails.map((g) => (
                          <Badge
                            key={g}
                            variant="outline"
                            className="border-amber-300 text-[10px] text-amber-800"
                          >
                            {GUARDRAIL_LABEL[g]}
                          </Badge>
                        ))}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>{formatWon(w.weeklyBidWon)}</TableCell>
                  <TableCell>{w.impressions.toLocaleString()}</TableCell>
                  <TableCell>{(w.ctr * 100).toFixed(2)}%</TableCell>
                  <TableCell>{w.conversions}</TableCell>
                  <TableCell className="pr-6 text-right">
                    {(w.shareOfVoice * 100).toFixed(0)}%
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <BidUpdateCard campaign={data} />
    </div>
  );
}

function StatusBadge({ status }: { status: AdCampaign["status"] }) {
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

function KPI({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="text-2xl">{value}</CardTitle>
      </CardHeader>
    </Card>
  );
}

function CampaignResubmitInline({
  initialCopy,
  initialBid,
  onChange,
}: {
  initialCopy: string;
  initialBid: number;
  onChange: (patch: Partial<Record<string, unknown>>) => void;
}) {
  const [copy, setCopy] = useState(initialCopy);
  const [bid, setBid] = useState(initialBid);
  return (
    <div className="grid gap-3">
      <div className="grid gap-1.5">
        <span className="text-xs font-medium">광고 카피 (40자 이내)</span>
        <Input
          value={copy}
          maxLength={40}
          onChange={(e) => {
            setCopy(e.target.value);
            onChange({ copy: e.target.value });
          }}
        />
      </div>
      <div className="grid gap-1.5">
        <span className="text-xs font-medium">주간 입찰</span>
        <Input
          type="number"
          min={10_000}
          step={5_000}
          value={bid}
          onChange={(e) => {
            const v = Number(e.target.value);
            setBid(v);
            onChange({ weeklyBidWon: v });
          }}
        />
      </div>
    </div>
  );
}

function BidUpdateCard({ campaign }: { campaign: AdCampaign }) {
  const queryClient = useQueryClient();
  const [bid, setBid] = useState(campaign.weeklyBidWon);

  const update = useMutation({
    mutationFn: () =>
      api<AdCampaign>(`/ads/campaigns/${campaign.id}/update-bid`, {
        method: "POST",
        body: JSON.stringify({ weeklyBidWon: bid }),
      }),
    onSuccess: (next) => {
      queryClient.setQueryData(["ads", "campaigns", campaign.id], next);
      queryClient.invalidateQueries({ queryKey: ["ads", "campaigns"] });
      toast.success("다음 주차부터 적용돼요.");
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error("이 상태에서는 입찰을 변경할 수 없어요.");
      } else {
        toast.error("입찰 변경 중 오류");
      }
    },
  });

  const editable =
    campaign.status === "running" || campaign.status === "in_review";

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Clock className="size-4" /> 다음 주차 입찰 변경
        </CardTitle>
        <CardDescription>
          주 시작 24시간 전까지 변경 가능. 진행 중 캠페인은 다음 주차부터
          적용됩니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {!editable && (
          <div className="flex items-center gap-2 rounded-md bg-muted/50 p-3 text-xs text-muted-foreground">
            <AlertTriangle className="size-3.5" /> 진행 중 / 검토 중 캠페인만
            입찰을 변경할 수 있어요.
          </div>
        )}
        <div className="flex items-center gap-2">
          <Input
            type="number"
            min={10_000}
            step={5_000}
            value={bid}
            onChange={(e) => setBid(Number(e.target.value))}
            disabled={!editable}
          />
          <Button
            onClick={() => update.mutate()}
            disabled={
              !editable ||
              update.isPending ||
              bid === campaign.weeklyBidWon
            }
          >
            저장
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
