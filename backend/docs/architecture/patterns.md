# DotShelf Backend Architecture Patterns

이 문서는 DotShelf 백엔드의 **단일 진실 소스**다. Claude와 Codex가 코드를 생성할 때 이 문서와 `exemplars/`를 **반드시** 먼저 읽고 따른다.

새 패턴이 생기면 여기서 수정하고, 기존 규칙과 충돌하면 이 문서가 우선한다. 코드 리뷰에서 이 규칙을 벗어난 게 발견되면 여기를 고칠지 코드를 고칠지 둘 중 하나를 고른다.

---

## 1. 전체 구조

**레이어드 아키텍처**: Controller → Service → Repository → (Entity/DB)

- **Controller** — HTTP 경계. 요청 검증, 인증 컨텍스트 추출, 응답 래핑. 비즈니스 로직 금지.
- **Service** — 비즈니스 로직의 유일한 소유자. 트랜잭션 경계(`@Transactional`)는 여기서 열림.
- **Repository** — DB 접근의 유일한 경로. 단순 조회는 Spring Data JPA 메서드, 복잡 조회는 QueryDSL.

**금기:**
- Controller → Repository 직접 호출 ❌
- Service에서 HTTP 타입(HttpServletRequest, ResponseEntity) 참조 ❌
- Repository에 비즈니스 로직 ❌

**패키지 구조**:
```
com.dotshelf
├── global/{config, common, error}        ← 횡단 관심사
└── domain/<name>/{controller, service, repository, entity, dto}
```

도메인 간 직접 의존은 피하고, 꼭 필요하면 Service 경유. 순환 의존은 설계 재검토 신호.

---

## 2. 엔티티 규칙

### 필수 규칙
- `BaseEntity`를 **반드시 상속** (createdAt/updatedAt 자동 관리)
- `@NoArgsConstructor(access = AccessLevel.PROTECTED)` — JPA 프록시용, 외부 new 금지
- 생성은 **오직 `@Builder`** 로만. setter 금지.
- `@Table(name = "...")` 명시, 다수 테이블 네이밍은 **snake_case 복수형** (`users`, `bookshelves`)
- 식별자: `@Id @GeneratedValue(strategy = IDENTITY)` + `Long id`

### 필드 규칙
- 컬럼 이름 기본은 snake_case로 자동 매핑되므로 `@Column(name=...)`은 **이름이 다를 때만** 명시
- `nullable`, `unique`, `length`는 의미있을 때 명시 (예: `@Column(nullable=false, unique=true, length=100)`)
- 컬렉션 필드는 **지연 로딩 기본** (`@OneToMany(fetch = LAZY)`) — EAGER 금지
- 양방향 연관관계는 **한쪽이 mappedBy 주인**. FK 컬럼 가진 쪽이 주인.

### 비즈니스 메서드
- 엔티티 내부 상태를 바꾸는 연산은 **엔티티 메서드**로 (setter 대신). 예: `user.changeNickname(...)`, `review.publish()`.
- 이러면 Service는 `user.changeNickname(...)`만 호출하고, 검증/invariant는 엔티티가 스스로 지킨다.

### 예시
```java
@Getter
@Entity
@Table(name = "users")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true, length = 100)
    private String email;

    @Column(nullable = false, length = 50)
    private String nickname;

    @Builder
    private User(String email, String nickname) {
        this.email = email;
        this.nickname = nickname;
    }

    public void changeNickname(String nickname) {
        if (nickname == null || nickname.isBlank()) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        this.nickname = nickname;
    }
}
```

---

## 3. DTO 규칙

- **Request DTO**: `record`로 작성. Bean Validation 애노테이션 붙임 (`@NotBlank`, `@Email`, `@Size` 등).
- **Response DTO**: `record`로 작성. 엔티티를 그대로 반환하지 말 것 (순환 참조/LAZY 예외 방지).
- **변환 책임**: Service에서 Entity ↔ DTO 변환. Controller는 Service에서 받은 DTO 그대로 전달.
  - 엔티티 → DTO: `static UserResponse from(User user)` 팩토리 메서드
  - DTO → 엔티티: DTO에 `toEntity()` 두거나 Service에서 변환
- 엔티티 자체를 Controller가 받거나 반환하지 않는다. 항상 DTO.

### 예시
```java
public record SignupRequest(
    @NotBlank @Email String email,
    @NotBlank @Size(min = 2, max = 20) String nickname
) {
    public User toEntity() {
        return User.builder()
                .email(email)
                .nickname(nickname)
                .build();
    }
}

public record UserResponse(Long id, String email, String nickname) {
    public static UserResponse from(User user) {
        return new UserResponse(user.getId(), user.getEmail(), user.getNickname());
    }
}
```

---

## 4. Service 규칙

- 클래스 레벨 `@Service` + `@RequiredArgsConstructor` (생성자 주입, final 필드)
- **읽기 전용 기본**: 클래스에 `@Transactional(readOnly = true)`, 쓰기 메서드에만 `@Transactional`
- 예외는 **반드시 `CustomException(ErrorCode.*)`** 사용. `throw new RuntimeException(...)` 금지.
- 도메인 객체의 상태 변경은 엔티티 메서드에 위임 (직접 setter 금지)
- 다른 도메인 Service를 주입할 수 있으나, 순환 주입은 설계 재고

### 예시
```java
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class UserService {

    private final UserRepository userRepository;

    @Transactional
    public UserResponse signup(SignupRequest request) {
        if (userRepository.existsByEmail(request.email())) {
            throw new CustomException(ErrorCode.USER_ALREADY_EXISTS);
        }
        User saved = userRepository.save(request.toEntity());
        return UserResponse.from(saved);
    }

    public UserResponse findById(Long id) {
        User user = userRepository.findById(id)
                .orElseThrow(() -> new CustomException(ErrorCode.USER_NOT_FOUND));
        return UserResponse.from(user);
    }
}
```

---

## 5. Repository 규칙

- `extends JpaRepository<T, Long>` 기본
- 복잡 조회는 **QueryDSL**로. Custom 인터페이스 + 구현체 분리 패턴:
  ```
  UserRepository extends JpaRepository<User, Long>, UserRepositoryCustom
  UserRepositoryCustom                   ← 인터페이스
  UserRepositoryImpl implements UserRepositoryCustom   ← 구현체, JPAQueryFactory 주입
  ```
- 메서드 이름 컨벤션: `findByEmail`, `existsByEmail`, `countByStatus` — Spring Data 네이밍에 따름
- N+1 방지: `@EntityGraph`, QueryDSL `fetchJoin()`, 또는 페치 조인 JPQL 활용
- 페이징은 `Pageable`/`Page<T>` 사용

---

## 6. Controller 규칙

- `@RestController` + `@RequestMapping("/api/<domain>")` 기본
- 모든 응답은 **`ApiResponse<T>`로 래핑**. 성공 → `ApiResponse.success(data)`, 예외는 던지면 `GlobalExceptionHandler`가 처리.
- Path는 **소문자 kebab-case**, 리소스 이름은 **복수형** (`/api/users`, `/api/bookshelves/{id}/items`)
- 요청 바디는 `@RequestBody @Valid <RequestDto>`로 검증
- 경로 변수는 `@PathVariable Long id` 그대로. Service에 id 넘겨서 처리.
- HTTP 상태 코드는 기본 200. 생성은 `@ResponseStatus(HttpStatus.CREATED)` 또는 `ResponseEntity` 사용. 그 외 상태는 예외로.

### 예시
```java
@RestController
@RequiredArgsConstructor
@RequestMapping("/api/users")
public class UserController {

    private final UserService userService;

    @PostMapping("/signup")
    @ResponseStatus(HttpStatus.CREATED)
    public ApiResponse<UserResponse> signup(@RequestBody @Valid SignupRequest request) {
        return ApiResponse.success("회원가입 완료", userService.signup(request));
    }

    @GetMapping("/{id}")
    public ApiResponse<UserResponse> find(@PathVariable Long id) {
        return ApiResponse.success(userService.findById(id));
    }
}
```

---

## 7. 예외 처리 규칙

- **모든 비즈니스 예외**: `CustomException(ErrorCode.*)`
- 새 에러 상황 → `ErrorCode` 열거형에 **신규 항목 추가**. 도메인 약어 + 번호 (예: `U001`, `B001`).
- `GlobalExceptionHandler`가 `CustomException`을 잡아 `ApiResponse.error(...)`로 변환
- Validation 실패는 이미 처리됨 — 커스텀 메시지는 `@NotBlank(message = "...")`로 붙임

---

## 8. QueryDSL 사용

- Q클래스는 `build/generated/querydsl/**/Q*.java`에 생성 (자동)
- `JPAQueryFactory`는 `QueryDslConfig` 빈 사용. Repository 구현체에 주입.
- 동적 조건은 **BooleanBuilder** 또는 **BooleanExpression 메서드**로. null 체크 필수.
- 프로젝션은 **DTO로 직접** (`Projections.constructor(UserResponse.class, ...)`) — `fetch()` 후 스트림 변환도 가능하지만 필요시 Projections 선호.

---

## 9. 테스트 규칙

- **단위 테스트**: 순수 JUnit + Mockito, `@ExtendWith(MockitoExtension.class)`
- **Repository 테스트**: `@DataJpaTest` + Testcontainers (MVP는 H2 또는 embedded PG도 허용)
- **통합 테스트**: `@SpringBootTest` + `@AutoConfigureMockMvc` — 최소한으로, 주요 플로우만
- 테스트 이름은 **한국어 BDD 스타일 허용**: `회원가입_이메일중복시_예외발생`
- `given/when/then` 주석으로 섹션 구분

---

## 10. 네이밍 컨벤션

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
- 목록 출력: 단일 Response + `List<T>` 또는 `Page<T>` — 별도 `ListResponse` 안 만듦

---

## 11. 설정 / 프로필

- 공통 설정은 `application.yml`
- 프로필별 설정은 `application-{profile}.yml` (local, dev, prod)
- 민감값은 **절대 git에 올리지 않음**. GitLab Variables 경유.
- `application-local.yml`의 `ddl-auto: create`는 초기 개발용. 스키마 안정화 시 Flyway로 전환.

---

## 12. 외부 의존성 의사결정

- 새 라이브러리 추가는 가볍게 하지 말 것. 먼저 기존 것으로 해결 가능한지 확인.
- 로깅: SLF4J (`@Slf4j` Lombok)
- JSON: Jackson 3 (Spring Boot 4 기본). Jackson 2 API (`com.fasterxml.jackson.*`) 참조 금지.
- 시간: `LocalDateTime` / `Instant` / `LocalDate`. Joda-Time, Date 금지.

---

## exemplars/ 참조 규칙

`exemplars/`에 있는 파일은 **"이 스타일로 쓰면 된다"**는 레퍼런스다. 새 코드를 짤 때:

1. 도메인 구조가 유사한 exemplar를 찾는다
2. 패키지 구조, 애노테이션 순서, 메서드 시그니처, 로그 스타일까지 거울처럼 따른다
3. 어긋나야 할 합리적 이유가 있으면 주석으로 설명하거나 이 `patterns.md`에 예외 규칙을 추가한다

처음에는 `User.java` 하나만 있지만, 각 도메인의 수직 슬라이스가 완성될 때마다 대표 파일을 여기 추가한다.
