# dotshelf admin-web

Dotshelf 운영팀·CS팀·법무팀·개발팀이 사용하는 PC 전용 백오피스. 콘텐츠 심사 / 신고 모더레이션 /
크리에이터 가입·정산 / 사용자·공간 관리를 한 곳에서 처리합니다.

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

기본 포트는 `3001`. (creator-web은 `3000`으로 분리.)

## v1.0 범위

- 사내 SSO + 강제 MFA + IP 화이트리스트(목업) 흐름
- 9개 기본 메뉴: 홈 / 심사 / 가입 / 모더레이션 / 정산 / 사용자·공간 / 통계 / 운영 / 감사 로그
- 콘텐츠 심사: 책·에셋·광고 탭, 큐 + 상세 + 일괄 처리, 위탁 제작 stepper, 가이드라인 패널
- 모더레이션: 신고 종류별 탭, SLA 카운트다운(P1 1h / P2 24h / P3 72h), 동일 패턴 신고 일괄 처리
- 정산 모니터링: 회계 대조 카드, 출금 4단계 stepper, 크리에이터별 / 계좌 변경 / 회계사 확인서
- 사용자: CS 메모, 환불, 페이지 잔액 조정, 공간 들여다보기, 데이터 다운로드
- 운영: 공지·약관 발행 모달, 관리자 권한 매트릭스 + 직원 목록
- RoleGate (6역할 × 31액션 매트릭스)
- 모든 쓰기 액션 감사 로그 기록 가정

## 환경 변수

- `NEXT_PUBLIC_USE_MOCKS` — `true` 일 때만 MSW 워커가 등록되어 `/api/*` 요청을 가짜 응답으로 잡습니다.
  - `npm run dev`는 이 변수를 자동으로 켭니다.
  - `npm run build` / `npm run start` 같은 운영 빌드에서는 환경에서 명시적으로 주입하지 않으면 mock이 꺼집니다 — 실제 백엔드와 연결 가능.

## 인증 / 보안 메모

- `lib/auth.ts` 는 mock 단계 절충으로 `sessionStorage`를 사용합니다 (XSS 표면 축소, 탭 종료 시 소실). 운영에서는 백엔드가 `Set-Cookie: HttpOnly; Secure; SameSite=Strict` 로 발급한 세션 쿠키를 사용하도록 인터페이스만 그대로 둔 채 내부 구현을 교체합니다. 호출부 변경은 불필요.
- 모든 RoleGate / 클라이언트 권한 체크는 UI 가시성 보조일 뿐입니다. 운영 백엔드는 모든 mutation에 대해 서버 측 권한 검증을 반드시 수행합니다.
- 비활성 30분 자동 로그아웃 + 5회 MFA 실패 시 잠금 정책은 UI 라벨로 표시되며, mock 흐름에서도 동작합니다.

## 모킹

`MSW`로 모든 API를 가로채며, 가짜 토큰·세션으로 진입할 수 있습니다.
- `/login` → 사내 SSO 버튼 → `/mfa` 6자리 입력(`000000` 외 통과) → `/dashboard`
- 우상단 플라스크 아이콘에서 6역할 토글로 RBAC 차등 노출 시연 가능

## 검증

```bash
npm run lint   # 0 errors / 0 warnings
npm run build  # static prerender
```
