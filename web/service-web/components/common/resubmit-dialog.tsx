"use client";

import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { RotateCcw } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";

export interface ResubmitDialogProps<T> {
  /** 트리거 버튼 라벨 (기본: "재신청"). */
  triggerLabel?: string;
  /** 반려 사유 (다이얼로그에 다시 표시). */
  rejectionReason: string | null;
  /** 콘텐츠 유형별 추가 가이드 (다이얼로그 상단 안내). */
  guideTips?: string[];
  /** 변경 사항 메모 외 인라인 수정 필드 (선택). */
  inlineEditor?: (helpers: {
    onChange: (patch: Partial<Record<string, unknown>>) => void;
  }) => ReactNode;
  /** 실제 API 호출. dialog는 결과를 기다린다. */
  resubmit: (input: {
    changeNote: string;
    extra: Record<string, unknown>;
  }) => Promise<T>;
  /** 성공 후 처리 (캐시 갱신 등). */
  onSuccess: (result: T) => void;
}

export function ResubmitDialog<T>({
  triggerLabel = "재신청",
  rejectionReason,
  guideTips,
  inlineEditor,
  resubmit,
  onSuccess,
}: ResubmitDialogProps<T>) {
  const [open, setOpen] = useState(false);
  const [changeNote, setChangeNote] = useState("");
  const [extra, setExtra] = useState<Record<string, unknown>>({});
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    if (changeNote.trim().length < 5) {
      toast.error("변경 사항 메모를 5자 이상 작성해주세요.");
      return;
    }
    setSubmitting(true);
    try {
      const result = await resubmit({ changeNote, extra });
      toast.success("재신청이 접수됐어요.");
      onSuccess(result);
      setOpen(false);
      setChangeNote("");
      setExtra({});
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message || "재신청 중 오류");
      } else {
        toast.error("재신청 중 오류");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button>
            <RotateCcw className="size-4" /> {triggerLabel}
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>재신청</DialogTitle>
          <DialogDescription>
            반려 사유를 확인하고 어떤 부분을 수정했는지 검수자에게 짧게
            알려주세요.
          </DialogDescription>
        </DialogHeader>

        {rejectionReason && (
          <div className="rounded-md bg-destructive/5 px-3 py-2 text-xs text-destructive">
            <strong>반려 사유</strong>
            <p className="mt-1 text-destructive/80">{rejectionReason}</p>
          </div>
        )}

        {guideTips && guideTips.length > 0 && (
          <div className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900">
            <strong>재신청 전 체크</strong>
            <ul className="mt-1 space-y-0.5 leading-relaxed text-amber-900/90">
              {guideTips.map((tip, i) => (
                <li key={i} className="flex gap-1.5">
                  <span aria-hidden>•</span>
                  <span>{tip}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {inlineEditor?.({
          onChange: (patch) =>
            setExtra((prev) => ({ ...prev, ...patch })),
        })}

        <div className="grid gap-2">
          <Label>변경 사항 메모 (필수)</Label>
          <Textarea
            rows={4}
            value={changeNote}
            onChange={(e) => setChangeNote(e.target.value)}
            placeholder="예: 표지 텍스처를 4096px로 재업로드, 가독성 개선을 위해 광고 카피 수정."
          />
          <p className="text-xs text-muted-foreground">
            5자 이상 입력해주세요. 검수자가 빠르게 확인합니다.
          </p>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => setOpen(false)}
            disabled={submitting}
          >
            취소
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={submitting || changeNote.trim().length < 5}
          >
            {submitting ? "보내는 중…" : "재신청 보내기"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
