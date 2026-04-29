"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Download } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { api } from "@/lib/api";
import {
  ADMIN_ROLE_LABEL,
  type AdminRole,
  type AuditLogEntry,
} from "@/lib/types";
import { canRole } from "@/lib/permissions";
import { useAdminAccount } from "@/lib/use-admin-account";
import { formatDateTime } from "@/lib/format";

const ALL = "__all__";

type RangeKey = "all" | "today" | "7d" | "30d";

const RANGE_LABEL: Record<RangeKey, string> = {
  all: "전체 기간",
  today: "오늘",
  "7d": "최근 7일",
  "30d": "최근 30일",
};

function rangeStart(key: RangeKey): number | null {
  const now = Date.now();
  switch (key) {
    case "today": {
      const d = new Date();
      d.setHours(0, 0, 0, 0);
      return d.getTime();
    }
    case "7d":
      return now - 7 * 24 * 3600 * 1000;
    case "30d":
      return now - 30 * 24 * 3600 * 1000;
    default:
      return null;
  }
}

function csvEscape(v: unknown): string {
  if (v === null || v === undefined) return "";
  const s = String(v);
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function downloadCsv(filename: string, rows: AuditLogEntry[]) {
  const header = [
    "occurredAt",
    "actorEmail",
    "actorRole",
    "action",
    "targetType",
    "targetId",
    "before",
    "after",
    "reason",
    "ip",
  ];
  const body = rows.map((e) =>
    header
      .map((k) => csvEscape((e as unknown as Record<string, unknown>)[k]))
      .join(","),
  );
  const csv = "﻿" + [header.join(","), ...body].join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export default function AuditPage() {
  const auth = useAdminAccount();
  const canExport = canRole(auth?.account.role, "audit.export");

  const { data, isLoading } = useQuery({
    queryKey: ["audit"],
    queryFn: () => api<AuditLogEntry[]>("/audit"),
  });

  const [q, setQ] = useState("");
  const [actionFilter, setActionFilter] = useState<string>(ALL);
  const [roleFilter, setRoleFilter] = useState<string>(ALL);
  const [range, setRange] = useState<RangeKey>("all");

  const actionOptions = useMemo(() => {
    const uniq = new Set<string>();
    (data ?? []).forEach((e) => uniq.add(e.action));
    return Array.from(uniq).sort();
  }, [data]);

  const filtered = useMemo(() => {
    const all = data ?? [];
    const startMs = rangeStart(range);
    const lq = q.trim().toLowerCase();
    return all.filter((e) => {
      if (actionFilter !== ALL && e.action !== actionFilter) return false;
      if (roleFilter !== ALL && e.actorRole !== roleFilter) return false;
      if (startMs && new Date(e.occurredAt).getTime() < startMs) return false;
      if (lq) {
        const hay = `${e.actorEmail} ${e.action} ${e.targetType} ${e.targetId} ${e.reason ?? ""}`.toLowerCase();
        if (!hay.includes(lq)) return false;
      }
      return true;
    });
  }, [data, q, actionFilter, roleFilter, range]);

  const handleExport = () => {
    if (!canExport) return;
    const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
    downloadCsv(`audit-log_${ts}.csv`, filtered);
    toast.success(`${filtered.length}건 CSV로 내려받았어요.`);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="감사 로그"
        description="모든 쓰기 액션 검색·필터·CSV 내보내기. 누가·언제·무엇을·이전값/이후값."
        actions={
          <RoleGate action="audit.export" fallback="disable" reason="슈퍼 관리자만">
            <Button size="sm" variant="outline" onClick={handleExport}>
              <Download className="size-3.5" /> CSV 내보내기 ({filtered.length})
            </Button>
          </RoleGate>
        }
      />

      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <Input
              placeholder="이메일 · 액션 · 대상 ID · 사유 검색"
              className="max-w-xs"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />

            <Select
              value={actionFilter}
              onValueChange={(v) => setActionFilter(v ?? ALL)}
            >
              <SelectTrigger size="sm" className="w-48">
                <SelectValue placeholder="액션" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>액션 전체</SelectItem>
                {actionOptions.map((a) => (
                  <SelectItem key={a} value={a}>
                    {a}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={roleFilter}
              onValueChange={(v) => setRoleFilter(v ?? ALL)}
            >
              <SelectTrigger size="sm" className="w-44">
                <SelectValue placeholder="역할" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>역할 전체</SelectItem>
                {(Object.keys(ADMIN_ROLE_LABEL) as AdminRole[]).map((r) => (
                  <SelectItem key={r} value={r}>
                    {ADMIN_ROLE_LABEL[r]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={range}
              onValueChange={(v) => setRange((v ?? "all") as RangeKey)}
            >
              <SelectTrigger size="sm" className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(Object.keys(RANGE_LABEL) as RangeKey[]).map((k) => (
                  <SelectItem key={k} value={k}>
                    {RANGE_LABEL[k]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {(q || actionFilter !== ALL || roleFilter !== ALL || range !== "all") && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setQ("");
                  setActionFilter(ALL);
                  setRoleFilter(ALL);
                  setRange("all");
                }}
              >
                필터 초기화
              </Button>
            )}
          </div>
          <CardDescription>
            실시간 적재 · 90일 이상 보존 · 표시 {filtered.length}건 / 전체 {(data ?? []).length}건
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>시각</TableHead>
                <TableHead>관리자</TableHead>
                <TableHead>액션</TableHead>
                <TableHead>대상</TableHead>
                <TableHead>이전 → 이후</TableHead>
                <TableHead>사유</TableHead>
                <TableHead>IP</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading && <TableSkeletonRows rows={6} cols={7} />}
              {!isLoading && filtered.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} className="text-center text-muted-foreground">
                    조건에 맞는 로그가 없어요.
                  </TableCell>
                </TableRow>
              )}
              {filtered.map((e) => (
                <TableRow key={e.id}>
                  <TableCell className="whitespace-nowrap">{formatDateTime(e.occurredAt)}</TableCell>
                  <TableCell>
                    <div className="text-sm font-medium">{e.actorEmail}</div>
                    <div className="text-xs text-muted-foreground">
                      {ADMIN_ROLE_LABEL[e.actorRole]}
                    </div>
                  </TableCell>
                  <TableCell className="font-mono text-xs">{e.action}</TableCell>
                  <TableCell>
                    <div className="text-sm">{e.targetType}</div>
                    <div className="font-mono text-xs text-muted-foreground">
                      {e.targetId}
                    </div>
                  </TableCell>
                  <TableCell className="font-mono text-xs">
                    {e.before ?? "∅"} → {e.after ?? "∅"}
                  </TableCell>
                  <TableCell className="max-w-[260px] whitespace-pre-wrap text-sm">
                    {e.reason ?? "—"}
                  </TableCell>
                  <TableCell className="font-mono text-xs">{e.ip}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
