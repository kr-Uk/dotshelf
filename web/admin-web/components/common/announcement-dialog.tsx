"use client";

/* eslint-disable react-hooks/set-state-in-effect --
   다이얼로그 form은 `open` prop 변화에 따라 외부 입력으로 초기화됨. */

import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
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
import { Textarea } from "@/components/ui/textarea";

const AUDIENCE_OPTIONS = [
  { value: "all_users", label: "사용자 앱 전체" },
  { value: "creators", label: "크리에이터" },
  { value: "internal", label: "사내 (관리자)" },
] as const;

export type AnnouncementPayload = {
  title: string;
  body: string;
  audience: (typeof AUDIENCE_OPTIONS)[number]["value"];
  startsAt: string;
  endsAt: string;
  isUrgent: boolean;
};

function todayIso(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}

export function AnnouncementDialog({
  open,
  onOpenChange,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (payload: AnnouncementPayload) => void | Promise<void>;
}) {
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [audience, setAudience] =
    useState<AnnouncementPayload["audience"]>("all_users");
  const [startsAt, setStartsAt] = useState(todayIso());
  const [endsAt, setEndsAt] = useState(todayIso(7));
  const [isUrgent, setIsUrgent] = useState(false);
  const [busy, setBusy] = useState(false);

  // 다이얼로그가 열릴 때마다 form 초기화 — open prop 외부 변화에 반응해야 함.
  useEffect(() => {
    if (open) {
      setTitle("");
      setBody("");
      setAudience("all_users");
      setStartsAt(todayIso());
      setEndsAt(todayIso(7));
      setIsUrgent(false);
    }
  }, [open]);

  const canSubmit = !busy && title.trim().length > 0 && body.trim().length > 0;

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    try {
      await onSubmit({
        title: title.trim(),
        body: body.trim(),
        audience,
        startsAt,
        endsAt,
        isUrgent,
      });
      onOpenChange(false);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>새 공지 작성</DialogTitle>
          <DialogDescription>
            긴급 공지는 인앱 풀스크린 모달로 표시됩니다. 발행은 감사 로그에 기록.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="ann-title">제목</Label>
            <Input
              id="ann-title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="예: 5/2(토) 03:00~04:00 정기 점검 안내"
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ann-body">본문</Label>
            <Textarea
              id="ann-body"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              rows={5}
              placeholder="공지 본문을 입력하세요. Markdown은 v1.0 미지원."
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ann-audience">대상 세그먼트</Label>
            <Select
              value={audience}
              onValueChange={(v) =>
                setAudience((v ?? "all_users") as AnnouncementPayload["audience"])
              }
            >
              <SelectTrigger id="ann-audience" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {AUDIENCE_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="ann-start">노출 시작</Label>
              <Input
                id="ann-start"
                type="date"
                value={startsAt}
                onChange={(e) => setStartsAt(e.target.value)}
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="ann-end">노출 종료</Label>
              <Input
                id="ann-end"
                type="date"
                value={endsAt}
                onChange={(e) => setEndsAt(e.target.value)}
              />
            </div>
          </div>
          <Label className="cursor-pointer">
            <Checkbox
              checked={isUrgent}
              onCheckedChange={(c) => setIsUrgent(c === true)}
            />
            긴급 공지 (인앱 풀스크린 모달)
          </Label>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
            취소
          </Button>
          <Button onClick={submit} disabled={!canSubmit}>
            {busy ? "발행 중…" : "발행"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
