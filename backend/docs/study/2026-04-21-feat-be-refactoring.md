# 2026-04-21 · feat/be/refactoring — 아키텍처 정비 학습 노트

"좋은 학생 프로젝트" 수준의 코드를 "실무 신입이 바로 붙어도 되는" 수준으로 올리기 위해 4개 감사 에이전트를 병렬로 돌려 현상을 파악하고, 리뷰 리포트에 따라 **Stage 1 + 2 + 3 + 5** 를 한 MR 에 묶어 반영한 날.

연결 문서:
- 리뷰 리포트: [`docs/architecture/2026-04-21-review-report.md`](../architecture/2026-04-21-review-report.md)
- 아키텍처 컨벤션: [`docs/architecture/patterns.md`](../architecture/patterns.md)
- 인프라 작업 기록: [`docs/infra/2026-04-21-runner-and-jwt.md`](../infra/2026-04-21-runner-and-jwt.md)

## TL;DR

- **4-에이전트 병렬 감사** 로 Swagger/레이어/횡단 관심사/컨벤션 문서 현황 수집 → 종합 리포트 작성
- 치명 구멍 4개 확인: `Security permitAll`, `Cursor pagination 허위`, `UTC 명세 위반`, `Swagger 전무`
- **Clean Architecture 는 의도적으로 풀 적용 안 함** (비용 > 이득 판단). 대신 Repository Custom+Impl 패턴(Lite) 을 patterns.md 에 공식화
- `BaseEntity` / `User` / `AuthRefreshToken` 의 시간 필드를 `LocalDateTime → Instant` 로 마이그레이션
- `JwtTokenProvider` / `JwtAuthenticationFilter` / `SecurityEntryPoints` 신설, `SecurityConfig` 를 화이트리스트 + JWT 필터 체인으로 개편
- `springdoc-openapi` 2.8 도입, 12개 컨트롤러에 `@Tag` 부착 (3개는 메서드별 `@Operation` 까지)
- `MdcRequestIdFilter` 로 `X-Request-Id` 승계·발급, `logback-spring.xml` 의 `%X{requestId}` 패턴으로 분산 추적 기반 마련
- `GlobalExceptionHandler` 가 검증 실패 시 전 필드 에러를 수집·조인해 반환하도록 교체
- `exemplars/User.java` 를 실제 운영 코드와 동기화 (비즈니스 메서드 포함, `Instant` 필드)
- 커밋 3개로 분리해 `feat/be/refactoring` 브랜치에 푸시 완료

## 무엇을 / 왜 / 어떻게

### 1. 의사결정: Clean Architecture 를 풀로 적용하지 않은 이유

**무엇**: Controller → Service → Repository 의 레이어드 아키텍처는 유지. Domain Entity 와 JPA Entity 를 분리하지 않고, Port/Adapter 도 도입하지 않음.

**왜**:
- 프로젝트 수명 1년 내, 프레임워크 교체 계획 없음 → CA 핵심 이득(프레임워크 중립성) 이 무의미
- 13개 도메인에서 모든 엔티티를 둘로 쪼개고 Mapper 를 다는 비용 > 얻는 이득
- 주니어 포트폴리오에서 CA 풀 적용은 "카고 컬트" 로 보일 위험 — 근거 질문 못 받아내면 감점
- 대신 `Repository Custom + Impl` 패턴으로 **복잡 쿼리 캡슐화** 라는 CA 의 실용적 부분만 취함

**어떻게**: `patterns.md §1` 에 의사결정을 명시적으로 기록. 면접 시 "왜 CA 안 썼나?" 에 답할 수 있는 근거 확보.

### 2. 시간/타임존: `LocalDateTime` → `Instant`

**무엇**: `BaseEntity.createdAt/updatedAt`, `User.lastLoginAt/deletedAt`, `AuthRefreshToken.issuedAt/expiresAt/revokedAt/reuseDetectedAt` 을 전부 `Instant` 로 교체.

**왜**:
- Notion API 명세 "시간: ISO-8601 UTC" 준수 필요
- `LocalDateTime` 은 타임존 정보 없음 → 서버 위치·JDBC 드라이버 기본값에 따라 해석이 달라져 모바일 다국가 출시 시 버그 위험
- `Instant` 는 정의상 UTC. 국제 표준시 하나로 통일해서 저장·전송

**어떻게**:
- 엔티티 필드 타입 교체 + `Instant.now()` 사용
- `application.yml` 에 `spring.jackson.time-zone: UTC`, `spring.jpa.properties.hibernate.jdbc.time_zone: UTC` 세팅
- `application-prod.yml` 의 기존 `time_zone: Asia/Seoul` 을 UTC 로 변경
- PostgreSQL 컬럼은 Hibernate 가 `TIMESTAMP WITH TIME ZONE` 으로 매핑 (ddl-auto update)

### 3. 인증: JWT 필터 체인 신규 구축

**무엇**: `SecurityConfig` 가 전 요청 `permitAll()` + `CurrentUserArgumentResolver` 가 `X-User-Id` 헤더 값을 그냥 신뢰하던 구조를 걷어내고, 정통 `Authorization: Bearer {jwt}` 방식으로 재설계.

**왜**:
- 이 전 상태로는 **누구든 `X-User-Id: 1` 헤더만 붙이면 1번 유저 행세 가능** — 보안 구멍이라기보단 인증이 아예 없음
- Notion 명세 = `Authorization: Bearer {accessToken}` + refresh rotation
- Spring Security 의 기본 필터 체인 위에 JWT 필터만 얹는 표준 패턴이 가장 명료

**어떻게**:
- `JwtProperties` — `@ConfigurationProperties(prefix="jwt")` 로 바인딩. 기동 시 `secret.length() >= 32` 강제
- `JwtTokenProvider` — HS256 + `typ` claim 으로 access/refresh 구분. `jjwt 0.12.6` 사용
- `JwtAuthenticationFilter` — `OncePerRequestFilter` 상속, `Bearer` 파싱 → `SecurityContextHolder` 에 `UsernamePasswordAuthenticationToken` 세팅
- `SecurityEntryPoints` — 미인증/권한부족 응답을 `ApiResponse` envelope 로 통일
- `SecurityConfig` — 화이트리스트(`/api/health`, `/v1/auth/**`, `/swagger-ui/**`, `/v3/api-docs/**`) + `authenticated()`
- `CurrentUserArgumentResolver` — 헤더 신뢰 코드 제거, `SecurityContext.principal` 에서 userId 추출
- `AuthService` — 기존 UUID 플레이스홀더 토큰을 실제 JWT 로 교체. refresh rotation + reuse detection 로직은 **그대로 유지**

### 4. Swagger/OpenAPI 도입

**무엇**: `springdoc-openapi-starter-webmvc-ui 2.8` 도입, `OpenApiConfig` 빈으로 Bearer 스킴 선언, 12개 컨트롤러에 `@Tag` 부착. 핵심 3개 (`Auth`/`User`/`Health`) 는 메서드별 `@Operation` + `@SecurityRequirements` 까지 상세화.

**왜**:
- API 문서가 Notion 에만 있어서 코드와 실시간 싱크 불가능
- 프론트가 스펙을 실시간으로 확인할 수 있어야 협업 비용 ↓
- 포트폴리오 관점에서 Swagger UI 스크린샷은 큰 어필 포인트

**어떻게**:
- 의존성 추가 (`org.springdoc:springdoc-openapi-starter-webmvc-ui:2.8.0`)
- `OpenApiConfig` 에서 `SecurityScheme` Bearer 정의 + `SecurityRequirement` 전역 적용
- `application.yml` 에 `springdoc.swagger-ui.path=/swagger-ui.html` 등 지정
- 비로그인 API 는 `@SecurityRequirements({})` 로 Bearer 요구 해제
- 나머지 10개 컨트롤러의 메서드별 `@Operation` 은 후속 MR 로 분리

### 5. 검증 에러 전 필드 수집

**무엇**: `GlobalExceptionHandler.handleValidation()` 이 `findFirst().map(...)` 로 **첫 에러만** 반환하던 것을 `stream().map(...).collect(joining(", "))` 로 전 필드 수집.

**왜**: 프론트가 폼 전체 검증 상태를 한 번에 받을 수 있어야 UX 가 좋음. 사용자가 한 필드씩 고치면서 왕복 호출하는 거 방지.

**어떻게**: `FieldError` 를 `field: message` 형태로 포맷 → `, ` 로 조인. 비어 있으면 `ErrorCode.INVALID_INPUT.getMessage()` fallback.

### 6. MDC 기반 correlation id

**무엇**: `MdcRequestIdFilter` 를 추가해서 요청당 `X-Request-Id` 헤더를 MDC 에 주입. `logback-spring.xml` 패턴은 `%X{requestId}` 를 포함해 모든 로그에 자동으로 id 찍힘.

**왜**:
- 장애 발생 시 "이 사용자가 친 요청의 전체 로그" 를 한 번에 뽑기 위해
- 분산 시스템으로 확장될 때 (Grafana/Loki 도입 후) 바로 쓸 수 있는 기반

**어떻게**:
- `OncePerRequestFilter` 에서 `X-Request-Id` 헤더가 있으면 그대로, 없으면 UUID16 자 신규 발급
- `MDC.put("requestId", id)` / `finally { MDC.remove() }` 로 누수 방지
- 응답에도 같은 `X-Request-Id` 헤더를 실어 보내 클라이언트가 지원 요청 시 함께 보낼 수 있도록

### 7. exemplar User.java 동기화

**무엇**: `exemplars/User.java` 가 기본 필드 2개짜리 miniature 였던 걸 실제 운영 `User.java` 와 동일하게 교체. `updateProfile()` / `completeOnboarding()` / `markLogin()` / `delete()` 4개 비즈니스 메서드 포함. 시간 필드는 `Instant`.

**왜**:
- exemplar 가 실제 코드와 드리프트되면 "거울처럼 따르라" 는 목적이 깨짐
- 신규 팀원이 User exemplar 만 보고 시작했을 때 실제 스타일을 알 수 있어야 함

**어떻게**:
- 파일 상단에 `갱신일 2026-04-21` 주석
- 주요 포인트(BaseEntity 상속, `@NoArgsConstructor(PROTECTED)` + `@Builder`, Instant, 상태 메서드, 물리 삭제 금지) 를 주석으로 명시
- `exemplars/README.md` 에 Controller/Service/Repository/DTO exemplar 는 "추후 추가" 로 남겨둠

## 배운 것

- **에이전트 병렬화의 효용**: 4개를 동시에 돌려서 정적 감사 5분 + 종합 15분 = 20분. 직렬이었으면 1시간 이상. "독립적인 조사" 에서는 에이전트 팀이 진짜 빠르다.
- **판단 근거가 있는 "안 함"** 이 "다 함" 보다 가치가 클 수 있다. Clean Architecture 를 쓸지 안 쓸지가 아니라 **"왜 이 규모엔 과하다" 를 설명할 수 있는가** 가 핵심. 포트폴리오에서 "의사결정 흔적" 은 코드만큼 중요.
- **`LocalDateTime` 은 함정**. 타임존 없는 시간 타입은 "서버 시간" 이라는 환상이고, 서버가 다국 배포되거나 컨테이너 TZ 가 바뀌는 순간 의미가 흔들린다. 새 프로젝트 시작할 때 반사적으로 `Instant` 선택하는 습관을 만들자.
- **`permitAll()` 은 "나중에 막자" 의 시그널**. 나중은 절대 오지 않는다. 초기 setup 시점에 화이트리스트 방식으로 짜두면 나중 수정 비용 거의 0, 안 짜두면 보안 리뷰 때 전수 재설계.
- **Spring Security 필터 체인 구조** — `addFilterBefore(jwtFilter, UsernamePasswordAuthenticationFilter.class)` 로 위치를 잡아주면 그 뒤 `authorizeHttpRequests` 가 자연스럽게 SecurityContext 의 principal 을 쓴다. 필터 순서는 외우지 말고 **"표준 필터 앞에 커스텀을 끼운다"** 만 기억.
- **OpenAPI 스펙은 `@Tag` 하나만 붙여둬도 Swagger UI 에서 그룹핑이 살아난다**. 메서드별 `@Operation` 은 완성도 작업. 완벽주의에 갇혀 "다 되면 머지" 가 아니라, 최소 viable 부터 끊어가는 게 리뷰 회전율상 유리.
- **검증 에러 수집**: "첫 에러만" 은 3초 생각 안 한 디폴트. 프론트 관점을 잠깐 떠올리면 바로 드러나는 구멍.
- **MDC 는 공짜에 가까운 관찰성 투자**. 한 번 filter 심어두면 이후 쭉 혜택. Grafana/Loki 를 안 쓰더라도 `grep requestId=abc123 app.log` 만으로도 디버깅 쾌감이 다르다.

## 더 공부할 것

- **JWT 탈취 시나리오 전반** — HttpOnly 쿠키 vs Authorization 헤더, refresh 저장 위치 (메모리/쿠키/보안스토리지), CSRF 와 상호작용. 현재는 헤더 기반 + DB refresh. 모바일 앱 중심이라 괜찮지만 웹 대시보드 추가 시 쿠키 전환 검토.
- **Cursor pagination 구현 디테일** — base64 인코딩 포맷, 마지막 레코드가 아닌 "정렬 키 + tie-breaker" 조합, stable sort. Stage 4 에서 `User` 도메인에 시범 구현 예정.
- **Hibernate 6+ 의 `Instant` 매핑 동작** — `TIMESTAMP` vs `TIMESTAMPTZ` 중 어느 쪽으로 DDL 이 찍히는지, `hibernate.jdbc.time_zone` 과 `@JdbcTypeCode(SqlTypes.TIMESTAMP_WITH_TIMEZONE)` 의 상호작용. 지금은 `ddl-auto=update` 로 넘어갔지만 Flyway 전환 시점에 명시 필요.
- **Spring Security 6.x 의 `AuthorizationManager` 스타일** — `hasRole()`, `hasAuthority()`, 커스텀 `AuthorizationManager<RequestAuthorizationContext>` 로 URL 기반 규칙 세밀화. 아직 단일 `ROLE_USER` 만 있지만 관리자 콘솔 생기면 필요.
- **`@ConfigurationProperties` vs `@Value`** — 지금 `JwtProperties` 는 전자. 여러 필드를 하나의 record 로 묶고 compact constructor 에서 검증까지 하는 패턴이 깔끔한데, Spring Boot 버전/버전별 지원 차이를 한 번 더 확인.

## 참고

- [Spring Security Reference — Servlet](https://docs.spring.io/spring-security/reference/servlet/index.html)
- [jjwt GitHub README](https://github.com/jwtk/jjwt)
- [springdoc-openapi Documentation](https://springdoc.org/)
- [Baeldung — `Instant` vs `LocalDateTime`](https://www.baeldung.com/java-localdatetime-timezone) (보조 자료, 핵심은 JDK `java.time` javadoc)
- [MDN — HTTP Bearer scheme (RFC 6750)](https://datatracker.ietf.org/doc/html/rfc6750)

## 다음 MR 에서 할 것 (Stage 4)

- `CursorUtils` 유틸 + `AssetService` 실제 cursor 기반 재구현
- 다른 리스트 조회를 `CursorResponse` 로 통일
- `UserService.java:95-96` 의 N+1 루프 제거 (배치 조회)
- `User` 도메인을 QueryDSL `Custom + Impl` 패턴 첫 exemplar 로 시범 구현
