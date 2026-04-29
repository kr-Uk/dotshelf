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
import type { PolicyVersion } from "@/lib/types";

export type PolicyPublishPayload = {
  policyId: string;
  version: string;
  effectiveAt: string;
  changelog: string;
  requiresReConsent: boolean;
};

export function PolicyPublishDialog({
  open,
  onOpenChange,
  policies,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  policies: PolicyVersion[];
  onSubmit: (payload: PolicyPublishPayload) => void | Promise<void>;
}) {
  const [policyId, setPolicyId] = useState(policies[0]?.id ?? "");
  const [version, setVersion] = useState(suggestVersion());
  const [effectiveAt, setEffectiveAt] = useState(todayIso(7));
  const [changelog, setChangelog] = useState("");
  const [requiresReConsent, setRequiresReConsent] = useState(false);
  const [busy, setBusy] = useState(false);

  // 다이얼로그가 열릴 때마다 form 초기화 — open prop 외부 변화에 반응해야 함.
  useEffect(() => {
    if (open) {
      setPolicyId(policies[0]?.id ?? "");
      setVersion(suggestVersion());
      setEffectiveAt(todayIso(7));
      setChangelog("");
      setRequiresReConsent(false);
    }
  }, [open, policies]);

  const canSubmit =
    !busy && policyId.length > 0 && version.trim().length > 0 && changelog.trim().length > 0;

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    try {
      await onSubmit({
        policyId,
        version: version.trim(),
        effectiveAt,
        changelog: changelog.trim(),
        requiresReConsent,
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
          <DialogTitle>약관·정책 새 버전 발행</DialogTitle>
          <DialogDescription>
            「재동의 필요」 체크 시 다음 접속에서 사용자 재동의 플로우가 자동 트리거됩니다.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="pol-id">대상 약관</Label>
            <Select value={policyId} onValueChange={(v) => setPolicyId(v ?? "")}>
              <SelectTrigger id="pol-id" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {policies.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.name} (현재 {p.version})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="pol-version">새 버전</Label>
              <Input
                id="pol-version"
                value={version}
                onChange={(e) => setVersion(e.target.value)}
                placeholder="2026.05.01"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="pol-eff">시행일</Label>
              <Input
                id="pol-eff"
                type="date"
                value={effectiveAt}
                onChange={(e) => setEffectiveAt(e.target.value)}
              />
            </div>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="pol-changelog">변경 요약</Label>
            <Textarea
              id="pol-changelog"
              rows={5}
              value={changelog}
              onChange={(e) => setChangelog(e.target.value)}
              placeholder="이번 개정에서 변경된 핵심 내용을 적어주세요. 사용자 재동의 화면에 그대로 노출됩니다."
            />
          </div>
          <Label className="cursor-pointer">
            <Checkbox
              checked={requiresReConsent}
              onCheckedChange={(c) => setRequiresReConsent(c === true)}
            />
            사용자 재동의 강제 (다음 접속 시 동의 화면 트리거)
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

function suggestVersion(): string {
  const d = new Date();
  return `${d.getFullYear()}.${String(d.getMonth() + 1).padStart(2, "0")}.${String(d.getDate()).padStart(2, "0")}`;
}

function todayIso(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}
