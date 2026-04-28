"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Area,
  AreaChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Download, TrendingUp } from "lucide-react";
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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { formatPages, formatWon, pagesToWon } from "@/lib/format";
import type {
  StatsCategoryRow,
  StatsContentRow,
  StatsKpi,
  StatsPeriod,
  StatsScope,
  StatsTimeseriesPoint,
} from "@/lib/types";

const PERIOD_OPTIONS: Array<{ value: StatsPeriod; label: string }> = [
  { value: "daily", label: "일별" },
  { value: "weekly", label: "주별" },
  { value: "monthly", label: "월별" },
];

export default function StatsPage() {
  const [scope, setScope] = useState<StatsScope>("all");
  const [period, setPeriod] = useState<StatsPeriod>("daily");
  const [breakdownByAd, setBreakdownByAd] = useState(false);

  const kpi = useQuery({
    queryKey: ["stats", "kpi", scope],
    queryFn: () => api<StatsKpi>(`/stats/kpi?scope=${scope}`),
  });

  const ts = useQuery({
    queryKey: ["stats", "timeseries", scope, period],
    queryFn: () =>
      api<StatsTimeseriesPoint[]>(
        `/stats/timeseries?scope=${scope}&period=${period}`,
      ),
  });

  const byCategory = useQuery({
    queryKey: ["stats", "by-category", scope],
    queryFn: () =>
      api<StatsCategoryRow[]>(`/stats/by-category?scope=${scope}`),
  });

  const content = useQuery({
    queryKey: ["stats", "content", scope],
    queryFn: () => api<StatsContentRow[]>(`/stats/content?scope=${scope}`),
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="text-xs font-medium text-muted-foreground">
            6. 판매 통계 대시보드
          </div>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">통계</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            매출·노출·전환을 시계열·카테고리·콘텐츠로 분해해서 봐요. CSV 다운로드도
            가능합니다.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Tabs
            value={scope}
            onValueChange={(v) => setScope(v as StatsScope)}
          >
            <TabsList>
              <TabsTrigger value="all">전체</TabsTrigger>
              <TabsTrigger value="book">책</TabsTrigger>
              <TabsTrigger value="asset">에셋</TabsTrigger>
            </TabsList>
          </Tabs>
          <Select
            value={period}
            onValueChange={(v) => setPeriod(v as StatsPeriod)}
          >
            <SelectTrigger className="w-32">
              <SelectValue>
                {(v: StatsPeriod) =>
                  PERIOD_OPTIONS.find((o) => o.value === v)?.label
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {PERIOD_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          label="매출"
          value={kpi.data ? formatWon(kpi.data.revenueWon) : null}
          sub={
            kpi.data
              ? `${formatPages(kpi.data.revenuePages)} · 전기 대비 ${(kpi.data.prevRevenueChange * 100).toFixed(1)}%`
              : null
          }
          accent={
            kpi.data
              ? kpi.data.prevRevenueChange >= 0
                ? "positive"
                : "negative"
              : "neutral"
          }
        />
        <KpiCard
          label="판매 건수"
          value={kpi.data ? `${kpi.data.sales.toLocaleString()}건` : null}
          sub={kpi.data ? `평균 단가 약 ${formatWon(kpi.data.revenueWon / Math.max(1, kpi.data.sales))}` : null}
        />
        <KpiCard
          label="광고 ROI"
          value={kpi.data ? `${kpi.data.adRoi.toFixed(2)}x` : null}
          sub="(광고 유입 매출 ÷ 광고 지출)"
        />
        <KpiCard
          label="카테고리 백분위"
          value={
            kpi.data ? `상위 ${(100 - kpi.data.categoryPercentile * 100).toFixed(0)}%` : null
          }
          sub={
            kpi.data
              ? `활성 캠페인 ${kpi.data.activeCampaigns}개`
              : null
          }
        />
      </div>

      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <CardTitle className="text-base">시계열 매출</CardTitle>
              <CardDescription>
                기간 단위: {period === "daily" ? "일" : period === "weekly" ? "주" : "월"}.{" "}
                {breakdownByAd ? "광고 유입 vs 오가닉 분리." : "총 매출 추이."}
              </CardDescription>
            </div>
            <Button
              variant={breakdownByAd ? "default" : "outline"}
              size="sm"
              onClick={() => setBreakdownByAd((v) => !v)}
            >
              <TrendingUp className="size-3.5" />{" "}
              {breakdownByAd ? "총합 보기" : "광고 영향도 분리"}
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {ts.isLoading || !ts.data ? (
            <Skeleton className="h-64 w-full" />
          ) : (
            <div className="h-72 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart
                  data={ts.data.map((p) => ({
                    ...p,
                    revenueWon: pagesToWon(p.revenuePages),
                    adRevenueWon: pagesToWon(p.adRevenuePages),
                    organicRevenueWon: pagesToWon(p.organicRevenuePages),
                  }))}
                  margin={{ top: 10, right: 8, bottom: 0, left: 0 }}
                >
                  <defs>
                    <linearGradient id="grad-total" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#0ea5e9" stopOpacity={0.4} />
                      <stop offset="95%" stopColor="#0ea5e9" stopOpacity={0} />
                    </linearGradient>
                    <linearGradient id="grad-ad" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#f59e0b" stopOpacity={0.5} />
                      <stop offset="95%" stopColor="#f59e0b" stopOpacity={0} />
                    </linearGradient>
                    <linearGradient id="grad-org" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#10b981" stopOpacity={0.5} />
                      <stop offset="95%" stopColor="#10b981" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis
                    dataKey="date"
                    tick={{ fontSize: 11, fill: "#6b7280" }}
                    tickFormatter={(v: string) => v.slice(5)}
                  />
                  <YAxis
                    tick={{ fontSize: 11, fill: "#6b7280" }}
                    tickFormatter={(v: number) =>
                      v >= 1_000_000
                        ? `${(v / 1_000_000).toFixed(1)}M`
                        : v >= 1_000
                          ? `${(v / 1_000).toFixed(0)}K`
                          : `${v}`
                    }
                  />
                  <Tooltip
                    contentStyle={{
                      borderRadius: 8,
                      border: "1px solid #e5e7eb",
                      fontSize: 12,
                    }}
                    formatter={(v) => formatWon(Number(v))}
                  />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  {breakdownByAd ? (
                    <>
                      <Area
                        type="monotone"
                        dataKey="adRevenueWon"
                        name="광고 유입"
                        stroke="#f59e0b"
                        fill="url(#grad-ad)"
                        stackId="1"
                      />
                      <Area
                        type="monotone"
                        dataKey="organicRevenueWon"
                        name="오가닉"
                        stroke="#10b981"
                        fill="url(#grad-org)"
                        stackId="1"
                      />
                    </>
                  ) : (
                    <Area
                      type="monotone"
                      dataKey="revenueWon"
                      name="총 매출"
                      stroke="#0ea5e9"
                      fill="url(#grad-total)"
                    />
                  )}
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <div>
              <CardTitle className="text-base">카테고리별 분해</CardTitle>
              <CardDescription>
                매출과 동일 카테고리 내 백분위.
              </CardDescription>
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={() => downloadCsv("by-category", byCategory.data ?? [])}
              disabled={!byCategory.data}
            >
              <Download className="size-3.5" /> CSV
            </Button>
          </CardHeader>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-6">카테고리</TableHead>
                  <TableHead>매출</TableHead>
                  <TableHead>판매</TableHead>
                  <TableHead className="pr-6 text-right">백분위</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {byCategory.isLoading &&
                  [1, 2, 3].map((i) => (
                    <TableRow key={i}>
                      <TableCell colSpan={4} className="px-6">
                        <Skeleton className="h-7 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}
                {byCategory.data?.map((r) => (
                  <TableRow key={`${r.scope}-${r.category}`}>
                    <TableCell className="pl-6">
                      <div className="flex items-center gap-2">
                        <Badge variant="secondary" className="text-[10px]">
                          {r.scope === "book" ? "책" : "에셋"}
                        </Badge>
                        <span className="font-medium">{r.category}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      {formatWon(pagesToWon(r.revenuePages))}
                    </TableCell>
                    <TableCell>{r.sales}건</TableCell>
                    <TableCell className="pr-6 text-right">
                      <Percentile p={r.percentile} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <div>
              <CardTitle className="text-base">콘텐츠별 매출</CardTitle>
              <CardDescription>
                책·에셋 매출 상위. 광고 ROI는 광고 진행 콘텐츠만.
              </CardDescription>
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={() => downloadCsv("content", content.data ?? [])}
              disabled={!content.data}
            >
              <Download className="size-3.5" /> CSV
            </Button>
          </CardHeader>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-6">제목</TableHead>
                  <TableHead>카테고리</TableHead>
                  <TableHead>매출</TableHead>
                  <TableHead className="pr-6 text-right">광고 ROI</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {content.isLoading &&
                  [1, 2, 3].map((i) => (
                    <TableRow key={i}>
                      <TableCell colSpan={4} className="px-6">
                        <Skeleton className="h-7 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}
                {content.data?.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="pl-6">
                      <div className="flex items-center gap-2">
                        <Badge variant="secondary" className="text-[10px]">
                          {r.scope === "book" ? "책" : "에셋"}
                        </Badge>
                        <span className="font-medium">{r.title}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {r.category}
                    </TableCell>
                    <TableCell>
                      {formatWon(pagesToWon(r.revenuePages))}
                    </TableCell>
                    <TableCell className="pr-6 text-right">
                      {r.adRoi !== null ? `${r.adRoi.toFixed(2)}x` : "—"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

    </div>
  );
}

function KpiCard({
  label,
  value,
  sub,
  accent = "neutral",
}: {
  label: string;
  value: string | null;
  sub: string | null;
  accent?: "positive" | "negative" | "neutral";
}) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="text-2xl">
          {value ?? <Skeleton className="h-7 w-32" />}
        </CardTitle>
      </CardHeader>
      <CardContent
        className={cn(
          "text-xs",
          accent === "positive"
            ? "text-emerald-700"
            : accent === "negative"
              ? "text-destructive"
              : "text-muted-foreground",
        )}
      >
        {sub ?? <Skeleton className="h-4 w-24" />}
      </CardContent>
    </Card>
  );
}

function Percentile({ p }: { p: number }) {
  const pct = Math.round(p * 100);
  return (
    <div className="inline-flex items-center gap-2">
      <span className="text-sm">상위 {100 - pct}%</span>
      <span className="inline-block h-1.5 w-20 overflow-hidden rounded-full bg-muted">
        <span
          className="block h-full bg-primary"
          style={{ width: `${pct}%` }}
        />
      </span>
    </div>
  );
}

function downloadCsv<T extends object>(filename: string, rows: T[]) {
  if (rows.length === 0) {
    toast.error("내려받을 데이터가 없어요.");
    return;
  }
  const headers = Object.keys(rows[0] as Record<string, unknown>);
  const escape = (v: unknown) => {
    const s = String(v ?? "");
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const csv = [
    headers.join(","),
    ...rows.map((r) =>
      headers.map((h) => escape((r as Record<string, unknown>)[h])).join(","),
    ),
  ].join("\n");
  const blob = new Blob([`﻿${csv}`], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `dotshelf-stats-${filename}-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
  toast.success("CSV가 다운로드됐어요.");
}
