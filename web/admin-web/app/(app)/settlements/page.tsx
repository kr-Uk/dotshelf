"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowUpRight, CheckCircle2, CircleAlert, Coins } from "lucide-react";

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
import { formatDateTime, formatWon } from "@/lib/format";
import type { AccountingReconciliation } from "@/lib/types";

export default function SettlementsPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["settlement-reconciliation"],
    queryFn: () => api<AccountingReconciliation>("/settlement/reconciliation"),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="정산 · 페이지 모니터링"
        description="사용자 보유 페이지와 에셋 구매 누적을 실제 장부 데이터로 확인합니다."
        actions={
          <Button size="sm" variant="outline" render={<Link href="/users" />}>
            사용자별 환불·잔액 조정 <ArrowUpRight className="size-3.5" />
          </Button>
        }
      />

      <Card className={data && !data.consistent ? "ring-2 ring-destructive" : undefined}>
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Coins className="size-4" /> 페이지 장부 대사
              </CardTitle>
              <CardDescription>
                {data ? `기준 ${formatDateTime(data.asOf)}` : "실시간 장부 계산"}
              </CardDescription>
            </div>
            <StatusBadge tone={data ? (data.consistent ? "success" : "danger") : "neutral"}>
              {!data ? (
                "—"
              ) : data.consistent ? (
                <span className="inline-flex items-center gap-1">
                  <CheckCircle2 className="size-3.5" /> 정상
                </span>
              ) : (
                <span className="inline-flex items-center gap-1">
                  <CircleAlert className="size-3.5" /> 확인 필요
                </span>
              )}
            </StatusBadge>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <StatCard
            label="사용자 보유 페이지"
            value={data ? formatWon(data.prepaidBalanceWon) : isLoading ? "—" : "0원"}
            caption="page_wallets 합계"
          />
          <StatCard
            label="에셋 구매 누적"
            value={data ? formatWon(data.assetPurchaseWon) : isLoading ? "—" : "0원"}
            caption="asset_purchases 합계"
          />
          <StatCard
            label="정산 대기"
            value={data ? formatWon(data.pendingPayoutWon) : isLoading ? "—" : "0원"}
            caption="현재 운영 범위에서는 보류성 금액만 표시"
          />
        </CardContent>
      </Card>
    </div>
  );
}
