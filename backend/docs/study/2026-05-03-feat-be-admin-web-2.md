# Kakao Desktop OAuth 고정 콜백 연동

- **브랜치**: `feat/be/admin-web`
- **범위**: `origin/feat/be/admin-web..HEAD` (커밋 1개)
- **기간**: 2026-05-03
- **관련 커밋**: `efe1c92 [Auth] Feat: Kakao desktop OAuth 고정 콜백 연동`

## TL;DR

Unity 데스크톱에서 Kakao REST OAuth를 사용할 수 있도록 `localhost:18080` 고정 콜백 흐름을 붙였다.
Unity는 Kakao 로그인 버튼 클릭 시 로컬 HTTP 리스너를 열고 브라우저 authorize URL을 띄운 뒤, 콜백의 authorization code와 state를 검증해 BE로 보낸다.
BE는 Kakao authorization code 교환 시 요청 `redirectUri`를 우선 사용하고, 없으면 기존 `KAKAO_REDIRECT_URI` 설정값으로 fallback한다.

## 무엇을 했나

### 1. Unity Kakao Desktop OAuth 클라이언트 추가

- 사용자 관점 효과: 데스크톱/에디터에서 Kakao 로그인 버튼으로 실제 브라우저 로그인 플로우를 시작할 수 있다.
- 시스템 관점 효과: Unity가 `http://localhost:18080/oauth/kakao/callback` 로컬 리스너에서 `code`와 `state`를 받아 BE 로그인 요청으로 연결한다.
- 주요 파일: `frontend/Assets/App/Auth/KakaoDesktopOAuthClient.cs`, `frontend/Assets/App/Bootstrap/LoginScreenController.cs`

### 2. social-login 요청에 redirectUri 전달

- 사용자 관점 효과: Unity가 authorization code 발급 때 사용한 redirect URI를 BE에 그대로 보낼 수 있다.
- 시스템 관점 효과: `SocialLoginRequestDto`와 `IAuthService` 경계가 Google/Kakao authorization code flow에 필요한 redirect URI를 운반한다.
- 주요 파일: `frontend/Assets/App/Auth/IAuthService.cs`, `frontend/Assets/App/Auth/FakeAuthService.cs`, `frontend/Assets/App/Infrastructure/Api/Dtos/NotebookApiDtos.cs`

### 3. BE Kakao token exchange redirect_uri 선택 기준 보강

- 사용자 관점 효과: Kakao token endpoint에서 redirect URI 불일치로 실패할 가능성을 줄인다.
- 시스템 관점 효과: request `redirectUri`를 우선 사용하고, 비어 있으면 기존 환경변수 `KAKAO_REDIRECT_URI`로 fallback한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/service/social/KakaoTokenVerifier.java`

### 4. 회귀 테스트 추가

- 사용자 관점 효과: 같은 redirect URI 누락/불일치 문제가 다시 들어오면 테스트에서 잡힌다.
- 시스템 관점 효과: Kakao token request body에 요청 `redirectUri`가 form-urlencoded `redirect_uri`로 들어가는지 검증한다.
- 주요 파일: `backend/src/test/java/com/dotshelf/domain/auth/service/social/KakaoTokenVerifierTest.java`

## 왜 했나

현재 BE의 Kakao authorization code 교환은 `KAKAO_REDIRECT_URI` 고정 설정을 사용하고 있었다.
Unity 데스크톱 OAuth는 브라우저 콜백에서 code를 받은 뒤 BE로 code를 넘기는 구조라, code 발급 시점과 token 교환 시점의 `redirect_uri`가 정확히 일치해야 한다.
따라서 이번 작업에서는 동적 포트 대신 팀이 이미 문서화한 `http://localhost:18080/oauth/kakao/callback` 고정 포트를 기준으로 Unity와 BE 요청 계약을 맞췄다.

## 어떻게 구현했나

### Unity: local callback listener + browser authorize

`KakaoDesktopOAuthClient`는 로그인 시작 시 랜덤 `state`를 만들고, Kakao authorize URL을 구성한다.
브라우저를 열기 전에 `TcpListener`를 `18080`에 띄워 콜백을 기다리고, 콜백 요청이 loopback에서 왔는지와 path/state가 맞는지 확인한다.
성공하면 authorization code를 `LoginScreenController`로 반환하고, Unity는 `tokenType=AUTHORIZATION_CODE`와 `redirectUri`를 함께 BE에 보낸다.

핵심 포인트:
- callback URI는 `http://localhost:18080/oauth/kakao/callback`로 고정했다.
- state mismatch는 실패 처리한다.
- callback 응답에는 브라우저를 닫고 Unity로 돌아가도 된다는 간단한 HTML을 반환한다.
- `UNITY_EDITOR || UNITY_STANDALONE`에서만 Kakao Desktop OAuth 버튼 흐름이 동작한다.

### Backend: request redirectUri 우선 사용

Kakao verifier는 authorization code인 경우 `exchangeCodeForAccessToken(code, redirectUri)`를 호출한다.
이때 요청 `redirectUri`가 있으면 그 값을 token endpoint form에 넣고, 없으면 기존 `properties.kakao().redirectUri()`를 사용한다.
이 방식은 기존 모바일 SDK `ACCESS_TOKEN` 흐름에는 영향을 주지 않는다.

핵심 포인트:
- Kakao SDK access token 로그인은 기존처럼 user info API만 호출한다.
- Desktop authorization code 로그인은 `client_id`, `client_secret`, `code`, `redirect_uri`를 함께 보낸다.
- `KAKAO_CLIENT_SECRET`이 설정되어 있으면 기존처럼 form에 포함한다.

## 배운 것

- **Desktop OAuth는 앱 내부 콜백 수신자가 필요하다**: 브라우저는 Unity 함수로 직접 code를 넘길 수 없으므로, loopback HTTP listener나 deep link 같은 수신 경로가 필요하다.
- **redirect_uri는 code 발급 컨텍스트의 일부다**: authorize 요청과 token 요청의 URI가 다르면 provider가 code 교환을 거부할 수 있다.
- **고정 포트는 단순하지만 충돌 관리가 필요하다**: `localhost:18080`을 쓰면 Kakao Developers 등록과 BE 설정이 쉬워지지만, 같은 포트를 다른 프로세스가 쓰면 로그인 시작이 실패한다.
- **프론트 DTO와 BE DTO는 동시에 맞춰야 한다**: BE가 `redirectUri`를 받더라도 Unity DTO가 직렬화하지 않으면 실제 요청에는 들어가지 않는다.

## 더 공부할 것

- [ ] Unity standalone에서 loopback listener를 사용할 때 Windows 방화벽/권한 프롬프트가 어떤 조건에서 뜨는가?
- [ ] OAuth desktop 앱에서 loopback redirect와 custom URL scheme 중 어떤 방식이 배포/보안 측면에서 더 적합한가?
- [ ] Kakao Developers의 redirect URI wildcard/등록 개수 제한을 데스크톱 테스트 환경에 어떻게 반영할 것인가?
- [ ] Kakao OAuth 에러(`KOE004`, `KOE006`, `invalid_grant`)를 Unity UI 메시지로 얼마나 구체적으로 노출할 것인가?

## 참고

- 관련 커밋: `efe1c92`
- 검증: `backend`에서 `./gradlew.bat test --console=plain`
- 미검증: 현재 로컬 환경에서 Unity Editor 실행 파일과 `.sln/.csproj`를 찾지 못해 Unity 컴파일은 별도 확인 필요
