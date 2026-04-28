import Link from "next/link";
import { ArrowRight, Check } from "lucide-react";

import { Button } from "@/components/ui/button";

const HIGHLIGHTS = [
  {
    title: "1페이지 = 10원",
    body: "사용자 앱과 동일한 페이지 단위로 가격을 책정하고, 정산은 원화로 받아요.",
  },
  {
    title: "매주 화요일 자동 정산",
    body: "최소 5만 원 이상은 즉시 출금도 가능. 분기마다 회계사무소 확인서로 잔액 일치 공시.",
  },
  {
    title: "주간 SoV 입찰 광고",
    body: "섹션(카테고리×연령) 단위 입찰. 가드레일로 점유율 25~30% 도달 시 분산.",
  },
  {
    title: "실제 책 위탁 3D 변환",
    body: "ISBN 매칭 책에 한해 직접 업로드 대신 책 1부 발송으로 3D 표지 제작 위탁.",
  },
];

const FAQS = [
  {
    q: "사업자 번호 없이도 가입할 수 있나요?",
    a: "v1.0은 출판사·작가·꾸미기 디자이너 모두 사업자 번호가 필요해요. 개인사업자도 OK입니다. 국세청 사업자상태 API로 자동 검증하며, 휴업·폐업 상태에서는 가입이 차단됩니다.",
  },
  {
    q: "ISBN이 없는 자가출판 책도 등록되나요?",
    a: "네. ISBN 매칭 실패/없음(자가출판)은 수동 입력 모드로 분기되어 메타데이터를 직접 입력합니다. 단, 위탁 3D 제작은 ISBN 매칭 책에 한해 가능합니다.",
  },
  {
    q: "광고 예치금은 어떻게 보관되나요?",
    a: "충전금은 운영 계좌와 별도로 보관됩니다(전자금융 컴플라이언스). 캠페인을 일시정지하면 잔여 예치금은 즉시 환불 처리됩니다.",
  },
  {
    q: "판매 중 가격 변경이 가능한가요?",
    a: "가능합니다. 다만 덤핑 방지를 위해 24시간 쿨다운이 적용됩니다. 판매 중지 후에도 기존 구매자는 보유·사용을 계속할 수 있어요.",
  },
];

export default function LandingPage() {
  return (
    <div className="flex min-h-screen flex-1 flex-col bg-background">
      <header className="flex h-16 items-center justify-between border-b border-border px-6">
        <Link href="/" className="flex items-center gap-2">
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground text-sm font-semibold">
            D
          </span>
          <span className="text-base font-semibold tracking-tight">
            dotshelf
          </span>
          <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
            크리에이터
          </span>
        </Link>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" render={<Link href="/login" />}>
            로그인
          </Button>
          <Button size="sm" render={<Link href="/signup" />}>
            입점 신청 <ArrowRight className="size-3.5" />
          </Button>
        </div>
      </header>

      <main className="flex-1">
        <section className="relative overflow-hidden border-b border-border">
          <div className="mx-auto max-w-6xl px-6 py-20 md:py-28">
            <div className="grid gap-10 lg:grid-cols-[1.2fr_1fr] lg:items-center">
              <div className="space-y-6">
                <div className="inline-flex items-center gap-2 rounded-full border border-border bg-muted/50 px-3 py-1 text-xs text-muted-foreground">
                  <span className="size-1.5 rounded-full bg-emerald-500" />
                  v1.0 · 출판사 · 작가 · 꾸미기 디자이너
                </div>
                <h1 className="text-4xl font-semibold leading-tight tracking-tight md:text-5xl">
                  당신의 책장을
                  <br />
                  누군가의 풍경으로 만드세요.
                </h1>
                <p className="max-w-xl text-base text-muted-foreground md:text-lg">
                  Dotshelf 크리에이터 백오피스에서 책과 3D 꾸미기 에셋을 등록하고,
                  주간 SoV 광고로 노출을 키우고, 매주 화요일에 정산받으세요.
                </p>
                <div className="flex flex-col gap-3 sm:flex-row">
                  <Button size="lg" render={<Link href="/signup" />}>
                    입점 신청 <ArrowRight className="size-4" />
                  </Button>
                  <Button
                    size="lg"
                    variant="outline"
                    render={<Link href="/login" />}
                  >
                    이미 계정이 있어요
                  </Button>
                </div>
              </div>

              <div className="relative">
                <div className="grid grid-cols-2 gap-3">
                  {HIGHLIGHTS.map((h) => (
                    <div
                      key={h.title}
                      className="rounded-xl border border-border bg-card p-4 text-card-foreground"
                    >
                      <Check className="size-4 text-emerald-600" />
                      <div className="mt-2 text-sm font-semibold">{h.title}</div>
                      <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                        {h.body}
                      </p>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="border-b border-border">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold tracking-tight">가격·정산 정책 요약</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              사용자 앱과 동일한 페이지 단위로 매출이 잡히고, 분리 보관 계좌에서 출금됩니다.
            </p>
            <div className="mt-8 grid gap-4 md:grid-cols-3">
              <div className="rounded-xl border border-border p-6">
                <div className="text-xs font-medium text-muted-foreground">재화 단위</div>
                <div className="mt-2 text-3xl font-semibold">1p = 10원</div>
                <p className="mt-3 text-sm text-muted-foreground">
                  책·에셋·광고 모두 페이지 단위. 정산은 원화 환산.
                </p>
              </div>
              <div className="rounded-xl border border-border p-6">
                <div className="text-xs font-medium text-muted-foreground">정산 주기</div>
                <div className="mt-2 text-3xl font-semibold">매주 화요일</div>
                <p className="mt-3 text-sm text-muted-foreground">
                  누적 5만 원 이상은 즉시 출금 신청 가능.
                </p>
              </div>
              <div className="rounded-xl border border-border p-6">
                <div className="text-xs font-medium text-muted-foreground">광고 예치금</div>
                <div className="mt-2 text-3xl font-semibold">분리 보관</div>
                <p className="mt-3 text-sm text-muted-foreground">
                  운영계좌와 별도. 분기별 회계사무소 확인서 공시.
                </p>
              </div>
            </div>
          </div>
        </section>

        <section className="border-b border-border">
          <div className="mx-auto max-w-3xl px-6 py-16">
            <h2 className="text-2xl font-semibold tracking-tight">자주 묻는 질문</h2>
            <div className="mt-8 space-y-3">
              {FAQS.map((f) => (
                <details
                  key={f.q}
                  className="group rounded-lg border border-border bg-card p-4 [&_summary::-webkit-details-marker]:hidden"
                >
                  <summary className="flex cursor-pointer items-center justify-between text-sm font-medium">
                    {f.q}
                    <span className="ml-4 text-muted-foreground transition group-open:rotate-180">
                      ▾
                    </span>
                  </summary>
                  <p className="mt-3 text-sm leading-relaxed text-muted-foreground">
                    {f.a}
                  </p>
                </details>
              ))}
            </div>
          </div>
        </section>

        <section className="bg-muted/30">
          <div className="mx-auto max-w-3xl px-6 py-16 text-center">
            <h2 className="text-2xl font-semibold tracking-tight">
              지금 입점하고 첫 책을 등록해보세요
            </h2>
            <p className="mt-3 text-sm text-muted-foreground">
              관리자 검수는 보통 1영업일 이내. 승인 후 바로 등록·판매할 수 있어요.
            </p>
            <div className="mt-6 flex justify-center gap-2">
              <Button size="lg" render={<Link href="/signup" />}>
                입점 신청 <ArrowRight className="size-4" />
              </Button>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-border bg-muted/20">
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-10 text-xs text-muted-foreground">
          {/* 통신판매중개자 면책고지 — 전상법 §20② 의무 */}
          <div className="rounded-md border border-border bg-background/60 px-4 py-3 leading-relaxed">
            <strong className="text-foreground">
              Dotshelf는 통신판매중개자입니다.
            </strong>{" "}
            Dotshelf는 통신판매의 당사자가 아니며, 등록된 상품·거래정보 및 거래에
            대한 책임은 판매자에게 있습니다. 결제·정산은 등록된 PG사(예:
            토스페이먼츠)를 통해 안전하게 처리됩니다.
          </div>

          {/* 사업자 정보 — 출시 전 실제 값으로 교체 필요 */}
          <div className="grid gap-2 md:grid-cols-2">
            <div className="space-y-1">
              <div>
                <span className="font-medium text-foreground">상호</span>{" "}
                : (주)도트셀프 · <span className="font-medium text-foreground">대표자</span>{" "}
                : 김지호
              </div>
              <div>
                <span className="font-medium text-foreground">사업자등록번호</span>{" "}
                : 000-00-00000{" "}
                <a
                  href="https://www.ftc.go.kr/bizCommPop.do?wrkr_no=0000000000"
                  target="_blank"
                  rel="noreferrer"
                  className="ml-1 underline-offset-4 hover:underline"
                >
                  사업자정보 확인
                </a>
              </div>
              <div>
                <span className="font-medium text-foreground">
                  통신판매업 신고번호
                </span>{" "}
                : 제 0000-서울XX-0000호
              </div>
            </div>
            <div className="space-y-1">
              <div>
                <span className="font-medium text-foreground">주소</span>{" "}
                : 서울특별시 …
              </div>
              <div>
                <span className="font-medium text-foreground">고객센터</span>{" "}
                : help@dotshelf.kr · 02-0000-0000 (평일 10–18시)
              </div>
              <div>
                <span className="font-medium text-foreground">호스팅</span> :
                Vercel Inc.
              </div>
            </div>
          </div>

          {/* 정책 링크 + 카피라이트 */}
          <div className="flex flex-col gap-3 border-t border-border pt-4 md:flex-row md:items-center md:justify-between">
            <span>© 2026 dotshelf</span>
            <div className="flex flex-wrap gap-x-4 gap-y-1">
              <Link
                href="/legal/terms-service"
                className="hover:text-foreground hover:underline"
              >
                서비스 이용약관
              </Link>
              <Link
                href="/legal/terms-seller"
                className="font-medium text-foreground hover:underline"
              >
                판매자 약관
              </Link>
              <Link
                href="/legal/terms-ad"
                className="hover:text-foreground hover:underline"
              >
                광고 약관
              </Link>
              <Link
                href="/legal/terms-ugc"
                className="hover:text-foreground hover:underline"
              >
                UGC 약관
              </Link>
              <Link
                href="/legal/privacy"
                className="font-medium text-foreground hover:underline"
              >
                개인정보 처리방침
              </Link>
            </div>
          </div>

          <p className="text-[10px] leading-relaxed text-muted-foreground/70">
            * 푸터의 사업자 정보(상호·대표자·사업자번호·통신판매신고번호·주소·고객센터)는
            법인 등록 및 통신판매중개업 신고 완료 후 실제 값으로 교체해주세요. 자세한
            가이드는 Notion [개발 외 체크리스트] 참조.
          </p>
        </div>
      </footer>
    </div>
  );
}
