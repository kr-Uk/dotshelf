"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowUpRight,
  Coins,
  Flag,
  Gauge,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { PageHeader } from "@/components/common/page-header";
import { StatCard } from "@/components/common/stat-card";
import { StatusBadge } from "@/components/common/status-badge";
import { api } from "@/lib/api";
import { formatNumber, formatWon } from "@/lib/format";
import type { DashboardStats } from "@/lib/types";

export default function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: () => api<DashboardStats>("/dashboard/stats"),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="홈 · 운영 현황 한눈에 보기"
        description="신고 큐 / 정산 알림 / 운영 KPI를 요약합니다."
      />

      <section className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>신고·모더레이션 큐</CardTitle>
              <Flag className="size-4 text-muted-foreground" />
            </div>
            <CardDescription>미처리 신고 + 자동 검출 우려 콘텐츠</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <DashboardRow label="미처리" value={data?.moderationQueue.open} loading={isLoading} />
            <DashboardRow label="자동 검출" value={data?.moderationQueue.autoFlagged} loading={isLoading} />
            <div className="flex items-center justify-between border-t pt-2">
              <span className="text-xs text-muted-foreground">P1(1시간 SLA)</span>
              <StatusBadge tone={data && data.moderationQueue.p1 > 0 ? "danger" : "neutral"}>
                {data ? `${data.moderationQueue.p1}건` : "—"}
              </StatusBadge>
            </div>
            <Button variant="outline" size="sm" className="w-full" render={<Link href="/moderation" />}>
              모더레이션 큐로 <ArrowUpRight className="size-3.5" />
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>정산 알림</CardTitle>
              <Coins className="size-4 text-muted-foreground" />
            </div>
            <CardDescription>이번 주 정산 대기 / 회계 대조 상태</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs text-muted-foreground">정산 대기</span>
              <span className="text-sm font-semibold tabular-nums">
                {data ? formatWon(data.settlement.pendingPayoutWon) : "—"}
              </span>
            </div>
            <div className="flex items-center justify-between border-t pt-2">
              <span className="text-xs text-muted-foreground">회계 대조</span>
              <StatusBadge tone={data?.settlement.reconciliationOk ? "success" : "danger"}>
                {data?.settlement.reconciliationOk ? "일치" : "불일치"}
              </StatusBadge>
            </div>
            <Button variant="outline" size="sm" className="w-full" render={<Link href="/settlements" />}>
              정산 화면으로 <ArrowUpRight className="size-3.5" />
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>운영 KPI</CardTitle>
              <Gauge className="size-4 text-muted-foreground" />
            </div>
            <CardDescription>DAU · 충전금 잔액 · API 에러율</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs text-muted-foreground">DAU</span>
              <span className="text-sm font-semibold tabular-nums">
                {data ? formatNumber(data.ops.dau) : "—"}
              </span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-xs text-muted-foreground">선불 충전금 잔액</span>
              <span className="text-sm font-semibold tabular-nums">
                {data ? formatWon(data.ops.prepaidBalanceWon) : "—"}
              </span>
            </div>
            <div className="flex items-center justify-between border-t pt-2">
              <span className="text-xs text-muted-foreground">API 에러율</span>
              <StatusBadge tone={data && data.ops.apiErrorRate < 0.01 ? "success" : "warn"}>
                {data ? `${(data.ops.apiErrorRate * 100).toFixed(2)}%` : "—"}
              </StatusBadge>
            </div>
            <Button variant="outline" size="sm" className="w-full" render={<Link href="/stats" />}>
              통계로 이동 <ArrowUpRight className="size-3.5" />
            </Button>
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 md:grid-cols-2">
        <StatCard
          label="모더레이션 SLA"
          value="P1 0건"
          caption="P1 1시간 / P2 24시간 / P3 72시간 정책"
        />
        <StatCard
          label="감사 로그 적재"
          value="12,481"
          caption="최근 30일 누적 — 전부 90일 이상 보존"
        />
      </section>
    </div>
  );
}

function DashboardRow({
  label,
  value,
  loading,
}: {
  label: string;
  value: number | undefined;
  loading: boolean;
}) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-semibold tabular-nums">
        {loading || value === undefined ? "—" : `${value}건`}
      </span>
    </div>
  );
}
