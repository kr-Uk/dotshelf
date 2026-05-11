import Link from "next/link";

import { Icon } from "@/components/marketing/icon";

const PERIOD = {
  from: "2026.04.15",
  to: "2026.06.30",
};

// 한정 테마 실제 자산 + 인벤토리 지급 path 가 backend 에 아직 없는 상태라 카드는
// placeholder 단일 톤으로 노출. 실 테마 ready 후 별도 PR 에서 카드 내용 갱신.
const THEMES = [
  { sub: "Theme 01" },
  { sub: "Theme 02" },
  { sub: "Theme 03" },
];

const STEPS = [
  {
    n: "01",
    title: "DotShelf 런처 설치",
    body: "데스크탑 베타에서 챌린지 안내를 받으려면 먼저 런처를 설치해 두세요.",
  },
  {
    n: "02",
    title: "참여 방식 안내 대기",
    body: "참여 기준과 카운트 방식은 이벤트 시작 시 이 페이지에서 별도 안내됩니다.",
  },
  {
    n: "03",
    title: "한정 테마 안내 예정",
    body: "지급 일정·방식은 이벤트 진행 상황에 따라 이 페이지에서 안내됩니다.",
  },
];

export default function Spring2026EventPage() {
  return (
    <div className="page-fade pb-16">
      <Hero />
      <Themes />
      <Steps />
      <FinePrint />
    </div>
  );
}

function Hero() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 pt-14 pb-10">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-4">
        ── 이벤트
      </div>
      <h1 className="serif text-[40px] sm:text-[52px] leading-[1.05] text-charcoal">
        봄맞이 책장
        <br />
        꾸미기 챌린지.
      </h1>
      <p className="mt-5 text-[16px] leading-[1.7] text-charcoal/75 max-w-xl">
        데스크탑에서 책장을 한 뼘씩 더 다듬어 보세요. 기간 한정{" "}
        <strong className="text-charcoal">봄 테마 3종</strong> 지급 일정·방식은
        이벤트 시작 시 이 페이지에서 별도 안내됩니다.
      </p>
      <div className="mt-7 inline-flex items-center gap-2 px-4 py-2 rounded-full border border-beige bg-cream/70">
        <Icon name="sparkle" size={14} className="text-wood" />
        <span className="mono text-[12px] tracking-wide text-charcoal">
          {PERIOD.from} — {PERIOD.to}
        </span>
      </div>
    </section>
  );
}

function Themes() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-12">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 지급 테마
      </div>
      <h2 className="serif text-[30px] sm:text-[38px] text-charcoal mb-10">
        곧 공개될 한정 테마.
      </h2>
      <div className="grid md:grid-cols-3 gap-5">
        {THEMES.map((t) => (
          <div
            key={t.sub}
            className="rounded-2xl border border-beige bg-cream/70 p-6 shadow-warm-sm"
          >
            <div className="w-full aspect-[4/3] rounded-xl bg-beige-soft/60 border border-beige mb-5 flex items-center justify-center text-paper-muted">
              <span className="mono text-[11px] uppercase tracking-widest">
                preview soon
              </span>
            </div>
            <div className="mono text-[11px] text-paper-muted">{t.sub}</div>
            <div className="serif text-[18px] text-charcoal/60 mt-2">
              안내 예정
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function Steps() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-12">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 참여 방법
      </div>
      <h2 className="serif text-[30px] sm:text-[38px] text-charcoal mb-10">
        3단계면 충분해요.
      </h2>
      <div className="grid md:grid-cols-3 gap-5">
        {STEPS.map((s) => (
          <div
            key={s.n}
            className="rounded-2xl bg-cream border border-beige p-6 shadow-warm-sm"
          >
            <div className="flex items-baseline justify-between mb-5">
              <span className="serif text-[40px] text-wood leading-none">
                {s.n}
              </span>
              <span className="mono text-[10px] uppercase tracking-widest text-paper-muted">
                Step
              </span>
            </div>
            <h3 className="serif text-[20px] text-charcoal">{s.title}</h3>
            <p className="text-[14px] text-charcoal/75 leading-[1.7] mt-3">
              {s.body}
            </p>
          </div>
        ))}
      </div>
      <div className="mt-10 flex flex-wrap items-center gap-3">
        <Link
          href="/download"
          className="btn-primary inline-flex items-center gap-2 px-5 py-2.5 rounded-full text-sm font-medium"
        >
          <Icon name="download" size={15} />런처 받기
        </Link>
        <Link
          href="/contact"
          className="inline-flex items-center gap-2 px-5 py-2.5 rounded-full text-sm font-medium text-charcoal border border-beige hover:border-wood/60 transition"
        >
          문의하기
          <Icon name="arrow-right" size={14} />
        </Link>
      </div>
    </section>
  );
}

function FinePrint() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-10">
      <div className="rounded-2xl border border-dashed border-beige bg-cream/40 p-6 text-[13px] text-paper-muted leading-[1.8]">
        <div className="mono text-[10px] uppercase tracking-widest text-wood mb-2">
          ── 안내
        </div>
        <p>
          본 이벤트는 베타 기간 한정으로 진행됩니다. 지급 테마는 사전 안내 없이
          변경될 수 있으며, 자세한 참여 약관과 한정 테마 미리보기는 곧 이
          페이지에서 안내됩니다. 문의는 <Link
            href="/contact"
            className="text-wood underline underline-offset-2 hover:text-wood-dark"
          >문의 페이지</Link>를 이용해 주세요.
        </p>
      </div>
    </section>
  );
}
