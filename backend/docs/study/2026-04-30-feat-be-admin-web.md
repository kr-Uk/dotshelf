# admin-web 운영 범위 축소와 실동작 API 연결

- **브랜치**: `feat/be/admin-web`
- **범위**: `origin/dev..HEAD`
- **기간**: 2026-04-30
- **관련 커밋**: `08b063d [BE] Feat: admin-web 운영 기능 정리`

## TL;DR

admin-web에서 지금 운영하지 않는 creator/심사 중심 기능을 걷어내고, 실제로 운영 가능한 사용자/정산/운영 기능만 남기도록 정리했다.
사용자 화면은 강제 로그아웃, 재로그인 요구, 닉네임 변경, 환불을 실제 API로 연결했다.
정산과 운영 화면은 mock 성공 처리나 하드코딩 값을 줄이고, 백엔드 집계/상태 점검 응답을 기준으로 렌더링하도록 바꿨다.

## 무엇을 했나

### 1. 사용자 운영 액션을 실제 API로 연결

- 사용자 관점 효과: 관리자가 사용자 세션 무효화, 재로그인 요구, 닉네임 변경, 환불 처리를 화면에서 바로 수행할 수 있다.
- 시스템 관점 효과: 관리자 액션이 DB 상태 변경, refresh token 삭제, audit log 기록으로 이어진다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/admin/service/AdminService.java`, `backend/src/main/java/com/dotshelf/domain/admin/controller/AdminController.java`, `web/admin-web/app/(app)/users/page.tsx`

### 2. access token 세션 무효화 기준 추가

- 사용자 관점 효과: 강제 로그아웃 이후 기존 access token으로 API를 계속 호출하는 상황을 막을 수 있다.
- 시스템 관점 효과: `users.session_invalidated_at` 이후 발급된 JWT만 유효하게 취급한다.
- 주요 파일: `backend/src/main/resources/db/migration/V12__add_user_session_invalidated_at.sql`, `backend/src/main/java/com/dotshelf/domain/user/entity/User.java`, `backend/src/main/java/com/dotshelf/global/security/JwtAuthenticationFilter.java`, `backend/src/main/java/com/dotshelf/global/security/JwtTokenProvider.java`

### 3. 현재 목표와 맞지 않는 admin-web 기능 축소

- 사용자 관점 효과: 지금 서비스에서 쓰지 않는 심사/creator 정산/CDN 항목이 운영자에게 노출되지 않는다.
- 시스템 관점 효과: 가짜 성공 버튼과 mock 전용 상태를 줄이고, 실제 도메인에서 검증 가능한 기능만 남겼다.
- 주요 파일: `web/admin-web/app/(app)/dashboard/page.tsx`, `web/admin-web/app/(app)/settlements/page.tsx`, `web/admin-web/app/(app)/operations/page.tsx`, `web/admin-web/app/(app)/stats/page.tsx`

### 4. API 명세 갱신

- 사용자 관점 효과: 새로 연결된 관리자 액션과 축소된 화면 범위를 문서에서 확인할 수 있다.
- 시스템 관점 효과: 프론트/백엔드가 같은 운영 범위와 요청/응답 계약을 기준으로 후속 구현을 이어갈 수 있다.
- 주요 파일: `backend/docs/api/admin-web-api-spec.md`

## 왜 했나

기존 admin-web은 creator 모집, 심사, 작가/출판사 정산까지 포함한 넓은 운영 콘솔을 가정하고 있었다.
하지만 현재 목표는 "지금 운영 가능한 기능만 명확히"이므로, 없는 기능을 억지로 성공 처리하면 테스트와 운영 판단을 흐리게 만든다.
그래서 숨길 기능은 숨기고, 남기는 기능은 백엔드 API와 DB 변경까지 연결하는 방향으로 정리했다.

## 어떻게 구현했나

### 백엔드: 관리자 액션을 도메인 상태 변경으로 연결

사용자 액션은 `AdminService`에서 처리하고, 필요한 기존 도메인 Repository를 조합했다.
강제 로그아웃/재로그인 요구는 refresh token을 삭제하고 사용자 세션 무효화 시각을 갱신한다.
닉네임 변경은 `User.changeNickname()`을 호출하고, 환불은 구매 기록 검증 후 지갑 충전과 정산 조정 레코드를 함께 만든다.

핵심 포인트:
- Controller는 `/v1/admin/users/{targetUserId}/actions`, `/v1/admin/users/{targetUserId}/refund`를 제공한다.
- 관리자 액션은 가능한 한 audit log를 남긴다.
- 환불은 구매자와 구매 기록을 함께 검증해 다른 사용자의 구매 내역을 환불하지 못하게 한다.

### 인증: stateless JWT의 강제 로그아웃 한계 보완

refresh token만 삭제하면 이미 발급된 access token은 만료 전까지 살아 있을 수 있다.
이를 보완하기 위해 `users.session_invalidated_at`을 추가하고, JWT의 issued-at 시간이 이 값보다 이전이면 인증을 거부한다.
이 방식은 token blacklist보다 구현이 단순하고, "사용자 단위 전체 로그아웃" 요구에는 충분히 맞는다.

### 프론트: mock 성공 흐름 제거

admin-web은 기존 UI 타입을 최대한 유지하되, 실제로 존재하지 않는 기능은 버튼/카드/컬럼을 제거하거나 이름을 바꿨다.
공지 등록은 날짜 입력값을 ISO instant로 변환해 백엔드 DTO와 맞췄고, 운영 상태에서는 CDN을 제거한 뒤 결제/외부 도서 API 상태만 표시한다.
정산 화면은 creator payout이 아니라 실제 잔액 조정/정산 조정 내역 중심으로 좁혔다.

## 배운 것

- **운영 콘솔은 가짜 성공이 위험하다**: 버튼이 성공처럼 보이면 운영자는 기능이 실제로 반영됐다고 판단한다. 없는 기능은 숨기거나 명확히 비활성화해야 한다.
- **소셜 로그인 서비스의 비밀번호 재설정은 의미가 다르다**: 자체 비밀번호가 없으면 "비밀번호 재설정" 대신 "재로그인 요구"가 더 정확한 운영 액션이다.
- **JWT 강제 로그아웃은 서버 상태가 필요하다**: 완전한 stateless 구조에서는 이미 발급된 access token을 즉시 무효화하기 어렵다. 사용자별 invalidatedAt은 단순하지만 실용적인 절충안이다.
- **화면 축소도 명세 변경이다**: UI에서 사라진 기능은 API 명세와 운영 문서에서도 같은 의미로 정리해야 후속 작업자가 혼동하지 않는다.

## 더 공부할 것

- [ ] access token 무효화 전략을 `session_invalidated_at`, token blacklist, token version 중 어떤 기준으로 확장할 것인가?
- [ ] 환불 액션의 멱등성 키와 중복 환불 방지 정책을 어디까지 강제할 것인가?
- [ ] 외부 도서 API health check를 실시간 호출할 때 rate limit과 장애 전파를 어떻게 완화할 것인가?
- [ ] 관리자 audit log에 IP, user-agent, before/after diff를 어느 수준까지 남길 것인가?
- [ ] admin-web API adapter가 커질 때 기능별 client module로 분리할 기준은 무엇인가?

## 검증

- `backend`: `.\gradlew.bat test` 통과
- `web/admin-web`: `npm.cmd run lint` 통과
- `web/admin-web`: `npm.cmd run build` 통과
- `git diff --check` 통과

LF/CRLF 변환 경고는 Windows 작업 환경의 line ending 경고이며, whitespace 오류는 발견되지 않았다.
