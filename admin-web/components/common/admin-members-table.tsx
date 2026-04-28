"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
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
import { StatusBadge } from "@/components/common/status-badge";
import { RoleGate } from "@/components/common/role-gate";
import { api } from "@/lib/api";
import {
  ADMIN_ROLE_LABEL,
  type AdminMember,
  type AdminRole,
} from "@/lib/types";
import { canRole } from "@/lib/permissions";
import { useAdminAccount } from "@/lib/use-admin-account";
import { formatDate, formatRelative } from "@/lib/format";

const ROLES: AdminRole[] = [
  "super_admin",
  "content_reviewer",
  "moderator",
  "settlement",
  "cs",
  "read_only",
];

export function AdminMembersTable() {
  const auth = useAdminAccount();
  const editable = canRole(auth?.account.role, "ops.role_assign");
  const qc = useQueryClient();

  const { data } = useQuery({
    queryKey: ["admin-members"],
    queryFn: () => api<AdminMember[]>("/operations/admins"),
  });

  const setRole = async (id: string, role: AdminRole) => {
    try {
      await api(`/operations/admins/${id}/role`, {
        method: "POST",
        body: JSON.stringify({ role }),
      });
      toast.success(`역할 변경 — ${ADMIN_ROLE_LABEL[role]}`);
      qc.setQueryData<AdminMember[]>(["admin-members"], (prev) =>
        prev?.map((m) => (m.id === id ? { ...m, role } : m)) ?? prev,
      );
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const toggleActive = async (m: AdminMember) => {
    try {
      await api(`/operations/admins/${m.id}/active`, {
        method: "POST",
        body: JSON.stringify({ active: !m.active }),
      });
      toast.success(`${m.name} ${m.active ? "비활성화" : "활성화"}`);
      qc.setQueryData<AdminMember[]>(["admin-members"], (prev) =>
        prev?.map((x) => (x.id === m.id ? { ...x, active: !x.active } : x)) ?? prev,
      );
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-xs text-muted-foreground">
          관리자 목록과 역할 부여. 변경은 즉시 감사 로그에 기록됩니다.
        </p>
        <RoleGate action="ops.role_assign" fallback="hide">
          <Button
            size="sm"
            variant="outline"
            onClick={() => toast.info("초대 메일 발송 (mock)")}
          >
            관리자 초대
          </Button>
        </RoleGate>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>이름</TableHead>
            <TableHead>이메일</TableHead>
            <TableHead>팀</TableHead>
            <TableHead>역할</TableHead>
            <TableHead>마지막 로그인</TableHead>
            <TableHead>상태</TableHead>
            <TableHead className="text-right">조치</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {(data ?? []).map((m) => (
            <TableRow key={m.id}>
              <TableCell className="font-medium">{m.name}</TableCell>
              <TableCell className="font-mono text-xs">{m.email}</TableCell>
              <TableCell>{m.team}</TableCell>
              <TableCell>
                {editable ? (
                  <Select
                    value={m.role}
                    onValueChange={(v) => setRole(m.id, v as AdminRole)}
                  >
                    <SelectTrigger size="sm" className="w-40">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ROLES.map((r) => (
                        <SelectItem key={r} value={r}>
                          {ADMIN_ROLE_LABEL[r]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <StatusBadge tone="info">{ADMIN_ROLE_LABEL[m.role]}</StatusBadge>
                )}
              </TableCell>
              <TableCell className="text-sm">
                <div>{m.lastSignInAt ? formatRelative(m.lastSignInAt) : "—"}</div>
                <div className="text-xs text-muted-foreground">가입 {formatDate(m.joinedAt)}</div>
              </TableCell>
              <TableCell>
                <StatusBadge tone={m.active ? "success" : "neutral"}>
                  {m.active ? "활성" : "비활성"}
                </StatusBadge>
              </TableCell>
              <TableCell className="text-right">
                <RoleGate action="ops.role_assign" fallback="hide">
                  <Button size="sm" variant="ghost" onClick={() => toggleActive(m)}>
                    {m.active ? "비활성화" : "활성화"}
                  </Button>
                </RoleGate>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
