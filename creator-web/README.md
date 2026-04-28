# dotshelf creator-web

Dotshelf 출판사·작가·꾸미기 디자이너용 PC 백오피스. v1.0은 책·에셋 등록과 광고·정산까지의 핵심 흐름.

## Getting Started

```bash
# 1. 의존성 설치
npm install

# 2. (옵션) 환경 변수
#    dev script가 NEXT_PUBLIC_USE_MOCKS=true 를 자동 주입하므로 별도 작업은 불필요.
#    .env.local로 직접 관리하고 싶다면:
cp .env.example .env.local

# 3. 개발 서버
npm run dev
```

기본 포트는 `3000`. (admin-web은 `3001`로 분리.)

## 환경 변수

- `NEXT_PUBLIC_USE_MOCKS` — `true` 일 때만 MSW 워커가 등록되어 `/api/*` 요청을 가짜 응답으로 잡습니다.
  - `npm run dev`는 이 변수를 자동으로 켭니다.
  - `npm run build` / `npm run start` 같은 운영 빌드에서는 환경에서 명시적으로 주입하지 않으면 mock이 꺼집니다 — 실제 백엔드와 연결 가능.

## 인증 / 보안 메모

- `lib/auth.ts` 는 mock 단계 절충으로 `sessionStorage`를 사용합니다 (XSS 표면 축소, 탭 종료 시 소실). 운영에서는 백엔드가 `Set-Cookie: HttpOnly; Secure; SameSite=Strict` 로 발급한 세션 쿠키를 사용하도록 인터페이스만 그대로 둔 채 내부 구현을 교체합니다. 호출부 변경은 불필요.
- 모든 RoleGate / 클라이언트 권한 체크는 UI 가시성 보조일 뿐입니다. 운영 백엔드는 모든 mutation에 대해 서버 측 권한 검증을 반드시 수행합니다.

## Mock API

`mocks/handlers.ts` 에 책·에셋·광고·정산·통계·고객지원 mock 응답이 in-memory로 살아있습니다. 새로고침 시 초기 mock으로 리셋.

## 검증

```bash
npm run lint   # 0 errors / 0 warnings
npm run build  # static prerender
```
