import Link from "next/link";

import { Icon } from "@/components/marketing/icon";

const RELEASE = {
  version: "0.1.2",
  date: "2026.05.11",
  channel: "Beta",
};

const HIGHLIGHTS = [
  {
    icon: "sparkle" as const,
    title: "룸 외부 풍경 (Exterior)",
    body: "책장 너머로 펼쳐지는 풍경이 추가됐어요. 기본 테마는 잔디 + 파스텔 톤 하늘로, 룸 안과 자연스럽게 이어집니다.",
  },
  {
    icon: "sparkle" as const,
    title: "노트북 사용성 개선",
    body: "노트북 화면 UI 호출/종료 동선을 정리하고, 다른 인터랙션과의 입력 충돌을 줄였습니다.",
  },
  {
    icon: "sparkle" as const,
    title: "데코레이션·자산 시스템 안정화",
    body: "자산 다운로드 메타데이터를 확장해 신규 콘텐츠 추가 시 호환성이 더 단단해졌습니다.",
  },
];

const SMALL_FIXES = [
  "방 기본 테마 자산 (벽지·바닥) 정리",
  "편집 모드 가구 배치·이동 검증 안정화",
  "썸네일 캡처 톤 보정",
];

export default function Changelog012Page() {
  return (
    <div className="page-fade pb-16">
      <Hero />
      <Highlights />
      <SmallFixes />
      <CtaRow />
    </div>
  );
}

function Hero() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 pt-14 pb-10">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-4">
        ── 업데이트
      </div>
      <h1 className="serif text-[40px] sm:text-[52px] leading-[1.05] text-charcoal">
        {RELEASE.version} 업데이트.
        <br />룸이 한 칸 더 넓어졌어요.
      </h1>
      <p className="mt-5 text-[16px] leading-[1.7] text-charcoal/75 max-w-xl">
        창 너머의 풍경, 노트북 UI 정돈, 자산 시스템 보강 — 책장 주변의
        디테일을 더 깊이 다듬은 베타 릴리스입니다.
      </p>
      <div className="mt-7 flex flex-wrap gap-3">
        <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-beige bg-cream/70">
          <Icon name="sparkle" size={14} className="text-wood" />
          <span className="mono text-[12px] tracking-wide text-charcoal">
            v{RELEASE.version} · {RELEASE.channel}
          </span>
        </span>
        <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-beige bg-cream/70">
          <span className="mono text-[12px] tracking-wide text-charcoal">
            {RELEASE.date}
          </span>
        </span>
      </div>
    </section>
  );
}

function Highlights() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-12">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 이번 빌드의 변화
      </div>
      <h2 className="serif text-[30px] sm:text-[38px] text-charcoal mb-10">
        주요 변경 3가지.
      </h2>
      <div className="grid md:grid-cols-3 gap-5">
        {HIGHLIGHTS.map((h) => (
          <div
            key={h.title}
            className="rounded-2xl border border-beige bg-cream/70 p-6 shadow-warm-sm"
          >
            <div className="w-10 h-10 rounded-xl bg-beige-soft border border-beige flex items-center justify-center text-wood mb-4">
              <Icon name={h.icon} size={18} />
            </div>
            <h3 className="serif text-[20px] text-charcoal">{h.title}</h3>
            <p className="text-[14px] text-charcoal/75 leading-[1.7] mt-3">
              {h.body}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}

function SmallFixes() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-12">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 잔잔한 개선
      </div>
      <div className="rounded-2xl border border-beige bg-cream/40 p-6">
        <ul className="space-y-3">
          {SMALL_FIXES.map((f) => (
            <li
              key={f}
              className="flex items-start gap-3 text-[14px] text-charcoal/85 leading-[1.7]"
            >
              <Icon
                name="check"
                size={14}
                className="text-wood mt-1 flex-shrink-0"
              />
              <span>{f}</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

function CtaRow() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-10">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          href="/download"
          className="btn-primary inline-flex items-center gap-2 px-5 py-2.5 rounded-full text-sm font-medium"
        >
          <Icon name="download" size={15} />최신 런처 받기
        </Link>
        <Link
          href="/contact"
          className="inline-flex items-center gap-2 px-5 py-2.5 rounded-full text-sm font-medium text-charcoal border border-beige hover:border-wood/60 transition"
        >
          버그·피드백
          <Icon name="arrow-right" size={14} />
        </Link>
      </div>
    </section>
  );
}
