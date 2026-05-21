# DotShelf Backend Architecture Patterns

이 문서는 DotShelf 백엔드의 **단일 진실 소스**다. Claude와 Codex가 코드를 생성할 때 이 문서와 `exemplars/`를 **반드시** 먼저 읽고 따른다.

새 패턴이 생기면 여기서 수정하고, 기존 규칙과 충돌하면 이 문서가 우선한다. 코드 리뷰에서 이 규칙을 벗어난 게 발견되면 여기를 고칠지 코드를 고칠지 둘 중 하나를 고른다.

---

## 1. 전체 구조

**레이어드 아키텍처**: Controller → Service → Repository → (Entity/DB)

- **Controller** — HTTP 경계. 요청 검증, 인증 컨텍스트 추출, 응답 래핑. 비즈니스 로직 금지.
- **Service** — 비즈니스 로직의 유일한 소유자. 트랜잭션 경계(`@Transactional`)는 여기서 열림.
- **Repository** — DB 접근의 유일한 경로. 단순 조회는 Spring Data JPA 메서드, 복잡 조회는 QueryDSL Custom + Impl.

**금기:**
- Controller → Repository 직접 호출 ❌
- Service에서 HTTP 타입(HttpServletRequest, ResponseEntity) 참조 ❌
- Repository에 비즈니스 로직 ❌

**패키지 구조**:
```
com.dotshelf
├── global/{config, common, error, security}   ← 횡단 관심사
└── domain/<name>/{controller, service, repository, entity, dto}
```

도메인 간 직접 의존은 피하고, 꼭 필요하면 Service 경유. 순환 의존은 설계 재검토 신호.

> 📌 **Clean Architecture 를 적용하지 않는 이유**: 프레임워크/DB 교체 계획이 없고 도메인 규모가 13개 수준이라, JPA Entity 와 Domain Entity 를 분리하는 Mapper 비용이 이득을 초과한다. 대신 Repository 레이어에서 QueryDSL Custom 인터페이스로 복잡 쿼리의 캡슐화 이득만 취한다.

---

## 2. 엔티티 규칙

### 필수 규칙
- `BaseEntity`를 **반드시 상속** (createdAt/updatedAt 자동 관리, UTC 보관)
- `@NoArgsConstructor(access = AccessLevel.PROTECTED)` — JPA 프록시용, 외부 new 금지
- 생성은 **오직 `@Builder`** 로만. setter 금지.
- `@Table(name = "...")` 명시, 다수 테이블 네이밍은 **snake_case 복수형** (`users`, `bookshelves`)
- 식별자: `@Id @GeneratedValue(strategy = IDENTITY)` + `Long id`

### 필드 규칙
- 컬럼 이름 기본은 snake_case로 자동 매핑되므로 `@Column(name=...)`은 **이름이 다를 때만** 명시
- `nullable`, `unique`, `length`는 의미있을 때 명시 (예: `@Column(nullable=false, unique=true, length=100)`)
- 컬렉션 필드는 **지연 로딩 기본** (`@OneToMany(fetch = LAZY)`) — EAGER 금지
- 양방향 연관관계는 **한쪽이 mappedBy 주인**. FK 컬럼 가진 쪽이 주인.
- **시간 필드는 `Instant` 사용** (UTC). `LocalDateTime` 금지 — "6절 시간/타임존" 참조.

### 비즈니스 메서드
- 엔티티 내부 상태를 바꾸는 연산은 **엔티티 메서드**로 (setter 대신). 예: `user.updateProfile(...)`, `user.delete()`.
- Service 는 `user.updateProfile(...)`만 호출하고, 검증/invariant는 엔티티가 스스로 지킨다.
- Soft delete 도 엔티티 메서드 (`user.delete()`) 로 표현 — Service 가 `setDeletedAt()` 직접 세팅 ❌.

### 예시
엔티티 모범 예시는 [`exemplars/User.java`](./exemplars/User.java) 참조. 신규 엔티티는 이걸 거울삼아 구조/어노테이션 순서 맞춘다.

---

## 3. DTO 규칙

- **Request DTO**: `record`로 작성. Bean Validation 애노테이션 (`@NotBlank`, `@Email`, `@Size` 등) 필수.
- **Response DTO**: `record`로 작성. 엔티티를 그대로 반환하지 말 것 (순환 참조/LAZY 예외 방지).
- **변환 책임**: Service에서 Entity ↔ DTO 변환. Controller는 Service에서 받은 DTO 그대로 전달.
  - 엔티티 → DTO: `static UserResponse from(User user)` 팩토리 메서드
  - DTO → 엔티티: DTO에 `toEntity()` 두거나 Service에서 변환
- 엔티티 자체를 Controller가 받거나 반환하지 않는다. 항상 DTO.
- **Swagger 스키마**: 공개 API 의 DTO 에는 `@Schema(description=..., example=...)` 를 필드마다 붙인다 — "13절 OpenAPI" 참조.

### DTO 묶기
- 도메인 DTO 가 많으면 `UserDtos` 같이 **정적 중첩 record 컨테이너** 허용.
- 또는 개별 파일로 분리. 둘 중 **도메인 내부적으로는 일관되게** 간다.

---

## 4. Service 규칙

- 클래스 레벨 `@Service` + `@RequiredArgsConstructor` (생성자 주입, final 필드)
- **읽기 전용 기본**: 클래스에 `@Transactional(readOnly = true)`, 쓰기 메서드에만 `@Transactional`
- 예외는 **반드시 `CustomException(ErrorCode.*)`** 사용. `throw new RuntimeException(...)` 금지.
- 도메인 객체의 상태 변경은 엔티티 메서드에 위임 (직접 setter 금지)
- 다른 도메인 Service를 주입할 수 있으나, 순환 주입은 설계 재고
- **반환 타입은 항상 DTO**. 엔티티는 Service 경계 밖으로 새면 안 됨.
- Facade 레이어는 **Service 들을 조율해야 하는 실제 케이스가 나타났을 때** 도입 (preemptive 도입 금지).

---

## 5. Repository 규칙

### 기본
- `extends JpaRepository<T, Long>` 로 단순 조회/변경 제공
- 메서드 이름 컨벤션: `findByEmail`, `existsByEmail`, `countByStatus` — Spring Data 네이밍 따름
- **메서드명 쿼리가 3개 조건을 넘기면 Custom 패턴으로 이관** (가독성 경계선)

### 복잡 조회 — Custom + Impl 패턴
복잡/동적/QueryDSL 이 필요한 조회는 **반드시 분리**한다. 레이어드 아키텍처에서 "도메인 언어 인터페이스 + JPA 구현 분리" 라는 Clean Architecture 의 Lite 버전.

```java
// 1) Spring Data 인터페이스는 Custom 을 extends
public interface UserRepository
        extends JpaRepository<User, Long>, UserRepositoryCustom {
    Optional<User> findByEmail(String email);
}

// 2) 도메인 언어 인터페이스 (의도만 기술)
public interface UserRepositoryCustom {
    List<User> searchActiveByNickname(String keyword, int limit, Long cursorId);
}

// 3) QueryDSL 구현체 (네이밍은 *Impl)
@Repository
@RequiredArgsConstructor
public class UserRepositoryImpl implements UserRepositoryCustom {
    private final JPAQueryFactory queryFactory;
    // QueryDSL 조건 조립
}
```

### N+1 방지
- `@EntityGraph`, QueryDSL `fetchJoin()`, 또는 페치 조인 JPQL
- `foos.stream().map(foo -> repo.findById(foo.getBarId()))` 패턴 **금지** (loop 내 쿼리)

### 페이징
- 리스트 API 는 **Cursor 기반** 사용 ("12절 Pagination" 참조)
- 관리자 툴 등 예외는 `Pageable/Page<T>` 허용

---

## 6. 시간 / 타임존

### 저장 / 전송 모두 **UTC (ISO-8601)**

- 엔티티 시간 필드: `java.time.Instant` 사용 (항상 UTC, 타임존 정보 포함)
- DTO 시간 필드도 `Instant` — Jackson 이 `2026-04-21T04:05:39Z` 형태로 직렬화
- **`LocalDateTime` 사용 금지** — 타임존 없어서 서버 위치에 따라 의미가 달라짐
- 날짜만 필요한 경우 `LocalDate` 허용 (예: 생일)

### Hibernate / DB
- PostgreSQL 컬럼 타입은 `TIMESTAMP WITH TIME ZONE` (TIMESTAMPTZ)
- `spring.jpa.properties.hibernate.jdbc.time_zone=UTC` 설정으로 JDBC 가 UTC 로 저장하도록 강제

### 표시용 변환
- 클라이언트가 "서울 시간" 으로 보여줘야 하면 클라이언트가 `Instant` 받아서 자기 타임존으로 변환
- 서버에서 포맷 책임지지 않음 — 국제화/타임존은 프레젠테이션 레이어의 문제

---

## 7. Controller 규칙

- `@RestController` + `@RequestMapping("/v1/<domain>")` 기본 (API 버저닝을 path 로 표현)
- 모든 응답은 **`ApiResponse<T>`로 래핑**. 성공 → `ApiResponse.success(data)`, 예외는 던지면 `GlobalExceptionHandler`가 처리.
- Path는 **소문자 kebab-case**, 리소스 이름은 **복수형** (`/v1/users`, `/v1/bookshelves/{id}/items`)
- 단, 인프라성 엔드포인트 (헬스체크 등) 는 `/api/*` 유지 — 모니터링/배포 파이프라인이 이 경로를 가정
- 요청 바디는 `@RequestBody @Valid <RequestDto>`로 검증
- 경로 변수는 `@PathVariable Long id` 그대로. Service에 id 넘겨서 처리.
- 인증 컨텍스트는 `@CurrentUser Long userId` — "11절 인증" 참조
- HTTP 상태 코드는 기본 200. 생성은 `@ResponseStatus(HttpStatus.CREATED)` 또는 Service 가 `ApiResponse` 의 적절 팩토리 사용.
- **Swagger 메타데이터**: 공개 API 는 `@Tag` (컨트롤러), `@Operation` (메서드), `@ApiResponses` (실패 케이스) 필수 — "13절 OpenAPI" 참조.

---

## 8. 공통 응답 형식 (ApiResponse)

모든 응답은 `ApiResponse<T>` 로 통일된 envelope.

| 필드 | 타입 | 의미 |
|------|------|------|
| `httpStatus` | `int` | HTTP 상태 코드 (200/201/400 등) |
| `isSuccess` | `boolean` | 성공/실패. primitive 로 nullable 배제 |
| `message` | `String` | 사용자에게 보여줄 메시지 |
| `code` | `String` | 내부 코드 (`SUCCESS`, `U001` 등). 프론트 분기 키 |
| `result` | `T` | 실제 페이로드. 실패 시 `null` |

- 성공: `ApiResponse.success(data)` or `ApiResponse.success(message, data)`
- 실패: `GlobalExceptionHandler` 가 자동 변환. Controller 가 직접 `error(...)` 호출 금지.

---

## 9. 예외 처리 규칙

- **모든 비즈니스 예외**: `CustomException(ErrorCode.*)`
- 새 에러 상황 → `ErrorCode` 열거형에 **신규 항목 추가**. 도메인 약어 + 번호 (예: `U001`, `B001`).
- `GlobalExceptionHandler`가 `CustomException`을 잡아 `ApiResponse.error(...)`로 변환
- **Validation 실패**: `MethodArgumentNotValidException` 핸들러는 **전 필드 에러를 수집** 해서 `message` 에 `, ` 로 조인. 프론트가 폼 전체 검증 메시지를 한 번에 받을 수 있도록.
- 커스텀 메시지는 `@NotBlank(message = "...")`로 붙임. 메시지 i18n 필요 시 `MessageSource` 경유 (현재는 한글 하드코딩 허용).

### 에러 코드 도메인 prefix (reserved)
- `C` Common, `A` Auth, `U` User, `B` Book, `S` Bookshelf/Space
- `R` ReadingRecord/Review, `F` Follow, `AS` Asset, `G` Guestbook
- `N` Notification, `RP` Report, `P` Payment, `FD` Feed
- `ST` Storage (S3 업로드/presign 공통)
- 신규 도메인 추가 시 충돌 없는 prefix 지정 후 이 목록 갱신

---

## 10. 로깅 / 관찰성

- 로깅 라이브러리: SLF4J + Logback (`@Slf4j` Lombok 사용)
- **MDC (Correlation ID)**: 모든 요청 진입 시 `requestId` 를 MDC 에 세팅 → 로그 한 줄마다 자동 주입
- Logback 패턴: `%d{ISO8601} [%X{requestId}] [%thread] %-5level %logger{36} - %msg%n`
- `GlobalExceptionHandler` 는 `log.warn` (4xx) / `log.error` (5xx) 로 구분

---

## 11. 인증 / 인가

### 토큰 규약
- **Authorization: Bearer {accessToken}** (Notion API 명세 준수)
- 소셜 로그인 → 자체 JWT 발급 (access + refresh)
- Access Token: 짧은 만료 (기본 30분), userId claim 포함
- Refresh Token: DB 저장 (`auth_refresh_tokens`), rotation + reuse detection

### 필터 체인
1. `JwtAuthenticationFilter` — `Authorization: Bearer` 파싱 → `JwtTokenProvider.validate()` → `Authentication` 생성 → `SecurityContextHolder.set()`
2. 미인증 요청은 `401 A001` (`ErrorCode.UNAUTHORIZED`)
3. 토큰 만료/서명 오류는 `401 A003` (`ErrorCode.INVALID_TOKEN`)

### 화이트리스트 (SecurityConfig)
- `/health`
- `/api/auth/**` (로그인/리프레시)
- `/swagger-ui/**`, `/v3/api-docs/**`
- 나머지는 `authenticated()`

### @CurrentUser
- Controller 파라미터에 `@CurrentUser Long userId` 로 현재 사용자 id 주입
- **반드시 `SecurityContextHolder` 기반** (헤더 직접 신뢰 금지)
- 인증이 없을 때는 `null` 반환하는 대신 Security 레이어에서 401 로 이미 막힘

### 진짜 권한 체크
- 도메인 소유권 검증은 Service 가 책임 (예: 내 책장만 수정 가능)
- URL 기반 role 은 지금은 불필요 (관리자 콘솔 생기면 도입)

---

## 12. Pagination

### 원칙
- **Cursor 기반** 기본. offset + limit 은 금지 (동적 데이터에서 누락/중복 발생).
- 기본 `limit = 20`, 최대 `limit = 50`
- 응답: `CursorResponse<T>`

```java
public record CursorResponse<T>(
    List<T> items,
    String nextCursor,   // null 이면 끝
    boolean hasNext
) {}
```

### 구현 패턴
- Controller 쿼리 파라미터: `?cursor=xxx&limit=20`
- Service → Repository 의 Custom 구현 (QueryDSL) 에서 `cursor` 디코딩 + `limit+1` 조회 → `hasNext` 판정
- Cursor 인코딩: `{lastId}_{lastCreatedAt.epochSecond}` base64 (향후 `CursorUtils` 유틸로 캡슐화 예정)

### 예외
- 관리자 툴, 통계 집계 등 총 건수 중요한 경우 `Pageable` + `Page<T>` 허용

---

## 13. OpenAPI / Swagger

### 필수 어노테이션
- **Controller**: 클래스 레벨 `@Tag(name="User", description="...")`
- **메서드**: `@Operation(summary="...", description="...")` + `@ApiResponses({ ... 실패 케이스 })`
- **DTO record**: 필드마다 `@Schema(description="...", example="...")` 또는 `@Schema(description=...)` 최소
- **공개 API**: Bearer 스킴 생략 시 `@SecurityRequirement(name="bearerAuth")` 명시
- **비로그인 API** (예: `/api/auth/social-login`): `@SecurityRequirements({})` 로 스킴 제거

### Bearer 스킴 정의 위치
`global/config/OpenApiConfig.java` 의 `@Bean OpenAPI` 에서 `SecurityScheme.Type.HTTP + scheme="bearer" + bearerFormat="JWT"` 로 1회 정의. 이후 컨트롤러는 `bearerAuth` 이름만 참조.

### 스펙 접근 경로
- Swagger UI: `/swagger-ui/index.html`
- OpenAPI JSON: `/v3/api-docs`
- `SecurityConfig` 화이트리스트에 반드시 포함

---

## 14. QueryDSL 사용

- Q클래스는 `build/generated/querydsl/**/Q*.java`에 생성 (자동)
- `JPAQueryFactory`는 `QueryDslConfig` 빈 사용. Repository 구현체(`*Impl`) 에 주입.
- 동적 조건은 **BooleanBuilder** 또는 **BooleanExpression 메서드**로. null 체크 필수.
- 프로젝션은 **DTO로 직접** (`Projections.constructor(UserResponse.class, ...)`) — `fetch()` 후 스트림 변환도 가능하지만 성능 중요하면 Projections 선호.

---

## 15. 테스트 규칙

- **단위 테스트**: 순수 JUnit + Mockito, `@ExtendWith(MockitoExtension.class)`
- **Repository 테스트**: `@DataJpaTest` + Testcontainers (MVP는 H2 또는 embedded PG도 허용)
- **통합 테스트**: `@SpringBootTest` + `@AutoConfigureMockMvc` — 최소한으로, 주요 플로우만
- 테스트 이름은 **한국어 BDD 스타일 허용**: `회원가입_이메일중복시_예외발생`
- `given/when/then` 주석으로 섹션 구분

---

## 16. 네이밍 컨벤션

| 대상 | 규칙 | 예 |
|------|------|-----|
| 패키지 | 소문자, 복수 안 씀 | `domain.user` |
| 클래스 | PascalCase | `UserService`, `SignupRequest` |
| 메서드 | camelCase, 동사부터 | `findById`, `existsByEmail` |
| 상수 | UPPER_SNAKE_CASE | `MAX_RETRY_COUNT` |
| 테이블 | snake_case 복수 | `users`, `bookshelf_items` |
| 컬럼 | snake_case | `created_at` |
| 엔드포인트 | kebab-case 복수 | `/api/bookshelves/{id}/items` |

DTO 네이밍:
- 입력: `<Verb>Request` (`SignupRequest`, `UpdateBookshelfRequest`)
- 출력: `<Noun>Response` (`UserResponse`, `BookshelfResponse`)
- 목록 출력: 단일 Response + `CursorResponse<T>` 로 감쌈 — 별도 `ListResponse` 안 만듦

---

## 17. 설정 / 프로필

- 공통 설정은 `application.yml`
- 프로필별 설정은 `application-{profile}.yml` (local, dev, prod)
- 민감값은 **절대 git에 올리지 않음**. GitLab Variables 경유.
- `application-local.yml`의 `ddl-auto: create`는 초기 개발용. 스키마 안정화 시 Flyway로 전환.
- JWT 비밀키: `jwt.secret` (환경변수 `JWT_SECRET`). 최소 32바이트 (HS256 요구사항).

---

## 18. 외부 의존성 의사결정

- 새 라이브러리 추가는 가볍게 하지 말 것. 먼저 기존 것으로 해결 가능한지 확인.
- 로깅: SLF4J (`@Slf4j` Lombok)
- JSON: Jackson 3 (Spring Boot 4 기본). Jackson 2 API (`com.fasterxml.jackson.*`) 참조 금지.
- JWT: `io.jsonwebtoken:jjwt` (0.12.x) — Spring Boot 4 호환, HS256 이상
- 시간: `Instant` / `LocalDate`. `LocalDateTime`, Joda-Time, Date 전부 금지.
- OpenAPI: `org.springdoc:springdoc-openapi-starter-webmvc-ui` (2.8+ for Spring Boot 4)

---

## exemplars/ 참조 규칙

`exemplars/`에 있는 파일은 **"이 스타일로 쓰면 된다"** 는 레퍼런스다. 새 코드를 짤 때:

1. 도메인 구조가 유사한 exemplar를 찾는다
2. 패키지 구조, 애노테이션 순서, 메서드 시그니처, 로그 스타일까지 거울처럼 따른다
3. 어긋나야 할 합리적 이유가 있으면 주석으로 설명하거나 이 `patterns.md`에 예외 규칙을 추가한다

각 레이어별로 **User 도메인 수직 슬라이스** 를 모범 사례로 유지한다. 도메인 규칙이 바뀌면 exemplar 도 함께 갱신.

---

## 변경 이력

- 2026-04-21 · 리뷰 보고서 기반 개정 (인증/시간/페이지네이션/OpenAPI/에러 수집 규칙 추가)
