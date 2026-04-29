"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Bell,
  ChevronRight,
  CreditCard,
  FileText,
  KeyRound,
  ShieldCheck,
  Trash2,
  User,
  Wallet,
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
import { Checkbox } from "@/components/ui/checkbox";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api, ApiError } from "@/lib/api";
import { clearAuth } from "@/lib/auth";
import { useAccount } from "@/lib/use-account";
import { formatWon } from "@/lib/format";
import { cn } from "@/lib/utils";
import {
  NOTIFICATION_CATEGORY_LABEL,
  NOTIFICATION_CHANNEL_LABEL,
  SUPPLIER_TYPE_LABEL,
  type EscrowBalance,
  type NotificationCategoryKey,
  type NotificationChannel,
  type NotificationPrefs,
  type SettlementSummary,
} from "@/lib/types";

type SettingsTab = "account" | "payments" | "notifications" | "policies";

const TAB_TRIGGERS: Array<{ value: SettingsTab; label: string }> = [
  { value: "account", label: "계정" },
  { value: "payments", label: "결제·정산" },
  { value: "notifications", label: "알림" },
  { value: "policies", label: "약관·탈퇴" },
];

const NOTIFICATION_CATEGORIES: NotificationCategoryKey[] = [
  "review",
  "payout",
  "ad_guardrail",
  "report",
  "system",
];

const NOTIFICATION_CHANNELS: NotificationChannel[] = [
  "inapp",
  "email",
  "sms",
];

type TaxInvoiceOption = "auto" | "auto_email";

const TAX_INVOICE_OPTIONS: Array<{ value: TaxInvoiceOption; label: string }> = [
  { value: "auto", label: "사업자번호 자동 발행" },
  { value: "auto_email", label: "사업자번호 + 이메일 동시 발송" },
];

export default function SettingsPage() {
  const [tab, setTab] = useState<SettingsTab>("account");

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs font-medium text-muted-foreground">
          9. 설정
        </div>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">설정</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          계정·결제·알림·약관/탈퇴
        </p>
      </div>

      <Tabs value={tab} onValueChange={(v) => setTab(v as SettingsTab)}>
        <TabsList>
          {TAB_TRIGGERS.map((t) => (
            <TabsTrigger key={t.value} value={t.value}>
              {t.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {tab === "account" ? <AccountTab /> : null}
      {tab === "payments" ? <PaymentsTab /> : null}
      {tab === "notifications" ? <NotificationsTab /> : null}
      {tab === "policies" ? <PoliciesTab /> : null}
    </div>
  );
}

// ---------------------------------------------------------------------------
// 계정 탭
// ---------------------------------------------------------------------------

function AccountTab() {
  const auth = useAccount();
  const [twoFa, setTwoFa] = useState(false);
  const [phone, setPhone] = useState("");

  const account = auth?.account;
  const supplierTypes = account?.supplierTypes ?? [];

  const handlePhoneSubmit = () => {
    if (!phone.trim()) {
      toast.error("휴대폰 번호를 입력해 주세요.");
      return;
    }
    toast.success("담당자 휴대폰이 변경됐어요.");
  };

  const handleBusinessChangeRequest = () => {
    toast.info("문의로 전환할까요? — 곧 1:1 문의로 연결돼요.");
  };

  const handleTwoFaChange = (checked: boolean) => {
    setTwoFa(checked);
    toast.success(
      checked ? "휴대폰 SMS 2FA를 켰어요." : "휴대폰 SMS 2FA를 껐어요.",
    );
  };

  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <User className="size-4" /> 계정 정보
          </CardTitle>
          <CardDescription>
            기본 계정 정보예요. 중요 항목은 관리자 재승인이 필요해요.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <SummaryRow label="이메일" value={account?.email} />
          <SummaryRow label="사업자명" value={account?.businessName} />
          <SummaryRow label="사업자번호" value={account?.businessNumber} />
          <SummaryRow
            label="대표자명"
            value={account?.representativeName}
          />
          <div className="sm:col-span-2">
            <div className="text-xs text-muted-foreground">가입 유형</div>
            <div className="mt-1 flex flex-wrap gap-1">
              {supplierTypes.length === 0 ? (
                <Skeleton className="h-5 w-24" />
              ) : (
                supplierTypes.map((t) => (
                  <Badge key={t} variant="secondary" className="text-[10px]">
                    {SUPPLIER_TYPE_LABEL[t]}
                  </Badge>
                ))
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <KeyRound className="size-4" /> 비밀번호 변경
          </CardTitle>
          <CardDescription>
            8자 이상의 새 비밀번호로 변경할 수 있어요.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ChangePasswordDialog />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldCheck className="size-4" /> 2단계 인증
          </CardTitle>
          <CardDescription>
            로그인 시 휴대폰 SMS로 추가 인증 코드를 받습니다.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <label className="flex cursor-pointer items-center gap-3 rounded-md border border-border bg-card px-3 py-3 hover:bg-muted/40">
            <Checkbox
              checked={twoFa}
              onCheckedChange={(v) => handleTwoFaChange(!!v)}
            />
            <div className="flex-1 text-sm">
              <div className="font-medium">휴대폰 SMS 2FA</div>
              <div className="text-xs text-muted-foreground">
                {twoFa
                  ? "켜져 있어요. 새 기기 로그인 시 인증 코드가 전송돼요."
                  : "꺼져 있어요. 켜는 것을 권장해요."}
              </div>
            </div>
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">담당자 휴대폰</CardTitle>
          <CardDescription>
            긴급 안내·2FA 코드 수신에 사용돼요.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap items-end gap-2">
            <div className="grid flex-1 gap-1.5 sm:max-w-xs">
              <Label htmlFor="contact-phone">휴대폰 번호</Label>
              <Input
                id="contact-phone"
                type="tel"
                inputMode="tel"
                placeholder="010-0000-0000"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
              />
            </div>
            <Button onClick={handlePhoneSubmit}>변경</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">사업자 정보 변경 안내</CardTitle>
          <CardDescription>
            중요 변경(상호·대표자명·사업자번호)은 관리자 재승인이 필요해요.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" onClick={handleBusinessChangeRequest}>
            변경 신청
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

function SummaryRow({
  label,
  value,
}: {
  label: string;
  value: string | undefined;
}) {
  return (
    <div className="grid gap-1">
      <div className="text-xs text-muted-foreground">{label}</div>
      {value ? (
        <div className="text-sm font-medium">{value}</div>
      ) : (
        <Skeleton className="h-5 w-32" />
      )}
    </div>
  );
}

function ChangePasswordDialog() {
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");

  const reset = () => {
    setCurrent("");
    setNext("");
    setConfirm("");
  };

  const handleSubmit = () => {
    if (!current) {
      toast.error("현재 비밀번호를 입력해 주세요.");
      return;
    }
    if (next.length < 8) {
      toast.error("새 비밀번호는 8자 이상이어야 해요.");
      return;
    }
    if (next !== confirm) {
      toast.error("새 비밀번호 확인이 일치하지 않아요.");
      return;
    }
    toast.success("비밀번호가 변경됐어요.");
    reset();
    setOpen(false);
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={<Button variant="outline">비밀번호 변경</Button>}
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>비밀번호 변경</DialogTitle>
          <DialogDescription>
            8자 이상의 새 비밀번호로 변경해 주세요.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="pw-current">현재 비밀번호</Label>
            <Input
              id="pw-current"
              type="password"
              autoComplete="current-password"
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="pw-new">새 비밀번호</Label>
            <Input
              id="pw-new"
              type="password"
              autoComplete="new-password"
              value={next}
              onChange={(e) => setNext(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="pw-confirm">새 비밀번호 확인</Label>
            <Input
              id="pw-confirm"
              type="password"
              autoComplete="new-password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <DialogClose render={<Button variant="outline">취소</Button>} />
          <Button onClick={handleSubmit}>변경</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// 결제·정산 탭
// ---------------------------------------------------------------------------

function PaymentsTab() {
  const escrow = useQuery({
    queryKey: ["escrow"],
    queryFn: () => api<EscrowBalance>("/escrow"),
  });

  const settlement = useQuery({
    queryKey: ["settlement", "summary"],
    queryFn: () => api<SettlementSummary>("/settlement/summary"),
  });

  const [taxInvoice, setTaxInvoice] = useState<TaxInvoiceOption>("auto");

  return (
    <div className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardDescription className="flex items-center gap-2">
              <Wallet className="size-3.5" /> 광고 예치금
              <Badge variant="outline" className="text-[10px]">
                분리 보관
              </Badge>
            </CardDescription>
            <CardTitle className="text-2xl">
              {escrow.isLoading ? (
                <Skeleton className="h-7 w-32" />
              ) : (
                formatWon(escrow.data?.balanceWon ?? 0)
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-xs text-muted-foreground">
            {escrow.data ? (
              <div>{escrow.data.custodianBankName}</div>
            ) : (
              <Skeleton className="h-4 w-32" />
            )}
            <ChargeDialog />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardDescription className="flex items-center gap-2">
              <CreditCard className="size-3.5" /> 정산 계좌
            </CardDescription>
            <CardTitle className="text-base">
              {settlement.isLoading ? (
                <Skeleton className="h-6 w-40" />
              ) : (
                settlement.data?.bankName ?? "—"
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {settlement.data ? (
              <div className="font-mono text-sm">
                {settlement.data.accountMasked}
              </div>
            ) : (
              <Skeleton className="h-5 w-40" />
            )}
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                toast.info("정산 페이지에서 변경할 수 있어요.")
              }
            >
              변경 신청
            </Button>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">세금계산서 옵션</CardTitle>
          <CardDescription>
            정산 회차마다 자동 발행 방식을 선택해요.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap items-center gap-3">
            <Label className="text-sm" htmlFor="tax-invoice-select">
              발행 방식
            </Label>
            <Select
              value={taxInvoice}
              onValueChange={(v) => setTaxInvoice(v as TaxInvoiceOption)}
            >
              <SelectTrigger id="tax-invoice-select" className="w-72">
                <SelectValue>
                  {(v: TaxInvoiceOption) =>
                    TAX_INVOICE_OPTIONS.find((o) => o.value === v)?.label
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {TAX_INVOICE_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function ChargeDialog() {
  const queryClient = useQueryClient();
  const [amount, setAmount] = useState(100_000);

  const charge = useMutation({
    mutationFn: () =>
      api<EscrowBalance>("/escrow/charge", {
        method: "POST",
        body: JSON.stringify({ amountWon: amount }),
      }),
    onSuccess: (next) => {
      queryClient.setQueryData(["escrow"], next);
      queryClient.invalidateQueries({ queryKey: ["escrow"] });
      toast.success(`${formatWon(amount)} 충전됐어요.`);
    },
    onError: () => toast.error("충전 중 오류"),
  });

  return (
    <Dialog>
      <DialogTrigger
        render={
          <Button variant="outline" size="sm" className={cn("mt-3 w-full")}>
            <CreditCard className="size-3.5" /> 예치금 충전
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>광고 예치금 충전</DialogTitle>
          <DialogDescription>
            카드 / 계좌이체. 충전금은 운영 계좌와 분리 보관(전자금융 컴플라이언스)됩니다.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid grid-cols-3 gap-2">
            {[100_000, 300_000, 500_000].map((v) => (
              <Button
                key={v}
                variant={amount === v ? "default" : "outline"}
                onClick={() => setAmount(v)}
              >
                {formatWon(v)}
              </Button>
            ))}
          </div>
          <Input
            type="number"
            value={amount}
            onChange={(e) => setAmount(Number(e.target.value))}
            min={10_000}
            step={10_000}
          />
        </div>
        <DialogFooter>
          <DialogClose
            render={
              <Button
                onClick={() => charge.mutate()}
                disabled={charge.isPending}
              >
                {charge.isPending ? "충전 중…" : `${formatWon(amount)} 충전`}
              </Button>
            }
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// 알림 탭
// ---------------------------------------------------------------------------

function NotificationsTab() {
  const queryClient = useQueryClient();
  const prefsQuery = useQuery({
    queryKey: ["notifications", "prefs"],
    queryFn: () => api<NotificationPrefs>("/notifications/prefs"),
  });

  const [draft, setDraft] = useState<NotificationPrefs | null>(null);

  // 서버 fetch 결과를 편집용 draft로 옮김. 외부 데이터 동기화라 effect가 정당.
  useEffect(() => {
    if (prefsQuery.data) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDraft(prefsQuery.data);
    }
  }, [prefsQuery.data]);

  const dirty = useMemo(() => {
    if (!draft || !prefsQuery.data) return false;
    return JSON.stringify(draft) !== JSON.stringify(prefsQuery.data);
  }, [draft, prefsQuery.data]);

  const save = useMutation({
    mutationFn: (next: NotificationPrefs) =>
      api<NotificationPrefs>("/notifications/prefs", {
        method: "PUT",
        body: JSON.stringify(next),
      }),
    onSuccess: (next) => {
      queryClient.setQueryData(["notifications", "prefs"], next);
      queryClient.invalidateQueries({
        queryKey: ["notifications", "prefs"],
      });
      toast.success("알림 설정이 저장됐어요.");
    },
    onError: () => toast.error("알림 설정 저장 중 오류"),
  });

  const toggle = (
    category: NotificationCategoryKey,
    channel: NotificationChannel,
    value: boolean,
  ) => {
    setDraft((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        [category]: {
          ...prev[category],
          [channel]: value,
        },
      };
    });
  };

  const handleReset = () => {
    if (prefsQuery.data) {
      setDraft(prefsQuery.data);
    }
  };

  const handleSave = () => {
    if (!draft) return;
    save.mutate(draft);
  };

  return (
    <Card>
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <Bell className="size-4" /> 알림 채널 설정
            </CardTitle>
            <CardDescription>
              카테고리별로 인앱·이메일·SMS 수신을 켜고 끌 수 있어요.
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleReset}
              disabled={!dirty || save.isPending}
            >
              되돌리기
            </Button>
            <Button
              size="sm"
              onClick={handleSave}
              disabled={!dirty || save.isPending || !draft}
            >
              {save.isPending ? "저장 중…" : "변경 저장"}
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="px-0">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-xs text-muted-foreground">
                <th className="px-6 py-2 text-left font-medium">카테고리</th>
                {NOTIFICATION_CHANNELS.map((ch) => (
                  <th
                    key={ch}
                    className="px-4 py-2 text-center font-medium"
                  >
                    {NOTIFICATION_CHANNEL_LABEL[ch]}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {prefsQuery.isLoading || !draft
                ? NOTIFICATION_CATEGORIES.map((cat) => (
                    <tr key={cat} className="border-b last:border-b-0">
                      <td className="px-6 py-3 text-sm font-medium">
                        {NOTIFICATION_CATEGORY_LABEL[cat]}
                      </td>
                      {NOTIFICATION_CHANNELS.map((ch) => (
                        <td
                          key={ch}
                          className="px-4 py-3 text-center"
                        >
                          <Skeleton className="mx-auto h-4 w-4" />
                        </td>
                      ))}
                    </tr>
                  ))
                : NOTIFICATION_CATEGORIES.map((cat) => (
                    <tr key={cat} className="border-b last:border-b-0">
                      <td className="px-6 py-3 text-sm font-medium">
                        {NOTIFICATION_CATEGORY_LABEL[cat]}
                      </td>
                      {NOTIFICATION_CHANNELS.map((ch) => (
                        <td
                          key={ch}
                          className="px-4 py-3 text-center"
                        >
                          <Checkbox
                            checked={draft[cat][ch]}
                            onCheckedChange={(v) => toggle(cat, ch, !!v)}
                            aria-label={`${NOTIFICATION_CATEGORY_LABEL[cat]} ${NOTIFICATION_CHANNEL_LABEL[ch]}`}
                          />
                        </td>
                      ))}
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// 약관·탈퇴 탭
// ---------------------------------------------------------------------------

const POLICY_ITEMS: Array<{ key: string; label: string }> = [
  { key: "supplier_terms", label: "입점 약관" },
  { key: "privacy", label: "개인정보 처리방침" },
  { key: "settlement", label: "정산 약관" },
  { key: "refund", label: "환불 정책" },
];

function PoliciesTab() {
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="size-4" /> 약관·정책
          </CardTitle>
          <CardDescription>
            현재 약관 본문은 별도 페이지로 곧 연결돼요.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-1">
          {POLICY_ITEMS.map((item) => (
            <Button
              key={item.key}
              variant="ghost"
              className="h-auto w-full justify-between px-3 py-3 text-sm"
              onClick={() =>
                toast.info("곧 약관 본문 페이지로 연결돼요.")
              }
            >
              <span>{item.label}</span>
              <ChevronRight className="size-4 text-muted-foreground" />
            </Button>
          ))}
        </CardContent>
      </Card>

      <WithdrawCard />
    </div>
  );
}

function WithdrawCard() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");

  const withdraw = useMutation({
    mutationFn: () =>
      api<void>("/account/withdraw", {
        method: "POST",
      }),
    onSuccess: () => {
      toast.success(
        "탈퇴 신청이 접수됐어요. 60일 보존 후 데이터가 삭제됩니다.",
      );
      clearAuth();
      router.replace("/creator/login");
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error(err.message || "탈퇴할 수 없어요.");
        return;
      }
      toast.error("탈퇴 신청 중 오류");
    },
  });

  const canSubmit = confirmText === "탈퇴 확인" && !withdraw.isPending;

  return (
    <Card className="border-destructive/40">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base text-destructive">
          <Trash2 className="size-4" /> 회원 탈퇴
        </CardTitle>
        <CardDescription>
          활성 캠페인이 있거나 미정산 잔액이 있으면 탈퇴할 수 없어요. 탈퇴 후 60일
          보존 기간이 지나면 데이터가 삭제됩니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Dialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            if (!next) setConfirmText("");
          }}
        >
          <DialogTrigger
            render={<Button variant="destructive">회원 탈퇴</Button>}
          />
          <DialogContent>
            <DialogHeader>
              <DialogTitle>회원 탈퇴</DialogTitle>
              <DialogDescription>
                탈퇴를 진행하면 활성 캠페인·미정산 잔액 검사가 이루어집니다.
                계속하려면 아래 입력란에{" "}
                <span className="font-medium text-foreground">탈퇴 확인</span>
                을 정확히 입력해 주세요.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-2">
              <Label htmlFor="withdraw-confirm">확인 문구</Label>
              <Input
                id="withdraw-confirm"
                value={confirmText}
                onChange={(e) => setConfirmText(e.target.value)}
                placeholder="탈퇴 확인"
                autoComplete="off"
              />
            </div>
            <DialogFooter>
              <DialogClose
                render={<Button variant="outline">취소</Button>}
              />
              <Button
                variant="destructive"
                disabled={!canSubmit}
                onClick={() => withdraw.mutate()}
              >
                {withdraw.isPending ? "처리 중…" : "탈퇴 신청"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </CardContent>
    </Card>
  );
}
