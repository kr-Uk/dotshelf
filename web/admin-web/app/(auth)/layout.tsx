import Link from "next/link";
import { ShieldCheck } from "lucide-react";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="grid min-h-screen grid-cols-1 lg:grid-cols-[minmax(0,1fr)_minmax(420px,520px)]">
      {/* 좌측 브랜드 영역 */}
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-[oklch(0.18_0.02_240)] via-[oklch(0.22_0.04_250)] to-[oklch(0.28_0.07_270)] lg:flex lg:flex-col lg:justify-between lg:p-12 text-white">
        <Link href="/" className="flex items-center gap-2">
          <span className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-white/15 text-base font-semibold backdrop-blur">
            D
          </span>
          <span className="text-lg font-semibold tracking-tight">dotshelf</span>
          <span className="ml-1 rounded-full bg-white/15 px-1.5 py-0.5 text-[10px] font-medium">
            어드민
          </span>
        </Link>

        <div className="max-w-md space-y-4">
          <h1 className="text-3xl font-semibold leading-tight tracking-tight">
            운영팀 전용
            <br />
            백오피스에 접속하세요.
          </h1>
          <p className="text-sm text-white/70">
            콘텐츠 심사 / 신고 모더레이션 / 가입·정산을 한 화면에서 처리합니다.
            모든 쓰기 액션은 감사 로그에 누가·언제·무엇을 기록됩니다.
          </p>
        </div>

        <div className="grid gap-2 text-xs text-white/60">
          <div className="flex items-center gap-2">
            <ShieldCheck className="size-3.5" /> 사내 SSO + 강제 MFA + IP 화이트리스트
          </div>
          <div>v1.0 · 비활성 30분 자동 로그아웃 · 5회 MFA 실패 시 잠금</div>
        </div>
      </div>

      <div className="flex items-center justify-center bg-background px-6 py-10">
        <div className="w-full max-w-md">{children}</div>
      </div>
    </div>
  );
}
