"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { TriangleAlert } from "lucide-react";

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
import { Kbd } from "@/components/common/kbd";
import { PageHeader } from "@/components/common/page-header";
import {
  ReasonDialog,
  type ReasonOption,
} from "@/components/common/reason-dialog";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { useKeyboardShortcut } from "@/lib/use-keyboard-shortcut";
import { api } from "@/lib/api";
import {
  SIGNUP_REJECT_REASON_LABEL,
  SIGNUP_STATUS_LABEL,
  type SignupApplication,
  type SignupRejectReason,
  type SignupStatus,
} from "@/lib/types";
import { formatDateTime, maskBusinessNumber } from "@/lib/format";

const REJECT_REASONS: ReasonOption<SignupRejectReason>[] = (
  Object.keys(SIGNUP_REJECT_REASON_LABEL) as SignupRejectReason[]
).map((v) => ({ value: v, label: SIGNUP_REJECT_REASON_LABEL[v] }));

const STATUS_TONE: Record<SignupStatus, "neutral" | "info" | "warn" | "success" | "danger"> = {
  pending: "info",
  info_requested: "warn",
  approved: "success",
  rejected: "danger",
};

const SUPPLIER_LABEL: Record<"publisher" | "author" | "designer", string> = {
  publisher: "출판사",
  author: "작가",
  designer: "꾸미기 디자이너",
};

export default function SignupsPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["signups"],
    queryFn: () => api<SignupApplication[]>("/signups"),
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const items = data ?? [];
  const selected = items.find((s) => s.id === selectedId) ?? items[0] ?? null;

  const moveBy = (delta: number) => {
    if (items.length === 0) return;
    const i = Math.max(0, items.findIndex((s) => s.id === selected?.id));
    const next = items[(i + delta + items.length) % items.length];
    if (next) setSelectedId(next.id);
  };
  useKeyboardShortcut({ key: "j" }, () => moveBy(1));
  useKeyboardShortcut({ key: "k" }, () => moveBy(-1));
  useKeyboardShortcut({ key: "ArrowDown" }, () => moveBy(1));
  useKeyboardShortcut({ key: "ArrowUp" }, () => moveBy(-1));

  const decide = async (
    decision: "approve" | "reject" | "info_request",
    payload?: { reason: SignupRejectReason; comment: string },
  ) => {
    if (!selected) return;
    try {
      await api(`/signups/${selected.id}/decision`, {
        method: "POST",
        body: JSON.stringify({ decision, ...(payload ?? {}) }),
      });
      const tail =
        payload?.reason
          ? ` — ${SIGNUP_REJECT_REASON_LABEL[payload.reason]}`
          : "";
      toast.success(`${selected.businessName} — ${decision}${tail}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const [rejectOpen, setRejectOpen] = useState(false);
  const [infoOpen, setInfoOpen] = useState(false);

  return (
    <div className="space-y-6">
      <PageHeader
        title="크리에이터 가입 심사"
        description="사업자 번호 검증·통장 사본·증빙 서류를 검토하고 위험 신호를 강조합니다."
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>큐</CardTitle>
            <CardDescription className="flex items-center gap-1">
              <span>대기 / 추가 자료 요청 / 승인 / 거절 ·</span>
              <Kbd>J</Kbd>
              <Kbd>K</Kbd>
              <span>이동</span>
            </CardDescription>
          </CardHeader>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>사업자</TableHead>
                  <TableHead>유형</TableHead>
                  <TableHead>국세청</TableHead>
                  <TableHead>상태</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading && <TableSkeletonRows rows={4} cols={4} />}
                {(data ?? []).map((s) => (
                  <TableRow
                    key={s.id}
                    data-state={selected?.id === s.id ? "selected" : undefined}
                    onClick={() => setSelectedId(s.id)}
                    className="cursor-pointer"
                  >
                    <TableCell>
                      <div className="font-medium">{s.businessName}</div>
                      <div className="text-xs text-muted-foreground">
                        {maskBusinessNumber(s.businessNumber)} · {s.representativeName}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {s.supplierTypes.map((t) => (
                          <Badge key={t} variant="secondary" className="text-[10px]">
                            {SUPPLIER_LABEL[t]}
                          </Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell>
                      <StatusBadge tone={s.ntsStatus === "active" ? "success" : "danger"}>
                        {s.ntsStatus === "active" ? "활성" : s.ntsStatus === "closed" ? "휴·폐업" : "미상"}
                      </StatusBadge>
                    </TableCell>
                    <TableCell>
                      <StatusBadge tone={STATUS_TONE[s.status]}>
                        {SIGNUP_STATUS_LABEL[s.status]}
                      </StatusBadge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>상세</CardTitle>
            <CardDescription>{selected ? selected.businessName : "큐에서 항목을 선택하세요."}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {selected && (
              <>
                <div className="grid grid-cols-2 gap-2 text-sm">
                  <span className="text-muted-foreground">사업자 번호</span>
                  <span>{maskBusinessNumber(selected.businessNumber)}</span>
                  <span className="text-muted-foreground">대표자</span>
                  <span>{selected.representativeName}</span>
                  <span className="text-muted-foreground">담당자 이메일</span>
                  <span>{selected.email}</span>
                  <span className="text-muted-foreground">신청일시</span>
                  <span>{formatDateTime(selected.submittedAt)}</span>
                </div>

                {selected.riskFlags.length > 0 && (
                  <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
                    <div className="mb-1 flex items-center gap-1.5 font-medium">
                      <TriangleAlert className="size-4" /> 위험 신호
                    </div>
                    <ul className="list-disc pl-5">
                      {selected.riskFlags.map((f) => (
                        <li key={f}>{f}</li>
                      ))}
                    </ul>
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2 border-t pt-4">
                  <RoleGate action="signup.decide" fallback="disable">
                    <Button size="sm" onClick={() => decide("approve")}>
                      승인 + 환영 메일/정산 약관 발송
                    </Button>
                  </RoleGate>
                  <RoleGate action="signup.decide" fallback="disable">
                    <Button size="sm" variant="destructive" onClick={() => setRejectOpen(true)}>
                      거절…
                    </Button>
                  </RoleGate>
                  <RoleGate action="signup.decide" fallback="disable">
                    <Button size="sm" variant="outline" onClick={() => setInfoOpen(true)}>
                      추가 자료 요청…
                    </Button>
                  </RoleGate>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <ReasonDialog<SignupRejectReason>
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        title={selected ? `${selected.businessName} 가입 거절` : "가입 거절"}
        description="신청자에게 정형 사유 + 자유 코멘트가 그대로 발송됩니다."
        reasons={REJECT_REASONS}
        submitLabel="거절"
        submitVariant="destructive"
        onSubmit={(p) => decide("reject", p)}
      />

      <ReasonDialog<SignupRejectReason>
        open={infoOpen}
        onOpenChange={setInfoOpen}
        title={selected ? `${selected.businessName} 추가 자료 요청` : "추가 자료 요청"}
        description="요청 사유와 함께 어떤 서류·정보를 보강해야 하는지 코멘트로 안내해주세요."
        reasons={REJECT_REASONS}
        submitLabel="자료 요청"
        commentRequired
        onSubmit={(p) => decide("info_request", p)}
      />
    </div>
  );
}
