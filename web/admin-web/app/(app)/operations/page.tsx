"use client";

import { useState } from "react";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { AdminMembersTable } from "@/components/common/admin-members-table";
import { AnnouncementDialog } from "@/components/common/announcement-dialog";
import { PageHeader } from "@/components/common/page-header";
import { PermissionMatrix } from "@/components/common/permission-matrix";
import { PolicyPublishDialog } from "@/components/common/policy-publish-dialog";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type {
  AnnouncementItem,
  PolicyVersion,
  SystemHealth,
} from "@/lib/types";

const HEALTH_TONE = {
  ok: "success",
  degraded: "warn",
  down: "danger",
} as const;

const HEALTH_LABEL = {
  ok: "정상",
  degraded: "지연",
  down: "장애",
} as const;

export default function OperationsPage() {
  const qc = useQueryClient();
  const [announceOpen, setAnnounceOpen] = useState(false);
  const [policyOpen, setPolicyOpen] = useState(false);

  const [annQ, polQ, healthQ] = useQueries({
    queries: [
      {
        queryKey: ["announcements"],
        queryFn: () => api<AnnouncementItem[]>("/operations/announcements"),
      },
      {
        queryKey: ["policies"],
        queryFn: () => api<PolicyVersion[]>("/operations/policies"),
      },
      {
        queryKey: ["system-health"],
        queryFn: () => api<SystemHealth>("/operations/system-health"),
      },
    ],
  });

  const toggleMaintenance = async () => {
    try {
      await api("/operations/maintenance", {
        method: "POST",
        body: JSON.stringify({
          enabled: !(health?.maintenanceMode ?? false),
          message: "관리자 콘솔에서 점검 모드를 변경했습니다.",
        }),
      });
      toast.success("점검 모드 토글 — 사용자 앱에 점검 안내 화면 노출");
      qc.invalidateQueries({ queryKey: ["system-health"] });
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const announcements = annQ.data ?? [];
  const policies = polQ.data ?? [];
  const health = healthQ.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="운영 관리"
        description="공지 / 약관·정책 / 권한 / 시스템 상태. 모든 변경은 감사 로그에 남습니다."
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>공지 사항</CardTitle>
              <RoleGate action="ops.announce" fallback="disable">
                <Button size="sm" onClick={() => setAnnounceOpen(true)}>
                  새 공지…
                </Button>
              </RoleGate>
            </div>
            <CardDescription>긴급 공지(점검·장애)는 인앱 풀스크린 모달</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {announcements.map((a) => (
              <div
                key={a.id}
                className="flex items-start justify-between gap-3 rounded-md border bg-card p-3 text-sm"
              >
                <div>
                  <div className="font-medium">{a.title}</div>
                  <div className="text-xs text-muted-foreground">
                    {formatDate(a.publishedAt)} ~ {formatDate(a.endsAt)} · 대상 {a.audience}
                  </div>
                </div>
                {a.isUrgent && <Badge variant="destructive">긴급</Badge>}
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>약관·정책</CardTitle>
              <RoleGate action="ops.policy_publish" fallback="disable" reason="슈퍼 관리자만">
                <Button size="sm" variant="outline" onClick={() => setPolicyOpen(true)}>
                  새 버전 발행…
                </Button>
              </RoleGate>
            </div>
            <CardDescription>변경 시 사용자 재동의 플로우 트리거</CardDescription>
          </CardHeader>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>약관</TableHead>
                  <TableHead>버전</TableHead>
                  <TableHead>시행일</TableHead>
                  <TableHead>재동의</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {policies.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-medium">{p.name}</TableCell>
                    <TableCell className="font-mono text-xs">{p.version}</TableCell>
                    <TableCell>{formatDate(p.effectiveAt)}</TableCell>
                    <TableCell>
                      <StatusBadge tone={p.requiresReConsent ? "warn" : "neutral"}>
                        {p.requiresReConsent ? "필요" : "불필요"}
                      </StatusBadge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle>시스템 상태</CardTitle>
              <CardDescription>결제 · 외부 API · CDN · 검수 큐 적체 · 점검 모드</CardDescription>
            </div>
            <RoleGate action="ops.maintenance_toggle" fallback="disable" reason="슈퍼 관리자만">
              <Button
                size="sm"
                variant={health?.maintenanceMode ? "destructive" : "outline"}
                onClick={toggleMaintenance}
              >
                점검 모드 {health?.maintenanceMode ? "끄기" : "켜기"}
              </Button>
            </RoleGate>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <HealthRow label="결제" status={health?.payment} />
          <HealthRow label="외부 도서 API" status={health?.externalBookApi} />
          <HealthRow label="CDN" status={health?.cdn} />
          <div className="rounded-md border bg-card p-3">
            <div className="text-xs text-muted-foreground">검수 큐 적체</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums">
              {health?.reviewQueueBacklog ?? "—"}건
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>관리자 권한 관리</CardTitle>
          <CardDescription>
            관리자(직원) 추가·역할 부여, 역할별 액션 허용 매트릭스. 슈퍼 관리자만 편집.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="members">
            <TabsList>
              <TabsTrigger value="members">관리자 목록</TabsTrigger>
              <TabsTrigger value="matrix">역할 × 액션 매트릭스</TabsTrigger>
            </TabsList>
            <TabsContent value="members" className="pt-4">
              <AdminMembersTable />
            </TabsContent>
            <TabsContent value="matrix" className="pt-4">
              <PermissionMatrix />
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      <AnnouncementDialog
        open={announceOpen}
        onOpenChange={setAnnounceOpen}
        onSubmit={async (p) => {
          await api("/operations/announcements", {
            method: "POST",
            body: JSON.stringify(p),
          });
          toast.success(`공지 발행 — ${p.isUrgent ? "긴급" : "일반"} · ${p.audience}`);
          qc.invalidateQueries({ queryKey: ["announcements"] });
        }}
      />

      <PolicyPublishDialog
        open={policyOpen}
        onOpenChange={setPolicyOpen}
        policies={policies}
        onSubmit={async (p) => {
          await api("/operations/policies/publish", {
            method: "POST",
            body: JSON.stringify(p),
          });
          toast.success(
            `약관 새 버전 ${p.version} 발행 — ${
              p.requiresReConsent ? "재동의 강제 활성" : "재동의 불필요"
            }`,
          );
          qc.invalidateQueries({ queryKey: ["policies"] });
        }}
      />
    </div>
  );
}

function HealthRow({
  label,
  status,
}: {
  label: string;
  status: SystemHealth["payment"] | undefined;
}) {
  return (
    <div className="rounded-md border bg-card p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-2">
        <StatusBadge tone={status ? HEALTH_TONE[status] : "neutral"}>
          {status ? HEALTH_LABEL[status] : "—"}
        </StatusBadge>
      </div>
    </div>
  );
}
