"use client";

import Image from "next/image";
import { useState } from "react";

import { Icon } from "@/components/marketing/icon";
import { LAUNCHER_WIN_URL } from "@/lib/launcher-download";

const RELEASE = {
  version: "0.2.0",
  date: "2026년 5월 14일",
  size: "런처 약 69 MB · 본체 별도 다운로드",
  channel: "Beta",
};

type Build = {
  label: string;
  sub: string;
  size: string;
  recommended: boolean;
  href: string | null;
};

const DOWNLOADS: Record<"mac" | "win", Build[]> = {
  mac: [],
  win: [
    {
      label: "Windows · DotShelf 런처",
      sub: ".exe · Windows 10/11 (64-bit)",
      size: "약 69 MB (.NET 8 런타임 포함)",
      recommended: true,
      href: LAUNCHER_WIN_URL,
    },
  ],
};

const REQUIREMENTS = [
  {
    os: "macOS",
    icon: "apple" as const,
    rows: [
      ["최소 OS", "macOS 13 Ventura"],
      ["권장 OS", "macOS 15 Sequoia"],
      ["프로세서", "Apple Silicon (권장) / Intel"],
      ["메모리", "8 GB / 16 GB 권장"],
      ["그래픽", "Metal 3 지원"],
      ["저장 공간", "500 MB"],
    ],
  },
  {
    os: "Windows",
    icon: "windows" as const,
    rows: [
      ["최소 OS", "Windows 10 (64-bit)"],
      ["권장 OS", "Windows 11 23H2"],
      ["프로세서", "Intel i5 8세대 / Ryzen 5 3000 이상"],
      ["메모리", "8 GB / 16 GB 권장"],
      ["그래픽", "DirectX 12 / 외장 GPU 권장"],
      ["저장 공간", "500 MB"],
    ],
  },
];

const STEPS = [
  {
    n: "01",
    title: "런처 실행",
    body: "다운로드한 DotShelfLauncher.exe를 실행하면 본체 빌드를 자동으로 받고 설치합니다. 진행률·작업 기록·이벤트 배너가 한 화면에서 보입니다.",
    mono: "DotShelfLauncher.exe",
  },
  {
    n: "02",
    title: "계정으로 로그인",
    body: "DotShelf 계정으로 로그인하세요. 처음이라면 가입까지 30초면 충분합니다.",
    mono: "sign in · email",
  },
  {
    n: "03",
    title: "첫 책장 만들기",
    body: "표지를 끌어다 놓아 첫 책장을 채워보세요. 책은 제목·ISBN으로 검색하거나 CSV로 한 번에 임포트할 수 있어요.",
    mono: "import — 412 books · 6 shelves",
  },
];

export default function DownloadPage() {
  return (
    <div className="page-fade pb-12">
      <DownloadHero />
      <SmartScreenHelp />
      <Requirements />
      <InstallSteps />
    </div>
  );
}

function DownloadHero() {
  const [os, setOs] = useState<"mac" | "win">("win");
  const items = DOWNLOADS[os];

  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 pt-14 pb-12">
      <div className="grid lg:grid-cols-12 gap-10 items-start">
        <div className="lg:col-span-5">
          <div className="mono text-[11px] tracking-widest uppercase text-wood mb-4">
            ── DotShelf for Desktop
          </div>
          <h1 className="serif text-[40px] sm:text-[52px] leading-[1.05] text-charcoal">
            데스크탑에서
            <br />
            책장을 다듬다.
          </h1>
          <p className="mt-5 text-[16px] leading-[1.7] text-charcoal/75">
            큰 화면에서 표지를 끌어다 놓고, 키보드로 빠르게 책을 임포트하세요.
            정돈된 서재가 클라우드에 안전하게 저장됩니다.
          </p>
          <div className="mt-7 grid grid-cols-2 gap-y-3 gap-x-5 max-w-sm">
            <Meta label="버전" value={RELEASE.version} />
            <Meta label="릴리스" value={RELEASE.date} />
            <Meta label="채널" value={RELEASE.channel} />
            <Meta label="용량" value={RELEASE.size} />
          </div>
          <a
            href="#changelog"
            className="mt-6 inline-flex items-center gap-1.5 text-sm text-wood hover:text-wood-dark ink-link"
          >
            전체 변경 로그 보기
            <Icon name="arrow-right" size={14} />
          </a>
        </div>

        <div className="lg:col-span-7">
          <div className="paper-dark rounded-3xl border border-beige p-6 sm:p-8 shadow-warm">
            <DetectedBanner />
            <div className="flex gap-2 mb-5 flex-wrap">
              <OSTabButton
                active={os === "mac"}
                onClick={() => setOs("mac")}
                icon="apple"
                label="macOS (준비 중)"
                disabled
              />
              <OSTabButton
                active={os === "win"}
                onClick={() => setOs("win")}
                icon="windows"
                label="Windows"
              />
            </div>
            <div className="space-y-3">
              {items.length === 0 ? (
                <EmptyOsCard />
              ) : (
                items.map((it) => (
                  <DownloadCard
                    key={it.label}
                    item={it}
                    icon={os === "mac" ? "apple" : "windows"}
                  />
                ))
              )}
            </div>
            <div className="mt-5 pt-5 border-t border-beige/70 text-[12px] text-paper-muted flex items-start gap-2">
              <Icon
                name="check"
                size={14}
                className="text-wood mt-0.5 flex-shrink-0"
              />
              <span>
                런처가 본체 빌드 다운로드·SHA-256 검증·자동 업데이트까지 처리합니다.
              </span>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function OSTabButton({
  active,
  onClick,
  icon,
  label,
  disabled = false,
}: {
  active: boolean;
  onClick: () => void;
  icon: "apple" | "windows";
  label: string;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={`flex-1 sm:flex-none inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-full text-sm font-medium transition border ${
        disabled
          ? "bg-cream/40 text-charcoal/40 border-beige/60 cursor-not-allowed"
          : active
            ? "bg-charcoal text-cream border-charcoal"
            : "bg-cream text-charcoal border-beige hover:border-wood/60"
      }`}
    >
      <Icon
        name={icon}
        size={15}
        className={
          disabled
            ? "text-charcoal/40"
            : active
              ? "text-cream"
              : "text-charcoal"
        }
      />
      {label}
    </button>
  );
}

function DownloadCard({
  item,
  icon,
}: {
  item: Build;
  icon: "apple" | "windows";
}) {
  const ready = !!item.href;
  return (
    <div
      className={`rounded-2xl border p-5 transition relative ${
        item.recommended
          ? "border-wood bg-cream shadow-warm"
          : "border-beige bg-cream/60"
      }`}
    >
      {item.recommended && (
        <span className="absolute -top-2 left-5 mono text-[10px] uppercase tracking-widest bg-wood text-cream px-2 py-0.5 rounded-full">
          권장
        </span>
      )}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3 min-w-0">
          <div className="w-10 h-10 rounded-xl bg-beige-soft border border-beige flex items-center justify-center text-charcoal flex-shrink-0">
            <Icon name={icon} size={18} />
          </div>
          <div className="min-w-0">
            <div className="serif text-[18px] text-charcoal leading-tight">
              {item.label}
            </div>
            <div className="mono text-[11px] text-paper-muted mt-1">
              {item.sub}
            </div>
            <div className="mono text-[11px] text-paper-muted mt-0.5">
              {item.size}
            </div>
          </div>
        </div>
        {ready ? (
          <a
            href={item.href ?? undefined}
            className="btn-primary inline-flex items-center justify-center gap-1.5 px-4 py-2 rounded-full text-[13px] font-medium flex-shrink-0"
          >
            <Icon name="download" size={14} />
            받기
          </a>
        ) : (
          <button
            type="button"
            disabled
            className="btn-primary inline-flex items-center justify-center gap-1.5 px-4 py-2 rounded-full text-[13px] font-medium flex-shrink-0 opacity-50 cursor-not-allowed"
            title="배포 준비 중"
          >
            <Icon name="download" size={14} />
            준비 중
          </button>
        )}
      </div>
    </div>
  );
}

function EmptyOsCard() {
  return (
    <div className="rounded-2xl border border-dashed border-beige bg-cream/40 p-6 text-center text-sm text-paper-muted">
      해당 OS 빌드는 아직 준비 중입니다.
    </div>
  );
}

function DetectedBanner() {
  return (
    <div className="rounded-2xl border border-beige bg-beige-soft/60 p-5 flex items-center gap-4 mb-6">
      <div className="w-10 h-10 rounded-full bg-cream border border-beige flex items-center justify-center text-wood">
        <Icon name="sparkle" size={18} />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm text-charcoal">
          현재 베타 빌드:{" "}
          <strong className="serif text-[16px]">Windows · DotShelf 런처</strong>
        </p>
        <p className="mono text-[11px] text-paper-muted mt-0.5">
          macOS 빌드는 아직 준비 중입니다
        </p>
      </div>
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="mono text-[10px] uppercase tracking-widest text-paper-muted">
        {label}
      </div>
      <div className="text-[15px] text-charcoal mt-1">{value}</div>
    </div>
  );
}

function Requirements() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-16">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 시스템 요구사항
      </div>
      <h2 className="serif text-[30px] sm:text-[38px] text-charcoal mb-10">
        부드러운 3D 책장을 위한 환경.
      </h2>
      <div className="grid md:grid-cols-2 gap-5">
        {REQUIREMENTS.map((c) => (
          <div
            key={c.os}
            className="rounded-3xl border border-beige bg-cream/70 overflow-hidden"
          >
            <div className="flex items-center gap-3 px-6 py-4 border-b border-beige bg-beige-soft/50">
              <Icon name={c.icon} size={18} className="text-charcoal" />
              <span className="serif text-[20px] text-charcoal">{c.os}</span>
            </div>
            <table className="w-full">
              <tbody>
                {c.rows.map(([k, v], i) => (
                  <tr
                    key={k}
                    className={
                      i < c.rows.length - 1 ? "border-b border-beige/60" : ""
                    }
                  >
                    <td className="py-3.5 px-6 mono text-[11px] uppercase tracking-widest text-paper-muted w-2/5 align-top">
                      {k}
                    </td>
                    <td className="py-3.5 px-6 text-[14px] text-charcoal">
                      {v}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </section>
  );
}

function InstallSteps() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-16">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── 설치 가이드
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
            <div className="mt-5 pt-4 border-t border-beige/70">
              <div className="mono text-[11px] text-paper-muted/90 truncate">
                $ {s.mono}
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

// SmartScreen / Chrome 다운로드 경고 우회 안내 — 베타 단계라 아직 코드 서명
// 인증서가 없어 SmartScreen Application Reputation 경고가 뜨는 케이스.
// Microsoft Defender Response 의 dispute 답 (2026-05-13) 에서 "수동 화이트리스트
// 없음, reputation 알고리즘 누적 또는 코드 서명 cert 만 진짜 해결책" 명시 →
// v1.0 출시 전까지는 사용자에게 우회 가이드 노출이 최선.
//
// TODO(v1.0): EV 또는 Authenticode cert 도입 결정 시 이 섹션 통째 제거하거나
// "안전한 다운로드 — 코드 서명 인증서 적용" 안내로 톤 변경. v1.0 release
// checklist 항목으로 추적.
const HELP_STEPS = [
  {
    n: "01",
    title: "브라우저 다운로드 경고",
    body: '브라우저 다운로드 바에 "이 파일은 일반적으로 다운로드되지 않으며 위험할 수 있습니다" (Chrome) / "유해할 수 있어 차단됨" (Edge) 같은 경고가 뜨면, 우측 메뉴(⋯) 또는 "유지" 를 눌러 진행합니다.',
    mono: "Browser · 의심스러운 파일",
  },
  {
    n: "02",
    title: "Windows SmartScreen",
    body: '받은 .exe 를 실행했을 때 "Windows의 PC 보호 / 인식할 수 없는 앱" 창이 뜨면, 작게 표시된 "추가 정보" 를 누른 뒤 나타나는 "실행" 버튼을 클릭합니다.',
    mono: "Windows · 추가 정보 → 실행",
  },
  {
    n: "03",
    title: "한 번 통과하면 끝",
    body: "첫 실행을 통과하면 같은 .exe 는 더 이상 경고하지 않습니다. 이후 자동 업데이트가 새 버전을 받아오면 그 버전에서 한 번 더 같은 절차가 필요할 수 있어요.",
    mono: "재실행 시 경고 없음",
  },
];

function SmartScreenHelp() {
  return (
    <section className="max-w-6xl mx-auto px-5 sm:px-8 py-12">
      <div className="mono text-[11px] tracking-widest uppercase text-wood mb-3">
        ── Windows 다운로드 경고 안내
      </div>
      <h2 className="serif text-[30px] sm:text-[38px] text-charcoal mb-5">
        경고 창이 떠도 정상이에요.
      </h2>
      <p className="text-[16px] leading-[1.7] text-charcoal/75 max-w-2xl mb-10">
        베타 단계라 아직 코드 서명 인증서가 없어 Windows · 브라우저가 처음 보는
        파일로 경고할 수 있습니다.{" "}
        <strong className="text-charcoal">
          이 페이지(dotshelf.digital/download) 에서 받은 DotShelf 런처의 경우
        </strong>{" "}
        아래 3단계만 거치면 같은 .exe 에 한해 경고가 사라집니다. (다른 .exe 의
        같은 경고는 무시하지 마세요.)
      </p>
      {/* <ol> semantic — 순서가 있는 절차라 스크린리더 사용자에게도 "1, 2, 3 단계"
          로 전달되도록. list-style:none 으로 시각적 자동 number 는 비활성, 카드의
          큰 'Step 01/02/03' 라벨이 시각 인덱스 역할. */}
      <ol className="grid md:grid-cols-3 gap-5 list-none p-0">
        {HELP_STEPS.map((s) => (
          <li
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
            <div className="mt-5 pt-4 border-t border-beige/70">
              <div className="mono text-[11px] text-paper-muted/90 truncate">
                {s.mono}
              </div>
            </div>
          </li>
        ))}
      </ol>
      {/* Step 02 의 핵심 단계 — '추가 정보 → 실행' 클릭 전후 비교. 캡쳐 1장 만으론
          '실행 안 함' 버튼만 보여서 사용자가 막다른 길로 오해할 수 있음. before/after
          나란히 두는 게 사용자 confusion 차감. <ol> semantic — 두 단계 순서. */}
      <ol className="mt-12 grid md:grid-cols-2 gap-5 list-none p-0">
        <li>
          <figure className="rounded-2xl bg-cream border border-beige p-5 shadow-warm-sm m-0">
            <Image
              src="/help/smartscreen-step1.webp"
              alt="Microsoft Defender SmartScreen 경고 창 — '추가 정보' 링크가 작게 표시되고 '실행 안 함' 버튼만 보이는 초기 상태"
              width={515}
              height={484}
              className="w-full h-auto"
            />
            <figcaption className="mt-4">
              <span className="mono text-[10px] uppercase tracking-widest text-wood">
                Step 02 — before
              </span>
              <p className="serif text-[15px] text-charcoal mt-1">
                왼쪽 위의{" "}
                <strong className="text-charcoal">&ldquo;추가 정보&rdquo;</strong>{" "}
                링크를 클릭합니다.
              </p>
            </figcaption>
          </figure>
        </li>
        <li>
          <figure className="rounded-2xl bg-cream border border-beige p-5 shadow-warm-sm m-0">
            <Image
              src="/help/smartscreen-step2.webp"
              alt="Microsoft Defender SmartScreen 경고 창 — '추가 정보' 클릭 후 게시자 정보가 노출되고 우측 하단에 '실행' 버튼이 활성화된 상태"
              width={516}
              height={483}
              className="w-full h-auto"
            />
            <figcaption className="mt-4">
              <span className="mono text-[10px] uppercase tracking-widest text-wood">
                Step 02 — after
              </span>
              <p className="serif text-[15px] text-charcoal mt-1">
                우측 하단에 나타나는{" "}
                <strong className="text-charcoal">&ldquo;실행&rdquo;</strong>{" "}
                버튼을 누르면 그대로 진행됩니다.
              </p>
            </figcaption>
          </figure>
        </li>
      </ol>
      <div className="mt-10 rounded-2xl border border-dashed border-beige bg-cream/40 p-6 text-[13px] text-paper-muted leading-[1.9]">
        <div className="mono text-[10px] uppercase tracking-widest text-wood mb-2">
          ── 왜 경고가 뜨나요?
        </div>
        <p>
          Microsoft Defender SmartScreen 의 Application Reputation 시스템이
          새로운 .exe 의 신뢰 점수를 평가합니다. 베타 단계에서 다운로드 이력이
          적고 코드 서명 인증서도 없는 상태라 처음 보는 사용자에게 경고를 보여요.
          DotShelf 런처는{" "}
          <strong>
            Microsoft 자체 분석에서 악성으로 분류되지 않음을 확인
          </strong>
          했으며 (case ID c9b94d97-e141-4a21-810a-99811a468763, 2026-05-13), v1.0
          정식 출시 시 코드 서명 인증서를 도입할 예정입니다. Microsoft 의 공식
          답변 발췌:
        </p>
        <blockquote className="mt-3 pl-4 border-l-2 border-wood/40 italic text-paper-muted">
          “The Application Reputation feature does not have an allow list,
          whitelist, or other equivalent. … reputation is established by how
          your download is used by Windows, Edge, Internet Explorer users and
          the SmartScreen® Service intelligence algorithms.”
        </blockquote>
      </div>
    </section>
  );
}
