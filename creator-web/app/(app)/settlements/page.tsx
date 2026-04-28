"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowDownToLine,
  Banknote,
  Download,
  FileSpreadsheet,
} from "lucide-react";
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
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import { formatDate, formatWon } from "@/lib/format";
import {
  WITHDRAW_STATUS_LABEL,
  type BankAccountChangeRequest,
  type SettlementHistoryRow,
  type SettlementSummary,
  type WithdrawRequest,
  type WithdrawRequestStatus,
} from "@/lib/types";

export default function SettlementsPage() {
  const summary = useQuery({
    queryKey: ["settlement", "summary"],
    queryFn: () => api<SettlementSummary>("/settlement/summary"),
  });

  const withdraws = useQuery({
    queryKey: ["settlement", "withdraws"],
    queryFn: () => api<WithdrawRequest[]>("/settlement/withdraws"),
  });

  const bankChange = useQuery({
    queryKey: ["settlement", "bank-change"],
    queryFn: () =>
      api<BankAccountChangeRequest[]>("/settlement/bank-change"),
  });

  const history = useQuery({
    queryKey: ["settlement", "history"],
    queryFn: () => api<SettlementHistoryRow[]>("/settlement/history"),
  });

  const data = summary.data;
  const hasPendingBankChange = (bankChange.data ?? []).some(
    (req) => req.status === "pending",
  );

  const handleDownloadCsv = () => {
    const rows = history.data ?? [];
    if (rows.length === 0) {
      toast.error("내려받을 정산 내역이 없어요.");
      return;
    }
    downloadCsv(rows);
    toast.success("정산 내역 CSV를 내려받았어요.");
  };

  const handleAccountingReport = () => {
    toast.info("분기별 회계사무소 확인서가 곧 메일로 발송돼요.");
  };

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs font-medium text-muted-foreground">7. 정산</div>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">정산</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          돈의 위치를 명확하게. 매주 화요일 자동 정산, 5만 원 이상 즉시 출금 가능.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardDescription className="flex items-center gap-2">
              예치 보관 중
              <Badge variant="outline" className="text-[10px]">
                {data?.bankName ?? "은행"}
              </Badge>
            </CardDescription>
            <CardTitle className="text-2xl">
              {data ? formatWon(data.heldWon) : <Skeleton className="h-7 w-32" />}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs text-muted-foreground">
            {data ? `${data.bankName} ${data.accountMasked}` : "—"}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>정산 대기</CardDescription>
            <CardTitle className="text-2xl">
              {data ? formatWon(data.pendingWon) : <Skeleton className="h-7 w-32" />}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs text-muted-foreground">
            {data ? `${formatDate(data.nextPayoutDate)} (화) 자동 입금 예정` : "—"}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>누적 정산 완료</CardDescription>
            <CardTitle className="text-2xl">
              {data ? formatWon(data.paidWon) : <Skeleton className="h-7 w-32" />}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs text-muted-foreground">
            세금계산서는 자동 발행됩니다.
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">즉시 출금 / 계좌 관리</CardTitle>
          <CardDescription>
            5만 원 이상 누적 시 활성. 본인 인증(2FA) 후 1~2영업일 내 입금.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {hasPendingBankChange && (
            <div className="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              <AlertTriangle className="size-3.5" />
              관리자 승인 대기 중인 변경 요청이 있어요.
            </div>
          )}
          <div className="flex flex-wrap gap-2">
            <WithdrawDialog summary={data} />
            <BankChangeDialog />
            <Button
              variant="ghost"
              onClick={handleDownloadCsv}
              disabled={history.isLoading}
            >
              <Download className="size-4" /> 정산 내역 다운로드 (CSV)
            </Button>
            <Button variant="ghost" onClick={handleAccountingReport}>
              <FileSpreadsheet className="size-4" /> 회계 대조 리포트
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">출금 신청 내역</CardTitle>
          <CardDescription>
            즉시 출금 요청은 접수 후 1~2영업일 내 입금돼요.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-6">신청일</TableHead>
                <TableHead>금액</TableHead>
                <TableHead>입금 예정</TableHead>
                <TableHead className="pr-6 text-right">상태</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {withdraws.isLoading &&
                [1, 2, 3].map((i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={4} className="px-6">
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                  </TableRow>
                ))}
              {withdraws.data?.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={4}
                    className="px-6 py-12 text-center text-sm text-muted-foreground"
                  >
                    최근 출금 신청 이력이 없어요.
                  </TableCell>
                </TableRow>
              )}
              {withdraws.data?.map((row) => (
                <TableRow key={row.id}>
                  <TableCell className="pl-6 text-sm">
                    {formatDate(row.requestedAt)}
                  </TableCell>
                  <TableCell className="text-sm font-medium">
                    {formatWon(row.amountWon)}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(row.expectedPayoutDate)}
                  </TableCell>
                  <TableCell className="pr-6 text-right">
                    {withdrawStatusBadge(row.status)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle className="text-base">월별 정산 내역</CardTitle>
              <CardDescription>
                매월 자동 정산 + 세금계산서 자동 발행 결과예요.
              </CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={handleDownloadCsv}
              disabled={history.isLoading}
            >
              <Download className="size-3.5" /> CSV 내려받기
            </Button>
          </div>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-6">회차</TableHead>
                <TableHead>금액</TableHead>
                <TableHead>입금일</TableHead>
                <TableHead className="pr-6 text-right">세금계산서</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {history.isLoading &&
                [1, 2, 3].map((i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={4} className="px-6">
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                  </TableRow>
                ))}
              {history.data?.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={4}
                    className="px-6 py-12 text-center text-sm text-muted-foreground"
                  >
                    아직 정산 내역이 없어요.
                  </TableCell>
                </TableRow>
              )}
              {history.data?.map((row) => (
                <TableRow key={row.id}>
                  <TableCell className="pl-6 text-sm font-medium">
                    {row.period}
                  </TableCell>
                  <TableCell className="text-sm">
                    {formatWon(row.amountWon)}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(row.paidAt)}
                  </TableCell>
                  <TableCell className="pr-6 text-right">
                    {row.taxInvoiceIssued ? (
                      <Badge variant="outline">발행됨</Badge>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function withdrawStatusBadge(status: WithdrawRequestStatus) {
  if (status === "paid") {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {WITHDRAW_STATUS_LABEL.paid}
      </Badge>
    );
  }
  if (status === "failed") {
    return <Badge variant="destructive">{WITHDRAW_STATUS_LABEL.failed}</Badge>;
  }
  if (status === "processing") {
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {WITHDRAW_STATUS_LABEL.processing}
      </Badge>
    );
  }
  return <Badge variant="secondary">{WITHDRAW_STATUS_LABEL.pending}</Badge>;
}

function WithdrawDialog({ summary }: { summary: SettlementSummary | undefined }) {
  const queryClient = useQueryClient();
  const heldWon = summary?.heldWon ?? 0;
  const canWithdraw = heldWon >= 50_000;

  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState<number>(heldWon);
  const [code, setCode] = useState("");

  const reset = () => {
    setAmount(heldWon);
    setCode("");
  };

  const withdraw = useMutation({
    mutationFn: () =>
      api<WithdrawRequest>("/settlement/withdraws", {
        method: "POST",
        body: JSON.stringify({ amountWon: amount }),
      }),
    onSuccess: () => {
      toast.success("출금 신청이 접수됐어요. 1~2영업일 내 입금돼요.");
      queryClient.invalidateQueries({ queryKey: ["settlement", "summary"] });
      queryClient.invalidateQueries({ queryKey: ["settlement", "withdraws"] });
      setOpen(false);
      reset();
    },
    onError: (err) => {
      if (err instanceof ApiError && (err.status === 400 || err.status === 402)) {
        toast.error(err.message);
      } else {
        toast.error("출금 신청 중 오류가 발생했어요.");
      }
    },
  });

  const codeOk = /^\d{6}$/.test(code);
  const amountOk = amount >= 50_000 && amount <= heldWon;
  const submitDisabled = !codeOk || !amountOk || withdraw.isPending;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          setAmount(heldWon);
          setCode("");
        }
      }}
    >
      <DialogTrigger
        render={
          <Button disabled={!canWithdraw}>
            <ArrowDownToLine className="size-4" /> 즉시 출금 요청
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>즉시 출금 요청</DialogTitle>
          <DialogDescription>
            본인 인증(2FA) 후 1~2영업일 내 등록된 계좌로 입금돼요.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="rounded-md border bg-muted/40 p-3 text-xs">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">예치 보관 중</span>
              <span className="font-medium">{formatWon(heldWon)}</span>
            </div>
            <div className="mt-1 flex items-center justify-between">
              <span className="text-muted-foreground">입금 계좌</span>
              <span className="flex items-center gap-1">
                <Banknote className="size-3" />
                {summary
                  ? `${summary.bankName} ${summary.accountMasked}`
                  : "—"}
              </span>
            </div>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="withdraw-amount">출금 금액 (원)</Label>
            <Input
              id="withdraw-amount"
              type="number"
              min={50_000}
              max={heldWon}
              step={10_000}
              value={amount}
              onChange={(e) => setAmount(Number(e.target.value))}
            />
            <p className="text-xs text-muted-foreground">
              최소 50,000원 · 최대 {formatWon(heldWon)}
            </p>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="withdraw-2fa">본인 인증 코드 (2FA)</Label>
            <Input
              id="withdraw-2fa"
              inputMode="numeric"
              pattern="\d{6}"
              maxLength={6}
              placeholder="6자리 숫자"
              value={code}
              onChange={(e) =>
                setCode(e.target.value.replace(/\D/g, "").slice(0, 6))
              }
            />
            <p className="text-xs text-muted-foreground">
              테스트 환경에서는 6자리 숫자면 통과돼요.
            </p>
          </div>
        </div>
        <DialogFooter>
          <DialogClose render={<Button variant="outline">취소</Button>} />
          <Button
            onClick={() => withdraw.mutate()}
            disabled={submitDisabled}
          >
            {withdraw.isPending ? "신청 중…" : "출금 신청"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function BankChangeDialog() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [bankName, setBankName] = useState("");
  const [accountNumber, setAccountNumber] = useState("");
  const [accountHolder, setAccountHolder] = useState("");
  const [proofFileName, setProofFileName] = useState<string | null>(null);

  const reset = () => {
    setBankName("");
    setAccountNumber("");
    setAccountHolder("");
    setProofFileName(null);
  };

  const change = useMutation({
    mutationFn: () =>
      api<BankAccountChangeRequest>("/settlement/bank-change", {
        method: "POST",
        body: JSON.stringify({
          bankName,
          accountNumber,
          accountHolder,
          proofFileName,
        }),
      }),
    onSuccess: () => {
      toast.success("관리자 승인 후 3영업일 이내 적용돼요.");
      queryClient.invalidateQueries({
        queryKey: ["settlement", "bank-change"],
      });
      setOpen(false);
      reset();
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        toast.error("계좌 변경 신청 중 오류가 발생했어요.");
      }
    },
  });

  const submitDisabled =
    !bankName.trim() ||
    !accountNumber.trim() ||
    !accountHolder.trim() ||
    change.isPending;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) reset();
      }}
    >
      <DialogTrigger
        render={
          <Button variant="outline">
            <Banknote className="size-4" /> 정산 계좌 변경
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>정산 계좌 변경</DialogTitle>
          <DialogDescription>
            본인 명의 계좌만 등록할 수 있어요. 관리자 승인 후 적용돼요.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="bank-name">은행</Label>
            <Input
              id="bank-name"
              placeholder="예: 토스뱅크"
              value={bankName}
              onChange={(e) => setBankName(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="bank-account">계좌번호</Label>
            <Input
              id="bank-account"
              inputMode="numeric"
              placeholder="숫자만 입력"
              value={accountNumber}
              onChange={(e) => setAccountNumber(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="bank-holder">예금주</Label>
            <Input
              id="bank-holder"
              placeholder="본인 명의"
              value={accountHolder}
              onChange={(e) => setAccountHolder(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="bank-proof">통장 사본 (PDF/JPG)</Label>
            <Input
              id="bank-proof"
              type="file"
              accept="application/pdf,image/jpeg,image/jpg"
              onChange={(e) =>
                setProofFileName(e.target.files?.[0]?.name ?? null)
              }
            />
            {proofFileName && (
              <p className="text-xs text-muted-foreground">
                선택됨: {proofFileName}
              </p>
            )}
          </div>
        </div>
        <DialogFooter>
          <DialogClose render={<Button variant="outline">취소</Button>} />
          <Button onClick={() => change.mutate()} disabled={submitDisabled}>
            {change.isPending ? "신청 중…" : "변경 신청"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function downloadCsv(rows: SettlementHistoryRow[]) {
  const header = ["회차", "금액(원)", "입금일", "세금계산서"];
  const body = rows.map((r) => [
    r.period,
    String(r.amountWon),
    r.paidAt,
    r.taxInvoiceIssued ? "발행됨" : "미발행",
  ]);
  const lines = [header, ...body].map((cols) =>
    cols.map(csvEscape).join(","),
  );
  const csv = "﻿" + lines.join("\r\n");
  const today = new Date();
  const filename = `dotshelf-settlements-${today.getFullYear()}-${String(
    today.getMonth() + 1,
  ).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}.csv`;
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

function csvEscape(value: string): string {
  if (/[",\r\n]/.test(value)) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}
