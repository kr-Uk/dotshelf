"use client";

import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Funnel,
  FunnelChart,
  LabelList,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import { formatNumber, formatWon } from "@/lib/format";
import type {
  CategoryRevenueSlice,
  FunnelStage,
  KpiTimePoint,
} from "@/lib/types";

// 차트 색상 팔레트 — globals.css 의 chart 토큰과 합쳐 사용.
const COLOR_PRIMARY = "var(--color-chart-2)";
const COLOR_SECONDARY = "var(--color-chart-3)";
const COLOR_TERTIARY = "var(--color-chart-4)";
const COLOR_AXIS = "var(--color-muted-foreground)";
const COLOR_GRID = "var(--color-border)";

const PIE_COLORS = [
  "var(--color-chart-1)",
  "var(--color-chart-2)",
  "var(--color-chart-3)",
  "var(--color-chart-4)",
  "var(--color-chart-5)",
  COLOR_AXIS,
];

const tooltipStyle = {
  background: "var(--color-popover)",
  color: "var(--color-popover-foreground)",
  border: `1px solid var(--color-border)`,
  borderRadius: 8,
  fontSize: 12,
  padding: "6px 8px",
};

function formatTickDate(d: string): string {
  const dt = new Date(d);
  return `${dt.getMonth() + 1}/${dt.getDate()}`;
}

export function DauChart({ data }: { data: KpiTimePoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <AreaChart data={data} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
        <defs>
          <linearGradient id="dauFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={COLOR_PRIMARY} stopOpacity={0.35} />
            <stop offset="100%" stopColor={COLOR_PRIMARY} stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid stroke={COLOR_GRID} strokeDasharray="3 3" vertical={false} />
        <XAxis
          dataKey="date"
          stroke={COLOR_AXIS}
          fontSize={11}
          tickFormatter={formatTickDate}
          tickMargin={6}
        />
        <YAxis stroke={COLOR_AXIS} fontSize={11} tickFormatter={(v) => formatNumber(v as number)} width={48} />
        <Tooltip
          contentStyle={tooltipStyle}
          labelFormatter={(d) => `${d}`}
          formatter={(v) => [formatNumber(Number(v ?? 0)), "DAU"]}
        />
        <Area
          type="monotone"
          dataKey="dau"
          stroke={COLOR_PRIMARY}
          strokeWidth={2}
          fill="url(#dauFill)"
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}

export function NewSignupsChart({ data }: { data: KpiTimePoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={data} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
        <CartesianGrid stroke={COLOR_GRID} strokeDasharray="3 3" vertical={false} />
        <XAxis dataKey="date" stroke={COLOR_AXIS} fontSize={11} tickFormatter={formatTickDate} tickMargin={6} />
        <YAxis stroke={COLOR_AXIS} fontSize={11} width={36} />
        <Tooltip
          contentStyle={tooltipStyle}
          formatter={(v) => [formatNumber(Number(v ?? 0)), "신규 가입"]}
        />
        <Bar dataKey="newSignups" fill={COLOR_SECONDARY} radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}

export function RevenueStackedChart({ data }: { data: KpiTimePoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <AreaChart data={data} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
        <defs>
          {[COLOR_PRIMARY, COLOR_SECONDARY, COLOR_TERTIARY].map((c, i) => (
            <linearGradient key={i} id={`rev-${i}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={c} stopOpacity={0.5} />
              <stop offset="100%" stopColor={c} stopOpacity={0.05} />
            </linearGradient>
          ))}
        </defs>
        <CartesianGrid stroke={COLOR_GRID} strokeDasharray="3 3" vertical={false} />
        <XAxis dataKey="date" stroke={COLOR_AXIS} fontSize={11} tickFormatter={formatTickDate} tickMargin={6} />
        <YAxis
          stroke={COLOR_AXIS}
          fontSize={11}
          tickFormatter={(v) => `${Math.round((v as number) / 1_000_000)}M`}
          width={42}
        />
        <Tooltip
          contentStyle={tooltipStyle}
          formatter={(v, name) => [formatWon(Number(v ?? 0)), name]}
        />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        <Area
          type="monotone"
          stackId="rev"
          dataKey="pageGmv"
          name="페이지 충전"
          stroke={COLOR_PRIMARY}
          fill="url(#rev-0)"
        />
        <Area
          type="monotone"
          stackId="rev"
          dataKey="bookSales"
          name="책·에셋 판매"
          stroke={COLOR_SECONDARY}
          fill="url(#rev-1)"
        />
        <Area
          type="monotone"
          stackId="rev"
          dataKey="adRevenue"
          name="광고"
          stroke={COLOR_TERTIARY}
          fill="url(#rev-2)"
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}

export function ModerationChart({ data }: { data: KpiTimePoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <LineChart data={data} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
        <CartesianGrid stroke={COLOR_GRID} strokeDasharray="3 3" vertical={false} />
        <XAxis dataKey="date" stroke={COLOR_AXIS} fontSize={11} tickFormatter={formatTickDate} tickMargin={6} />
        <YAxis stroke={COLOR_AXIS} fontSize={11} width={36} />
        <Tooltip contentStyle={tooltipStyle} />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        <Line
          type="monotone"
          dataKey="openModeration"
          name="미처리 큐"
          stroke={COLOR_PRIMARY}
          strokeWidth={2}
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}

export function SystemChart({ data }: { data: KpiTimePoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <LineChart data={data} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
        <CartesianGrid stroke={COLOR_GRID} strokeDasharray="3 3" vertical={false} />
        <XAxis dataKey="date" stroke={COLOR_AXIS} fontSize={11} tickFormatter={formatTickDate} tickMargin={6} />
        <YAxis
          yAxisId="left"
          stroke={COLOR_AXIS}
          fontSize={11}
          width={42}
          tickFormatter={(v) => `${((v as number) * 100).toFixed(2)}%`}
        />
        <Tooltip
          contentStyle={tooltipStyle}
          formatter={(v, name) => [`${(Number(v ?? 0) * 100).toFixed(2)}%`, name]}
        />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        <Line
          yAxisId="left"
          type="monotone"
          dataKey="apiErrorRate"
          name="API 에러율"
          stroke="var(--color-destructive)"
          strokeWidth={2}
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}

export function CategoryRevenuePie({ data }: { data: CategoryRevenueSlice[] }) {
  const total = data.reduce((acc, s) => acc + s.won, 0);
  return (
    <ResponsiveContainer width="100%" height={260}>
      <PieChart>
        <Tooltip
          contentStyle={tooltipStyle}
          formatter={(v, name) => {
            const n = Number(v ?? 0);
            return [`${formatWon(n)} (${((n / total) * 100).toFixed(1)}%)`, name];
          }}
        />
        <Legend
          layout="vertical"
          verticalAlign="middle"
          align="right"
          wrapperStyle={{ fontSize: 12 }}
        />
        <Pie
          data={data}
          dataKey="won"
          nameKey="category"
          innerRadius={50}
          outerRadius={90}
          paddingAngle={1.5}
        >
          {data.map((_, i) => (
            <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
          ))}
        </Pie>
      </PieChart>
    </ResponsiveContainer>
  );
}

export function ConversionFunnel({ data }: { data: FunnelStage[] }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <FunnelChart>
        <Tooltip
          contentStyle={tooltipStyle}
          formatter={(v) => [formatNumber(Number(v ?? 0)), "유저"]}
        />
        <Funnel dataKey="count" data={data} isAnimationActive>
          {data.map((_, i) => (
            <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
          ))}
          <LabelList
            position="right"
            fill="var(--color-foreground)"
            stroke="none"
            dataKey="stage"
            style={{ fontSize: 12 }}
          />
        </Funnel>
      </FunnelChart>
    </ResponsiveContainer>
  );
}
