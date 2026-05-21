import Link from "next/link";

import { BookRow, Bookshelf3D } from "@/components/marketing/bookshelf";
import { Icon } from "@/components/marketing/icon";

const VALUE_PROPS = [
  {
    icon: "shelf" as const,
    eyebrow: "01 / Curate",
    title: "내 취향을\n책장으로",
    body: "읽은 책, 읽고 싶은 책, 아끼는 책을 가상의 3D 책장 위에 직접 배치해요. 칸을 나누고, 색을 맞추고, 표지를 바라보며 정돈하는 그 시간이 곧 큐레이션입니다.",
    chips: ["드래그 정렬", "책장 테마", "시즌 컬렉션"],
  },
  {
    icon: "eye" as const,
    eyebrow: "02 / Visit",
    title: "친구의 책장을\n들여다보기",
    body: "친구의 서재를 1인칭으로 거닐며, 책등을 살펴보고 짧은 코멘트를 남겨요. SNS의 피드 대신 한 사람이 만든 ‘공간’을 천천히 둘러보는 새로운 방식입니다.",
    chips: ["1인칭 투어", "방명록", "스포트라이트"],
  },
];

const STATS: ReadonlyArray<readonly [string, string]> = [
  ["12,400+", "큐레이션된 서재"],
  ["180만 권", "등록된 책"],
  ["37개국", "독자가 머무는 곳"],
  ["4.8 / 5.0", "이용자 만족도"],
];

const GALLERY = [
  {
    span: "col-span-12 md:col-span-7",
    ratio: "16 / 10",
    label: "3D 서재 — 메인 화면",
    tag: "macOS · Windows",
    big: true,
  },
  {
    span: "col-span-6 md:col-span-5",
    ratio: "4 / 5",
    label: "책장 큐레이션",
    tag: "Desktop",
    big: false,
  },
  {
    span: "col-span-6 md:col-span-5",
    ratio: "4 / 5",
    label: "친구 방문하기",
    tag: "Desktop",
    big: false,
  },
  {
    span: "col-span-12 md:col-span-7",
    ratio: "16 / 10",
    label: "데스크탑 — 책 임포트 & 정렬",
    tag: "macOS · Windows",
    big: true,
  },
];

export default function HomePage() {
  return (
    <div className="page-fade">
      <Hero />
      <Stats />
      <ValuesSection />
      <BookRow />
      <Gallery />
      <CallToAction />
    </div>
  );
}

function Hero() {
  return (
    <section className="relative overflow-hidden">
      <div className="max-w-6xl mx-auto px-5 sm:px-8 pt-14 pb-20 sm:pt-20 sm:pb-28 grid lg:grid-cols-12 gap-12 items-center">
        <div className="lg:col-span-7 relative z-10">
          <div className="inline-flex items-center gap-2 mono text-[11px] tracking-widest uppercase text-wood mb-6">
            <span className="w-6 h-px bg-wood" />
            DotShelf · 1.4 · Spring Update
          </div>
          <h1 className="serif font-light text-[42px] sm:text-[58px] lg:text-[68px] leading-[1.05] text-charcoal tracking-tight">
            좋아하는 책을
            <br />
            <span className="font-medium text-wood">가상의 책장</span>에
            <br />
            천천히 모으기.
          </h1>
          <p className="mt-7 text-[17px] sm:text-[18px] leading-[1.7] text-charcoal/80 max-w-lg">
            DotShelf는 당신의 독서 기록을 리스트가 아닌 하나의 서재로 만들어요.
            데스크탑에서 책장을 천천히 정돈하고, 친구의 서재를 마치 방문하듯
            둘러보세요.
          </p>
          <div className="mt-9 flex flex-col sm:flex-row gap-3">
            <Link
              href="/download"
              className="btn-primary inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-full text-[15px] font-medium"
            >
              <Icon name="download" size={17} />
              데스크탑 앱 다운로드
            </Link>
            <a
              href="#gallery"
              className="btn-secondary inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-full text-[15px] font-medium"
            >
              둘러보기
              <Icon name="arrow-right" size={16} />
            </a>
          </div>
          <div className="mt-9 flex items-center gap-6 text-[13px] text-paper-muted">
            <div className="flex items-center gap-2">
              <Icon name="apple" size={14} className="text-charcoal" />
              <span>macOS 13+</span>
            </div>
            <div className="w-px h-4 bg-beige" />
            <div className="flex items-center gap-2">
              <Icon name="windows" size={12} className="text-charcoal" />
              <span>Windows 10+</span>
            </div>
          </div>
        </div>

        <div className="lg:col-span-5 relative">
          <div className="relative">
            <Bookshelf3D />
          </div>
        </div>
      </div>

      <div
        className="pointer-events-none absolute top-20 right-0 w-[500px] h-[500px] rounded-full opacity-50"
        style={{
          background:
            "radial-gradient(circle, rgba(214,196,173,0.55), transparent 65%)",
        }}
      />
    </section>
  );
}

function Stats() {
  return (
    <section className="border-y border-beige/60 paper-dark">
      <div className="max-w-6xl mx-auto px-5 sm:px-8 py-12 grid grid-cols-2 md:grid-cols-4 gap-y-8 gap-x-6">
        {STATS.map(([num, label], i) => (
          <div
            key={label}
            className={i > 0 ? "md:border-l border-beige/70 md:pl-6" : ""}
          >
            <div className="serif text-[28px] sm:text-[34px] text-charcoal leading-none">
              {num}
            </div>
            <div className="mono text-[11px] uppercase tracking-widest text-paper-muted mt-2.5">
              {label}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function ValuesSection() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-24 sm:py-28">
      <div className="grid md:grid-cols-12 gap-8 items-end mb-14">
        <div className="md:col-span-7">
          <div className="mono text-[11px] tracking-widest uppercase text-wood mb-4">
            ── 핵심 가치
          </div>
          <h2 className="serif text-[36px] sm:text-[46px] leading-[1.1] text-charcoal">
            리스트가 아닌, <span className="italic">공간</span>으로 기록하는
            <br />
            새로운 독서 경험.
          </h2>
        </div>
        <div className="md:col-span-5 md:pl-8">
          <p className="text-[15px] leading-[1.7] text-charcoal/75">
            저희는 책 한 권의 무게, 책장에 꽂힌 모습, 햇살에 비친 표지의 색까지
            디지털로 옮기고 싶었어요. DotShelf는 그 작은 의식을 위한 도구입니다.
          </p>
        </div>
      </div>
      <div className="grid md:grid-cols-2 gap-6">
        {VALUE_PROPS.map((v) => (
          <article
            key={v.eyebrow}
            className="paper-dark rounded-3xl p-7 sm:p-8 border border-beige/70 shadow-warm-sm relative overflow-hidden h-full flex flex-col"
          >
            <div
              className="absolute -top-6 -right-6 w-24 h-24 rounded-full"
              style={{
                background:
                  "radial-gradient(circle, rgba(163,128,105,0.16), transparent 70%)",
              }}
            />
            <div className="flex items-center gap-3 mb-6">
              <div className="w-11 h-11 rounded-xl bg-cream border border-beige flex items-center justify-center text-wood">
                <Icon name={v.icon} size={22} strokeWidth={1.5} />
              </div>
              <span className="mono text-[11px] tracking-widest uppercase text-paper-muted">
                {v.eyebrow}
              </span>
            </div>
            <h3 className="serif text-[28px] sm:text-[30px] leading-[1.15] text-charcoal whitespace-pre-line">
              {v.title}
            </h3>
            <p className="mt-4 text-[15px] leading-[1.7] text-charcoal/80 flex-1">
              {v.body}
            </p>
            <div className="mt-6 flex flex-wrap gap-2">
              {v.chips.map((c) => (
                <span
                  key={c}
                  className="text-[12px] px-3 py-1 rounded-full bg-cream border border-beige text-charcoal/80"
                >
                  {c}
                </span>
              ))}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function Gallery() {
  return (
    <section
      id="gallery"
      className="max-w-6xl mx-auto px-5 sm:px-8 pb-24 scroll-mt-24"
    >
      <div className="flex items-end justify-between mb-10 flex-wrap gap-4">
        <div>
          <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
            ── 앱 둘러보기
          </div>
          <h2 className="serif text-[34px] sm:text-[42px] text-charcoal leading-[1.1]">
            데스크탑에서 만나는 책장.
          </h2>
        </div>
        <Link
          href="/download"
          className="text-sm text-wood hover:text-wood-dark inline-flex items-center gap-1.5 ink-link"
        >
          데스크탑 앱 받기
          <Icon name="arrow-right" size={14} />
        </Link>
      </div>

      <div className="grid grid-cols-12 gap-4 sm:gap-5">
        {GALLERY.map((item) => (
          <div key={item.label} className={item.span}>
            <div
              className="relative rounded-2xl overflow-hidden border border-beige shadow-warm group"
              style={{ aspectRatio: item.ratio }}
            >
              <div className="ph-stripes absolute inset-0" />
              <div className="absolute inset-0 flex flex-col justify-between p-5">
                <div className="flex items-center justify-between">
                  <span className="mono text-[10px] uppercase tracking-widest text-charcoal/70 bg-cream/80 px-2.5 py-1 rounded-full border border-beige">
                    {item.tag}
                  </span>
                  <span className="mono text-[10px] text-charcoal/50">
                    PLACEHOLDER
                  </span>
                </div>
                <div>
                  <div
                    className={`serif ${
                      item.big ? "text-[28px]" : "text-[18px]"
                    } text-charcoal leading-tight`}
                  >
                    {item.label}
                  </div>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function CallToAction() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 pb-24">
      <div className="rounded-3xl border border-beige overflow-hidden bg-charcoal text-cream relative shadow-warm-lg">
        <div
          className="absolute inset-0 opacity-30 pointer-events-none"
          style={{
            background:
              "radial-gradient(circle at 80% 20%, rgba(214,196,173,0.5), transparent 50%), radial-gradient(circle at 10% 80%, rgba(163,128,105,0.4), transparent 50%)",
          }}
        />
        <div className="relative grid md:grid-cols-12 gap-8 p-10 sm:p-14 items-center">
          <div className="md:col-span-7">
            <div className="mono text-[11px] tracking-widest uppercase text-beige/80 mb-4">
              ── 오늘의 한 권
            </div>
            <h2 className="serif text-[34px] sm:text-[44px] leading-[1.1] text-cream">
              책장을 짓는 일은,
              <br />
              <span className="italic text-beige">
                자기 자신을 짓는 일
              </span>
              이기도 해요.
            </h2>
            <p className="mt-5 text-cream/75 text-[15px] leading-[1.7] max-w-md">
              지금 가입하고 첫 번째 책장을 만들어보세요. 30초면 충분합니다.
            </p>
          </div>
          <div className="md:col-span-5 flex flex-col gap-3">
            <Link
              href="/download"
              className="inline-flex items-center justify-center gap-2 px-6 py-4 rounded-full text-[15px] font-medium bg-cream text-charcoal hover:bg-beige transition"
            >
              <Icon name="download" size={17} />
              데스크탑 앱 다운로드
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
