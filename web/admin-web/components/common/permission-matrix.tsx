"use client";

import { Check, Minus } from "lucide-react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ROLE_ALLOW, type AdminAction } from "@/lib/permissions";
import { ADMIN_ROLE_LABEL, type AdminRole } from "@/lib/types";
import { cn } from "@/lib/utils";

type Group = { label: string; actions: { key: AdminAction; label: string }[] };

const GROUPS: Group[] = [
  {
    label: "콘텐츠 심사",
    actions: [
      { key: "review.claim", label: "클레임" },
      { key: "review.approve", label: "승인" },
      { key: "review.reject", label: "반려" },
      { key: "review.info_request", label: "추가 정보 요청" },
      { key: "review.bulk_decide", label: "일괄 처리" },
    ],
  },
  {
    label: "가입 심사",
    actions: [{ key: "signup.decide", label: "승인·거절·자료요청" }],
  },
  {
    label: "모더레이션",
    actions: [
      { key: "moderation.hide", label: "가림" },
      { key: "moderation.delete", label: "완전 삭제" },
      { key: "moderation.warn", label: "경고" },
      { key: "moderation.suspend_temp", label: "일시 정지" },
      { key: "moderation.suspend_perm", label: "영구 정지 (2단계)" },
      { key: "moderation.bulk", label: "일괄 처리" },
    ],
  },
  {
    label: "사용자",
    actions: [
      { key: "user.force_logout", label: "강제 로그아웃" },
      { key: "user.rename_nickname", label: "닉네임 강제 변경" },
      { key: "user.password_reset", label: "비밀번호 재설정" },
      { key: "user.suspend_temp", label: "일시 정지" },
      { key: "user.suspend_perm", label: "영구 정지" },
      { key: "user.anonymize", label: "데이터 익명화" },
      { key: "user.adjust_balance", label: "페이지 잔액 조정" },
    ],
  },
  {
    label: "정산",
    actions: [
      { key: "withdrawal.approve_first", label: "출금 1차 승인" },
      { key: "withdrawal.approve_settlement", label: "출금 2차 승인" },
      { key: "withdrawal.reject", label: "출금 거절" },
      { key: "settlement.adjust", label: "장애 보상·환불 조정" },
    ],
  },
  {
    label: "운영",
    actions: [
      { key: "ops.announce", label: "공지 발행" },
      { key: "ops.policy_publish", label: "약관 새 버전 발행" },
      { key: "ops.maintenance_toggle", label: "점검 모드 토글" },
      { key: "ops.role_assign", label: "관리자 권한 부여" },
    ],
  },
  {
    label: "감사",
    actions: [{ key: "audit.export", label: "감사 로그 CSV 내보내기" }],
  },
];

const ROLES: AdminRole[] = [
  "super_admin",
  "content_reviewer",
  "moderator",
  "settlement",
  "cs",
  "read_only",
];

export function PermissionMatrix() {
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="min-w-[220px]">액션</TableHead>
            {ROLES.map((r) => (
              <TableHead key={r} className="text-center">
                {ADMIN_ROLE_LABEL[r]}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {GROUPS.map((g) => (
            <>
              <TableRow key={g.label} className="bg-muted/30">
                <TableCell
                  colSpan={1 + ROLES.length}
                  className="text-xs font-semibold text-muted-foreground"
                >
                  {g.label}
                </TableCell>
              </TableRow>
              {g.actions.map((a) => (
                <TableRow key={a.key}>
                  <TableCell>
                    <div className="text-sm font-medium">{a.label}</div>
                    <div className="font-mono text-[10px] text-muted-foreground">
                      {a.key}
                    </div>
                  </TableCell>
                  {ROLES.map((r) => {
                    const allowed = ROLE_ALLOW[a.key].includes(r);
                    return (
                      <TableCell key={r} className="text-center">
                        {allowed ? (
                          <Check
                            className={cn(
                              "mx-auto size-4",
                              r === "super_admin"
                                ? "text-emerald-700"
                                : "text-emerald-600",
                            )}
                          />
                        ) : (
                          <Minus className="mx-auto size-4 text-muted-foreground/40" />
                        )}
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
