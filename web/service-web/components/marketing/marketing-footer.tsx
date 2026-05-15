import Link from "next/link";

import { MarketingLogo } from "@/components/marketing/logo";
import {
  getMerchantInfo,
  getSiteHost,
  isMerchantInfoComplete,
} from "@/lib/merchant-info";

export function MarketingFooter() {
  const merchant = getMerchantInfo();
  const siteHost = getSiteHost();
  // partial set 시 일관성 없는 한 줄 (예: "DotShelf · 대표 김지호") 노출 방지.
  // 핵심 4개 (상호 / 사업자등록번호 / 통신판매업 / 주소) 모두 set 시에만 표시.
  // KG 사전 심사 통과 + dev 환경 (env 미주입) 빌드 호환 양립.
  const businessLine = isMerchantInfoComplete(merchant)
    ? buildBusinessLine(merchant)
    : "";

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
            { label: "이용 약관", href: "/legal/payment-terms" },
            { label: "개인정보 처리방침", href: "/legal/privacy" },
            { label: "환불 규정", href: "/legal/refund-policy" },
            { label: "사업자 정보", href: "/legal/business-info" },
          ]}
        />
      </div>
      <div className="border-t border-beige/60">
        <div className="max-w-6xl mx-auto px-5 sm:px-8 py-5 flex flex-col gap-3">
          {businessLine && (
            <p className="mono text-[11px] text-paper-muted leading-relaxed">
              {businessLine}
            </p>
          )}
          <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2">
            <p className="mono text-[11px] text-paper-muted">
              {siteHost}
              {merchant.email && ` · ${merchant.email}`}
            </p>
            <div className="flex items-center gap-4 mono text-[11px] text-paper-muted">
              <span>v1.4.0</span>
              <span>·</span>
              <span>한국어</span>
            </div>
          </div>
        </div>
      </div>
    </footer>
  );
}

function buildBusinessLine(merchant: ReturnType<typeof getMerchantInfo>): string {
  const parts: string[] = [];
  if (merchant.name) parts.push(merchant.name);
  if (merchant.representative) parts.push(`대표 ${merchant.representative}`);
  if (merchant.registrationNumber)
    parts.push(`사업자등록번호 ${merchant.registrationNumber}`);
  if (merchant.mailOrderRegistrationNumber)
    parts.push(`통신판매업 ${merchant.mailOrderRegistrationNumber}`);
  if (merchant.address) parts.push(merchant.address);
  if (merchant.tel) parts.push(merchant.tel);
  return parts.join(" · ");
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
