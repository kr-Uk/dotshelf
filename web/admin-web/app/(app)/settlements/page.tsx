"use client";

import { useState } from "react";
import { useQueries } from "@tanstack/react-query";
import { toast } from "sonner";
import { CheckCircle2, CircleAlert, FileText, Upload } from "lucide-react";

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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PageHeader } from "@/components/common/page-header";
import {
  ReasonDialog,
  type ReasonOption,
} from "@/components/common/reason-dialog";
import { RoleGate } from "@/components/common/role-gate";
import { StatCard } from "@/components/common/stat-card";
import { StatusBadge } from "@/components/common/status-badge";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { WorkflowStepper, type StepperStep } from "@/components/common/workflow-stepper";
import { api } from "@/lib/api";
import {
  BANK_ACCOUNT_CHANGE_STATUS_LABEL,
  BALANCE_ADJUST_REASON_LABEL,
  WITHDRAWAL_STATUS_LABEL,
  type AccountingReconciliation,
  type AutoPayoutBatch,
  type BalanceAdjustReason,
  type BankAccountChangeRequest,
  type CreatorPayoutSummary,
  type QuarterlyAttestation,
  type WithdrawalRequest,
  type WithdrawalStatus,
} from "@/lib/types";
import { formatDate, formatDateTime, formatWon } from "@/lib/format";

const WD_TONE: Record<WithdrawalStatus, "neutral" | "info" | "warn" | "success" | "danger"> = {
  pending: "warn",
  first_approved: "info",
  settlement_approved: "info",
  rejected: "danger",
  remitted: "success",
};

const WITHDRAWAL_STEPS: StepperStep[] = [
  { key: "pending", label: "본인 인증" },
  { key: "first_approved", label: "1차 승인" },
  { key: "settlement_approved", label: "정산 승인" },
  { key: "remitted", label: "송금 완료" },
];

const ADJUST_REASONS: ReasonOption<BalanceAdjustReason>[] = (
  Object.keys(BALANCE_ADJUST_REASON_LABEL) as BalanceAdjustReason[]
).map((v) => ({ value: v, label: BALANCE_ADJUST_REASON_LABEL[v] }));

export default function SettlementsPage() {
  const [recQ, wdQ, payoutQ, accQ, crQ, attQ] = useQueries({
    queries: [
      {
        queryKey: ["settlement-reconciliation"],
        queryFn: () => api<AccountingReconciliation>("/settlement/reconciliation"),
      },
      {
        queryKey: ["settlement-withdrawals"],
        queryFn: () => api<WithdrawalRequest[]>("/settlement/withdrawals"),
      },
      {
        queryKey: ["settlement-payout-batches"],
        queryFn: () => api<AutoPayoutBatch[]>("/settlement/payout-batches"),
      },
      {
        queryKey: ["settlement-account-changes"],
        queryFn: () => api<BankAccountChangeRequest[]>("/settlement/account-changes"),
      },
      {
        queryKey: ["settlement-creators"],
        queryFn: () => api<CreatorPayoutSummary[]>("/settlement/creators"),
      },
      {
        queryKey: ["settlement-attestations"],
        queryFn: () => api<QuarterlyAttestation[]>("/settlement/attestations"),
      },
    ],
  });

  const [adjustOpen, setAdjustOpen] = useState(false);

  const withdrawalStep = (status: WithdrawalStatus) => {
    if (status === "rejected") return null;
    return status;
  };

  const advanceWithdrawal = async (id: string, action: "approve" | "reject") => {
    try {
      await api(`/settlement/withdrawals/${id}/${action}`, { method: "POST" });
      toast.success(`출금 ${id} ${action === "approve" ? "다음 단계로" : "거절"}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const decideAccountChange = async (id: string, decision: "approve" | "reject") => {
    try {
      await api(`/settlement/account-changes/${id}/decision`, {
        method: "POST",
        body: JSON.stringify({ decision }),
      });
      toast.success(`정산 계좌 변경 ${id} — ${decision}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const publishAttestation = async () => {
    try {
      await api("/settlement/attestations", { method: "POST" });
      toast.success("회계사 확인서 발행 완료 — 크리에이터 대시보드에 공시됨");
    } catch {
      toast.error("발행 실패");
    }
  };

  const rec = recQ.data;
  const wds = wdQ.data ?? [];
  const batches = payoutQ.data ?? [];
  const accChanges = accQ.data ?? [];
  const creators = crQ.data ?? [];
  const attestations = attQ.data ?? [];
  const accPending = accChanges.filter((c) => c.status === "pending_review").length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="정산 모니터링·승인"
        description="회계 대조 / 출금 단계 / 정산 계좌 변경 / 크리에이터별 / 회계사 확인서 / 장애 보상까지."
        actions={
          <RoleGate action="settlement.adjust" fallback="hide">
            <Button size="sm" variant="outline" onClick={() => setAdjustOpen(true)}>
              장애 보상·환불 조정…
            </Button>
          </RoleGate>
        }
      />

      <Card className={rec && !rec.consistent ? "ring-2 ring-destructive" : undefined}>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>회계 대조</CardTitle>
            <StatusBadge tone={rec?.consistent ? "success" : "danger"}>
              {rec?.consistent ? (
                <span className="inline-flex items-center gap-1">
                  <CheckCircle2 className="size-3.5" /> 일치
                </span>
              ) : (
                <span className="inline-flex items-center gap-1">
                  <CircleAlert className="size-3.5" /> 불일치
                </span>
              )}
            </StatusBadge>
          </div>
          <CardDescription>
            총 선불 충전금 = 은행 예치금 + 정산 대기 — 분기 1회 회계사무소 확인서로 공시
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <StatCard
            label="선불 충전금 잔액"
            value={rec ? formatWon(rec.prepaidBalanceWon) : "—"}
            caption="장부상"
          />
          <StatCard
            label="은행 예치금"
            value={rec ? formatWon(rec.bankDepositWon) : "—"}
            caption="분리 보관 계좌"
          />
          <StatCard
            label="정산 대기"
            value={rec ? formatWon(rec.pendingPayoutWon) : "—"}
            caption={rec ? `기준 ${formatDateTime(rec.asOf)}` : "—"}
          />
        </CardContent>
      </Card>

      <Tabs defaultValue="withdrawals">
        <TabsList>
          <TabsTrigger value="withdrawals">출금 요청 ({wds.length})</TabsTrigger>
          <TabsTrigger value="creators">크리에이터별 정산</TabsTrigger>
          <TabsTrigger value="account-changes">
            계좌 변경 {accPending > 0 ? `(${accPending})` : ""}
          </TabsTrigger>
          <TabsTrigger value="batches">자동 정산 배치</TabsTrigger>
          <TabsTrigger value="attestations">회계사 확인서</TabsTrigger>
        </TabsList>

        <TabsContent value="withdrawals" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>출금 요청 큐</CardTitle>
              <CardDescription>
                본인 인증 → 1차 승인 → 정산 담당 2차 승인 → 송금 트리거
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 pt-0">
              {wdQ.isLoading && (
                <div className="text-sm text-muted-foreground">불러오는 중…</div>
              )}
              {wds.map((w) => {
                const step = withdrawalStep(w.status);
                return (
                  <div
                    key={w.id}
                    className="grid gap-3 rounded-lg border bg-card p-4 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)_minmax(0,2fr)]"
                  >
                    <div className="space-y-1">
                      <div className="font-medium">{w.creatorName}</div>
                      <div className="text-lg font-semibold tabular-nums">
                        {formatWon(w.amountWon)}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        요청 {formatDateTime(w.requestedAt)}
                      </div>
                      <div className="flex items-center gap-2 text-xs">
                        <StatusBadge tone={w.identityVerified ? "success" : "danger"}>
                          본인 인증 {w.identityVerified ? "통과" : "미통과"}
                        </StatusBadge>
                        <StatusBadge tone={WD_TONE[w.status]}>
                          {WITHDRAWAL_STATUS_LABEL[w.status]}
                        </StatusBadge>
                      </div>
                    </div>

                    <div className="flex items-center">
                      {step ? (
                        <WorkflowStepper steps={WITHDRAWAL_STEPS} currentKey={step} />
                      ) : (
                        <div className="text-sm text-destructive">거절됨 — 단계 없음</div>
                      )}
                    </div>

                    <div className="flex flex-wrap items-center justify-end gap-2">
                      {w.status === "pending" && (
                        <RoleGate action="withdrawal.approve_first" fallback="disable">
                          <Button
                            size="sm"
                            onClick={() => advanceWithdrawal(w.id, "approve")}
                            disabled={!w.identityVerified}
                          >
                            1차 승인
                          </Button>
                        </RoleGate>
                      )}
                      {w.status === "first_approved" && (
                        <RoleGate
                          action="withdrawal.approve_settlement"
                          fallback="disable"
                          reason="정산 담당 / 슈퍼 관리자만"
                        >
                          <Button size="sm" onClick={() => advanceWithdrawal(w.id, "approve")}>
                            2차 승인 (송금)
                          </Button>
                        </RoleGate>
                      )}
                      {w.status === "settlement_approved" && (
                        <Button size="sm" disabled>
                          송금 진행 중…
                        </Button>
                      )}
                      <RoleGate action="withdrawal.reject" fallback="disable">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => advanceWithdrawal(w.id, "reject")}
                          disabled={w.status === "remitted" || w.status === "rejected"}
                        >
                          거절
                        </Button>
                      </RoleGate>
                    </div>
                  </div>
                );
              })}
              {!wdQ.isLoading && wds.length === 0 && (
                <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                  대기 중인 출금 요청이 없어요.
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="creators" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>크리에이터별 정산 대기·완료</CardTitle>
              <CardDescription>이번 달 / 누적 / 다음 정산 예정.</CardDescription>
            </CardHeader>
            <CardContent className="px-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>크리에이터</TableHead>
                    <TableHead>은행</TableHead>
                    <TableHead className="text-right">정산 대기</TableHead>
                    <TableHead className="text-right">이번 달 입금</TableHead>
                    <TableHead className="text-right">누적 입금</TableHead>
                    <TableHead>다음 정산</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {crQ.isLoading && <TableSkeletonRows rows={4} cols={6} />}
                  {creators.map((c) => (
                    <TableRow key={c.creatorId}>
                      <TableCell>
                        <div className="font-medium">{c.creatorName}</div>
                        <div className="text-xs text-muted-foreground">{c.creatorId}</div>
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">{c.bank}</div>
                        <div className="text-xs text-muted-foreground">{c.accountMasked}</div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatWon(c.pendingWon)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatWon(c.paidThisMonthWon)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums text-muted-foreground">
                        {formatWon(c.totalPaidWon)}
                      </TableCell>
                      <TableCell className="text-sm">{formatDate(c.nextPayoutAt)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="account-changes" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>정산 계좌 변경 큐</CardTitle>
              <CardDescription>
                통장 사본 + 사업자명 일치 검증 후 승인. 보통 3영업일 이내.
              </CardDescription>
            </CardHeader>
            <CardContent className="px-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>크리에이터</TableHead>
                    <TableHead>변경 전</TableHead>
                    <TableHead>변경 후</TableHead>
                    <TableHead>증빙</TableHead>
                    <TableHead>요청</TableHead>
                    <TableHead>상태</TableHead>
                    <TableHead className="text-right">조치</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {accQ.isLoading && <TableSkeletonRows rows={3} cols={7} />}
                  {accChanges.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell>
                        <div className="font-medium">{c.creatorName}</div>
                        <div className="text-xs text-muted-foreground">{c.creatorId}</div>
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">{c.oldBank}</div>
                        <div className="text-xs text-muted-foreground">{c.oldAccountMasked}</div>
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">{c.newBank}</div>
                        <div className="text-xs text-muted-foreground">{c.newAccountMasked}</div>
                      </TableCell>
                      <TableCell className="font-mono text-xs">{c.bankbookFile}</TableCell>
                      <TableCell className="text-sm">{formatDateTime(c.requestedAt)}</TableCell>
                      <TableCell>
                        <StatusBadge
                          tone={
                            c.status === "approved"
                              ? "success"
                              : c.status === "rejected"
                                ? "danger"
                                : "warn"
                          }
                        >
                          {BANK_ACCOUNT_CHANGE_STATUS_LABEL[c.status]}
                        </StatusBadge>
                      </TableCell>
                      <TableCell className="text-right">
                        {c.status === "pending_review" && (
                          <RoleGate action="withdrawal.approve_first" fallback="disable">
                            <div className="inline-flex gap-2">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => decideAccountChange(c.id, "approve")}
                              >
                                승인
                              </Button>
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => decideAccountChange(c.id, "reject")}
                              >
                                거절
                              </Button>
                            </div>
                          </RoleGate>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="batches" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>주기 자동 정산 (매주 화요일)</CardTitle>
              <CardDescription>실패 건은 재시도, 결과는 감사 로그로 보존.</CardDescription>
            </CardHeader>
            <CardContent className="px-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>배치 ID</TableHead>
                    <TableHead>예정 일시</TableHead>
                    <TableHead className="text-right">총액</TableHead>
                    <TableHead>성공 / 실패</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {batches.map((b) => (
                    <TableRow key={b.id}>
                      <TableCell className="font-mono text-xs">{b.id}</TableCell>
                      <TableCell>{formatDateTime(b.scheduledAt)}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatWon(b.totalWon)}
                      </TableCell>
                      <TableCell>
                        <StatusBadge tone={b.countFailed > 0 ? "warn" : "success"}>
                          {b.countSucceeded} / {b.countFailed}
                        </StatusBadge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="attestations" className="pt-4">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle>분기별 회계사 확인서</CardTitle>
                  <CardDescription>
                    회계사무소 확인서 첨부 + 공시. 크리에이터 대시보드에 노출.
                  </CardDescription>
                </div>
                <RoleGate action="settlement.adjust" fallback="hide">
                  <Button size="sm" onClick={publishAttestation}>
                    <Upload className="size-3.5" /> 새 확인서 발행…
                  </Button>
                </RoleGate>
              </div>
            </CardHeader>
            <CardContent className="space-y-2">
              {attestations.map((a) => (
                <div
                  key={a.id}
                  className="flex items-center justify-between rounded-md border bg-card p-3 text-sm"
                >
                  <div className="flex items-center gap-2">
                    <FileText className="size-4 text-muted-foreground" />
                    <div>
                      <div className="font-medium">{a.quarter} 회계 대조 확인서</div>
                      <div className="text-xs text-muted-foreground">
                        {a.attestor} · {formatDate(a.publishedAt)}
                      </div>
                    </div>
                  </div>
                  <Button size="sm" variant="ghost">
                    {a.fileUrl}
                  </Button>
                </div>
              ))}
              {!attQ.isLoading && attestations.length === 0 && (
                <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                  발행된 확인서가 없어요.
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ReasonDialog<BalanceAdjustReason>
        open={adjustOpen}
        onOpenChange={setAdjustOpen}
        title="장애 보상·환불 페이지 조정"
        description="영향 사용자 리스트 + 사유를 함께 남깁니다. 슈퍼 관리자 + 사유 필수."
        reasons={ADJUST_REASONS}
        submitLabel="조정 적용"
        submitVariant="destructive"
        commentRequired
        onSubmit={async (p) => {
          await api("/settlement/adjust", {
            method: "POST",
            body: JSON.stringify(p),
          });
          toast.success(`보상·환불 조정 적용 — 사유: ${p.reason}`);
        }}
      />
    </div>
  );
}
