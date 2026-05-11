"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { Icon } from "@/components/marketing/icon";
import { MarketingLogo } from "@/components/marketing/logo";
import { LAUNCHER_WIN_URL } from "@/lib/launcher-download";

const NAV_LINKS = [
  { href: "/", label: "홈" },
  { href: "/download", label: "다운로드" },
  { href: "/contact", label: "문의" },
] as const;

function NavLink({
  href,
  label,
  onNavigate,
}: {
  href: string;
  label: string;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();
  const active = href === "/" ? pathname === "/" : pathname.startsWith(href);
  return (
    <Link
      href={href}
      onClick={onNavigate}
      aria-current={active ? "page" : undefined}
      className={`relative px-1 py-1 text-[15px] transition ${
        active ? "text-charcoal" : "text-paper-muted hover:text-charcoal"
      }`}
    >
      {label}
      {active && (
        <span className="absolute left-0 right-0 -bottom-1 h-[2px] bg-wood rounded-full" />
      )}
    </Link>
  );
}

export function MarketingHeader() {
  const [open, setOpen] = useState(false);
  const close = () => setOpen(false);
  // /download 페이지에서만 same-path navigate 무반응 회피를 위해 .exe 직접 hit.
  // 다른 페이지(/, /contact 등) 에서는 <Link> 유지 — Mac/모바일 사용자가 OS 선택
  // 화면(/download) 으로 진입할 수 있게 보호. 초기 PR #148 가 모든 페이지에서 .exe
  // 받기로 했다가 Mac/모바일 회귀 리뷰로 분기 추가.
  const pathname = usePathname();
  const onDownloadPage = pathname === "/download";

  return (
    <header className="sticky top-0 z-40 backdrop-blur-md bg-cream/80 border-b border-beige/60">
      <div className="max-w-6xl mx-auto px-5 sm:px-8 h-16 flex items-center justify-between">
        <MarketingLogo />
        <nav className="hidden md:flex items-center gap-7">
          {NAV_LINKS.map((link) => (
            <NavLink key={link.href} href={link.href} label={link.label} />
          ))}
          <Link
            href="/creator"
            className="text-[15px] text-paper-muted hover:text-charcoal transition flex items-center gap-1.5"
            title="별도 크리에이터 콘솔로 이동"
          >
            크리에이터
            <Icon name="external" size={11} strokeWidth={2} />
          </Link>
        </nav>
        <div className="hidden md:flex items-center gap-3">
          {onDownloadPage && LAUNCHER_WIN_URL ? (
            <a
              href={LAUNCHER_WIN_URL}
              className="btn-primary inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium"
            >
              <Icon name="download" size={15} />데스크탑 앱 다운로드
            </a>
          ) : (
            <Link
              href="/download"
              className="btn-primary inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium"
            >
              <Icon name="download" size={15} />데스크탑 앱 다운로드
            </Link>
          )}
        </div>
        <button
          type="button"
          className="md:hidden text-charcoal p-2 -mr-2"
          onClick={() => setOpen((v) => !v)}
          aria-label={open ? "메뉴 닫기" : "메뉴 열기"}
          aria-expanded={open}
        >
          <Icon name={open ? "close" : "menu"} size={22} />
        </button>
      </div>
      {open && (
        <div className="md:hidden border-t border-beige/60 bg-cream/95">
          <div className="px-5 py-4 flex flex-col gap-3">
            {NAV_LINKS.map((link) => (
              <NavLink
                key={link.href}
                href={link.href}
                label={link.label}
                onNavigate={close}
              />
            ))}
            <Link
              href="/creator"
              onClick={close}
              className="text-[15px] text-paper-muted flex items-center gap-1.5"
            >
              크리에이터
              <Icon name="external" size={11} strokeWidth={2} />
            </Link>
            {onDownloadPage && LAUNCHER_WIN_URL ? (
              <a
                href={LAUNCHER_WIN_URL}
                onClick={close}
                className="btn-primary inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-full text-sm font-medium mt-2"
              >
                <Icon name="download" size={15} />데스크탑 앱 다운로드
              </a>
            ) : (
              <Link
                href="/download"
                onClick={close}
                className="btn-primary inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-full text-sm font-medium mt-2"
              >
                <Icon name="download" size={15} />데스크탑 앱 다운로드
              </Link>
            )}
          </div>
        </div>
      )}
    </header>
  );
}
