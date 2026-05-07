# 백엔드 테스트 인프라와 커버리지 게이트 보강

- **브랜치**: `feat/be/testcode`
- **범위**: `feat/be/testcode`의 백엔드 테스트 보강 커밋들
- **기간**: 2026-05-07
- **관련 커밋**: `test: reinforce backend coverage` 및 MR121 리뷰 반영 후속 커밋

## TL;DR

백엔드 테스트 기반을 Controller / Service / Repository / Integration 레이어로 넓히고, PostgreSQL Testcontainers와 Jacoco coverage gate를 CI에 연결했다.

최종 검증 기준은 `./gradlew.bat clean build -DrunPostgresTests=true`이며, 테스트 `497개`, 실패 `0개`, 제외 기준 line coverage `86.25%`로 Jacoco gate `85%`를 통과한다.

기능 로직도 함께 보강했다. 소셜 신규 가입 시 기본 `Space`가 생성되도록 했고, 기존 사용자 중 활성 `Space`가 없는 계정은 migration으로 backfill한다. 책장 데코 아이템 매핑에서 `assetInventoryId == null`일 때 NPE가 나지 않게 방어했다.

## 무엇을 했나

### 1. 테스트 인프라와 CI 기준 강화

- 사용자 관점 효과: 신규/변경 API가 테스트 없이 배포되는 위험을 줄인다.
- 시스템 관점 효과: `check` 단계에서 Jacoco coverage verification이 실행되며, 85% 미만이면 빌드가 실패한다.
- 주요 파일:
  - `backend/build.gradle.kts`
  - `.gitlab-ci.yml`
  - `backend/src/test/resources/application-test.yml`
  - `backend/src/test/java/com/dotshelf/support/*`

핵심 설정:

```kotlin
tasks.named<org.gradle.testing.jacoco.tasks.JacocoCoverageVerification>("jacocoTestCoverageVerification") {
    violationRules {
        rule {
            limit {
                counter = "LINE"
                value = "COVEREDRATIO"
                minimum = "0.85".toBigDecimal()
            }
        }
    }
}
```

### 2. 도메인별 테스트 대량 보강

- Controller: 요청 매핑, validation, 응답 envelope, 인증 userId 주입 확인
- Service: 권한, 예외, 멱등성, 상태 변경, 이벤트 발행 확인
- Repository/JPA: unique 제약, custom query, soft delete, 정렬/페이징 확인
- Integration: 실제 HTTP 요청에서 DB 반영까지 이어지는 주요 플로우 확인

보강한 주요 도메인:

- Auth / Social login
- User / Follow / Block / Preference
- Space / Visit / Like / Theme / Guestbook whitelist
- Book / Bookshelf
- Asset / Purchase / Wishlist / Inventory
- ReadingRecord / Guestbook / Report / Notification
- Payment / INICIS
- Admin
- Global error / Security / Health
- S3 storage / 이미지 업로드 검증

### 3. 신규 소셜 가입 시 기본 Space 생성 보장

소셜 신규 가입은 user row만 만드는 것으로 끝나면 안 된다. DotShelf의 기본 UX는 가입 사용자가 자기 공간을 즉시 가져야 하므로, 신규 user 생성 흐름에서 `SpaceService.findOrCreateMySpace`를 호출하도록 연결했다.

핵심 효과:

- 신규 소셜 로그인 후 `users`, `user_social_accounts`, 기본 알림 설정, 기본 `spaces` 생성까지 검증
- 기존 소셜 로그인은 중복 생성 없이 기존 계정을 재사용

### 4. Bookshelf 데코 아이템 null inventory 방어

책장 아이템 중 `DECOR` 타입인데 `assetInventoryId`가 비어 있는 데이터가 들어올 수 있다. 기존 매핑이 이 값을 바로 사용하면 NPE가 날 수 있어서 null일 때 안전하게 응답을 구성하도록 보강했다.

## 왜 했나

기존 백엔드는 `spring-boot-starter-test`는 있었지만 실제 테스트 피라미드가 약했다. 특히 API가 많아졌는데 Controller / Service / Repository / Integration 테스트가 도메인별로 균등하지 않았고, 결제/관리자/소셜 로그인/권한 검증처럼 위험한 플로우도 일부만 확인하고 있었다.

이번 작업의 목적은 단순히 coverage 숫자를 올리는 것이 아니라, 앞으로 API를 추가할 때 테스트가 함께 따라오도록 기준선을 만드는 것이다. 그래서 Jacoco를 `check`에 연결하고, CI에서도 Testcontainers 기반 PostgreSQL 검증과 coverage artifact를 남기도록 했다.

## 어떻게 구현했나

### Testcontainers와 H2를 역할별로 나눔

빠른 단위/레이어 테스트는 기존 Spring test 기반을 유지하고, PostgreSQL 고유 제약이나 JPA 동작이 필요한 부분은 `-DrunPostgresTests=true`로 Testcontainers를 활성화했다.

이렇게 한 이유:

- 모든 테스트를 통합 테스트로 만들면 느리고 리뷰가 어려워진다.
- H2만 쓰면 PostgreSQL 제약조건, enum, timestamp, Flyway 차이를 놓칠 수 있다.
- CI에서는 명시적으로 `-DrunPostgresTests=true`를 넘겨 실제 DB 호환성을 같이 본다.

### rollback 통합 테스트와 AFTER_COMMIT 테스트를 분리

`IntegrationTestSupport`는 기본적으로 `@Transactional` rollback 격리를 사용한다. 이 방식은 테스트 데이터 정리가 단순하지만 실제 commit이 발생하지 않는다.

그래서 `@TransactionalEventListener(phase = AFTER_COMMIT)`처럼 commit 후 listener가 발사되어야 하는 흐름은 이 베이스를 상속하지 않고, 테스트별 cleanup으로 격리한다. 알림 flow처럼 production commit 경계가 중요한 테스트는 이 규칙을 따라야 한다.

### 테스트 지원 객체를 공통화

`TestFixtures`, `TestUsers`, `TestSpaces`, `TestBooks`, `TestAssets`, `IntegrationTestSupport`, `RepositoryTestSupport`를 추가해 테스트 데이터 생성 방식을 통일했다.

핵심 포인트:

- 테스트마다 builder 반복을 줄임
- id 세팅이 필요한 단위 테스트에서 `TestFixtures.setId`로 JPA 저장 없이 엔티티 상태를 구성
- 통합 테스트는 공통 header와 profile을 공유

### 고위험 플로우를 실패 케이스 중심으로 보강

Payment와 Admin, Auth verifier는 성공 케이스만으로 충분하지 않다. 그래서 다음 같은 실패 분기를 추가했다.

- INICIS 승인 실패, 주문번호 불일치, tid 누락, 중복 provider transaction
- 결제 return 실패 코드에서 외부 승인 호출이 반복되지 않는지
- Google/Kakao token type 불일치, 누락된 id/access token, redirectUri/client id 설정 누락
- Admin 신고 조치, 사용자 상태 변경, 강제 로그아웃, 잔액 조정, 환불, 감사 로그
- Guestbook cursor 오류, 답장 중복, post-it whitelist, 삭제 권한

## 배운 것

- **Coverage gate는 품질의 하한선이다**: 100%를 목표로 하는 대신, 85% 이상을 유지하면서 고위험 분기에 테스트를 집중하는 편이 실용적이다.
- **Controller 테스트와 Integration 테스트의 역할은 다르다**: Controller 테스트는 mapping/validation/envelope를 빠르게 확인하고, Integration 테스트는 실제 HTTP부터 DB 반영까지 이어지는 핵심 플로우만 검증하는 것이 적절하다.
- **Testcontainers는 CI runner 조건까지 같이 설계해야 한다**: PostgreSQL 테스트를 켜려면 GitLab runner에서 Docker dind와 privileged 설정이 필요하다.
- **결제 테스트는 멱등성이 핵심이다**: 성공 여부보다 중복 callback, 실패 승인, provider transaction 중복 같은 회귀가 실제 장애로 이어질 가능성이 높다.
- **테스트 데이터 팩토리도 아키텍처다**: fixture가 중구난방이면 테스트가 늘어날수록 유지보수가 어려워진다.

## 더 공부할 것

- [ ] Jacoco line coverage와 branch coverage의 차이 — 현재 gate는 line 기준이므로 조건 분기 품질을 어떻게 추가로 볼지 정해야 한다.
- [ ] Mutation testing 적용 가능성 — 테스트가 줄만 실행하는지, 실제 버그를 잡는지 확인하는 방법을 검토해야 한다.
- [ ] Testcontainers CI 최적화 — Docker dind 비용, 병렬 실행, 컨테이너 재사용 전략을 정리해야 한다.
- [ ] Spring Boot 4 테스트 슬라이스 — `@WebMvcTest`, `@DataJpaTest`, `@SpringBootTest` 경계를 팀 규칙으로 더 명확히 만들 수 있다.

## 참고

- 최종 검증:

```text
./gradlew.bat clean build -DrunPostgresTests=true

BUILD SUCCESSFUL
tests=497 failures=0 errors=0 skipped=0
Jacoco excluded line coverage=86.25%
covered=3231 missed=515 total=3746
```

- MR 설명 초안: `backend/docs/test-coverage-mr-description.md`
