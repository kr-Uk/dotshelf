"use client";

/* eslint-disable react-hooks/set-state-in-effect --
   다이얼로그 form은 `open` prop 변화에 따라 외부 입력으로 초기화되는 게
   정당한 useEffect 사용. 매번 컴포넌트를 unmount/remount 시키면 부모에서
   key 관리가 번거로워지므로 의도적으로 룰을 비활성. */

import { useEffect, useState, type ReactNode } from "react";

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

export type ReasonOption<T extends string = string> = {
  value: T;
  label: string;
};

export type ReasonPayload<T extends string = string> = {
  reason: T;
  comment: string;
};

/**
 * 정형 사유(드롭다운) + 자유 코멘트(필요 시 코멘트 필수)를 받는 범용 다이얼로그.
 * 페이지 잔액 조정처럼 "사유 + 추가 폼" 이 필요한 경우 `extraField` slot 활용.
 */
export function ReasonDialog<T extends string>({
  open,
  onOpenChange,
  title,
  description,
  reasons,
  defaultReason,
  submitLabel = "확인",
  submitVariant = "default",
  commentRequired = false,
  extraField,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: ReactNode;
  reasons: ReasonOption<T>[];
  defaultReason?: T;
  submitLabel?: string;
  submitVariant?: "default" | "destructive";
  commentRequired?: boolean;
  extraField?: ReactNode;
  onSubmit: (payload: ReasonPayload<T>) => void | Promise<void>;
}) {
  const [reason, setReason] = useState<T>(defaultReason ?? reasons[0]!.value);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);

  // 다이얼로그가 열릴 때마다 form 초기화 — open prop 외부 변화에 반응해야 함.
  useEffect(() => {
    if (open) {
      setReason(defaultReason ?? reasons[0]!.value);
      setComment("");
    }
  }, [open, defaultReason, reasons]);

  const canSubmit = !busy && (!commentRequired || comment.trim().length > 0);

  const submit = async () => {
    if (!canSubmit) return;
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
          {description ? <DialogDescription>{description}</DialogDescription> : null}
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="reason-select">정형 사유</Label>
            <Select
              value={reason}
              onValueChange={(v) => setReason((v ?? reasons[0]!.value) as T)}
            >
              <SelectTrigger id="reason-select" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {reasons.map((r) => (
                  <SelectItem key={r.value} value={r.value}>
                    {r.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {extraField}

          <div className="grid gap-1.5">
            <Label htmlFor="reason-comment">
              자유 코멘트 {commentRequired ? "(필수)" : "(선택)"}
            </Label>
            <Textarea
              id="reason-comment"
              placeholder="감사 로그에 사유로 함께 남습니다."
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows={4}
              aria-invalid={commentRequired && comment.trim().length === 0}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
            취소
          </Button>
          <Button onClick={submit} disabled={!canSubmit} variant={submitVariant}>
            {busy ? "처리 중…" : submitLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
