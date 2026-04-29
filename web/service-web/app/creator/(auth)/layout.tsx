import Link from "next/link";

import { ApiProviders } from "@/providers/app-providers";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ApiProviders>
    <div className="grid min-h-screen grid-cols-1 lg:grid-cols-[minmax(0,1fr)_minmax(420px,520px)]">
      {/* 좌측 브랜드 영역 */}
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-[oklch(0.18_0.04_260)] via-[oklch(0.22_0.05_265)] to-[oklch(0.32_0.08_280)] lg:flex lg:flex-col lg:justify-between lg:p-12 text-white">
        <Link href="/creator" className="flex items-center gap-2">
          <span className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-white/15 text-base font-semibold backdrop-blur">
            D
          </span>
          <span className="text-lg font-semibold tracking-tight">dotshelf</span>
        </Link>

        <div className="max-w-md space-y-4">
          <h1 className="text-3xl font-semibold leading-tight tracking-tight">
            당신의 책장을
            <br />
            누군가의 풍경으로.
          </h1>
          <p className="text-sm text-white/70">
            출판사 · 작가 · 꾸미기 디자이너를 위한 PC 백오피스. 책과 에셋을 등록하고,
            광고를 운용하고, 매주 화요일에 정산받으세요.
          </p>
        </div>

        <div className="text-xs text-white/60">
          v1.0 · 정산은 매주 화요일 자동 입금 · 최소 정산 5만 원
        </div>
      </div>

      {/* 우측 폼 영역 */}
      <div className="flex items-center justify-center bg-background px-6 py-10">
        <div className="w-full max-w-md">{children}</div>
      </div>
    </div>
    </ApiProviders>
  );
}
