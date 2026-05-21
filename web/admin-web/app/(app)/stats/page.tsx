"use client";

import { useQueries } from "@tanstack/react-query";

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
import {
  CategoryRevenuePie,
  ConversionFunnel,
  DauChart,
  ModerationChart,
  NewSignupsChart,
  RevenueStackedChart,
  SystemChart,
} from "@/components/common/charts";
import { api } from "@/lib/api";
import { formatNumber, formatWon } from "@/lib/format";
import type { KpiSnapshot, KpiTimeseries } from "@/lib/types";

export default function StatsPage() {
  const [kpiQ, tsQ] = useQueries({
    queries: [
      {
        queryKey: ["kpi"],
        queryFn: () => api<KpiSnapshot>("/stats/kpi"),
      },
      {
        queryKey: ["kpi-timeseries"],
        queryFn: () => api<KpiTimeseries>("/stats/timeseries"),
      },
    ],
  });

  const data = kpiQ.data;
  const ts = tsQ.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="통계 · KPI"
        description="사용자 / 수익 / 모더레이션 / 시스템 KPI를 시계열·구성비로."
        actions={
          <>
            <Button size="sm" variant="outline">기간: 최근 30일</Button>
            <Button size="sm" variant="outline">CSV 다운로드</Button>
          </>
        }
      />

      <Section title="사용자">
        <StatCard label="DAU" value={data ? formatNumber(data.dau) : "—"} />
        <StatCard label="WAU" value={data ? formatNumber(data.wau) : "—"} />
        <StatCard label="MAU" value={data ? formatNumber(data.mau) : "—"} />
        <StatCard
          label="신규 가입"
          value={data ? formatNumber(data.newSignups) : "—"}
          caption="최근 7일"
        />
        <StatCard
          label="게스트 → 정식 전환"
          value={data ? `${(data.guestConversion * 100).toFixed(1)}%` : "—"}
          caption="14일 코호트"
        />
      </Section>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>DAU 추이 (30일)</CardTitle>
            <CardDescription>주말 효과 + 누적 성장</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <DauChart data={ts.daily} /> : <ChartSkeleton />}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>신규 가입 (30일)</CardTitle>
            <CardDescription>일별 가입 수</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <NewSignupsChart data={ts.daily} /> : <ChartSkeleton />}
          </CardContent>
        </Card>
      </div>

      <Section title="콘텐츠 · 모더레이션">
        <StatCard
          label="책 추가 건수"
          value={data ? formatNumber(data.booksAdded) : "—"}
          caption="최근 7일"
        />
        <StatCard
          label="모더레이션 미처리"
          value={data ? `${data.openModerationQueue}건` : "—"}
          caption="P1 / P2 / P3 합계"
        />
      </Section>

      <Card>
        <CardHeader>
          <CardTitle>모더레이션 큐 길이 (30일)</CardTitle>
          <CardDescription>미처리 신고 큐 추이</CardDescription>
        </CardHeader>
        <CardContent>
          {ts ? <ModerationChart data={ts.daily} /> : <ChartSkeleton />}
        </CardContent>
      </Card>

      <Section title="수익">
        <StatCard
          label="페이지 충전 GMV"
          value={data ? formatWon(data.pageGmvWon) : "—"}
          caption="최근 30일"
        />
        <StatCard
          label="책·에셋 판매 GMV"
          value={data ? formatWon(data.bookSalesWon) : "—"}
          caption="최근 30일"
        />
        <StatCard
          label="광고 매출"
          value={data ? formatWon(data.adRevenueWon) : "—"}
          caption="최근 30일"
        />
      </Section>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>매출 구성 추이 (30일)</CardTitle>
            <CardDescription>페이지 충전 / 책·에셋 판매 / 광고 — 일별 누적 영역</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <RevenueStackedChart data={ts.daily} /> : <ChartSkeleton />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>카테고리별 매출</CardTitle>
            <CardDescription>최근 30일 누적</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <CategoryRevenuePie data={ts.categoryRevenue} /> : <ChartSkeleton />}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <Card>
          <CardHeader>
            <CardTitle>전환 깔대기</CardTitle>
            <CardDescription>공간 진입 → 책 추가 → 독서기록 → 유료 표지 → 꾸미기·광고</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <ConversionFunnel data={ts.funnel} /> : <ChartSkeleton />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>시스템 헬스 (30일)</CardTitle>
            <CardDescription>API 에러율</CardDescription>
          </CardHeader>
          <CardContent>
            {ts ? <SystemChart data={ts.daily} /> : <ChartSkeleton />}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>알림 설정</CardTitle>
          <CardDescription>
            KPI 임계값 — 매출 급락·신고 급증·서버 에러 급증 시 운영 채널로 발송 (TBD)
          </CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          v1.0 mock — 실제 임계값 편집 UI는 후순위.
        </CardContent>
      </Card>
    </div>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <h2 className="text-sm font-semibold text-muted-foreground">{title}</h2>
      <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-5">{children}</div>
    </section>
  );
}

function ChartSkeleton() {
  return (
    <div className="flex h-[220px] flex-col gap-2">
      <div className="h-full w-full animate-pulse rounded-md bg-muted" />
    </div>
  );
}
