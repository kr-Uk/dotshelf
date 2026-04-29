"use client";

import { useState } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  ReasonDialog,
  type ReasonOption,
  type ReasonPayload,
} from "@/components/common/reason-dialog";
import {
  BALANCE_ADJUST_REASON_LABEL,
  type BalanceAdjustReason,
} from "@/lib/types";

const REASONS: ReasonOption<BalanceAdjustReason>[] = (
  Object.keys(BALANCE_ADJUST_REASON_LABEL) as BalanceAdjustReason[]
).map((v) => ({ value: v, label: BALANCE_ADJUST_REASON_LABEL[v] }));

export type BalanceAdjustPayload = ReasonPayload<BalanceAdjustReason> & {
  pages: number;
};

export function BalanceAdjustDialog({
  open,
  onOpenChange,
  subjectName,
  currentPages,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subjectName: string;
  currentPages: number;
  onSubmit: (payload: BalanceAdjustPayload) => void | Promise<void>;
}) {
  const [pages, setPages] = useState<string>("");

  return (
    <ReasonDialog<BalanceAdjustReason>
      open={open}
      onOpenChange={(v) => {
        if (!v) setPages("");
        onOpenChange(v);
      }}
      title={`${subjectName} 페이지 잔액 조정`}
      description={`현재 잔액 ${currentPages.toLocaleString()}p · 음수면 차감, 양수면 지급. 슈퍼 관리자 + 사유 필수.`}
      reasons={REASONS}
      submitLabel="조정 적용"
      submitVariant="destructive"
      commentRequired
      extraField={
        <div className="grid gap-1.5">
          <Label htmlFor="balance-pages">조정 금액 (페이지)</Label>
          <Input
            id="balance-pages"
            inputMode="numeric"
            type="number"
            placeholder="예: -500 또는 1000"
            value={pages}
            onChange={(e) => setPages(e.target.value)}
          />
          {pages !== "" && !Number.isNaN(Number(pages)) && (
            <p className="text-xs text-muted-foreground">
              조정 후 예상 잔액 {(currentPages + Number(pages)).toLocaleString()}p
            </p>
          )}
        </div>
      }
      onSubmit={async (p) => {
        const n = Number(pages);
        if (!Number.isFinite(n) || n === 0) return;
        await onSubmit({ ...p, pages: n });
        setPages("");
      }}
    />
  );
}
