# Apple Desktop callback CORS 허용

- **브랜치**: `feat/fe/apple`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-05-20 ~ 2026-05-20

## TL;DR

Desktop Apple 로그인에서 Apple 계정 인증까지는 통과했지만, Apple이 백엔드 callback으로 보내는 `form_post` 요청이 Spring CORS 필터에서 `Invalid CORS request`로 막혔다.

`/v1/auth/apple/desktop-callback` 경로에 한정해 `https://appleid.apple.com` Origin을 허용했고, 실제 Spring Security/CORS 필터를 통과하는 통합 테스트를 추가했다. 기존 컨트롤러 단위 테스트는 standalone MockMvc라 CORS 필터 회귀를 잡지 못했기 때문에, 이번에는 필터 체인을 포함하는 테스트로 보강했다.

## 무엇을 했나

### 1. Apple callback 전용 CORS Origin 추가

- 사용자 관점 효과: Apple 로그인 완료 후 `Invalid CORS request` 페이지에서 멈추지 않고 Unity loopback callback으로 돌아갈 수 있다.
- 시스템 관점 효과: 전체 API가 아니라 Apple Desktop callback 경로에만 Apple Origin을 허용한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/global/config/WebConfig.java`

### 2. Spring Security 필터 포함 통합 테스트 추가

- 사용자 관점 효과: 실제 브라우저에서 재현된 Apple `form_post` CORS 차단을 테스트로 고정한다.
- 시스템 관점 효과: standalone controller test가 놓친 WebConfig/Security CORS 필터 동작을 검증한다.
- 주요 파일: `backend/src/test/java/com/dotshelf/integration/SecurityFilterIntegrationTest.java`

## 왜 했나

Apple Desktop OAuth 흐름은 일반 API 요청처럼 Unity가 직접 백엔드에 요청하는 구조가 아니다. Apple 인증이 끝나면 Apple 페이지가 `response_mode=form_post`로 백엔드 callback URL에 POST를 보낸다.

이때 브라우저 요청에는 `Origin: https://appleid.apple.com`이 붙고, Spring Security의 CORS 필터는 기존 허용 목록에 Apple Origin이 없으면 컨트롤러 진입 전에 403을 반환한다. 실제 배포 서버에서도 같은 요청을 보내면 `403 Invalid CORS request`가 재현됐다.

## 어떻게 구현했나

### callback 경로에만 Apple Origin 허용

```java
private static final String APPLE_SIGN_IN_ORIGIN = "https://appleid.apple.com";

registry.addMapping("/v1/auth/apple/desktop-callback")
        .allowedOrigins(APPLE_SIGN_IN_ORIGIN)
        .allowedMethods("GET", "POST", "OPTIONS")
        .allowedHeaders("*")
        .maxAge(3600);
```

핵심 포인트:
- Apple Origin은 전체 `/**`에 넣지 않고 callback 경로에만 허용했다.
- Apple은 authorize 결과를 `form_post`로 보내므로 `POST` 허용이 필요하다.
- 개발/호환용 query callback도 유지되므로 `GET`도 같이 열었다.

### CORS 필터까지 태우는 테스트

```java
mockMvc.perform(post("/v1/auth/apple/desktop-callback")
                .header(HttpHeaders.ORIGIN, APPLE_ORIGIN)
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .param("code", "apple-code")
                .param("state", state))
        .andExpect(status().isFound())
        .andExpect(header().string(HttpHeaders.ACCESS_CONTROL_ALLOW_ORIGIN, APPLE_ORIGIN));
```

핵심 포인트:
- `@SpringBootTest` + `@AutoConfigureMockMvc` 기반 테스트라 Spring Security/CORS 필터가 포함된다.
- 응답이 403이 아니라 302 redirect인지 확인한다.
- `Access-Control-Allow-Origin`이 Apple Origin으로 내려오는지도 확인한다.

## 배운 것

- **CORS는 컨트롤러 테스트만으로는 충분하지 않다**: `MockMvcBuilders.standaloneSetup`은 WebConfig와 Security filter chain을 통과하지 않으므로 CORS 회귀를 놓칠 수 있다.
- **Apple `form_post`는 서버 간 호출이 아니다**: Apple 페이지가 사용자의 브라우저에서 백엔드로 POST하므로 Origin 기반 CORS 검사를 받는다.
- **허용 범위는 경로 단위로 좁히는 편이 낫다**: `appleid.apple.com`을 전체 API에 허용할 필요는 없고 callback endpoint만 열면 된다.

## 더 공부할 것

- [ ] Spring Security 6/Boot 4에서 `CorsConfigurationSource` bean과 `WebMvcConfigurer#addCorsMappings`의 적용 순서
- [ ] OAuth `response_mode=form_post`가 브라우저 CORS 정책과 만나는 실제 조건
- [ ] Apple callback endpoint를 별도 filter chain으로 분리할 필요가 있는지
- [ ] 운영에서 CORS 403을 빠르게 탐지하기 위한 로그/메트릭 설계

## 참고

- 관련 커밋: `8c19fc31`
- 관련 증상: Apple 로그인 후 `https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback`에서 `Invalid CORS request`
- 검증:
  - `./gradlew.bat test --tests com.dotshelf.integration.SecurityFilterIntegrationTest`
  - `./gradlew.bat test --tests com.dotshelf.domain.auth.controller.AuthControllerTest`
