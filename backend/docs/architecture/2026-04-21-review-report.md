# 2026-04-21 · DotShelf 백엔드 아키텍처 리뷰

작성자: Claude (4-에이전트 병렬 감사 + Notion API 명세/DB 스키마 교차 대조 기반)
대상 브랜치: `feat/be/refactoring` (dev 기준)
리뷰 축: Swagger/OpenAPI · 레이어드 아키텍처 · 횡단 관심사 · 컨벤션 문서

---

## TL;DR

- **아키텍처 품질 자체는 SSAFY 기준으로 꽤 좋음**. 도메인 기준 패키지, Controller/Service/Repository 책임 분리, BaseEntity 상속, ApiResponse 통일, CustomException 일관 사용까지 기본이 탄탄함.
- **그러나 "운영/포트폴리오 수준" 으로 가려면 4개 핵심 구멍이 있음**: ① Swagger 전무 ② JWT 인증 미적용 (permitAll + `X-User-Id` 헤더 신뢰) ③ Cursor 페이지네이션이 실제론 offset ④ `LocalDateTime` 로 UTC 명세 위반.
- **patterns.md 는 좋지만 exemplars 가 빈약**. 실제 코드가 문서보다 더 충실한 상태 (드문 케이스).
- 수정 예상: 핵심 4개 구멍 + 예외 핸들러 + 도메인별 Swagger 어노테이션 = **파일 약 40개 변경**.

---

## 현재 상태 요약

### 🟢 잘 되어 있는 것 (유지 권장)

| 항목 | 근거 |
|------|------|
| 도메인 기준 패키지 | `domain/{user, book, bookshelf, follow, auth, asset, feed, guestbook, notification, payment, readingrecord, report, space}` 13개 도메인 |
| Controller 계약 | 전 메서드가 `ApiResponse<T>` 래핑, 엔티티 직접 반환 0건 |
| Service 트랜잭션 규칙 | 클래스 레벨 `@Transactional(readOnly=true)` + 쓰기 메서드만 `@Transactional` |
| 예외 계층 | 모든 비즈니스 예외를 `CustomException(ErrorCode.*)` 로 통일 (54개 코드 도메인별) |
| Entity 불변성 | 전 엔티티 `extends BaseEntity`, `@NoArgsConstructor(access=PROTECTED)`, setter 금지 |
| Refresh Token Rotation | `AuthRefreshToken.rotateTo()` + `markReuseDetected()` 구현됨 |

### 🔴 치명 이슈 (머지 전 반드시)

#### 1. Security: JWT 가 사실상 **아무것도 안 함**
- `SecurityConfig.java:18` — `.anyRequest().permitAll()` 로 전 요청 허용
- `CurrentUserArgumentResolver.java:28-31` — `X-User-Id` 헤더 값을 그냥 신뢰해서 userId 주입
- 즉 **어떤 클라이언트든 `X-User-Id: 1` 헤더만 붙이면 1번 유저 행세 가능**
- Notion 명세는 `Authorization: Bearer {accessToken}` 요구
- 영향: **이 상태로 프로덕션 올리면 모든 API 가 인증 우회됨**

#### 2. Cursor 페이지네이션이 실제로는 offset=0 고정
- `AssetService.java:46` — `PageRequest.of(0, Math.min(limit, 50), sort)` → **항상 첫 페이지**
- `CursorResponse<T>` 클래스는 정의만 있고 실사용 거의 없음
- 즉 **무한 스크롤/페이지 넘김 불가**, API 명세 위반

#### 3. 시간대 UTC 명세 위반
- `BaseEntity.java:20-21` — `LocalDateTime` (타임존 없음)
- Jackson 설정 없어서 JSON 직렬화 시 타임존 정보 유실
- Notion 명세 "시간: ISO-8601 UTC" 위반
- 영향: 모바일 앱이 여러 타임존에서 쓰일 때 시간 어긋남

#### 4. GlobalExceptionHandler 가 검증 에러 **하나만** 반환
- `GlobalExceptionHandler.java:28-30` — `findFirst().map(FieldError::getDefaultMessage)`
- 예: `{provider:필수, accessToken:필수}` 둘 다 빠진 요청 → 첫 번째만 내려감
- 프론트가 폼 전체 검증 메시지를 한 번에 못 받음

#### 5. Swagger/OpenAPI **전무**
- `build.gradle.kts` 에 springdoc 의존성 없음
- `@Tag @Operation @Schema` 사용률 **0%** (13개 Controller 중 0개)
- API 문서가 Notion 에만 있음 → 코드와 실시간 싱크 불가능
- 포트폴리오 관점: **압도적으로 큰 점수 깎임 요인**

### 🟡 중요 이슈 (이번 PR에서 함께 처리 권장)

#### 6. `ApiResponse.isSuccess` 가 `Boolean` (래퍼)
- `ApiResponse.java:14` — null 될 수 있음. 원시형 `boolean` 으로.

#### 7. N+1 쿼리 패턴 1건 발견
- `UserService.java:95-96` — 팔로워 리스트를 루프로 하나씩 조회
  ```java
  followRepository.findByFollowingUserId(currentUserId).stream()
      .map(follow -> UserSummaryResponse.from(getUser(follow.getFollowerUserId())))
  ```
- 팔로워 많을수록 DB 왕복 선형 증가. 배치 조회 또는 QueryDSL fetch join 필요.

#### 8. patterns.md 는 QueryDSL Custom Repository 패턴을 명시하지만 구현 0건
- 전 34 Repository 가 `JpaRepository` 만 상속
- 복잡 조회 필요 지점 (Book search, Feed filtering, Asset 커서) 에서 유지보수 어려워짐
- 패턴 문서와 코드의 gap → exemplar 로 1~2개 도메인에서 시범 구현 필요

#### 9. MDC / Correlation ID 미사용
- `logback-spring.xml` 없음
- 한 요청의 전 로그 추적 불가
- 장애 발생 시 디버깅 비용 큼

#### 10. `exemplars/User.java` 가 실제 구현보다 빈약
- 실제 `User.java` 는 `updateProfile()`, `completeOnboarding()`, `markLogin()`, `delete()` 4개 비즈니스 메서드 보유
- exemplar 은 기본 필드만 → 참고 가치 낮음. **실제 코드 기준으로 업데이트 필요**

### 🟢 추가 개선 권장 (선택)

| 항목 | 이유 |
|------|------|
| Jackson ObjectMapper Bean | UTC 타임존 + ISO-8601 포맷 일관성 |
| MessageSource | 에러 메시지 다국어 대응 (모바일 앱 글로벌 출시 시) |
| `@RestControllerAdvice` + `@ExceptionHandler(Throwable.class)` | 진짜 최종 catch-all 추가 |

---

## patterns.md 개정 방향

### 유지할 것
- 레이어 책임 구분
- Entity 불변성/Builder 규칙
- Service @Transactional 규칙
- DTO record 패턴
- 네이밍 규칙

### 추가/보완할 것
1. **인증 섹션 신설** — `Authorization: Bearer` 규약, `@CurrentUser` 사용법, JWT 검증 흐름
2. **시간 섹션 신설** — `Instant` 또는 `OffsetDateTime` 강제, Jackson 설정 위치
3. **Pagination 섹션 신설** — CursorResponse 사용 규칙, 기본 20/최대 50, base64 cursor 인코딩 가이드
4. **Repository 패턴 섹션 갱신** — "복잡 쿼리는 QueryDSL Custom + Impl 분리" 예제 코드 추가. 단순 조회는 method name 허용.
5. **OpenAPI 문서화 규칙 신설** — `@Tag/@Operation/@ApiResponses/@Schema` 필수 기준, springdoc 위치
6. **예외 핸들러 규칙** — 검증 에러 수집 정책, 에러 코드 prefix 체계 (13 도메인 reserved)
7. **exemplar 갱신 규칙** — 도메인 추가 시 checklist

### exemplars 보강
- `User.java` 실사용 코드로 갱신 + 어노테이션 의미 주석
- `UserController.java` 모범 예시 추가 (ApiResponse + @Operation + @CurrentUser)
- `UserService.java` (class-level readOnly + method @Transactional)
- `UserRepository.java` + `UserRepositoryCustom.java` + `UserRepositoryImpl.java` (QueryDSL 예시)
- `UserResponse.java` / `UpdateUserRequest.java` (record + @Valid + @Schema)

---

## 제안 수정 순서 (Phase 3)

> 각 단계마다 빌드/테스트 통과 확인 후 다음 단계.

### 🥇 Stage 1 — 기반 정비 (patterns + config)
1. `patterns.md` + `checklist.md` 개정 (인증/시간/페이지네이션/Repository/OpenAPI 섹션 추가)
2. `BaseEntity` `LocalDateTime` → `Instant`
3. `Jackson` ObjectMapper Bean — UTC + ISO-8601
4. `ApiResponse.isSuccess` `Boolean` → `boolean`
5. `GlobalExceptionHandler` 검증 에러 전수집 + correlation ID (MDC)
6. `logback-spring.xml` + MDC 필터 추가

### 🥈 Stage 2 — 보안 정비
7. `JwtTokenProvider` 구현 (서명 / 만료 / claims)
8. `JwtAuthenticationFilter` 구현 (Authorization 헤더 파싱 + SecurityContext 세팅)
9. `SecurityConfig` 화이트리스트(`/api/health`, `/swagger-ui/**`, `/v3/api-docs/**`, `/api/auth/**`) 외에는 `authenticated()`
10. `CurrentUserArgumentResolver` 를 **SecurityContext 기반** 으로 변경 (헤더 신뢰 제거)
11. 로그인/리프레시 플로우에서 JWT 서명된 토큰 발급으로 전환

### 🥉 Stage 3 — Swagger
12. `build.gradle.kts` 에 `springdoc-openapi-starter-webmvc-ui` 2.8+
13. `OpenApiConfig` — `@Bean OpenAPI` (Bearer 스킴 + server url + info)
14. 모든 Controller 에 `@Tag`, 메서드마다 `@Operation + @ApiResponses`
15. 주요 DTO record 에 `@Schema(description, example)`
16. Security 화이트리스트에 Swagger 경로 포함

### 🏅 Stage 4 — 페이지네이션/성능
17. Cursor 인코더 유틸 (`CursorUtils` — 마지막 id + 시간 base64)
18. `AssetService` 커서 기반으로 실구현
19. 다른 리스트 조회 (Book, Feed, Reading Record 등) `CursorResponse` 로 통일
20. `UserService` N+1 제거 (배치 조회)
21. QueryDSL Custom Repository 패턴 시범 적용 — **User 도메인을 exemplar 로**

### 🎖 Stage 5 — exemplar 갱신
22. `exemplars/User.java` 실제 코드 반영
23. `exemplars/UserController.java`, `UserService.java`, `UserRepository + Custom + Impl.java`, `UserRequest/Response.java` 추가
24. `exemplars/README.md` 인덱스 정리

---

## 수정 파일 예상 개수

| Stage | 파일 수 | 추정 시간 |
|-------|--------|-----------|
| 1. 기반 | ~8 | 1.5h |
| 2. 보안 | ~5 | 2h |
| 3. Swagger | ~20 | 2h (Controller/DTO 많음) |
| 4. 페이지네이션 | ~10 | 2h |
| 5. exemplar | ~7 | 1h |
| **합계** | **~50** | **8~9시간** |

단번에 다 하기 버겁다면 Stage 1~3 (기반+보안+Swagger) 를 이번 MR 로 끊고, Stage 4~5 를 다음 MR 로 분리 가능.

---

## 추가 코멘트

- **N+1 의심 구간은 UserService 외에도 잠복** 가능성 높음. Stage 4 때 전수 스캔 권장.
- **Notion API 명세 78개** 와 실제 Controller 13개 사이 갭 큼. 이번 리뷰 범위 아니지만 곧 순차 구현 필요.
- **DB 분리 (Phase 2)** 는 이 리팩토링과 독립. 먼저 해도, 나중에 해도 영향 없음.

---

## 변경 이력

- 2026-04-21 · 4-에이전트 병렬 감사 결과 최초 작성
