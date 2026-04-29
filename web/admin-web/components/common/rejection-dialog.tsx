"use client";

/* eslint-disable react-hooks/set-state-in-effect --
   다이얼로그 form은 `open` prop 변화에 따라 외부 입력으로 초기화됨. */

import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import {
  REJECTION_REASON_LABEL,
  type RejectionReason,
} from "@/lib/types";

export type RejectionPayload = {
  reason: RejectionReason;
  comment: string;
};

export function RejectionDialog({
  open,
  onOpenChange,
  title = "반려 사유 입력",
  description = "정형 사유 + 자유 코멘트로 신청자에게 안내됩니다.",
  reasons,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  description?: string;
  reasons: RejectionReason[];
  onSubmit: (payload: RejectionPayload) => void | Promise<void>;
}) {
  const [reason, setReason] = useState<RejectionReason>(reasons[0]);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);

  // 다이얼로그가 열릴 때마다 form 초기화 — open prop 외부 변화에 반응해야 함.
  useEffect(() => {
    if (open) {
      setReason(reasons[0]);
      setComment("");
    }
  }, [open, reasons]);

  const submit = async () => {
    setBusy(true);
    try {
      await onSubmit({ reason, comment: comment.trim() });
      onOpenChange(false);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="rejection-reason">정형 사유</Label>
            <Select
              value={reason}
              onValueChange={(v) => setReason(v as RejectionReason)}
            >
              <SelectTrigger id="rejection-reason" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {reasons.map((r) => (
                  <SelectItem key={r} value={r}>
                    {REJECTION_REASON_LABEL[r]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="rejection-comment">자유 코멘트 (선택)</Label>
            <Textarea
              id="rejection-comment"
              placeholder="재신청에 도움이 될 가이드를 적어주세요."
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows={4}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
            취소
          </Button>
          <Button onClick={submit} disabled={busy} variant="destructive">
            {busy ? "처리 중…" : "반려"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
