# Admin Web API Spec

작성일: 2026-04-29

## 범위

`web/admin-web`의 하드코딩/MSW 데이터를 백엔드 API로 교체하기 위한 1차 명세다.

현재 좌측 메뉴 중 `심사`, `가입`은 사용하지 않는다. 화면과 라우트는 남기되 사이드바에서는 비활성화하고 하단으로 이동한다. 백엔드 1차 구현 범위에서도 제외한다.

구현 대상 메뉴:

- 홈
- 모더레이션
- 정산
- 사용자·공간
- 통계
- 운영
- 감사 로그

## 기본 규칙

### Base Path

백엔드 컨트롤러는 기존 앱 API와 분리해 아래 prefix를 사용한다.

```text
/v1/admin
```

admin-web의 현재 클라이언트는 `/api/*`를 호출한다. 프론트 연동 시 둘 중 하나를 선택한다.

- 권장: admin-web에서 `/api/*`를 `/v1/admin/*`로 rewrite
- 대안: `web/admin-web/lib/api.ts`의 base URL을 환경변수화

### 인증/권한

모든 `/v1/admin/**` API는 인증 필요.

```http
Authorization: Bearer <access_token>
```

1차 구현은 기존 `users.role = 'ADMIN'`만 허용한다. 현재 admin-web 타입에는 `super_admin`, `moderator`, `settlement` 같은 세분화 role이 있지만, 기존 DB에는 세분화된 관리자 권한 테이블이 없다. 세분화 권한은 2차에서 `admin_members` 테이블로 확장한다.

테스트 환경에서만 `ADMIN_AUTH_BYPASS=true`를 설정하면 `/v1/admin/**` 인증/인가를 우회한다. 기본값은 `false`다.

### 응답 Envelope

기존 백엔드의 `ApiResponse<T>`를 그대로 사용한다.

```json
{
  "httpStatus": 200,
  "isSuccess": true,
  "message": "요청이 성공했습니다.",
  "code": "SUCCESS",
  "result": {}
}
```

### 페이징

목록 API는 offset 대신 cursor 기반을 기본으로 한다. admin 화면은 검색/필터가 많으므로 `limit` 기본값은 20, 최대값은 100으로 둔다.

```json
{
  "items": [],
  "nextCursor": "createdAt_id",
  "totalCount": 123
}
```

## 현재 DB 매핑

기존 테이블로 바로 활용 가능한 영역:

| 화면 | 활용 테이블 |
| --- | --- |
| 홈 | `users`, `books`, `assets`, `reports`, `page_wallets`, `asset_purchases`, `spaces`, `space_visits` |
| 모더레이션 | `reports`, `users`, `reading_records`, `reading_record_comments`, `guestbooks`, `spaces` |
| 사용자·공간 | `users`, `user_social_accounts`, `page_wallets`, `spaces`, `bookshelves`, `user_books`, `reports`, `user_blocks`, `asset_purchases` |
| 통계 | `users`, `spaces`, `user_books`, `reading_records`, `reports`, `space_visits`, `asset_purchases`, `page_wallets` |
| 운영 일부 | `users.role = 'ADMIN'`, `/health`, actuator health |

추가 테이블이 필요한 영역:

| 화면 | 필요한 테이블 |
| --- | --- |
| 감사 로그 | `admin_audit_logs` |
| 사용자 메모 | `admin_user_notes` |
| 정산 출금/배치 | `settlement_withdrawals`, `settlement_payout_batches`, `settlement_account_change_requests`, `settlement_adjustments` |
| 운영 공지/정책 | `admin_announcements`, `policy_versions`, `maintenance_settings` |
| 관리자 세분화 권한 | `admin_members` 또는 `users.role` 확장 |

## 공통 DTO

```ts
type AdminRole = "admin";

interface PageResponse<T> {
  items: T[];
  nextCursor: string | null;
  totalCount: number;
}

interface OkResponse {
  ok: true;
}
```

## 인증/내 계정

### GET `/v1/admin/me`

현재 토큰의 사용자가 관리자 계정인지 확인하고 admin-web 세션 정보를 반환한다.

DB source:

- `users`

Response:

```json
{
  "id": "1004",
  "email": "admin.mock@dotshelf.dev",
  "name": "dotshelf.admin",
  "role": "admin",
  "team": "운영",
  "ipAllowlisted": true,
  "mfaEnrolled": false,
  "lastSignInAt": "2026-04-29T06:00:00Z"
}
```

비고:

- 기존 admin-web의 `/auth/sso/start`, `/auth/mfa/verify`는 실제 사내 SSO/MFA가 없으므로 1차 범위에서 제외한다.
- 개발/테스트는 기존 JWT access token을 발급해 `GET /v1/admin/me`로 진입한다.

## 홈

### GET `/v1/admin/dashboard/summary`

홈 카드와 대시보드 수치를 반환한다.

DB source:

- `reports`: 미처리 신고, P1 근사치
- `users`: 가입자, 관리자
- `books`, `assets`: 콘텐츠 수
- `asset_purchases`: 에셋 구매액
- `page_wallets`: 선불 페이지 잔액
- `spaces`, `space_visits`: 공간/방문 수

Response:

```json
{
  "content": {
    "books": 6,
    "assets": 6,
    "activePromotions": 3
  },
  "moderation": {
    "open": 3,
    "autoFlagged": 0,
    "p1": 0
  },
  "settlement": {
    "pendingPayoutPages": 0,
    "walletBalancePages": 116499,
    "reconciliationOk": true
  },
  "ops": {
    "users": 4,
    "spaces": 4,
    "todayVisits": 0,
    "apiStatus": "UP"
  }
}
```

## 모더레이션

### GET `/v1/admin/reports`

신고 목록을 조회한다.

Query:

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `status` | string | `PENDING`, `RESOLVED`, `REJECTED` |
| `targetType` | string | `READING_RECORD`, `READING_RECORD_COMMENT`, `GUESTBOOK`, `SPACE`, `USER` |
| `q` | string | 작성자 닉네임/본문 일부 검색 |
| `cursor` | string | 다음 페이지 커서 |
| `limit` | number | 기본 20 |

DB source:

- `reports`
- `users` as reporter/reported user
- target resolver: `reading_records`, `reading_record_comments`, `guestbooks`, `spaces`

Response:

```json
{
  "items": [
    {
      "id": "1001",
      "targetType": "GUESTBOOK",
      "targetId": "1003",
      "targetExcerpt": "SF 책 추천해요",
      "targetAuthorId": "1001",
      "targetAuthorName": "minji.reader",
      "reporterCount": 1,
      "reasonType": "SPAM",
      "reasonDetail": "반복 홍보성 문구로 신고된 샘플입니다.",
      "status": "PENDING",
      "severity": "P3",
      "isBlindedToReporter": true,
      "isGloballyBlinded": false,
      "createdAt": "2026-04-29T05:00:00Z",
      "reviewedByUserId": null,
      "reviewedAt": null
    }
  ],
  "nextCursor": null,
  "totalCount": 3
}
```

### GET `/v1/admin/reports/{reportId}`

신고 상세와 대상 원문을 조회한다.

Response:

```json
{
  "id": "1001",
  "target": {
    "type": "GUESTBOOK",
    "id": "1003",
    "excerpt": "SF 책 추천해요",
    "authorId": "1001",
    "authorName": "minji.reader"
  },
  "reporters": [
    {
      "userId": "1001",
      "nickname": "minji.reader",
      "reasonType": "SPAM",
      "reasonDetail": "반복 홍보성 문구로 신고된 샘플입니다.",
      "reportedAt": "2026-04-29T05:00:00Z"
    }
  ],
  "status": "PENDING"
}
```

### POST `/v1/admin/reports/{reportId}/action`

신고 조치를 수행한다.

Request:

```json
{
  "action": "RESOLVE",
  "blindToReporter": true,
  "globallyBlind": false,
  "reason": "운영자 검토 완료"
}
```

Action:

- `START_REVIEW`: `status = IN_REVIEW`
- `RESOLVE`: `status = RESOLVED`
- `REJECT`: `status = REJECTED`
- `GLOBAL_BLIND`: `is_globally_blinded = true`
- `UNBLIND`: `is_globally_blinded = false`

Side effect:

- `reports.reviewed_by_user_id`, `reports.reviewed_at` 갱신
- `admin_audit_logs` 기록

### POST `/v1/admin/reports/bulk-action`

여러 신고에 같은 조치를 수행한다.

Request:

```json
{
  "ids": ["1001", "1002"],
  "action": "RESOLVE",
  "reason": "동일 정책 위반 일괄 처리"
}
```

## 사용자·공간

### GET `/v1/admin/users`

사용자 목록과 운영 지표를 조회한다.

Query:

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `q` | string | id, email, nickname |
| `status` | string | `ACTIVE`, `SUSPENDED_TEMP`, `SUSPENDED_PERM`, `DELETED` |
| `provider` | string | `GOOGLE`, `KAKAO`, `APPLE` |
| `cursor` | string | 다음 페이지 커서 |
| `limit` | number | 기본 20 |

DB source:

- `users`
- `user_social_accounts`
- `page_wallets`
- `spaces`
- `bookshelves`
- `user_books`
- `reports`

Response:

```json
{
  "items": [
    {
      "id": "1001",
      "nickname": "minji.reader",
      "email": "minji.mock@dotshelf.dev",
      "joinedAt": "2026-04-29T05:00:00Z",
      "lastSeenAt": "2026-04-29T06:00:00Z",
      "state": "ACTIVE",
      "socialProvider": "GOOGLE",
      "spaces": 1,
      "bookshelves": 1,
      "books": 3,
      "reportsAgainst": 1,
      "pageBalance": 5200
    }
  ],
  "nextCursor": null,
  "totalCount": 4
}
```

### GET `/v1/admin/users/{userId}`

사용자 상세, 공간 요약, 설정, 지갑 정보를 조회한다.

DB source:

- `users`
- `user_social_accounts`
- `user_notification_settings`
- `user_feed_settings`
- `page_wallets`
- `spaces`

### POST `/v1/admin/users/{userId}/action`

사용자 계정 상태를 변경한다.

Request:

```json
{
  "action": "SUSPEND_TEMP",
  "reason": "신고 누적 운영 조치",
  "until": "2026-05-06T00:00:00Z"
}
```

Action:

- `ACTIVATE`
- `SUSPEND_TEMP`
- `SUSPEND_PERM`
- `ANONYMIZE`

DB note:

- 현재 `users.status`는 문자열이므로 상태 저장은 가능하다.
- 임시 정지 만료일을 보존하려면 `user_suspensions` 테이블이 추가로 필요하다.

### GET `/v1/admin/users/{userId}/spaces`

사용자가 가진 공간과 책장 현황을 조회한다.

DB source:

- `spaces`
- `bookshelves`
- `bookshelf_sections`
- `bookshelf_items`
- `user_books`
- `books`
- `user_asset_inventories`
- `assets`

### GET `/v1/admin/users/{userId}/purchases`

구매 내역을 조회한다.

DB source:

- `asset_purchases`
- `assets`

1차에서는 에셋 구매만 제공한다. 페이지 충전, 도서 표지 구매, 광고 예치금은 현재 DB 테이블이 없다.

### GET `/v1/admin/users/{userId}/history`

신고/차단 이력을 조회한다.

DB source:

- `reports` as reporter/reported
- `user_blocks`

### GET `/v1/admin/users/{userId}/notes`

관리자 메모를 조회한다.

DB requirement:

- `admin_user_notes` 신규 필요

### POST `/v1/admin/users/{userId}/notes`

관리자 메모를 작성한다.

Request:

```json
{
  "body": "CS 문의 후 환불 정책 안내 완료"
}
```

### POST `/v1/admin/users/{userId}/adjust-balance`

페이지 잔액을 수기 조정한다.

Request:

```json
{
  "deltaPages": 500,
  "reason": "incident_compensation",
  "memo": "장애 보상"
}
```

DB note:

- 현재 `page_wallets.balance_pages`는 수정 가능하다.
- 조정 이력 보존을 위해 `page_wallet_adjustments` 또는 `settlement_adjustments` 테이블 추가가 필요하다.

## 정산

현재 DB에는 실제 출금, 은행 계좌, 송금 배치 테이블이 없다. 1차 구현은 지갑/구매 기반의 read-only 요약부터 제공하고, 쓰기 액션은 마이그레이션 후 구현한다.

### GET `/v1/admin/settlement/reconciliation`

페이지 지갑 잔액과 구매 합계를 비교한다.

DB source:

- `page_wallets`
- `asset_purchases`

Response:

```json
{
  "walletBalancePages": 116499,
  "assetPurchasePages": 2800,
  "pendingPayoutPages": 0,
  "consistent": true,
  "asOf": "2026-04-29T06:00:00Z"
}
```

### GET `/v1/admin/settlement/withdrawals`

출금 요청 목록.

DB requirement:

- `settlement_withdrawals` 신규 필요

1차에서는 빈 목록을 반환할 수 있다.

### GET `/v1/admin/settlement/payout-batches`

송금 배치 목록.

DB requirement:

- `settlement_payout_batches` 신규 필요

### GET `/v1/admin/settlement/account-changes`

계좌 변경 요청 목록.

DB requirement:

- `settlement_account_change_requests` 신규 필요

### GET `/v1/admin/settlement/creators`

정산 대상 크리에이터 요약.

DB requirement:

- 크리에이터/공급자 모델이 현재 DB에 없다.
- 2차에서 `creators` 또는 `creator_profiles` 테이블을 추가해야 한다.

## 통계

### GET `/v1/admin/stats/kpi`

현재 시점 KPI 스냅샷을 반환한다.

DB source:

- `users`
- `spaces`
- `books`
- `user_books`
- `reading_records`
- `reports`
- `asset_purchases`
- `space_visits`

Response:

```json
{
  "dau": 0,
  "wau": 3,
  "mau": 4,
  "newSignups": 4,
  "guestConversion": 0,
  "booksAdded": 9,
  "openModerationQueue": 1,
  "pageGmvPages": 2800,
  "assetPurchaseCount": 4,
  "spaceVisits": 7,
  "apiErrorRate": 0,
  "cdnHitRate": null
}
```

### GET `/v1/admin/stats/timeseries`

일자별 KPI를 반환한다.

Query:

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `from` | date | 시작일 |
| `to` | date | 종료일 |
| `granularity` | string | `day`, `week`, 기본 `day` |

Response:

```json
{
  "daily": [
    {
      "date": "2026-04-29",
      "newSignups": 4,
      "booksAdded": 9,
      "assetPurchasePages": 2800,
      "openModeration": 1,
      "spaceVisits": 7
    }
  ],
  "categoryRevenue": [
    {
      "category": "INTERIOR",
      "pages": 1650
    }
  ],
  "funnel": []
}
```

## 운영

### GET `/v1/admin/operations/system-health`

운영 상태를 조회한다.

DB/source:

- Spring Actuator `HealthEndpoint`
- `reports` backlog
- 외부 API 상태는 1차에서 `UNKNOWN` 또는 설정값 반환

Response:

```json
{
  "api": "UP",
  "db": "UP",
  "redis": "UP",
  "externalBookApi": "UNKNOWN",
  "cdn": "UNKNOWN",
  "reviewQueueBacklog": 0,
  "moderationBacklog": 1,
  "maintenanceMode": false
}
```

### GET `/v1/admin/operations/admins`

관리자 목록을 조회한다.

1차 DB source:

- `users where role = 'ADMIN'`

2차 DB requirement:

- `admin_members` 신규 필요: 세분화 role, team, active, mfa, IP allowlist

### POST `/v1/admin/operations/admins/{userId}/role`

관리자 role 변경.

2차 범위. 현재 DB의 `users.role`은 `USER`/`ADMIN` 수준만 표현한다.

### GET `/v1/admin/operations/announcements`

공지 목록.

DB requirement:

- `admin_announcements` 신규 필요

### POST `/v1/admin/operations/announcements`

공지 생성.

Request:

```json
{
  "title": "점검 안내",
  "body": "서비스 점검이 예정되어 있습니다.",
  "audience": "ALL_USERS",
  "startsAt": "2026-04-30T00:00:00Z",
  "endsAt": "2026-04-30T02:00:00Z",
  "urgent": true
}
```

### GET `/v1/admin/operations/policies`

정책 버전 목록.

DB requirement:

- `policy_versions` 신규 필요

### POST `/v1/admin/operations/maintenance`

점검 모드 토글.

DB requirement:

- `maintenance_settings` 신규 필요
- 또는 Redis/config 기반으로 관리

## 감사 로그

### GET `/v1/admin/audit-logs`

관리자 액션 로그를 조회한다.

Query:

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `actorUserId` | long | 수행자 |
| `action` | string | 액션 |
| `targetType` | string | 대상 타입 |
| `from` | datetime | 시작 시각 |
| `to` | datetime | 종료 시각 |
| `cursor` | string | 다음 페이지 커서 |
| `limit` | number | 기본 20 |

DB requirement:

- `admin_audit_logs` 신규 필요

Response:

```json
{
  "items": [
    {
      "id": "1",
      "actorUserId": "1004",
      "actorEmail": "admin.mock@dotshelf.dev",
      "action": "REPORT_RESOLVE",
      "targetType": "REPORT",
      "targetId": "1001",
      "before": "{\"status\":\"PENDING\"}",
      "after": "{\"status\":\"RESOLVED\"}",
      "reason": "운영자 검토 완료",
      "occurredAt": "2026-04-29T06:00:00Z",
      "ip": "10.0.0.10"
    }
  ],
  "nextCursor": null,
  "totalCount": 1
}
```

## 전역 검색

### GET `/v1/admin/search`

상단 글로벌 검색.

Query:

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `q` | string | 검색어 |
| `types` | string | `USER,REPORT,SPACE,BOOK` |
| `limit` | number | 기본 12 |

DB source:

- `users`
- `reports`
- `spaces`
- `books`

Response:

```json
{
  "items": [
    {
      "id": "1001",
      "type": "USER",
      "title": "minji.reader",
      "subtitle": "minji.mock@dotshelf.dev · 1001",
      "href": "/users?selected=1001"
    }
  ]
}
```

## 신규 마이그레이션 제안

1차 API를 실제 운영 액션까지 닫으려면 아래 테이블이 필요하다.

```sql
CREATE TABLE admin_audit_logs (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    created_at timestamp(6) NOT NULL,
    updated_at timestamp(6) NOT NULL,
    actor_user_id bigint NOT NULL,
    action varchar(80) NOT NULL,
    target_type varchar(80) NOT NULL,
    target_id bigint,
    before_payload text,
    after_payload text,
    reason varchar(500),
    ip_address varchar(45)
);

CREATE TABLE admin_user_notes (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    created_at timestamp(6) NOT NULL,
    updated_at timestamp(6) NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    body varchar(1000) NOT NULL
);

CREATE TABLE admin_announcements (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    created_at timestamp(6) NOT NULL,
    updated_at timestamp(6) NOT NULL,
    title varchar(150) NOT NULL,
    body text NOT NULL,
    audience varchar(30) NOT NULL,
    starts_at timestamp(6),
    ends_at timestamp(6),
    is_urgent boolean NOT NULL DEFAULT false,
    created_by_user_id bigint NOT NULL
);

CREATE TABLE maintenance_settings (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    created_at timestamp(6) NOT NULL,
    updated_at timestamp(6) NOT NULL,
    enabled boolean NOT NULL,
    message varchar(500),
    updated_by_user_id bigint NOT NULL
);
```

정산 도메인은 현재 결제/출금 모델이 없으므로 별도 설계가 필요하다.

- `settlement_withdrawals`
- `settlement_payout_batches`
- `settlement_account_change_requests`
- `creator_profiles`
- `settlement_adjustments`

## 구현 우선순위

1. `/v1/admin/me`
2. `/v1/admin/dashboard/summary`
3. `/v1/admin/reports`, `/v1/admin/reports/{id}/action`
4. `/v1/admin/users`, `/v1/admin/users/{id}`, `/v1/admin/users/{id}/spaces`
5. `/v1/admin/stats/kpi`, `/v1/admin/stats/timeseries`
6. `admin_audit_logs`, `admin_user_notes` 마이그레이션
7. `/v1/admin/audit-logs`, `/v1/admin/users/{id}/notes`
8. 운영/정산 추가 테이블 설계 및 API 확장
