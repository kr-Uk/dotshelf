import Link from "next/link";

import { MarketingLogo } from "@/components/marketing/logo";

export function MarketingFooter() {
  return (
    <footer className="border-t border-beige/60 bg-[#EFE6D6] mt-24">
      <div className="max-w-6xl mx-auto px-5 sm:px-8 py-14 grid grid-cols-2 md:grid-cols-5 gap-10">
        <div className="col-span-2">
          <MarketingLogo />
          <p className="mt-4 text-sm text-paper-muted max-w-xs leading-relaxed">
            좋아하는 책을 가상의 3D 책장에 모으고, 친구의 책장을 들여다보는
            서비스.
          </p>
          <p className="mt-6 mono text-[11px] text-paper-muted/80">
            © 2026 DotShelf, Inc.
          </p>
        </div>
        <FooterColumn
          title="제품"
          items={[
            { label: "홈", href: "/" },
            { label: "다운로드", href: "/download" },
            { label: "iOS 앱", href: "#", trailing: "App Store" },
            { label: "Android 앱", href: "#", trailing: "Play" },
          ]}
        />
        <FooterColumn
          title="회사"
          items={[
            { label: "소개", href: "#" },
            { label: "블로그", href: "#" },
            { label: "문의", href: "/contact" },
            { label: "채용", href: "#" },
          ]}
        />
        <FooterColumn
          title="법적 고지"
          items={[
            { label: "이용 약관", href: "#" },
            { label: "개인정보 처리방침", href: "#" },
            { label: "쿠키 설정", href: "#" },
          ]}
        />
      </div>
      <div className="border-t border-beige/60">
        <div className="max-w-6xl mx-auto px-5 sm:px-8 py-5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
          <p className="mono text-[11px] text-paper-muted">
            dotshelf.digital · 서울특별시 · contact@dotshelf.digital
          </p>
          <div className="flex items-center gap-4 mono text-[11px] text-paper-muted">
            <span>v1.4.0</span>
            <span>·</span>
            <span>한국어</span>
          </div>
        </div>
      </div>
    </footer>
  );
}

type FooterItem = {
  label: string;
  href: string;
  trailing?: string;
};

function FooterColumn({
  title,
  items,
}: {
  title: string;
  items: FooterItem[];
}) {
  return (
    <div>
      <h4 className="text-[12px] font-semibold tracking-wider mono uppercase text-paper-muted mb-4">
        {title}
      </h4>
      <ul className="space-y-2.5 text-sm">
        {items.map((item) => (
          <li key={`${title}:${item.label}`}>
            <Link
              href={item.href}
              className="hover:text-wood transition flex items-center gap-1.5"
            >
              {item.label}
              {item.trailing && (
                <span className="mono text-[10px] text-paper-muted">
                  {item.trailing}
                </span>
              )}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
