# Unity Desktop/iOS Apple 로그인 확장

- **브랜치**: `feat/fe/ioslogin`
- **범위**: `origin/dev..HEAD`
- **기간**: 2026-05-19 ~ 2026-05-19
- **티켓**: 없음

## TL;DR

Apple 로그인을 iOS 네이티브 앱과 데스크톱 Unity 실행 환경에서 모두 사용할 수 있도록 분기했다.
iOS에서는 `AuthenticationServices` 네이티브 플러그인으로 `identityToken`과 `nonce`를 받아 기존 소셜 로그인 API에 전달하고, 데스크톱에서는 브라우저 기반 Apple OAuth authorization code를 백엔드가 교환하도록 했다.
Apple의 `response_mode=form_post` 제약 때문에 백엔드에 desktop callback relay를 추가했고, relay는 Unity 로컬 loopback callback으로만 되돌아가도록 검증한다.

## 무엇을 했나

### 1. Unity Apple 로그인 플랫폼 분기

- iOS 빌드에서는 native plugin `DotShelfAppleSignIn.mm`을 통해 Apple 로그인 UI를 띄운다.
- iOS 성공 callback은 `identityToken`과 요청 때 만든 `nonce`를 `/v1/auth/social-login`으로 보낸다.
- 데스크톱과 에디터에서는 Apple 로그인 버튼이 막히지 않고, 브라우저 OAuth flow를 시작한다.
- 주요 파일: `frontend/Assets/App/Bootstrap/LoginScreenController.cs`, `frontend/Assets/Plugins/iOS/DotShelfAppleSignIn.mm`

### 2. 데스크톱 Apple OAuth flow 보강

- Apple authorization 요청에는 Services ID용 redirect URI를 사용한다.
- Unity 로컬 listener URI는 `state`에 함께 넣어 백엔드 callback relay가 다시 돌려줄 수 있게 했다.
- Apple flow에서는 PKCE를 쓰지 않고, `response_mode=form_post`를 붙여 Apple callback 정책에 맞췄다.
- 주요 파일: `frontend/Assets/App/Auth/WindowsOAuthBrowserFlow.cs`

### 3. 백엔드 authorization code 교환 지원

- `AppleTokenVerifier`가 `AUTHORIZATION_CODE` token type을 처리하도록 확장됐다.
- 백엔드는 Apple token endpoint에 code를 교환하고, 반환된 `id_token`을 기존 JWKS/audience/nonce 검증 경로로 검증한다.
- Apple client secret은 `APPLE_TEAM_ID`, `APPLE_KEY_ID`, `APPLE_PRIVATE_KEY`, `APPLE_DESKTOP_CLIENT_ID` 기반 ES256 JWT로 생성한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/service/social/AppleTokenVerifier.java`, `backend/src/main/java/com/dotshelf/global/security/SocialLoginProperties.java`

### 4. Apple desktop callback relay 추가

- Apple은 데스크톱 앱의 `http://127.0.0.1` loopback으로 직접 `form_post` redirect를 보내기 어렵다.
- 그래서 백엔드 `/v1/auth/apple/desktop-callback`이 Apple callback을 받은 뒤 Unity loopback callback으로 redirect한다.
- relay 대상은 `http://127.0.0.1`, 포트 `18080~18084`, path `/dotshelf/oauth/callback/`만 허용해 open redirect 위험을 줄였다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/auth/service/AppleDesktopOAuthRelayService.java`, `backend/src/main/java/com/dotshelf/domain/auth/controller/AuthController.java`

### 5. 운영 설정과 테스트 보강

- 배포 compose, test compose, `.env.example`, `application.yml`에 Apple desktop OAuth 설정값을 추가했다.
- controller test는 form_post 성공, query error fallback, invalid loopback 거부를 검증한다.
- verifier test는 authorization code 교환 성공, redirect URI 누락 실패, token response의 `id_token` 누락 실패를 검증한다.
- 주요 파일: `backend/src/test/java/com/dotshelf/domain/auth/controller/AuthControllerTest.java`, `backend/src/test/java/com/dotshelf/domain/auth/service/social/AppleTokenVerifierTest.java`, `frontend/SOCIAL_LOGIN_SETUP.md`

## 왜 했나

기존 Apple 로그인은 iOS 네이티브 `identityToken`을 전제로 한 구조였다. 이 방식은 iOS 앱에서는 자연스럽지만, Windows/macOS 데스크톱 Unity 실행 파일에서는 Apple 네이티브 인증 UI를 바로 사용할 수 없다.

데스크톱에서는 Apple Developer의 Services ID 기반 웹 로그인을 써야 한다. 이때 클라이언트는 authorization code를 받고, 서버가 Apple에 code를 교환하면서 client secret을 서명해야 한다. `.p8` private key는 앱에 포함되면 안 되므로 교환 책임을 백엔드로 옮기는 것이 맞다.

또한 Apple은 웹 로그인 callback에서 `response_mode=form_post`를 요구하는 경우가 많다. Unity 로컬 listener는 브라우저 redirect query를 받는 구조라서 Apple의 POST callback을 직접 받기 어렵다. 백엔드 callback relay를 둔 이유가 여기에 있다.

## 어떻게 구현했나

### 데스크톱 relay state 구조

Unity는 Apple authorization URL을 만들 때 실제 Apple Return URL과 Unity loopback callback URI를 분리한다.

```text
Apple Return URL: https://{api-host}/v1/auth/apple/desktop-callback
Unity loopback:   http://127.0.0.1:{18080~18084}/dotshelf/oauth/callback/
state:            {random}.{base64url(loopback)}
```

Apple은 백엔드 Return URL로 `code`와 `state`를 보낸다. 백엔드는 `state`에서 loopback URI를 꺼내고 허용된 로컬 callback인지 검증한 뒤, `code`, `state`, `error` 값을 Unity listener로 redirect한다.

### 백엔드 code exchange

`AppleTokenVerifier`는 `tokenType=AUTHORIZATION_CODE` 요청을 받으면 다음 순서로 처리한다.

1. `redirectUri`가 있는지 확인한다.
2. Apple client secret JWT를 ES256으로 서명한다.
3. Apple token endpoint에 `grant_type=authorization_code`, `code`, `client_id`, `client_secret`, `redirect_uri`를 보낸다.
4. 응답의 `id_token`을 기존 ID token 검증 로직으로 넘긴다.
5. issuer, audience, signature, nonce, subject를 검증한 뒤 공통 social profile로 변환한다.

핵심은 code exchange와 id token 검증을 분리하지 않은 점이다. Apple이 code를 정상 교환해도 최종 인증 판단은 서명된 `id_token` claim 검증으로 끝나야 한다.

### iOS native path

iOS 빌드에서는 Unity C#에서 Objective-C++ plugin을 호출한다.
plugin은 `ASAuthorizationAppleIDProvider` 요청을 만들고, 성공 시 `identityToken`을 UTF-8 문자열로 변환해 Unity callback object로 전달한다.
이 경로는 desktop code exchange를 타지 않고 기존 `ID_TOKEN` 로그인 API 계약을 유지한다.

## 검증

- `backend`: `.\gradlew.bat test --tests "com.dotshelf.domain.auth.service.social.*" --tests "com.dotshelf.domain.auth.controller.AuthControllerTest"`
- `frontend`: `dotnet build frontend\Assembly-CSharp.csproj`
- `git`: `git diff --cached --check`

## 배운 것

- **Apple 로그인은 플랫폼별 client id가 다르다**: iOS native는 Bundle ID, desktop/web은 Services ID를 audience로 쓰므로 백엔드 허용 목록과 Apple Developer 설정을 같이 관리해야 한다.
- **Apple code exchange secret은 서버 책임이다**: `.p8` private key로 만든 client secret은 앱에 들어가면 안 되고, 백엔드 환경변수로만 주입되어야 한다.
- **form_post callback은 데스크톱 loopback과 바로 맞지 않는다**: Apple callback을 백엔드가 받은 뒤 검증된 local loopback으로 relay해야 Unity 브라우저 flow와 연결된다.
- **relay는 open redirect 검증이 핵심이다**: state에 redirect URI를 넣는 구조는 편하지만, host/port/path allowlist가 없으면 외부 redirect 취약점이 될 수 있다.
- **nonce는 두 경로 모두 유지해야 한다**: native token path와 desktop code path 모두 최종 `id_token`의 nonce를 검증해야 로그인 요청과 토큰 응답이 묶인다.

## 더 공부할 것

- [ ] Apple Services ID Return URL을 운영/테스트 API 도메인별로 어떻게 분리해서 관리할지 정리
- [ ] Bundle ID와 Services ID에서 Apple `sub` 안정성이 달라지는지 확인하고 계정 병합 정책 검토
- [ ] iOS Xcode capability와 entitlements 설정을 Unity build pipeline에서 자동화할 수 있는지 확인
- [ ] Apple token exchange 실패 로그를 운영에서 식별하기 쉽게 error code/metric으로 분리
