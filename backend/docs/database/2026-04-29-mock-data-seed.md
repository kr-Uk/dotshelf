# 2026-04-29 Mock Data Seed

## 목적

빈 `dotshelf` PostgreSQL DB를 바로 API 연동 테스트에 사용할 수 있도록, 앱 테이블 전체에 관계가 이어지는 더미데이터를 넣었다.

- 실행 SQL: `backend/scripts/seed-mock-data.sql`
- 적용 DB: `dotshelf`
- 제외 테이블: `flyway_schema_history`
- 적용 방식: 앱 서버에서 `/opt/dotshelf/.env`의 DB 접속 정보를 읽어 `psql`로 실행
- 검증 API:
  - `GET https://api.dotshelf.digital/health` -> 200, DB/Redis UP
  - `GET https://api.dotshelf.digital/v1/books?limit=3` -> 200, 시드 도서 반환

> 참고: 공개 도메인의 `/api/v1/books` 경로는 401을 반환했고, 현재 비인증 도서 카탈로그는 `/v1/books` 경로에서 정상 조회된다.

## 주요 시나리오

### 사용자

총 4명.

- `1001 minji.reader`: 일반 사용자, 에세이/소설 중심 독자
- `1002 junho.shelf`: 일반 사용자, SF/개발서 독자
- `1003 seoyeon.space`: 일반 사용자, 공간 탐색 중심 사용자
- `1004 dotshelf.admin`: 관리자 화면 검증용 계정

각 사용자에 대해 소셜 계정, 알림 설정, 피드 설정, 페이지 지갑을 함께 생성했다. 일반 사용자 3명은 서로 팔로우 관계를 갖고, 관리자 계정은 신고/검수 시나리오에 쓰인다.

`auth_refresh_tokens` 3건은 토큰 테이블 관계와 관리자 조회 검증용 더미 값이다. 실제 보호 API 호출은 소셜 로그인 흐름에서 발급된 정상 JWT를 사용해야 한다.

### 에셋과 인벤토리

카테고리 3개와 에셋 6개를 만들었다.

- 카테고리: `BOOKSHELF`, `INTERIOR`, `THEME`
- 책장 에셋: 월넛 기본 책장, 아이보리 책장 스킨
- 공간 오브젝트: 스카이 노트북, 작은 화분
- 방명록 에셋: 민트 방명록 포스트잇
- 테마 에셋: 차분한 서재 테마

사용자 인벤토리는 12건이다. 책장 바디, 스킨, 테마, 오브젝트, 포스트잇이 실제 소유자와 연결되도록 배치했다. 구매 이력은 4건이며, 구매 이력의 `inventory_id`는 실제 인벤토리와 맞춰 두었다.

### 공간과 책장

공간 4개, 템플릿 3개, 레이아웃 스냅샷 3개를 만들었다.

- `1001 민지의 저녁 서재`
- `1002 준호의 SF 책장`
- `1003 서연의 창가 공간`
- `1004 관리자 검수 공간`

각 공간에는 책장 1개 이상이 연결된다. 일반 사용자 책장에는 실제 `user_books`와 `user_asset_inventories`를 참조하는 책장 아이템을 배치했다.

책장 아이템 예시:

- 민지 책장: `모순`, `불편한 편의점`, `작은 화분`
- 준호 책장: `모순`, `클린 코드`, `스카이 노트북`
- 서연 책장: `모순`, `아몬드`, `작은 화분`

### 도서와 독서 기록

도서 6권을 만들었다.

- `모순`
- `불편한 편의점`
- `아몬드`
- `작별인사`
- `달러구트 꿈 백화점`
- `클린 코드`

사용자 책은 9건이며, 리뷰는 9건이다. 독서 기록은 5건, 댓글은 6건, 기록 좋아요는 5건, 댓글 좋아요는 3건이다. 시드 마지막 단계에서 도서 평점/리뷰 수/책장 추가 수, 독서 기록 좋아요/댓글 수, 댓글 좋아요 수를 실제 집계값으로 다시 맞췄다.

### 소셜/운영 데이터

다음 테스트 데이터를 함께 넣었다.

- 방명록 6건, 방명록 답글 6건
- 방명록 마이그레이션 import 3건
- 공간 좋아요 6건, 공간 방문 7건
- 알림 6건
- 피드 이벤트 6건
- 신고 3건
- 도서 프로모션 3건
- 유저 차단 3건
- 에셋 위시리스트 3건

## 적용 후 테이블별 건수

| 테이블 | 건수 |
| --- | ---: |
| `asset_categories` | 3 |
| `asset_purchases` | 4 |
| `asset_wishlists` | 3 |
| `assets` | 6 |
| `auth_refresh_tokens` | 3 |
| `book_promotions` | 3 |
| `book_reviews` | 9 |
| `books` | 6 |
| `bookshelf_items` | 9 |
| `bookshelf_sections` | 5 |
| `bookshelves` | 4 |
| `feed_events` | 6 |
| `follows` | 6 |
| `guest_migration_imports` | 3 |
| `guestbook_replies` | 6 |
| `guestbooks` | 6 |
| `notifications` | 6 |
| `page_wallets` | 4 |
| `reading_record_comment_likes` | 3 |
| `reading_record_comments` | 6 |
| `reading_record_likes` | 5 |
| `reading_records` | 5 |
| `reports` | 3 |
| `space_layout_snapshots` | 3 |
| `space_likes` | 6 |
| `space_templates` | 3 |
| `space_visits` | 7 |
| `spaces` | 4 |
| `user_asset_inventories` | 12 |
| `user_blocks` | 3 |
| `user_books` | 9 |
| `user_feed_settings` | 4 |
| `user_notification_settings` | 4 |
| `user_social_accounts` | 4 |
| `users` | 4 |

## 관계 검증

실행 후 아래 관계를 쿼리로 확인했다.

- `users -> spaces -> bookshelves`: 사용자 4명 모두 자기 공간과 책장을 가진다.
- `bookshelf_items -> user_books -> books`: 책 아이템이 실제 사용자 책과 도서로 이어진다.
- `bookshelf_items -> user_asset_inventories -> assets`: 에셋 아이템이 실제 소유 인벤토리와 에셋으로 이어진다.
- `guestbooks -> user_asset_inventories -> assets`: 방명록 포스트잇이 작성자별 포스트잇 인벤토리와 이어진다.
- 집계 컬럼: 책 평점/리뷰 수/책장 추가 수, 독서 기록 좋아요/댓글 수, 방명록 답글 수, 공간 좋아요/방문 수를 재계산했다.

## 재실행 메모

시드 SQL은 고정 ID를 사용하고 `ON CONFLICT DO NOTHING`으로 작성되어 있다. 이미 적용된 DB에서 재실행하면 기존 행은 중복 삽입되지 않는다. 다만 집계 컬럼은 현재 테이블 상태 기준으로 다시 계산된다.
