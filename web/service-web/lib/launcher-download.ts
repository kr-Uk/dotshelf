// service-web 마케팅 사이트의 Windows 런처 .exe 다운로드 URL.
// download/page.tsx 와 marketing-header.tsx 가 둘 다 이 모듈만 의존하도록 분리 —
// header 가 page module(use client + DownloadHero/Requirements/InstallSteps 등) 을
// import 하면 모든 marketing 페이지 bundle 에 다운로드 페이지 전용 코드가 같이
// 끌려오는 회귀 방지 (MR #148 리뷰 Finding 1, ~21.8KB 분량 회귀 실측).
//
// bump-version.ps1 -Target launcher 가 여기 파일명만 갱신한다 — page.tsx 의
// 다른 occurrence 와 충돌하지 않는 single source of truth.
export const LAUNCHER_WIN_URL: string | null =
  "https://cdn.dotshelf.digital/windows/DotShelfLauncher-0.1.2.exe";
