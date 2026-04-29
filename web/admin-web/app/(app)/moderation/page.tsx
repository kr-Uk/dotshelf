"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Kbd } from "@/components/common/kbd";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { SlaTimer } from "@/components/common/sla-timer";
import { StatusBadge } from "@/components/common/status-badge";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { api } from "@/lib/api";
import { useKeyboardShortcut } from "@/lib/use-keyboard-shortcut";
import { reportSlaDueAt } from "@/lib/sla";
import {
  REPORT_SEVERITY_LABEL,
  REPORT_STATUS_LABEL,
  REPORT_TARGET_LABEL,
  type ReportItem,
  type ReportStatus,
  type ReportTargetType,
} from "@/lib/types";
import { formatRelative } from "@/lib/format";

const STATUS_TONE: Record<ReportStatus, "neutral" | "info" | "warn" | "success" | "danger"> = {
  open: "warn",
  in_review: "info",
  resolved: "success",
  dismissed: "neutral",
};

const TARGET_OPTIONS: ReportTargetType[] = [
  "reading_log",
  "comment",
  "guestbook",
  "profile",
  "space",
  "content",
];

export default function ModerationPage() {
  const [tab, setTab] = useState<ReportTargetType | "all">("all");
  const { data, isLoading } = useQuery({
    queryKey: ["reports"],
    queryFn: () => api<ReportItem[]>("/reports"),
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [bulkIds, setBulkIds] = useState<string[]>([]);

  const filtered = useMemo(() => {
    const all = data ?? [];
    return tab === "all" ? all : all.filter((r) => r.targetType === tab);
  }, [data, tab]);

  // SLA 임박 순으로 정렬해서 P1 먼저 보이게
  const sorted = useMemo(() => {
    return [...filtered].sort((a, b) => {
      const da = new Date(reportSlaDueAt(a.severity, a.createdAt)).getTime();
      const db = new Date(reportSlaDueAt(b.severity, b.createdAt)).getTime();
      return da - db;
    });
  }, [filtered]);

  const selected =
    sorted.find((r) => r.id === selectedId) ?? sorted[0] ?? null;

  const moveBy = (delta: number) => {
    if (sorted.length === 0) return;
    const i = Math.max(0, sorted.findIndex((r) => r.id === selected?.id));
    const next = sorted[(i + delta + sorted.length) % sorted.length];
    if (next) setSelectedId(next.id);
  };
  useKeyboardShortcut({ key: "j" }, () => moveBy(1));
  useKeyboardShortcut({ key: "k" }, () => moveBy(-1));
  useKeyboardShortcut({ key: "ArrowDown" }, () => moveBy(1));
  useKeyboardShortcut({ key: "ArrowUp" }, () => moveBy(-1));

  const action = async (kind: string, id?: string) => {
    const targetId = id ?? selected?.id;
    if (!targetId) return;
    try {
      await api(`/reports/${targetId}/action`, {
        method: "POST",
        body: JSON.stringify({ kind }),
      });
      toast.success(`신고 처리 — ${kind}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const bulkAction = async (kind: string) => {
    if (bulkIds.length === 0) return;
    try {
      await api("/reports/bulk", {
        method: "POST",
        body: JSON.stringify({ ids: bulkIds, action: kind }),
      });
      toast.success(`${bulkIds.length}건 일괄 ${kind}`);
      setBulkIds([]);
    } catch {
      toast.error("일괄 처리에 실패했어요.");
    }
  };

  const visibleIds = sorted.map((r) => r.id);
  const allChecked = visibleIds.length > 0 && bulkIds.length === visibleIds.length;
  const someChecked = bulkIds.length > 0 && !allChecked;

  // 동일 패턴 반복 신고 추천 — 같은 작성자에 대해 동일 키워드 연관된 신고 묶기
  const dupSuggestion = useMemo(() => {
    if (!selected) return [];
    return sorted.filter(
      (r) =>
        r.id !== selected.id &&
        r.targetAuthorId === selected.targetAuthorId &&
        r.status === "open",
    );
  }, [sorted, selected]);

  // 같은 *콘텐츠*에 대한 다른 신고 — targetExcerpt가 동일/유사한 신고 묶기
  const sameContent = useMemo(() => {
    if (!selected) return [];
    const excerpt = selected.targetExcerpt.slice(0, 20).toLowerCase();
    return sorted.filter(
      (r) =>
        r.id !== selected.id &&
        r.targetType === selected.targetType &&
        r.targetExcerpt.toLowerCase().includes(excerpt),
    );
  }, [sorted, selected]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="신고 · 커뮤니티 모더레이션"
        description="SLA 카운트다운 + 동일 패턴 일괄 처리. 영구 정지 등 강한 조치는 2단계 검토."
        actions={
          <div className="hidden items-center gap-2 text-xs text-muted-foreground md:flex">
            <span>SLA · </span>
            <Badge variant="outline">P1 1h</Badge>
            <Badge variant="outline">P2 24h</Badge>
            <Badge variant="outline">P3 72h</Badge>
          </div>
        }
      />

      <Tabs value={tab} onValueChange={(v) => { setTab(v as typeof tab); setSelectedId(null); setBulkIds([]); }}>
        <TabsList>
          <TabsTrigger value="all">전체</TabsTrigger>
          {TARGET_OPTIONS.map((t) => (
            <TabsTrigger key={t} value={t}>
              {REPORT_TARGET_LABEL[t]}
            </TabsTrigger>
          ))}
        </TabsList>

        <TabsContent value={tab}>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <CardTitle>큐 ({sorted.length}건)</CardTitle>
                    <CardDescription className="flex items-center gap-1">
                      <span>SLA 임박 순 자동 정렬 ·</span>
                      <Kbd>J</Kbd>
                      <Kbd>K</Kbd>
                      <span>이동</span>
                    </CardDescription>
                  </div>
                  {bulkIds.length > 0 && (
                    <RoleGate action="moderation.bulk" fallback="hide">
                      <div className="inline-flex flex-wrap gap-2">
                        <Button size="sm" variant="outline" onClick={() => bulkAction("hide")}>
                          {bulkIds.length}건 가림
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => bulkAction("warn")}>
                          {bulkIds.length}건 경고
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => bulkAction("dismiss")}>
                          {bulkIds.length}건 기각
                        </Button>
                      </div>
                    </RoleGate>
                  )}
                </div>
              </CardHeader>
              <CardContent className="px-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-10">
                        <Checkbox
                          checked={allChecked}
                          indeterminate={someChecked}
                          onCheckedChange={(c) => setBulkIds(c === true ? visibleIds : [])}
                        />
                      </TableHead>
                      <TableHead>대상</TableHead>
                      <TableHead>신고</TableHead>
                      <TableHead>SLA</TableHead>
                      <TableHead>상태</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {isLoading && <TableSkeletonRows rows={5} cols={5} />}
                    {sorted.map((r) => {
                      const checked = bulkIds.includes(r.id);
                      const due = reportSlaDueAt(r.severity, r.createdAt);
                      return (
                        <TableRow
                          key={r.id}
                          data-state={selected?.id === r.id ? "selected" : undefined}
                        >
                          <TableCell onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={checked}
                              onCheckedChange={(c) =>
                                setBulkIds((prev) =>
                                  c === true
                                    ? [...prev, r.id]
                                    : prev.filter((x) => x !== r.id),
                                )
                              }
                            />
                          </TableCell>
                          <TableCell
                            className="max-w-[260px] cursor-pointer"
                            onClick={() => setSelectedId(r.id)}
                          >
                            <div className="line-clamp-1 text-sm">{r.targetExcerpt}</div>
                            <div className="text-xs text-muted-foreground">
                              {REPORT_TARGET_LABEL[r.targetType]} · {r.targetAuthorName}
                            </div>
                          </TableCell>
                          <TableCell className="cursor-pointer" onClick={() => setSelectedId(r.id)}>
                            <div className="text-sm font-semibold">{r.reporterCount}건</div>
                            <div className="text-xs text-muted-foreground">
                              신뢰도 {(r.reporterReputation * 100).toFixed(0)}%
                            </div>
                          </TableCell>
                          <TableCell className="cursor-pointer" onClick={() => setSelectedId(r.id)}>
                            <div className="flex flex-col gap-1">
                              <SlaTimer dueAt={due} />
                              <span className="text-[10px] text-muted-foreground">
                                {REPORT_SEVERITY_LABEL[r.severity]}
                              </span>
                            </div>
                          </TableCell>
                          <TableCell className="cursor-pointer" onClick={() => setSelectedId(r.id)}>
                            <StatusBadge tone={STATUS_TONE[r.status]}>
                              {REPORT_STATUS_LABEL[r.status]}
                            </StatusBadge>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <div className="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle>상세</CardTitle>
                  <CardDescription>
                    {selected ? `${REPORT_TARGET_LABEL[selected.targetType]} · ${selected.targetAuthorName}` : "큐에서 항목 선택"}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {selected && (
                    <>
                      <div className="rounded-md border bg-muted/40 p-3 text-sm">
                        <div className="text-xs font-medium text-muted-foreground">
                          신고된 콘텐츠
                        </div>
                        <div className="mt-1">{selected.targetExcerpt}</div>
                      </div>

                      <div className="grid grid-cols-2 gap-2 text-sm">
                        <span className="text-muted-foreground">접수</span>
                        <span>{formatRelative(selected.createdAt)}</span>
                        <span className="text-muted-foreground">SLA</span>
                        <span>
                          <SlaTimer
                            dueAt={reportSlaDueAt(selected.severity, selected.createdAt)}
                          />
                        </span>
                        <span className="text-muted-foreground">키워드</span>
                        <span className="flex flex-wrap gap-1">
                          {selected.keywords.map((k) => (
                            <Badge key={k} variant="outline" className="text-[10px]">
                              {k}
                            </Badge>
                          ))}
                        </span>
                        <span className="text-muted-foreground">NSFW 점수</span>
                        <span>{(selected.nsfwScore * 100).toFixed(0)}%</span>
                        <span className="text-muted-foreground">대상자 ID</span>
                        <span className="font-mono text-xs">{selected.targetAuthorId}</span>
                      </div>

                      <div className="flex flex-wrap items-center gap-2 border-t pt-4">
                        <RoleGate action="moderation.hide" fallback="disable">
                          <Button size="sm" variant="outline" onClick={() => action("hide")}>
                            가림 처리
                          </Button>
                        </RoleGate>
                        <RoleGate action="moderation.delete" fallback="disable">
                          <Button size="sm" variant="destructive" onClick={() => action("delete")}>
                            완전 삭제
                          </Button>
                        </RoleGate>
                        <RoleGate action="moderation.warn" fallback="disable">
                          <Button size="sm" variant="outline" onClick={() => action("warn")}>
                            경고
                          </Button>
                        </RoleGate>
                        <RoleGate action="moderation.suspend_temp" fallback="disable">
                          <Button size="sm" variant="outline" onClick={() => action("suspend_7d")}>
                            7일 정지
                          </Button>
                        </RoleGate>
                        {selected.targetType === "space" && (
                          <RoleGate action="moderation.hide" fallback="disable">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => action("force_private_space")}
                            >
                              공간 비공개 강제
                            </Button>
                          </RoleGate>
                        )}
                        <RoleGate
                          action="moderation.suspend_perm"
                          fallback="disable"
                          reason="슈퍼 관리자 + 2단계 승인"
                        >
                          <Button size="sm" variant="destructive" onClick={() => action("suspend_perm")}>
                            영구 정지 (2단계)
                          </Button>
                        </RoleGate>
                        <Button size="sm" variant="ghost" onClick={() => action("dismiss")}>
                          기각
                        </Button>
                      </div>
                    </>
                  )}
                </CardContent>
              </Card>

              {selected && dupSuggestion.length > 0 && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-sm">
                      동일 패턴 신고 ({dupSuggestion.length}건)
                    </CardTitle>
                    <CardDescription>
                      같은 작성자({selected.targetAuthorName}) · 미처리 신고. 일괄 처리 추천.
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {dupSuggestion.map((r) => (
                      <div
                        key={r.id}
                        className="flex items-center justify-between gap-2 rounded-md border bg-card p-2 text-sm"
                      >
                        <div className="min-w-0 flex-1">
                          <div className="line-clamp-1">{r.targetExcerpt}</div>
                          <div className="text-xs text-muted-foreground">
                            {REPORT_TARGET_LABEL[r.targetType]} · 신고 {r.reporterCount}건
                          </div>
                        </div>
                        <SlaTimer dueAt={reportSlaDueAt(r.severity, r.createdAt)} />
                      </div>
                    ))}
                    <RoleGate action="moderation.bulk" fallback="hide">
                      <div className="flex flex-wrap items-center gap-2 pt-1">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setBulkIds([selected.id, ...dupSuggestion.map((r) => r.id)]);
                            toast.info(`${1 + dupSuggestion.length}건 선택됨 — 큐에서 일괄 처리`);
                          }}
                        >
                          모두 선택해서 일괄 처리 준비
                        </Button>
                      </div>
                    </RoleGate>
                  </CardContent>
                </Card>
              )}

              {selected && sameContent.length > 0 && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-sm">
                      같은 콘텐츠의 다른 신고 ({sameContent.length}건)
                    </CardTitle>
                    <CardDescription>
                      유사 본문 매칭. 동일 결정으로 묶어 처리할 수 있어요.
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {sameContent.map((r) => (
                      <div
                        key={r.id}
                        className="flex items-center justify-between gap-2 rounded-md border bg-card p-2 text-sm"
                      >
                        <div className="min-w-0 flex-1">
                          <div className="line-clamp-1">{r.targetExcerpt}</div>
                          <div className="text-xs text-muted-foreground">
                            {r.targetAuthorName} · 신고 {r.reporterCount}건
                          </div>
                        </div>
                        <SlaTimer dueAt={reportSlaDueAt(r.severity, r.createdAt)} />
                      </div>
                    ))}
                    <RoleGate action="moderation.bulk" fallback="hide">
                      <div className="flex flex-wrap items-center gap-2 pt-1">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setBulkIds([selected.id, ...sameContent.map((r) => r.id)]);
                            toast.info(
                              `${1 + sameContent.length}건 선택됨 — 큐에서 일괄 처리`,
                            );
                          }}
                        >
                          같은 콘텐츠 모두 선택
                        </Button>
                      </div>
                    </RoleGate>
                  </CardContent>
                </Card>
              )}
            </div>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
