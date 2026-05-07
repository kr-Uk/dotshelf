# MR Description: Backend Test Coverage Reinforcement

## Summary

백엔드 테스트 기반과 도메인별 테스트를 대폭 보강했습니다.

- 신규 소셜 가입 시 기본 `Space`가 생성되도록 `AuthService` 로직을 보강했습니다.
- 기존 사용자 중 활성 `Space`가 없는 계정을 위한 backfill migration을 추가했습니다.
- `BookshelfService`의 데코 아이템 매핑에서 `assetInventoryId == null`일 때 발생할 수 있는 NPE를 방어했습니다.
- Controller, Service, Repository/JPA, Integration 테스트 기반을 추가했습니다.
- Auth/User/Space/Book/Bookshelf/Asset/ReadingRecord/Guestbook/Report/Notification/Payment/Admin 주요 플로우 테스트를 추가했습니다.
- PostgreSQL Testcontainers 기반 JPA 검증을 추가했습니다.
- PostgreSQL 17 Testcontainers 기반 Flyway V1~V20 migration/backfill 검증을 추가했습니다.
- Jacoco line coverage gate를 85%로 상향했습니다.
- GitLab CI에서 Postgres Testcontainers 테스트와 Jacoco 리포트 아티팩트를 실행/보관하도록 보강했습니다.

## Main Test Scope

- Auth
  - 소셜 신규 가입 시 `users`, `user_social_accounts`, 기본 알림 설정, 기본 `spaces` 생성 검증
  - 기존 소셜 계정 로그인 시 중복 생성 방지
  - refresh token rotation, reuse detection, logout revoke
  - Auth controller validation 및 인증 응답 envelope

- User
  - 내 프로필 조회/수정, 닉네임 중복 확인
  - 팔로우/언팔로우 멱등성
  - 차단/차단 해제 및 양방향 팔로우 제거
  - 알림/피드 설정 생성 및 수정
  - 탈퇴 soft delete

- Space
  - 내 공간 자동 생성/조회
  - 공개 범위 및 방명록 공개 범위 변경
  - 방문/좋아요/좋아요 해제 멱등성
  - 카테고리 에셋 적용, 타입 불일치, 잠긴 테마 reset
  - guestbook post-it whitelist default allow/restricted allow

- Book / Bookshelf
  - 책 검색 로컬+Kakao 병합 및 중복 제거
  - 추천/랭킹 정렬, cursor, rank
  - 책장에 책 추가, Kakao 책 추가, 이동, 보관, 삭제
  - 책장 생성/수정/삭제/순서 변경
  - 섹션 정렬, 아이템 이동/삭제
  - 데코 아이템 null inventory 방어

- Asset
  - 에셋 목록/상세/카테고리
  - 인벤토리 조회
  - 위시리스트
  - 구매 성공, 잔액 부족, 중복 구매, 무료 에셋 구매
  - 관리자 에셋 번들/프리뷰 이미지 presigned upload URL 및 confirm 검증

- ReadingRecord / Guestbook / Report / Notification
  - CRUD, 좋아요/댓글, 권한 검증
  - 알림 생성/읽음 처리
  - 중복 신고 방지 및 내 신고 목록 필터링
  - 알림 설정 비활성화 시 알림 미생성

- Payment / Admin
  - 페이지 충전 주문 생성 및 상태 조회
  - INICIS form/signature/client 처리
  - 결제 완료 멱등성, wallet/page transaction 반영
  - Admin 목록/상세/신고 조치/사용자 상태 조치/잔액 조정/공지/점검/감사 로그

- Global / Security
  - `/health` 200/503 응답
  - Spring Security 401/403 entry point envelope
  - GlobalExceptionHandler 주요 예외 변환
  - S3 storage wrapper, 이미지 매직 바이트 검증, 업로드 예외 변환

- Migration
  - Flyway로 V1~V19를 실제 migrate한 뒤 기존 사용자/공간 데이터를 seed
  - V20 적용 시 활성 Space가 없는 활성 사용자에게만 기본 공간 backfill
  - 기존 활성 Space 중복 생성 방지
  - 삭제된 Space만 있는 활성 사용자는 새 활성 Space 생성
  - `DELETED` status 또는 `deleted_at` 존재 사용자는 제외

## CI / Test Infra

- `build.gradle.kts`
  - Jacoco plugin 및 XML/HTML report 활성화
  - DTO/entity/config/Application/Q-class 제외 기준 적용
  - entity는 coverage 산식에서는 제외하되 entity 상태 변경은 service 테스트에서 간접 검증
  - `check`가 `jacocoTestCoverageVerification`을 수행하도록 연결
  - line coverage minimum: `0.85`
  - `-DrunPostgresTests=true` 전달 시 Postgres Testcontainers 테스트 활성화

- `.gitlab-ci.yml`
  - `backend:build`에서 `./gradlew check bootJar -DrunPostgresTests=true --no-daemon` 실행
  - Docker dind service로 Testcontainers 실행 환경 구성
  - test report, Jacoco report, Gradle problems report를 artifact로 보관

> CI runner가 Docker dind를 사용하려면 privileged runner 설정이 필요합니다. runner 정책상 dind가 막혀 있다면 Postgres Testcontainers 테스트는 별도 job 또는 runner 설정 조정이 필요합니다.

> `IntegrationTestSupport`는 rollback 격리용 `@Transactional` 베이스입니다. `@TransactionalEventListener(AFTER_COMMIT)` 검증처럼 실제 commit이 필요한 통합 테스트는 이 베이스를 상속하지 않고 테스트별 cleanup으로 격리합니다.

## Verification

```text
./gradlew.bat clean build -DrunPostgresTests=true

BUILD SUCCESSFUL
tests=498 failures=0 errors=0 skipped=0
Jacoco excluded line coverage=86.25%
covered=3231 missed=515 total=3746
```
