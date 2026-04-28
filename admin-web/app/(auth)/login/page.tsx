"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { ShieldAlert } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { api, ApiError } from "@/lib/api";
import { savePendingMfa } from "@/lib/auth";

interface SsoStartResponse {
  challengeId: string;
  email: string;
  ipAllowlisted: boolean;
}

export default function LoginPage() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);

  async function handleSso() {
    if (busy) return;
    setBusy(true);
    try {
      const res = await api<SsoStartResponse>("/auth/sso/start", {
        method: "POST",
      });
      if (!res.ipAllowlisted) {
        toast.error("사무실/VPN IP에서만 접속할 수 있어요. 슈퍼 관리자에게 알림이 발송됐습니다.");
        return;
      }
      savePendingMfa({ challengeId: res.challengeId, email: res.email });
      toast.success("사내 IdP 인증 완료. MFA를 진행하세요.");
      router.push("/mfa");
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "SSO 인증에 실패했어요.";
      toast.error(message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1.5">
        <h2 className="text-2xl font-semibold tracking-tight">관리자 로그인</h2>
        <p className="text-sm text-muted-foreground">
          사내 SSO로 로그인 후 강제 MFA(TOTP)를 입력하세요.
        </p>
      </div>

      <div className="space-y-3">
        <Button
          size="lg"
          className="w-full"
          onClick={handleSso}
          disabled={busy}
        >
          {busy ? "사내 IdP로 이동 중…" : "사내 SSO로 로그인"}
        </Button>

        <p className="text-center text-xs text-muted-foreground">
          이메일/비밀번호 직접 로그인은 지원하지 않습니다.
        </p>
      </div>

      <Alert variant="destructive">
        <ShieldAlert />
        <AlertTitle>접근 제한</AlertTitle>
        <AlertDescription>
          사무실 또는 회사 VPN IP에서만 접속할 수 있습니다. 외부 IP에서 시도하면
          슈퍼 관리자에게 알림이 발송됩니다.
        </AlertDescription>
      </Alert>

      <p className="rounded-md bg-muted/50 px-3 py-2 text-center text-xs text-muted-foreground">
        Mock 모드 — SSO 버튼을 누르면 가짜 challenge가 발급됩니다.
      </p>
    </div>
  );
}
