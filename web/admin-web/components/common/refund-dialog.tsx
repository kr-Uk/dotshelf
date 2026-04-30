"use client";

import { useMemo, useState } from "react";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import {
  ReasonDialog,
  type ReasonOption,
  type ReasonPayload,
} from "@/components/common/reason-dialog";
import {
  REFUND_REASON_LABEL,
  USER_PURCHASE_KIND_LABEL,
  type RefundReason,
  type UserPurchase,
} from "@/lib/types";
import { formatDate, formatWon } from "@/lib/format";

const REASONS: ReasonOption<RefundReason>[] = (
  Object.keys(REFUND_REASON_LABEL) as RefundReason[]
).map((v) => ({ value: v, label: REFUND_REASON_LABEL[v] }));

export type RefundPayload = ReasonPayload<RefundReason> & {
  purchaseId: string;
};

export function RefundDialog({
  open,
  onOpenChange,
  subjectName,
  purchases,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subjectName: string;
  purchases: UserPurchase[];
  onSubmit: (payload: RefundPayload) => void | Promise<void>;
}) {
  const refundable = useMemo(() => purchases.filter((p) => !p.refunded), [purchases]);
  const [purchaseId, setPurchaseId] = useState<string>(refundable[0]?.id ?? "");
  const selectedPurchaseId = refundable.some((purchase) => purchase.id === purchaseId)
    ? purchaseId
    : refundable[0]?.id ?? "";

  return (
    <ReasonDialog<RefundReason>
      open={open}
      onOpenChange={onOpenChange}
      title={`${subjectName} 환불 처리`}
      description="스토어 정책상 환불 가능 범위 내에서만 처리합니다. 모든 환불은 감사 로그에 기록."
      reasons={REASONS}
      submitLabel="환불 처리"
      submitVariant="destructive"
      commentRequired
      extraField={
        <div className="grid gap-1.5">
          <Label htmlFor="refund-purchase">환불 대상 결제</Label>
          <Select
            value={selectedPurchaseId}
            onValueChange={(v) => setPurchaseId(v ?? "")}
          >
            <SelectTrigger id="refund-purchase" className="w-full">
              <SelectValue placeholder="결제를 선택하세요" />
            </SelectTrigger>
            <SelectContent>
              {refundable.length === 0 && (
                <SelectItem value="__none" disabled>
                  환불 가능한 결제가 없어요
                </SelectItem>
              )}
              {refundable.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  [{USER_PURCHASE_KIND_LABEL[p.kind]}] {p.itemName} · {formatWon(p.amountWon)} ·{" "}
                  {formatDate(p.purchasedAt)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      }
      onSubmit={async (p) => {
        if (!selectedPurchaseId) return;
        await onSubmit({ ...p, purchaseId: selectedPurchaseId });
      }}
    />
  );
}
