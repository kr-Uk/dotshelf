# 챕터 2 · 인증 — 누구인지 어떻게 알까

> 목표: "로그인은 어떻게 되고, 그 이후 요청마다 어떻게 나인 걸 증명하나" 전체 흐름 이해하기.

읽고 나면 답할 수 있는 것:
- **"인증" 과 "인가"** 는 뭐가 다른가
- 우리 프로젝트가 **쿠키 대신 토큰** 을 쓰는 이유
- **JWT** 가 정확히 무엇인가
- **OAuth2 소셜 로그인** 은 어떻게 동작하는가
- **refresh 토큰** 은 왜 필요한가
- 우리 코드에서 누가 토큰 발급/검증을 담당하는가

---

## 1. 알아둘 기초

### 1-1. 인증 vs 인가 (Authentication vs Authorization)

**인증(Authentication)** = **"너 누구야?"** — 신분 확인
**인가(Authorization)** = **"너 이거 해도 돼?"** — 권한 확인

비유: 회사 건물
- 인증 = 1층 입구 **사원증 스캔** (너 우리 직원 맞냐?)
- 인가 = 서버실 들어가는 문에서 **권한 검사** (너 서버 관리팀 맞냐?)

둘은 다른 단계. 인증을 했다고 아무거나 할 수 있는 건 아님.

### 1-2. 세션 방식 vs 토큰 방식

옛날(쿠키 + 세션) 방식:
```
사용자 로그인 → 서버가 "세션 ID" 생성 → 서버 메모리에 (ID → 유저) 저장
             → 클라이언트 브라우저에 쿠키로 ID 박아둠
요청할 때마다:
    클라이언트: 쿠키 자동 전송
    서버: 쿠키 ID 로 메모리 찾아 "아 이 사람 민욱이네"
```

**문제점**:
- 서버가 많아지면 **모든 서버가 세션을 공유** 해야 함 (Redis 같은 공유 저장소 필요)
- 모바일 앱은 쿠키 처리 불편
- CSRF 같은 쿠키 특화 보안 공격에 노출

요즘(토큰) 방식:
```
사용자 로그인 → 서버가 "JWT 토큰" 을 만들어서 클라이언트에게 줌
           → 이 토큰 안에 "나 민욱이야" 정보가 이미 들어있음
요청할 때마다:
    클라이언트: Authorization 헤더에 토큰 담아 보냄
    서버: 토큰을 "검증만" 함 (저장소 조회 불필요)
```

**장점**:
- 서버는 **토큰이 진짜인지만** 확인하면 됨 → 서버 여러 대여도 공유 저장소 불필요
- 모바일 앱 친화적
- 우리는 이 방식.

### 1-3. 놀이공원 팔찌 비유로 JWT

**JWT** = **JSON Web Token**. 입장할 때 손목에 채워주는 팔찌 같은 거.

```
[팔찌 앞면]  "이름: 민욱 / 등급: VIP / 유효기간: 오늘까지"
[팔찌 뒷면]  놀이공원 로고와 위조 방지 홀로그램
```

- 앞면(정보)은 누구나 읽을 수 있음 (암호화 아님)
- **뒷면(서명) 은 놀이공원만 만들 수 있음** → 위조 방지
- 어떤 직원이든 팔찌만 보고 "아 VIP 민욱 맞네" 판단 가능

서버가 "놀이공원 직원", 토큰이 "팔찌". 유저마다 따로 명단 보관할 필요 없음.

### 1-4. JWT 를 뜯어보면

JWT 는 3 부분으로 생긴 문자열:

```
eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIiwidHlwIjoiYWNjZXNzIn0.abc123signatureHere
└─── Header ────────┘└─── Payload ───────────────────┘└── Signature ────┘
```

- **Header**: "이 토큰은 HS256 알고리즘으로 서명됐어" 같은 메타 정보
- **Payload**: 실제 유저 정보 (`sub=1` 이면 유저 id 1번, `typ=access` 이면 access 토큰)
- **Signature**: **서버만 아는 비밀키(JWT_SECRET) 로 앞 두 부분을 암호화** 한 서명

검증은 이 서명을 다시 계산해서 같으면 OK. **비밀키 모르면 서명 위조 불가능**.

> ⚠️ 주의: Payload 는 **그냥 Base64 인코딩**. 암호화 아님. 누구나 디코딩해서 내용을 볼 수 있음.
> → **민감한 정보(비밀번호 등)를 payload 에 넣으면 안 됨**. 우리는 `userId` 와 `typ` 만 담음.

### 1-5. OAuth2 가 뭐야?

**"다른 서비스(구글, 카카오) 계정으로 로그인하게 해주는 표준 프로토콜"**.

사용자 관점:
```
1. "구글로 로그인" 버튼 클릭
2. 구글 로그인 화면으로 리다이렉트
3. 구글에 비밀번호 입력 (구글이 확인)
4. 구글: "DotShelf 에서 당신의 이메일/이름을 보고 싶대요. 허용하시겠습니까?"
5. 수락하면 DotShelf 로 돌아옴
6. 로그인 완료 ✅
```

**비밀번호를 DotShelf 가 절대 안 봄**. 구글이 대신 인증하고 "이 사람은 someone@gmail.com 맞다" 라는 증명서만 DotShelf 에 줌.

> 왜 쓰나?
> - 사용자는 비번 덜 만들어도 됨 (회원가입 귀찮 ↓)
> - DotShelf 는 비번 저장/관리 부담 없음 (털려도 피해 ↓)
> - 구글/카카오처럼 **큰 회사의 보안** 을 빌려쓰는 셈

---

## 2. 전체 로그인 + 이후 요청 흐름

단계가 많지만 한 번 보고 나면 나머지는 쉬워요.

```
[1] 사용자: "카카오로 로그인" 클릭
        │
        ▼
[2] 카카오 OAuth 화면 → 사용자 비번 입력 → 허용
        │
        ▼
[3] 카카오 → DotShelf 로 "authorization code" 전달
        │
        ▼
[4] DotShelf: 이 code 를 카카오에 다시 보내 "access_token" 받음
        │
        ▼
[5] DotShelf: 그 access_token 으로 카카오 API 호출 → 유저 이메일 등 받아옴
        │
        ▼
[6] DotShelf DB 에서 해당 유저 찾기 or 없으면 새로 만듦
        │
        ▼
[7] DotShelf: 우리 JWT (access + refresh) 발급해서 클라이언트에 응답
        │
        ▼
[8] 클라이언트: access 토큰을 저장해뒀다가
    이후 모든 요청에 Authorization: Bearer <access> 헤더로 첨부
        │
        ▼
[9] 서버: 요청마다 JwtAuthenticationFilter 가 토큰 검증
        │
        ▼
[10] 토큰 유효하면 "SecurityContext" 에 "이 요청은 유저 1번이 보낸 거야" 기록
        │
        ▼
[11] Controller/Service 에서 현재 유저 정보 꺼내 사용

[access 만료 시]
[12] 클라이언트: 저장해둔 refresh 토큰을 보냄
        │
        ▼
[13] 서버: refresh 검증 → 새 access + 새 refresh 발급 (rotation)
```

---

## 3. 단계별 자세히

### 단계 ①~⑥: 로그인

우리 프로젝트의 `AuthController` / `AuthService` 가 담당.

**핵심 파일**:
- `com.dotshelf.domain.auth.controller.AuthController` — `/v1/auth/kakao`, `/v1/auth/google` 엔드포인트
- `com.dotshelf.domain.auth.service.AuthService` — 실제 로그인 로직
- `com.dotshelf.domain.auth.entity.UserSocialAccount` — "이 유저의 카카오 ID 는 이거" 매핑 저장
- `com.dotshelf.domain.user.entity.User` — 우리 앱의 유저 기본 정보

가상 흐름:
```java
// AuthController
@PostMapping("/v1/auth/kakao")
public ApiResponse<LoginResponse> kakaoLogin(@RequestBody KakaoCodeRequest req) {
    return ApiResponse.success(authService.loginWithKakao(req.code()));
}

// AuthService (의사코드)
public LoginResponse loginWithKakao(String code) {
    KakaoProfile profile = kakaoApi.exchange(code);  // 카카오에 code 주고 유저정보 받음
    User user = userRepo.findByKakaoId(profile.id())
        .orElseGet(() -> createNewUser(profile));    // DB 에서 찾거나 새로 만듦
    return jwtTokenProvider.issue(user);             // JWT 발급
}
```

### 단계 ⑦: JWT 발급

담당: `JwtTokenProvider`.

**HS256 알고리즘** 을 사용해서 서명된 JWT 를 2종 발급:

- **Access Token** — 짧은 수명 (예: 30분). 요청마다 첨부.
- **Refresh Token** — 긴 수명 (예: 30일). access 만료 시 갱신용.

```java
// JwtTokenProvider (의사코드)
public JwtPair issue(User user) {
    String access = Jwts.builder()
        .subject(user.getId().toString())
        .claim("typ", "access")
        .expiration(now().plus(30분))
        .signWith(secret)    // ← JWT_SECRET 으로 서명
        .compact();

    String refresh = /* 동일하나 typ=refresh, 만료 30일 */;

    // refresh 토큰은 DB 에도 저장 (재사용 감지용)
    refreshTokenRepo.save(new AuthRefreshToken(user.getId(), refresh, ...));

    return new JwtPair(access, refresh);
}
```

이 때 `JWT_SECRET` 은 **서버의 `.env`** 에 저장된 비밀문자열. [챕터 0 용어 사전](./README.md)에서 언급한 그것. 이게 털리면 누구나 토큰 위조 가능하니까 아주 조심.

### 단계 ⑧: 클라이언트가 토큰 보관

- 모바일 앱: 안드로이드 `EncryptedSharedPreferences` / iOS 키체인
- 웹: `localStorage` (XSS 주의) 또는 `HttpOnly` 쿠키

**DotShelf 는 모바일 중심이라 헤더 방식**.

### 단계 ⑨: 요청마다 헤더 첨부

```http
GET /api/bookshelves/1 HTTP/1.1
Host: api.dotshelf.digital
Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIiwidHlwIjoiYWNjZXNzIn0.abc...
```

**`Authorization: Bearer <토큰>`** 이 표준. `Bearer` = "이 토큰 가진 사람이 곧 본인" 이라는 의미.

### 단계 ⑩~⑪: 필터가 검증

[챕터 1](./01-request-lifecycle.md#단계-6--spring-boot-실제로-일하는-곳) 에서 언급한 필터 체인의 한 조각.

**담당**: `JwtAuthenticationFilter`

```java
// 의사코드
public class JwtAuthenticationFilter extends OncePerRequestFilter {

    @Override
    protected void doFilterInternal(HttpServletRequest req, HttpServletResponse resp, FilterChain chain) {
        String header = req.getHeader("Authorization");

        if (header != null && header.startsWith("Bearer ")) {
            String token = header.substring(7);

            // 서명 검증 + 만료 확인
            if (jwtTokenProvider.validate(token)) {
                Long userId = jwtTokenProvider.getUserId(token);

                // SecurityContext 에 "이 요청은 userId 가 보낸 거" 기록
                var authentication = new UsernamePasswordAuthenticationToken(userId, null, List.of());
                SecurityContextHolder.getContext().setAuthentication(authentication);
            }
        }

        // 다음 필터로 통과
        chain.doFilter(req, resp);
    }
}
```

검증 실패하거나 토큰 없는 요청이 **인증 필요한 URL** 에 들어가면 `SecurityEntryPoints` 가 401 응답으로 잘라냄.

**인증 필요 없는 URL** 은 `SecurityConfig` 의 whitelist 에 등록:
```java
private static final String[] PUBLIC_ENDPOINTS = {
    "/api/health",
    "/actuator/health",
    "/v1/auth/**",      // 로그인 자체는 인증 안 된 상태에서 해야 하므로 제외
    "/swagger-ui/**",
    "/v3/api-docs/**"
};
```

### 단계에서 Controller/Service 가 현재 유저 알아내는 법

```java
// 예시 — CurrentUser 어노테이션으로 SecurityContext 에서 userId 꺼냄
@GetMapping("/api/users/me")
public ApiResponse<UserDto> me(@CurrentUser Long userId) {
    return ApiResponse.success(userService.findById(userId));
}
```

담당: `CurrentUserArgumentResolver` — `@CurrentUser` 어노테이션을 만나면 `SecurityContext.principal` 에서 userId 를 꺼내 주입.

> **이 전 상태엔 `X-User-Id` 헤더를 그대로 믿었음** — 즉 "나 1번 유저야" 라고 헤더만 보내면 누구든 1번이 될 수 있었음. [2026-04-21 리팩토링](../study/2026-04-21-feat-be-refactoring.md) 에서 이걸 정상 JWT 방식으로 바꿨음.

### 단계 ⑫~⑬: Refresh 로 갱신

Access 토큰이 만료되면 클라이언트는 401 을 받음 → 저장해둔 refresh 를 보냄:

```http
POST /v1/auth/refresh
Body: { "refreshToken": "eyJ..." }
```

서버는:
1. refresh 서명 + 만료 검증
2. **DB 에 저장된 refresh** 와 비교 (재사용/탈취 감지)
3. 검증 OK → **새 access + 새 refresh** 발급 (이 쌍을 **rotation** 이라 함)
4. 옛 refresh 는 invalidate

왜 refresh 를 DB 에 저장하냐? → **재사용 감지(reuse detection)** 때문. 만약 탈취당한 옛 refresh 로 요청이 오면 "어 이건 이미 쓴 거 아니야?" 감지하고 해당 유저의 모든 토큰을 무효화 → 공격자가 로그인 유지 불가.

**담당 엔티티**: `AuthRefreshToken`.
**담당 서비스**: `AuthService.refreshWith(String refreshToken)`.

---

## 4. 실제 예시: `/api/users/me` 호출

가입 후 내 정보 조회 시나리오:

1. `POST /v1/auth/kakao { code: "..." }` → `{access: "...", refresh: "..."}` 받음
2. `GET /api/users/me` 보냄, 헤더에 `Authorization: Bearer <access>`
3. 서버: JwtAuthenticationFilter → 서명 OK, 만료 OK, userId=1 추출 → SecurityContext 에 박음
4. UserController.me(userId=1) 호출 → UserService → UserRepository → DB 조회 → DTO 변환
5. 응답 `{id: 1, nickname: "민욱", ...}`

만약 30분 뒤 다시 호출:
1. `GET /api/users/me` + 옛 access
2. 필터: "만료됨" → 401
3. 클라이언트: refresh 로 갱신 요청 → 새 access 받음
4. 원래 요청 재시도 → 이번엔 성공

사용자는 이 갱신 과정을 모름. 앱이 자동 처리.

---

## 5. 자주 헷갈리는 것들

### Q. JWT 안에 정보가 있는데, 왜 DB 를 또 조회해?
- JWT 의 payload 는 로그인 "시점의" 정보. 현재 상태가 아님.
- 예: 유저가 닉네임 바꾸거나 탈퇴해도 이미 발급된 토큰은 옛 정보
- 그래서 "최신 정보 필요하면 DB 조회" 가 원칙. JWT 는 **신원 확인 (userId 얻기)** 에만 사용.

### Q. 왜 access 와 refresh 두 개야? 그냥 수명 긴 거 하나만 쓰면 안 돼?
- **보안상 맞바꿈**: access 는 요청마다 네트워크에 돌아다녀서 노출 위험 ↑ → 짧은 수명
- refresh 는 가끔만 쓰니 노출 적음 → 긴 수명 허용
- access 털려도 금방 만료. refresh 털려도 재사용 감지로 차단 가능.

### Q. JWT 가 서명돼 있어서 못 고친다면서 왜 만료가 필요?
- 서명 위조는 불가능. 근데 **이미 발급된 진짜 토큰이 훔쳐지는 경우** 도 있음 (XSS, 중간자 공격 등)
- 훔친 토큰이 영원히 유효하면 안 되니까 만료.
- 짧은 만료 + refresh 로 "탈취 피해 최소화".

### Q. "비밀키(JWT_SECRET)" 는 어디?
- 서버의 `.env` 파일 (git 에 없음).
- 우리 프로젝트는 `application.yml` 의 `${JWT_SECRET}` placeholder 로 주입.
- 현재 prod / SSAFY 양쪽 서버에 동일 값 박혀있음.
- 유출 시 **즉시 새 값으로 교체** 하고 모든 활성 토큰 무효화 (유저 전원 재로그인 필요).

### Q. `/v1/auth/**` 가 인증 없이 되는 건 알겠는데, `/api/health` 는 왜 열어둠?
- 헬스체크는 외부 모니터링/로드밸런서가 로그인 없이 호출해야 함
- CI/CD 파이프라인도 이 엔드포인트 씀
- 민감한 정보 안 주므로 공개해도 안전

### Q. 쿠키 방식으로 하면 더 안전하다는 얘기 들어봤는데
- 맞음, **HttpOnly 쿠키** + CSRF 토큰 조합은 웹에서는 보안상 더 나은 선택.
- 하지만 **모바일 앱 중심 서비스** 라 헤더 방식이 단순하고 구현이 편함.
- 웹 대시보드 추가되면 쿠키 방식 검토 가능.

---

## 6. 실제 코드에서 확인하기

| 개념 | 파일 |
|---|---|
| JWT 설정 (시크릿/만료시간) 바인딩 | `src/main/java/com/dotshelf/global/security/JwtProperties.java` |
| JWT 생성/검증 유틸 | `src/main/java/com/dotshelf/global/security/JwtTokenProvider.java` |
| 요청마다 JWT 검사하는 필터 | `src/main/java/com/dotshelf/global/security/JwtAuthenticationFilter.java` |
| 401/403 응답 포맷 | `src/main/java/com/dotshelf/global/security/SecurityEntryPoints.java` |
| Security 전체 설정 (whitelist 포함) | `src/main/java/com/dotshelf/global/config/SecurityConfig.java` |
| @CurrentUser 주입 | `src/main/java/com/dotshelf/global/security/CurrentUserArgumentResolver.java` |
| 로그인 API | `src/main/java/com/dotshelf/domain/auth/controller/AuthController.java` |
| 로그인 로직 | `src/main/java/com/dotshelf/domain/auth/service/AuthService.java` |
| refresh 저장 엔티티 | `src/main/java/com/dotshelf/domain/auth/entity/AuthRefreshToken.java` |
| OAuth 카카오/구글 연결 정보 | `src/main/java/com/dotshelf/domain/auth/entity/UserSocialAccount.java` |

---

## 7. 챕터 2 정리

- ✅ 인증 = "누구야?", 인가 = "해도 돼?"
- ✅ 세션 대신 **JWT 토큰** 사용 (서버 stateless)
- ✅ JWT 는 Header.Payload.Signature. Signature 가 위조 방지
- ✅ OAuth2 로 카카오/구글 로그인 → 유저 정보 받아 DB 에 저장 → 우리 JWT 발급
- ✅ 요청마다 `Authorization: Bearer <access>` → `JwtAuthenticationFilter` 가 검증
- ✅ access 만료되면 refresh 로 갱신. refresh 는 DB 저장 + rotation + reuse detection

다음 챕터 👉 [**챕터 3: 엔티티 — 자바 객체와 DB 는 어떻게 이어져 있나**](./03-domain-entities.md)

JWT 발급 과정에서 나온 `User`, `AuthRefreshToken` 같은 엔티티가 어떻게 DB 테이블로 연결되는지, 왜 `Instant` / `@Builder` / `protected 생성자` 같은 패턴을 쓰는지를 다룹니다.
