# 챕터 5 · 에러가 나면 어떻게 되나

> 목표: 예외(exception)가 발생했을 때 서버가 어떻게 일관된 형태로 응답을 내보내는지, 그리고 왜 그렇게 설계했는지 이해하기.

읽고 나면 답할 수 있는 것:
- **Exception (예외)** 이 뭐야, 왜 생겨
- **공통 응답 포맷(envelope)** 이 왜 필요한가
- `ApiResponse` 래퍼가 하는 일
- 예외가 어디서 어떻게 "잡히는가"
- `GlobalExceptionHandler` 의 역할
- **검증(Validation)** 실패는 어떻게 처리되나
- `ErrorCode` enum 은 왜 있는가

---

## 1. 알아둘 기초

### 1-1. "Exception(예외)" 이 뭐야?

자바에서 **"뭔가 잘못됐을 때 던지는 객체"**.

```java
User user = userRepository.findById(999L).orElseThrow();
// ↑ 999번 유저가 없으면 NoSuchElementException 발생 ("던짐")
```

- 예외가 발생하면 **해당 메서드는 거기서 중단**
- 호출한 쪽으로 예외가 **위로 튀어 오름 (bubble up)**
- 누군가 `try-catch` 로 잡지 않으면 계속 위로, 결국 프로그램 종료

예외 예시:
- `NullPointerException` — null 인 걸 건드림
- `IllegalArgumentException` — 잘못된 인자 전달
- `IOException` — 파일/네트워크 문제
- 커스텀 예외 (우리가 만든 것) — `UserNotFoundException`, `InvalidTokenException` 등

### 1-2. 예외 처리를 **한 곳에 모아야** 하는 이유

컨트롤러마다 각자 처리하면:
```java
// ControllerA
try { ... } catch (UserNotFoundException e) { return ResponseEntity.status(404).body("유저 없음"); }

// ControllerB
try { ... } catch (UserNotFoundException e) { return ResponseEntity.status(404).body(Map.of("err", "user not found")); }
```

→ **응답 포맷이 엔드포인트마다 다름** → 프론트 개발자 혼란.

해결: **전역 예외 처리기(GlobalExceptionHandler)** 하나가 모든 예외를 잡아 **일관된 형태** 로 응답.

### 1-3. "공통 응답 포맷(envelope)" 이 뭐야?

모든 API 응답이 **같은 겉모습** 을 가지게 감싸는 것.

**포장 없음**:
```json
// GET /api/users/1 → 성공
{"id": 1, "nickname": "민욱"}

// GET /api/users/999 → 실패
{"error": "not found"}
```

→ 프론트가 매번 "성공이면 바로 쓰고, 실패면 error 필드 체크" 조건 분기 복잡.

**공통 envelope**:
```json
// 성공
{
  "httpStatus": 200,
  "isSuccess": true,
  "code": "SUCCESS",
  "message": "요청이 성공했습니다.",
  "result": { "id": 1, "nickname": "민욱" }
}

// 실패
{
  "httpStatus": 404,
  "isSuccess": false,
  "code": "U001",
  "message": "유저를 찾을 수 없습니다",
  "result": null
}
```

같은 5 필드 구조. 프론트는 `isSuccess` / `code` 로 분기하면 됨.

---

## 2. 우리 프로젝트의 `ApiResponse`

실제 파일: [`src/main/java/com/dotshelf/global/common/ApiResponse.java`](../../src/main/java/com/dotshelf/global/common/ApiResponse.java)

```java
public class ApiResponse<T> {
    private final int httpStatus;
    private final boolean isSuccess;
    private final String message;
    private final String code;
    private final T result;

    public static <T> ApiResponse<T> success(T data) {
        return new ApiResponse<>(200, true, "요청이 성공했습니다.", "SUCCESS", data);
    }

    public static <T> ApiResponse<T> error(int httpStatus, String code, String message) {
        return new ApiResponse<>(httpStatus, false, message, code, null);
    }
    // ... success(msg, data), success(), error(msg) 등 오버로드
}
```

`<T>` = **제네릭(Generic)**. "이 envelope 안에 어떤 타입의 data 가 들어갈지" 를 유연하게.

```java
ApiResponse<User> success(User u)           // result 에 User 가 들어감
ApiResponse<List<Book>> success(List books) // result 에 책 목록이 들어감
ApiResponse<Void> success()                 // result 없음 (204 같은 응답)
```

Controller 반환 시:
```java
@GetMapping("/api/users/{id}")
public ApiResponse<UserDto> findUser(@PathVariable Long id) {
    return ApiResponse.success(userService.findById(id));
}
```

---

## 3. 예외가 발생하면 일어나는 일

### 3-1. 단계별

```
Controller / Service / Repository 어딘가에서 예외 발생
  │
  ▼
호출 스택을 거슬러 올라감 (아무도 안 잡으면 계속 위로)
  │
  ▼
Spring 이 이 예외를 감지
  │
  ▼
@RestControllerAdvice 로 등록된 핸들러가 잡음
  │
  ▼
핸들러가 예외 → ApiResponse 변환
  │
  ▼
적절한 HTTP 상태 코드 + JSON body 응답
```

### 3-2. `@RestControllerAdvice` 가 뭐야?

Spring 의 "**모든 컨트롤러에 공통으로 적용될 조언**" 이라는 어노테이션.

```java
@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(UserNotFoundException.class)
    public ResponseEntity<ApiResponse<Void>> handleUserNotFound(UserNotFoundException e) {
        return ResponseEntity.status(404)
            .body(ApiResponse.error(404, ErrorCode.USER_NOT_FOUND.getCode(), e.getMessage()));
    }

    @ExceptionHandler(Exception.class)   // 마지막 그물망: 모든 예상 못 한 예외
    public ResponseEntity<ApiResponse<Void>> handleUnknown(Exception e) {
        log.error("Unhandled exception", e);
        return ResponseEntity.status(500)
            .body(ApiResponse.error(500, "INTERNAL_ERROR", "서버 에러가 발생했습니다"));
    }
}
```

각 `@ExceptionHandler` 가 특정 예외 타입을 받아서 응답으로 변환. 예외 종류별로 다른 HTTP 상태/메시지 줌.

---

## 4. `ErrorCode` enum — 도메인 에러를 표준화

왜 에러마다 짧은 코드(`U001`, `B002`) 가 있나?

- HTTP 상태(404) 는 너무 대분류 ("Not Found" 가 유저 얘기인지 책 얘기인지 모름)
- 메시지(`유저 없음`) 는 번역/변경될 수 있음
- **변하지 않는 식별자** 가 필요 → `ErrorCode`

```java
public enum ErrorCode {
    USER_NOT_FOUND("U001", "유저를 찾을 수 없습니다"),
    INVALID_TOKEN("A001", "유효하지 않은 토큰입니다"),
    BOOK_ALREADY_REGISTERED("B001", "이미 등록된 책입니다"),
    ...

    private final String code;
    private final String message;
}
```

프론트 입장:
- "`code == 'U001'`" 이면 "로그인 페이지로 보내야겠다" 같은 분기
- 메시지는 UI 에 그대로 표시하거나 번역 테이블 조회
- 서버가 메시지 바꿔도 `code` 는 고정이라 프론트 영향 없음

### 커스텀 예외와 짝꿍

```java
public class UserNotFoundException extends RuntimeException {
    public UserNotFoundException() {
        super(ErrorCode.USER_NOT_FOUND.getMessage());
    }
}
```

Service:
```java
public User findById(Long id) {
    return userRepository.findById(id)
        .orElseThrow(UserNotFoundException::new);
}
```

Handler:
```java
@ExceptionHandler(UserNotFoundException.class)
public ResponseEntity<ApiResponse<Void>> handle(UserNotFoundException e) {
    ErrorCode ec = ErrorCode.USER_NOT_FOUND;
    return ResponseEntity.status(404)
        .body(ApiResponse.error(404, ec.getCode(), ec.getMessage()));
}
```

이렇게 엮으면 **도메인 오류 하나가 → 커스텀 예외 → ErrorCode → 일관된 응답** 파이프라인이 됨.

---

## 5. 검증(Validation) 실패 처리

### 5-1. 검증이란?

사용자가 보낸 입력이 **올바른지** 확인하는 절차.

예: 회원가입 요청
```json
POST /v1/auth/signup
{
  "email": "not-an-email",   // 이메일 형식 아님
  "nickname": "",            // 비어있음
  "age": 200                 // 비현실적
}
```

서버가 "어 이 입력 이상한데?" 잡아내는 게 검증.

### 5-2. `@Valid` + Jakarta Validation 어노테이션

```java
// DTO
public record SignupRequest(
    @Email @NotBlank String email,
    @NotBlank @Size(min = 2, max = 20) String nickname,
    @Min(0) @Max(150) int age
) {}
```

Controller:
```java
@PostMapping("/v1/auth/signup")
public ApiResponse<Void> signup(@RequestBody @Valid SignupRequest req) {
    //                                    ↑ @Valid 가 검증 시작 신호
    authService.signup(req);
    return ApiResponse.success();
}
```

검증 실패하면 Spring 이 **`MethodArgumentNotValidException`** 을 자동으로 던짐.

### 5-3. 우리 핸들러: 전 필드 에러 수집

```java
@ExceptionHandler(MethodArgumentNotValidException.class)
public ResponseEntity<ApiResponse<Void>> handleValidation(MethodArgumentNotValidException e) {
    String message = e.getBindingResult().getFieldErrors().stream()
        .map(err -> err.getField() + ": " + err.getDefaultMessage())
        .collect(Collectors.joining(", "));
    return ResponseEntity.status(400)
        .body(ApiResponse.error(400, ErrorCode.INVALID_INPUT.getCode(),
                                message.isEmpty() ? ErrorCode.INVALID_INPUT.getMessage() : message));
}
```

반환 예:
```json
{
  "httpStatus": 400,
  "isSuccess": false,
  "code": "V001",
  "message": "email: 올바른 이메일 형식이 아닙니다, nickname: 공백일 수 없습니다, age: 150 이하이어야 합니다",
  "result": null
}
```

→ 프론트가 **모든 잘못된 필드를 한 번에** 받음. 사용자는 한 번에 다 고칠 수 있음 (한 필드씩 고치며 왕복 X).

> 왜 이렇게 바뀌었냐면, 원래는 `findFirst()` 로 **첫 에러만** 반환했었음. [2026-04-21 리팩토링](../study/2026-04-21-feat-be-refactoring.md) 에서 UX 개선.

---

## 6. 인증/인가 실패는 별도 경로

일반 예외가 아닌 **Spring Security 레벨의 차단** 은 다른 곳에서 처리:

- **인증 실패** (토큰 없음/만료) → `AuthenticationEntryPoint` (우리는 `SecurityEntryPoints.unauthorizedHandler()`)
- **인가 실패** (토큰은 있는데 권한 부족) → `AccessDeniedHandler` (우리는 `SecurityEntryPoints.accessDeniedHandler()`)

왜 GlobalExceptionHandler 가 안 잡냐? → Security 필터 체인은 컨트롤러 **이전** 에서 끝나는 단계라 `@RestControllerAdvice` 가 관여 못 함.

그래서 Security 용 커스텀 핸들러가 따로 있고, 둘 다 **같은 `ApiResponse` 포맷** 으로 응답하게 맞춰둠 (프론트 입장엔 동일한 모양).

실제 파일: `src/main/java/com/dotshelf/global/security/SecurityEntryPoints.java`

---

## 7. 전체 예외 흐름 그림

```
              예외 발생 지점
                    │
     ┌──────────────┴──────────────┐
     ▼                              ▼
  Controller/Service              Spring Security 필터
  에서 발생하는 예외                 단에서 차단된 요청
     │                              │
     │                              │
     ▼                              ▼
  @RestControllerAdvice         AuthenticationEntryPoint /
  (GlobalExceptionHandler)      AccessDeniedHandler
     │                              │
     │                              │
     ▼                              ▼
  ApiResponse.error(status,      ApiResponse.error(401/403,
                    code, msg)                     code, msg)
     │                              │
     └──────────┬───────────────────┘
                ▼
        같은 JSON 포맷으로 응답
```

두 경로 다른 곳에서 시작하지만 **최종 응답은 똑같이 envelope 형태**. 프론트는 항상 같은 방식으로 파싱.

---

## 8. 실제 예시

### 시나리오 1: 없는 유저 조회
```http
GET /api/users/99999
Authorization: Bearer <valid>
```

내부:
1. Filter 통과 (토큰 OK)
2. UserController → UserService.findById(99999)
3. Repository 결과 없음 → `orElseThrow(UserNotFoundException::new)`
4. 예외 bubble up → GlobalExceptionHandler 가 잡음
5. 응답:
```json
{
  "httpStatus": 404,
  "isSuccess": false,
  "code": "U001",
  "message": "유저를 찾을 수 없습니다",
  "result": null
}
```

### 시나리오 2: 잘못된 포맷 검증 실패
```http
POST /v1/auth/signup
{ "email": "bad", "nickname": "" }
```

내부:
1. `@Valid` 가 검증 → 실패
2. `MethodArgumentNotValidException` 발생
3. GlobalExceptionHandler 가 전 필드 수집
4. 응답:
```json
{
  "httpStatus": 400,
  "isSuccess": false,
  "code": "V001",
  "message": "email: 올바른 이메일 형식이 아닙니다, nickname: 공백일 수 없습니다",
  "result": null
}
```

### 시나리오 3: 토큰 없이 protected API
```http
GET /api/users/me
(헤더 없음)
```

내부:
1. JwtAuthenticationFilter → 토큰 없음
2. SecurityContext 에 인증 안 박힘
3. 이후 Security 가 "이 URL 은 인증 필요" 판단 → 차단
4. `SecurityEntryPoints.unauthorized` 호출
5. 응답:
```json
{
  "httpStatus": 401,
  "isSuccess": false,
  "code": "A001",
  "message": "인증이 필요합니다",
  "result": null
}
```

---

## 9. 자주 헷갈리는 것들

### Q. 모든 예외를 `@ExceptionHandler` 로 잡아야 돼?
- 아니요. `@ExceptionHandler(Exception.class)` 로 "fallback" 하나 만들어두면 예상 못 한 예외도 500 응답으로 처리됨.
- 자주 나오는 것만 개별 처리 (UserNotFoundException, InvalidTokenException 등)

### Q. 예외 메시지를 그대로 응답에 넣어도 되나?
- **주의**. 내부 구조가 노출될 위험 (DB 에러 메시지 등).
- 공개해도 괜찮은 메시지만 `message` 필드로. 상세 에러는 로그에만.
- 우리는 `ErrorCode` 의 message 를 씀 → 사전에 검토된 텍스트만 노출.

### Q. HTTP 상태(400) 와 `code`(U001) 가 둘 다 있는 이유?
- HTTP 상태: 표준 (브라우저/캐시/LB 가 해석)
- `code`: 우리 도메인 세분화 (프론트가 분기용으로 사용)
- 둘 다 있어야 "표준 준수 + 앱 특화" 충족.

### Q. 성공 응답도 굳이 envelope 로 감싸야 돼? 그냥 데이터 바로 주면 안 돼?
- 가능. REST 순수주의자는 그걸 선호.
- 우리는 **일관성** 을 중시 → 모든 응답이 같은 구조 → 프론트 코드 단순.
- 성능 영향 거의 없음 (JSON 몇 필드 추가).

### Q. `try-catch` 를 Service 에서도 해야 돼?
- **웬만하면 하지 말 것**. catch 해서 아무것도 안 하면 예외 숨기는 셈.
- 예외는 **정말 복구 가능한 상황** 에서만 catch. (예: 재시도 로직)
- 아니면 그냥 위로 던져서 GlobalExceptionHandler 가 처리하게.

### Q. 로깅은 어디서 해?
- GlobalExceptionHandler 의 fallback (500 처리) 에서 **스택트레이스 포함 로그**.
- 비즈니스 예외 (404 등) 는 보통 WARN 정도. 너무 많이 찍으면 노이즈.
- MDC 의 `requestId` 덕분에 로그에서 같은 요청 추적 가능 ([챕터 1](./01-request-lifecycle.md) 참조).

---

## 10. 실제 코드에서 확인하기

| 개념 | 파일 |
|---|---|
| 공통 응답 포맷 | [`src/main/java/com/dotshelf/global/common/ApiResponse.java`](../../src/main/java/com/dotshelf/global/common/ApiResponse.java) |
| 테스트 예시 | [`src/test/java/com/dotshelf/global/common/ApiResponseTest.java`](../../src/test/java/com/dotshelf/global/common/ApiResponseTest.java) |
| 전역 예외 처리 | `src/main/java/com/dotshelf/global/exception/GlobalExceptionHandler.java` |
| 에러 코드 enum | `src/main/java/com/dotshelf/global/exception/ErrorCode.java` |
| Security 인증/인가 실패 핸들링 | `src/main/java/com/dotshelf/global/security/SecurityEntryPoints.java` |
| 커스텀 예외들 | `src/main/java/com/dotshelf/domain/*/exception/` |

---

## 11. 챕터 5 정리

- ✅ 예외는 잘못된 상황을 알리는 객체. 호출 스택을 타고 올라감
- ✅ 모든 응답을 `ApiResponse` envelope 로 통일 → 프론트 단순화
- ✅ `@RestControllerAdvice + @ExceptionHandler` 로 예외를 한 곳에서 잡음
- ✅ `ErrorCode` enum 으로 도메인 에러를 표준 식별자로 관리
- ✅ `@Valid` 와 검증 어노테이션으로 입력 검증. 실패 시 **전 필드 에러 수집**
- ✅ Security 단의 차단(401/403) 은 별도 경로(`SecurityEntryPoints`) 지만 **같은 응답 포맷** 유지

다음 챕터 👉 [**챕터 6: 배포 — 내 코드가 서버에 도달하기까지**](./06-deployment.md)

지금까지 "코드가 어떻게 돌아가는지" 봤으니, 이제 내가 쓴 코드가 **어떻게 실제 서버에 올라가 사용자에게 전달되는지** 의 파이프라인을 다룹니다.
