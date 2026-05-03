# Google OAuth authorization code 교환 redirect_uri 전달 수정

- **브랜치**: `feat/be/admin-web`
- **범위**: `origin/feat/be/admin-web..HEAD` (커밋 1개)
- **기간**: 2026-05-03
- **관련 커밋**: `a012dcf [BE] Fix: Google OAuth code redirect URI 전달`

## TL;DR

Unity Google OAuth에서 받은 authorization code를 백엔드가 Google token endpoint로 교환할 때 `redirect_uri`가 누락되어 `invalid_grant`가 발생하던 흐름을 수정했다.
`POST /v1/auth/social-login` 요청 DTO에 `redirectUri`를 추가하고, Google verifier까지 전달한 뒤 token 교환 form에 `redirect_uri`를 포함했다.
인터페이스 시그니처 변경으로 Apple/Kakao verifier 호출부도 함께 맞췄고, Google verifier 단위 테스트로 form 전송 여부를 고정했다.

## 무엇을 했나

### 1. 소셜 로그인 요청 DTO에 redirectUri 추가

- 사용자 관점 효과: Unity 클라이언트가 authorization code 발급 때 사용한 redirect URI를 백엔드에 그대로 전달할 수 있다.
- 시스템 관점 효과: 백엔드가 Google token endpoint에 code, client 정보, redirect URI를 모두 포함해 OAuth2 authorization code 교환 계약을 만족한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java`, `backend/src/main/java/com/dotshelf/domain/auth/service/AuthService.java`

### 2. SocialTokenVerifier 계약 확장

- 사용자 관점 효과: 기존 Google ID token, Apple identity token, Kakao access token 로그인 흐름은 유지된다.
- 시스템 관점 효과: provider별 verifier가 authorization code 교환에 필요한 부가 파라미터를 받을 수 있는 구조가 됐다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/service/social/SocialTokenVerifier.java`, `backend/src/main/java/com/dotshelf/domain/auth/service/social/SocialTokenVerifierRegistry.java`

### 3. Google token 교환 form에 redirect_uri 포함

- 사용자 관점 효과: `401 유효하지 않은 토큰입니다`로만 보이던 실패의 실제 원인인 Google `invalid_grant`를 제거한다.
- 시스템 관점 효과: code 발급 시점의 redirect URI와 token 교환 시점의 redirect URI가 일치하도록 백엔드가 클라이언트 값을 전달한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/service/social/GoogleTokenVerifier.java`

### 4. 회귀 테스트 추가

- 사용자 관점 효과: 같은 문제가 다시 생기면 테스트 단계에서 잡힌다.
- 시스템 관점 효과: authorization code 교환 요청 body에 `redirect_uri`가 포함되는지, 누락 시 `INVALID_INPUT`이 나는지 검증한다.
- 주요 파일: `backend/src/test/java/com/dotshelf/domain/auth/service/social/GoogleTokenVerifierTest.java`

## 왜 했나

Google OAuth2 authorization code flow에서는 token endpoint로 code를 교환할 때, code 발급 당시 사용한 `redirect_uri`와 같은 값을 다시 보내야 한다.
Unity 클라이언트는 이미 `redirectUri=http://127.0.0.1:<port>/dotshelf/oauth/callback/`를 body에 포함해 보내고 있었지만, 백엔드 `SocialLoginRequest`에 필드가 없어 값이 DTO 단계에서 사라졌다.
그 결과 `GoogleTokenVerifier.exchangeCodeForIdToken()`이 `redirect_uri` 없이 Google token endpoint를 호출했고, Google은 code와 redirect URI 검증에 실패해 `invalid_grant`를 반환했다.

## 어떻게 구현했나

### 요청 경로: DTO에서 verifier까지 전달

`SocialLoginRequest`에 `redirectUri`를 추가하고, `AuthService`가 `SocialTokenVerifierRegistry`를 호출할 때 해당 값을 함께 넘기도록 했다.
기존 verifier 인터페이스는 `token`, `tokenType`, `nonce`만 받았기 때문에 `redirectUri` 파라미터를 추가했고, Apple/Kakao verifier는 현재 사용하지 않으므로 시그니처만 맞췄다.

핵심 포인트:
- JSON body의 `redirectUri`가 Java record 필드로 바인딩된다.
- provider registry는 provider에 맞는 verifier로 `redirectUri`를 전달한다.
- Apple nonce 검증과 Kakao access token 교환 로직은 기능 변화 없이 유지된다.

### Google token exchange: redirect_uri form 값 추가

Google verifier는 authorization code로 판정된 경우에만 `exchangeCodeForIdToken(code, redirectUri)`를 호출한다.
이때 `redirectUri`가 비어 있으면 Google에 애매한 요청을 보내지 않고 `INVALID_INPUT`으로 빠르게 실패시킨다.

핵심 포인트:
- `tokenType=AUTHORIZATION_CODE`이면 code 교환 경로를 탄다.
- Google token endpoint form에 `grant_type`, `code`, `client_id`, `client_secret`, `redirect_uri`를 모두 포함한다.
- `tokenType=ID_TOKEN`이면 기존처럼 code 교환 없이 JWT 검증으로 바로 간다.

### 테스트: 외부 Google 대신 로컬 token server 사용

`GoogleTokenVerifierTest`는 JDK `HttpServer`로 임시 token endpoint를 띄운다.
테스트는 실제 `RestClient` 요청 body를 캡처해서 `redirect_uri`가 form-urlencoded 값으로 들어갔는지 확인한다.
응답의 `id_token`은 일부러 유효하지 않은 값으로 두어, token 교환 요청 자체가 끝난 뒤 검증 실패가 `INVALID_TOKEN`으로 이어지는 흐름만 확인한다.

## 배운 것

- **authorization code는 redirect URI와 묶여 있다**: code는 단독 토큰이 아니라 client id, redirect URI, 만료 시간 같은 발급 컨텍스트와 함께 검증된다.
- **프론트가 보낸 필드도 DTO에 없으면 사라진다**: Jackson이 알 수 없는 JSON 필드를 무시하면 요청 body에는 있어도 서비스 계층에서는 사용할 수 없다.
- **OAuth 에러는 백엔드 예외 메시지만 보면 원인이 흐려진다**: 클라이언트에는 `INVALID_TOKEN`으로 보여도, provider 응답의 `invalid_grant` 원인을 로그나 CLI로 확인해야 정확히 고칠 수 있다.
- **외부 API 연동은 요청 form 테스트가 중요하다**: JWT 검증 성공 여부보다 provider에 어떤 form을 보냈는지가 이번 버그의 핵심이었다.

## 더 공부할 것

- [ ] OAuth2 authorization code flow에서 `redirect_uri` 일치 검증이 보안적으로 어떤 공격을 막는가?
- [ ] Unity/Native OAuth에서 loopback redirect URI와 PKCE를 함께 쓰는 권장 패턴은 무엇인가?
- [ ] Google OAuth client type(Web/Desktop/Android)에 따라 `client_secret`과 `redirect_uri` 요구사항이 어떻게 달라지는가?
- [ ] provider별 OAuth 에러 응답을 내부 `ErrorCode`와 로그 메시지에 어느 수준까지 노출할 것인가?

## 참고

- 관련 커밋: `a012dcf`
- 검증: `./gradlew.bat build --console=plain`
