# 관리자 콘솔 API 및 admin-web 실데이터 연동

- **브랜치**: `feat/be/create-web`
- **범위**: `origin/feat/be/create-web..HEAD` (커밋 1개)
- **기간**: 2026-04-29
- **관련 커밋**: `8ee4b4b [BE] Feat: 관리자 콘솔 API 및 admin-web 연동`

## TL;DR

admin-web의 mock 기반 화면을 백엔드 API와 직접 연결할 수 있도록 `/v1/admin` 수직 슬라이스를 추가했다.
백엔드는 관리자 대시보드, 신고, 사용자, 정산, 통계, 운영, 감사 로그, 검색 API를 `ApiResponse` envelope으로 제공한다.
프론트는 기존 화면 컴포넌트 변경을 최소화하기 위해 `web/admin-web/lib/api.ts`에서 백엔드 DTO를 기존 UI 타입으로 변환한다.
운영 배포에서는 `admin.dotshelf.digital/api/admin/*`가 Nginx를 통해 `/v1/admin/*`로 프록시된다.

## 무엇을 했나

### 1. 관리자 API 수직 슬라이스 추가

- 사용자 관점 효과: admin-web에서 홈, 모더레이션, 정산, 사용자공간, 통계, 운영, 감사 로그 화면이 실제 DB 기반 데이터를 조회할 수 있다.
- 시스템 관점 효과: `AdminController -> AdminService -> Repository` 레이어를 만들어 운영 기능을 한 도메인으로 모았다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/admin/controller/AdminController.java`, `backend/src/main/java/com/dotshelf/domain/admin/service/AdminService.java`, `backend/src/main/java/com/dotshelf/domain/admin/dto/AdminDtos.java`

### 2. admin 스키마와 운영 테이블 추가

- 사용자 관점 효과: 공지, 정책, 정산 요청, 관리자 메모, 감사 로그 같은 운영 데이터가 DB에 저장될 수 있다.
- 시스템 관점 효과: Flyway migration `V7__create_admin_tables.sql`로 배포 시 스키마가 자동 생성된다.
- 주요 파일: `backend/src/main/resources/db/migration/V7__create_admin_tables.sql`, `backend/src/main/java/com/dotshelf/domain/admin/entity/*`

### 3. admin-web API 연결

- 사용자 관점 효과: 기존 하드코딩 화면이 실제 API 응답을 받아 렌더링한다.
- 시스템 관점 효과: 페이지 컴포넌트는 거의 유지하고, `api()` 클라이언트에서 envelope unwrap, PageResponse unwrap, DTO mapping을 담당한다.
- 주요 파일: `web/admin-web/lib/api.ts`, `web/admin-web/next.config.ts`, `backend/deploy/prod/nginx/conf.d/default.conf`

## 왜 했나

admin-web의 화면은 이미 사용 흐름이 잡혀 있었지만 데이터가 MSW/mock에 묶여 있었다.
BE API를 먼저 만들고 화면의 기존 타입으로 변환하면, 화면 컴포넌트 전체를 갈아엎지 않고 실제 DB 연동 테스트를 바로 시작할 수 있다.
또한 `심사`, `가입`은 당장 사용하지 않기로 했기 때문에 그 외 운영 탭부터 실제 API를 붙이는 쪽이 테스트 효율이 높다.

## 어떻게 구현했나

### 백엔드: 한 도메인 안에 운영 API를 모음

관리자 기능은 여러 기존 도메인 데이터를 읽어야 한다. 그래서 `admin` 도메인 Service에서 `User`, `Report`, `Space`, `AssetPurchase`, `PageWallet` 등 기존 Repository를 조합한다.
쓰기 액션은 사용자 상태 변경, 신고 처리, 메모 작성, 페이지 잔액 조정처럼 실제 도메인 상태를 바꾸고 감사 로그를 남기는 방식으로 구현했다.

핵심 포인트:
- Controller는 `ApiResponse<T>`만 반환하고 Repository를 직접 호출하지 않는다.
- DTO는 `AdminDtos` record 묶음으로 유지해 엔티티가 HTTP 경계 밖으로 새지 않게 했다.
- 신규 테이블은 `admin` schema 아래에 두어 기존 앱 테이블과 운영 테이블을 구분했다.

### 프론트: API 어댑터로 UI 타입을 유지

admin-web 기존 화면은 `DashboardStats`, `ReportItem`, `UserSummary` 같은 mock 타입을 기대한다.
백엔드 DTO 이름과 상태 값은 다르므로 `lib/api.ts`에서 다음 책임을 처리한다.

- `/api/admin` 기본 경로 사용
- `ApiResponse`의 `result` unwrap
- `PageResponse.items` unwrap
- 백엔드 상태 값(`PENDING`, `SUSPENDED_TEMP`, `READING_RECORD`)을 프론트 상태 값(`open`, `suspended_temp`, `reading_log`)으로 변환
- 로컬 테스트용 SSO/MFA 세션은 프론트에서 처리하되, fake token은 백엔드로 보내지 않음

### 배포 경로: same-origin API 프록시

운영 admin-web은 브라우저에서 `/api/admin/*`로 호출하고, Nginx가 이를 backend upstream의 `/v1/admin/*`로 보낸다.
로컬에서는 Next rewrite가 같은 경로를 `http://localhost:8080/v1/admin/*`로 넘긴다.
이 구조는 브라우저 CORS 문제를 줄이고, 로컬/운영의 프론트 호출 코드를 동일하게 유지한다.

## 배운 것

- **ApiResponse 직렬화 이름 주의**: Java getter가 `isSuccess()` 형태이면 JSON 필드가 `success`로 내려올 수 있다. 프론트 envelope 판별은 `isSuccess`와 `success` 둘 다 받아야 안전하다.
- **프론트 타입 유지 전략**: 화면이 이미 큰 경우 컴포넌트 전체를 백엔드 DTO에 맞추기보다 API client에서 변환하는 방식이 blast radius를 줄인다.
- **Flyway migration 원칙**: 기존 migration 수정은 checksum 문제를 만들 수 있으므로 새 기능은 새 `V*.sql`로 추가한다.
- **permitAll과 서비스 인가의 차이**: SecurityConfig에서 요청을 permitAll로 열어도 Service가 `@CurrentUser`를 요구하면 무토큰 테스트는 실패한다. 테스트 허용 범위를 어디에서 처리할지 명확히 해야 한다.

## 더 공부할 것

- [ ] admin API의 read model을 지금처럼 Service 조합으로 둘지, 통계/검색용 전용 projection이나 QueryDSL custom으로 분리할 기준은 무엇인가?
- [ ] 운영 도메인 테이블을 별도 schema로 둘 때 Flyway baseline, 권한, 백업 정책은 어떻게 가져가야 하는가?
- [ ] admin-web API adapter가 커질 때 endpoint별 client module로 나눌 기준은 어디인가?
- [ ] Cloudflare Access, 자체 SSO/MFA, 백엔드 JWT 인가를 어떤 단계별 전략으로 통합할 것인가?

## 검증

- `backend`: `.\gradlew.bat test` 통과
- `web/admin-web`: `npm.cmd run lint` 통과
- `web/admin-web`: `npm.cmd exec tsc -- --noEmit --pretty false` 통과
- `git diff --cached --check` 통과

로컬 Spring Boot smoke test는 `localhost:5432` PostgreSQL이 꺼져 있어 부팅 전 DB 연결 단계에서 실패했다. 코드 검증은 통과했지만 실제 화면 데이터 확인은 DB 또는 배포 API가 준비된 환경에서 확인해야 한다.
