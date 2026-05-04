"use client";

import { useMemo, useState } from "react";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import { Pencil, Trash2 } from "lucide-react";
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
import {
  AnnouncementDialog,
  type AnnouncementPayload,
} from "@/components/common/announcement-dialog";
import { PageHeader } from "@/components/common/page-header";
import { PermissionMatrix } from "@/components/common/permission-matrix";
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
  not_integrated: "neutral",
  unknown: "neutral",
} as const;

const HEALTH_LABEL = {
  ok: "정상",
  degraded: "지연",
  down: "장애",
  not_integrated: "미연동",
  unknown: "확인 전",
} as const;

export default function OperationsPage() {
  const qc = useQueryClient();
  const [announceOpen, setAnnounceOpen] = useState(false);
  const [editingAnnouncement, setEditingAnnouncement] =
    useState<AnnouncementItem | null>(null);
  const [deletingAnnouncementId, setDeletingAnnouncementId] = useState<string | null>(null);

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
  const announcementInitialValue = useMemo(
    () =>
      editingAnnouncement
        ? {
            title: editingAnnouncement.title,
            body: editingAnnouncement.body,
            audience: editingAnnouncement.audience,
            startsAt: editingAnnouncement.startsAt,
            endsAt: editingAnnouncement.endsAt,
            isUrgent: editingAnnouncement.isUrgent,
          }
        : null,
    [editingAnnouncement],
  );

  const openCreateAnnouncement = () => {
    setEditingAnnouncement(null);
    setAnnounceOpen(true);
  };

  const openEditAnnouncement = (announcement: AnnouncementItem) => {
    setEditingAnnouncement(announcement);
    setAnnounceOpen(true);
  };

  const submitAnnouncement = async (payload: AnnouncementPayload) => {
    try {
      if (editingAnnouncement) {
        await api(`/operations/announcements/${editingAnnouncement.id}`, {
          method: "PUT",
          body: JSON.stringify(payload),
        });
        toast.success("공지 수정 완료");
      } else {
        await api("/operations/announcements", {
          method: "POST",
          body: JSON.stringify(payload),
        });
        toast.success(`공지 발행 — ${payload.isUrgent ? "긴급" : "일반"} · ${payload.audience}`);
      }
      qc.invalidateQueries({ queryKey: ["announcements"] });
    } catch (error) {
      toast.error(editingAnnouncement ? "공지 수정에 실패했어요." : "공지 발행에 실패했어요.");
      throw error;
    }
  };

  const deleteAnnouncement = async (announcement: AnnouncementItem) => {
    if (!window.confirm(`공지 "${announcement.title}"을 삭제할까요?`)) return;
    setDeletingAnnouncementId(announcement.id);
    try {
      await api(`/operations/announcements/${announcement.id}`, {
        method: "DELETE",
      });
      toast.success("공지 삭제 완료");
      qc.invalidateQueries({ queryKey: ["announcements"] });
    } catch {
      toast.error("공지 삭제에 실패했어요.");
    } finally {
      setDeletingAnnouncementId(null);
    }
  };

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
                <Button size="sm" onClick={openCreateAnnouncement}>
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
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{a.title}</div>
                    {a.isUrgent && <Badge variant="destructive">긴급</Badge>}
                  </div>
                  <div className="mt-1 line-clamp-2 text-xs text-muted-foreground">
                    {a.body}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {formatDate(a.startsAt ?? a.publishedAt)} ~{" "}
                    {formatOptionalDate(a.endsAt)} · 대상 {a.audience}
                  </div>
                </div>
                <div className="flex shrink-0 items-center gap-1">
                  <RoleGate action="ops.announce" fallback="disable">
                    <Button
                      size="icon-sm"
                      variant="ghost"
                      title="공지 수정"
                      onClick={() => openEditAnnouncement(a)}
                    >
                      <Pencil className="size-3.5" />
                    </Button>
                  </RoleGate>
                  <RoleGate action="ops.announce" fallback="disable">
                    <Button
                      size="icon-sm"
                      variant="destructive"
                      title="공지 삭제"
                      disabled={deletingAnnouncementId === a.id}
                      onClick={() => deleteAnnouncement(a)}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  </RoleGate>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>약관·정책</CardTitle>
            </div>
            <CardDescription>현재 발행된 약관·정책 버전</CardDescription>
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
              <CardDescription>결제 · 외부 도서 API · 신고 큐 적체 · 점검 모드</CardDescription>
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
        <CardContent className="grid gap-4 md:grid-cols-3">
          <HealthRow label="결제" status={health?.payment} />
          <HealthRow label="외부 도서 API" status={health?.externalBookApi} />
          <div className="rounded-md border bg-card p-3">
            <div className="text-xs text-muted-foreground">신고 큐 적체</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums">
              {health?.moderationBacklog ?? "—"}건
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
        onOpenChange={(open) => {
          setAnnounceOpen(open);
          if (!open) setEditingAnnouncement(null);
        }}
        initialValue={announcementInitialValue}
        onSubmit={submitAnnouncement}
      />
    </div>
  );
}

function formatOptionalDate(iso: string | null): string {
  return iso ? formatDate(iso) : "무기한";
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
