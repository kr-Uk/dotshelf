# DotShelf Social Login Frontend Setup

Unity 프론트에서 Google, Kakao, Apple 소셜 로그인을 연동할 때 필요한 공통 설정과 BE 요청 형식이다.

## Backend Endpoint

```http
POST https://api.dotshelf.digital/v1/auth/social-login
Content-Type: application/json
```

BE 응답의 `accessToken`, `refreshToken`은 Google/Kakao/Apple 토큰이 아니라 DotShelf 자체 JWT다.

## App Identifiers

```text
Android package name: com.jihocompany.dotshelf
iOS bundle ID: com.jihocompany.dotshelf
```

Android mobile과 Android tablet은 같은 Android 설정을 사용한다.

## Android Debug Keystore

Unity Android 개발 빌드는 아래 debug keystore를 사용한다.

```text
Keystore path: frontend/keystores/dotshelf-debug.keystore
Alias: dotshelfdebug
Keystore password: android
Key password: android
```

Unity 설정 위치:

```text
Unity
-> Edit
-> Project Settings
-> Player
-> Android
-> Publishing Settings
-> Custom Keystore 체크
```

## Google

### Google Android SHA-1

```text
40:BB:69:92:B3:77:19:1F:99:6F:37:08:0B:E5:F4:92:4C:FC:D1:7E
```

### Google OAuth Client IDs

```text
Google Web/Server Client ID:
502970247403-pkmsp8nfdlcs6i48skqer3n0ie4d9se6.apps.googleusercontent.com

Google Android Client ID:
502970247403-tlqvh5ulnaa6mkb4h481o9untb5a3lha.apps.googleusercontent.com

Google Desktop Client ID:
502970247403-j8f8ctqsdk9h1kog2ahiu2n9g0usb9ha.apps.googleusercontent.com

Google iOS Client ID:
502970247403-hkqc9hr7osl902nnp11deuccigin4fi4.apps.googleusercontent.com
```

Google은 플랫폼 구현 방식에 따라 `id_token` 또는 `server auth code`를 BE로 보낸다.

Desktop은 `WindowsOAuthBrowserFlow.OAuthBrowserDefaults`에 Desktop Client ID가 hardcoded되어
있어 인스펙터 입력 없이 바로 동작한다. 환경별로 다른 clientId가 필요하면 LoginScreenController
의 `Desktop Browser OAuth Providers` 인스펙터로 override.

Google은 RFC 8252대로 loopback IP의 모든 port를 자동 허용하므로 Kakao처럼 redirect URI를
콘솔에 일일이 등록할 필요 없음.

Desktop은 **PKCE (RFC 7636)** 흐름 사용 — `client_secret` 없이 `code_verifier` + `code_challenge`
로 Google이 검증. Desktop client는 본질적으로 public client이므로 client_secret 의존 제거가
정공법 (native app OAuth standard).

### Google BE Request

Mobile (id_token 직접 전송):

```json
{
  "provider": "GOOGLE",
  "tokenType": "ID_TOKEN",
  "token": "google id_token"
}
```

Desktop (PKCE authorization-code):

```json
{
  "provider": "GOOGLE",
  "tokenType": "AUTHORIZATION_CODE",
  "token": "google authorization code (= code 값과 동일)",
  "code": "google authorization code",
  "redirectUri": "http://127.0.0.1:{선택된_port}/dotshelf/oauth/callback/",
  "codeVerifier": "PKCE code_verifier (RFC 7636)"
}
```

BE는 `codeVerifier` 있으면 PKCE 흐름 (`client_secret` 없이 `code_verifier`로 교환), 없으면
legacy server pair (`GOOGLE_CLIENT_SECRET`) fallback. Desktop client_id는 BE의
`GOOGLE_DESKTOP_CLIENT_ID` env로 등록 필요 — FE authorize 시 사용한 client_id와 정확 일치.

## Kakao

### Kakao Android Key Hash

Kakao Developers Android 플랫폼에는 SHA-1이 아니라 아래 key hash를 등록한다.

```text
QLtpkrN3GR+ZbzcIC+X0kkz80X4=
```

### Kakao Native App Key

Unity Android/iOS Kakao SDK 설정에 사용한다.

```text
eeabd73a76a6aa3fa5619b73836df269
```

### Kakao REST API Key (Desktop OAuth용)

Desktop authorization-code 흐름에서 사용. **Native App Key와 다름** — Kakao 콘솔의 \"앱 키\"
탭에서 \"REST API 키\"로 별도 표기되는 값을 등록한다.

현재 FE 코드 (`WindowsOAuthBrowserFlow.OAuthBrowserDefaults`)에 hardcoded된 값:

```text
d8c6ed5428758bd9a50e1f41a70d3d69
```

이 값은 인프라 담당자가 운영하는 Dotshelf 카카오 앱의 REST API Key와 일치해야 한다. 다르면
KOE006 (등록되지 않은 redirect URI) 또는 KOE101 (invalid_client)로 흐름 깨짐 — Kakao가
client_id로 매칭한 앱과 redirect URI 등록한 앱이 달라지기 때문.

### Kakao 동의항목 (scope)

FE가 authorize 호출 시 보내는 scope. 콘솔의 \"카카오 로그인 → 동의항목\" 활성화 상태와
정확히 일치해야 한다 (활성화 안 된 항목 보내면 KOE205).

```text
account_email profile_nickname
```

각 항목은 사용자가 OAuth 동의 화면에서 선택적으로 거부 가능 → BE 응답에서 해당 필드 null
가능. BE의 `User.email`은 nullable + UNIQUE 제거 (V17 migration). 사용자 nickname 거부 시
처리는 BE의 `KakaoTokenVerifier`가 임시 닉네임 생성 (현재 처리 검증은 follow-up).
콘솔에서 동의항목 추가/제거 시 FE `OAuthBrowserDefaults.CreateDefault(Kakao).scope`도 동기 갱신.

### Kakao Redirect URI

Desktop REST OAuth 흐름에서 Unity 데스크탑 앱이 authorization code를 받을 URI다.
BE는 이 URI를 직접 호출받지 않고, token exchange 요청의 `redirect_uri` 값으로만 사용한다.

Kakao OAuth는 redirect URI를 콘솔 등록값과 **정확 일치** 검증 (와일드카드 X). 임의 ephemeral
port를 쓰면 등록 불가하므로 **고정 port range 5개를 사전 등록**하고, FE는 비어있는 첫 port를
차례로 시도한다 (`WindowsOAuthBrowserFlow.PreferredCallbackPorts`).

```text
http://127.0.0.1:18080/dotshelf/oauth/callback/
http://127.0.0.1:18081/dotshelf/oauth/callback/
http://127.0.0.1:18082/dotshelf/oauth/callback/
http://127.0.0.1:18083/dotshelf/oauth/callback/
http://127.0.0.1:18084/dotshelf/oauth/callback/
```

주의:
- `127.0.0.1` 사용 (방화벽/사용자 hosts 영향 회피, RFC 8252 권장).
- `localhost` 등록은 OAuth 제공자별 동작이 갈리므로 사용 X.
- path 끝의 trailing slash `/` 필수 — Unity HttpListener prefix가 trailing slash를 요구하고
  카카오는 정확 일치이므로 콘솔에도 동일하게 등록.

Kakao Developers 설정:

```text
내 애플리케이션
-> 앱 선택
-> 앱 설정
-> 앱 키
-> REST API 키 수정
-> 카카오 로그인 리다이렉트 URI
-> 위 5개 URI 모두 추가
```

API 서버 주소를 카카오 로그인 리다이렉트 URI로 등록하지 않는다.

port range 변경 시 (코드 + 콘솔 동시 갱신 필요):
- FE: `frontend/Assets/App/Auth/WindowsOAuthBrowserFlow.cs` 의 `PreferredCallbackPorts`
- 카카오 콘솔: 위 등록 목록

### Kakao Android / iOS BE Request

Android mobile, Android tablet, iOS는 Kakao SDK 로그인 후 받은 `accessToken`을 BE로 보낸다.

```json
{
  "provider": "KAKAO",
  "tokenType": "ACCESS_TOKEN",
  "token": "Kakao SDK accessToken"
}
```

### Kakao Desktop BE Request

Desktop은 Kakao REST OAuth로 받은 authorization code를 BE로 보낸다. 브라우저 callback 응답에서 DotShelf JWT를 직접 받는 경로는 사용하지 않는다.

Desktop 구현 순서 (`WindowsOAuthBrowserFlow.ExecuteAsync`):

1. 로그인 시작 시 랜덤 `state` + PKCE `code_verifier` 생성 (RFC 7636).
2. `code_challenge = BASE64URL(SHA256(code_verifier))` 계산.
3. `PreferredCallbackPorts` (18080~18084)를 차례로 시도해 비어있는 첫 port에 HttpListener 시작.
4. Kakao authorize URL에 `redirect_uri`, `state`, `code_challenge`, `code_challenge_method=S256` 포함.
5. Unity 데스크탑 앱이 해당 port local callback으로 `code`와 `state`를 받음.
6. 받은 `state`가 저장한 값과 같을 때만 아래 BE 요청 (`SubmitSocialLoginCode`) — `codeVerifier` 함께 전달.

```json
{
  "provider": "KAKAO",
  "tokenType": "AUTHORIZATION_CODE",
  "token": "Kakao authorization code",
  "redirectUri": "http://127.0.0.1:{선택된_port}/dotshelf/oauth/callback/",
  "codeVerifier": "PKCE code_verifier"
}
```

BE는 `codeVerifier`를 Kakao token 교환 호출에 그대로 forward해서 Kakao가 SHA256(code_verifier)
== code_challenge 검증 (defense in depth — Kakao는 PKCE + client_secret 동시 사용 가능).
`redirectUri`는 카카오 콘솔 등록값과 정확 일치해야 함.

## Apple

Apple은 클라이언트에서 받은 `identityToken`을 BE로 보낸다. BE는 Apple JWKS로 서명을 검증하고, `aud`가 `APPLE_CLIENT_IDS`에 포함되는지 확인한다.

### Apple Identifiers

```text
iOS Bundle ID: {apple-bundle-id}
Services ID: {apple-services-id}
```

iOS 네이티브 Apple 로그인은 보통 Bundle ID가 `identityToken`의 `aud`로 들어온다. WebView, Android, Desktop 등 Services ID 기반 흐름을 쓰면 Services ID가 `aud`가 되므로 BE의 `APPLE_CLIENT_IDS`에 함께 등록한다.
Apple Developer 값은 Unity `ProjectSettings`와 Xcode 프로젝트의 실제 Bundle ID가 확정된 뒤 그 값과 일치시킨다.

백엔드 런타임 환경변수에는 아래 값을 등록한다. 현재 배포에서는 GitLab CI/CD Variables가 아니라 app 서버 `/opt/dotshelf/.env`가 런타임 source다.

```text
APPLE_CLIENT_ID={apple-bundle-id-or-services-id}
APPLE_CLIENT_IDS={apple-bundle-id},{apple-services-id}
APPLE_DESKTOP_CLIENT_ID={apple-services-id}
APPLE_TEAM_ID={apple-team-id}
APPLE_KEY_ID={apple-signin-key-id}
APPLE_PRIVATE_KEY={apple-p8-private-key}
APPLE_DESKTOP_REDIRECT_URI=https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback
```

### Apple BE Request

Apple 로그인 시작 전에 매번 충분히 긴 랜덤 `nonce`를 만든다. 클라이언트는 Apple 로그인 요청에 넣은 `nonce`와 같은 값을 BE에도 전달해야 한다.
플랫폼 SDK가 raw nonce 대신 SHA-256 nonce를 Apple 요청에 넣도록 요구하면, BE에는 `identityToken`의 `nonce` claim과 동일하게 들어갈 값을 보낸다.

```json
{
  "provider": "APPLE",
  "tokenType": "ID_TOKEN",
  "token": "Apple identityToken",
  "nonce": "Apple 요청에 넣은 nonce"
}
```

`tokenType`을 생략해도 BE는 Apple 토큰을 `ID_TOKEN`으로 처리한다. 단, `provider=APPLE`에서는 `nonce`가 필수이며, BE는 `identityToken`의 `nonce` claim과 요청 `nonce`가 다르면 토큰을 거절한다.

Apple은 이메일과 이름을 최초 동의 시점에만 내려줄 수 있다. 이후 로그인에서는 `email`이 없을 수 있으므로, 클라이언트는 로그인 성공 후 사용자 프로필 보완 화면을 열 수 있어야 한다.

### Apple Desktop BE Request

Desktop Unity는 Apple이 localhost loopback URL을 직접 redirect target으로 쓰기 어려우므로, Apple authorize 요청에는 BE의 HTTPS 콜백을 사용한다.

```text
https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback
```

이 URL은 Apple Developer의 Services ID Return URL에 등록되어 있어야 한다. Unity는 state 안에 실제 loopback callback URL을 base64url로 넣고, BE 콜백은 Apple form_post 응답을 다시 Unity loopback으로 302 redirect한다.

Unity `LoginScreenController`의 `Desktop Browser OAuth Providers`에 Apple 항목을 추가할 때 `clientId`는 Apple Services ID를 넣는다. `authorizationRedirectUri`를 비워두면 기본값으로 `https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback`을 사용한다.

Unity가 최종적으로 BE `/v1/auth/social-login`에 보내는 요청:

```json
{
  "provider": "APPLE",
  "tokenType": "AUTHORIZATION_CODE",
  "token": "Apple authorization code",
  "redirectUri": "https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback",
  "nonce": "Apple authorize 요청에 넣은 nonce"
}
```

BE는 `APPLE_TEAM_ID`, `APPLE_KEY_ID`, `APPLE_PRIVATE_KEY`로 Apple `client_secret` JWT를 생성해 Apple token endpoint에서 `id_token`으로 교환한 뒤, 기존 Apple JWKS/nonce 검증을 그대로 수행한다.
Apple Desktop flow는 PKCE를 사용하지 않는다. Apple Services ID Return URL allowlist, 서버에서만 생성하는 `client_secret` JWT, 그리고 `id_token` nonce 검증을 조합해 authorization code 교환을 보호한다.
CSRF/state 검증은 Unity loopback listener에서 수행한다. BE relay는 state에 포함된 loopback URI를 decode하되, redirect 대상은 `http://127.0.0.1:18080~18084/dotshelf/oauth/callback/`으로만 제한한다.

`APPLE_PRIVATE_KEY`는 Sign in with Apple `.p8` private key이므로 Git, Slack, MR 본문, Unity 앱에 넣지 않는다. 운영 환경에서는 승인된 secret store 또는 서버 전용 secret 주입 경로에서만 로드하고, key id와 함께 rotation 일정을 관리한다.

Desktop Apple 수동 검증:
- Apple Developer Services ID Return URL에 `https://dev-api.dotshelf.digital/v1/auth/apple/desktop-callback` 등록
- Windows 또는 macOS desktop build/editor에서 Apple 로그인 버튼 클릭
- Apple 로그인 후 BE callback이 Unity loopback으로 돌아오는지 확인
- `/v1/auth/social-login` 요청의 `tokenType=AUTHORIZATION_CODE`, `redirectUri`, `nonce` 전달 확인
- 로그인 취소 시 사용자 메시지와 버튼 재활성화 확인
- 임의 state redirect URI가 BE에서 400으로 거부되는지 확인

## Platform Summary

| Platform | Google | Kakao | Apple |
| --- | --- | --- | --- |
| Android mobile | Google Android Client ID 사용 | Kakao SDK `accessToken` 전달 | Services ID 기반 `identityToken` 전달 |
| Android tablet | Android mobile과 동일 | Android mobile과 동일 | Android mobile과 동일 |
| iOS | Google iOS Client ID 사용 | Kakao SDK `accessToken` 전달 | Apple 로그인 `identityToken` 전달 |
| Desktop | Google Desktop Client ID 또는 server auth code 흐름 | Kakao REST OAuth `authorization code` 전달 | Services ID 기반 `authorization code` 전달 |

## BE Environment Variables 요구사항

배포 서버 (`/opt/dotshelf/.env`)에 아래 값이 채워져 있어야 한다. dev/prod 양쪽 동일.

| Env Var | 용도 | 출처 |
| --- | --- | --- |
| `GOOGLE_SERVER_CLIENT_ID` | Google id_token `aud` 검증용 (Web/Server clientId) | Google Cloud Console |
| `GOOGLE_CLIENT_IDS` | Google id_token 다중 audience 검증. Desktop/Android/iOS client_id 자동 포함됨 | Google Cloud Console |
| `GOOGLE_CLIENT_SECRET` | Mobile server auth code 흐름 fallback용 (PKCE 흐름은 불필요) | Google Cloud Console (Web/Server Client) |
| **`GOOGLE_DESKTOP_CLIENT_ID`** | **Desktop PKCE 흐름의 client_id (FE authorize 시 사용한 값과 정확 일치 필수)** | **Google Cloud Console (Desktop Client)** |
| `KAKAO_REST_API_KEY` | Desktop authorization-code 교환 + access_token 검증 | Kakao Developers (REST API 키, **Native App Key 아님**) |
| `KAKAO_CLIENT_SECRET` | Kakao 보안 강화 (선택, 콘솔에서 발급한 경우만). PKCE와 동시 사용 가능 (defense in depth) | Kakao Developers |
| `KAKAO_REDIRECT_URI` | Desktop code 교환 호출 시 default redirect URI fallback (FE가 매번 보내므로 사실상 미사용) | 위 등록 URI 중 하나 |
| `APPLE_CLIENT_IDS` | Apple identityToken `aud` 검증 (Bundle ID + Services ID 콤마구분) | Apple Developer |
| `APPLE_DESKTOP_CLIENT_ID` | Desktop/Web Apple authorize 시 사용하는 Services ID. BE가 `client_secret` JWT의 `sub`로 사용 | Apple Developer |
| `APPLE_TEAM_ID` | Apple `client_secret` JWT의 issuer | Apple Developer |
| `APPLE_KEY_ID` | Sign in with Apple key id. JWT header `kid` | Apple Developer |
| `APPLE_PRIVATE_KEY` | Sign in with Apple `.p8` private key. 서버 전용 secret store/주입 경로로만 보관 | Apple Developer |
| `APPLE_DESKTOP_REDIRECT_URI` | Apple Services ID Return URL. 기본값/운영값 모두 HTTPS BE 콜백 URL 사용 | Apple Developer / BE 배포 URL |

PKCE 흐름 (Desktop):
- `client_secret` 의존 제거 — Desktop client는 본질적으로 public이므로 PKCE가 정공법 (RFC 8252)
- BE는 `codeVerifier` 있으면 PKCE 모드, 없으면 server pair fallback (mobile 흐름 호환)
- 머지 후 dev/prod `.env`에 `GOOGLE_DESKTOP_CLIENT_ID` 추가 필요

확인 방법 (서버 접근 가능 시):
```bash
ssh ubuntu@<dev-server> 'grep -E "GOOGLE_|KAKAO_|APPLE_" /opt/dotshelf/.env | sed "s/=.*/=***/"'
```

## Do Not Put These In Frontend

아래 값은 Unity 앱, Git, 문서에 넣지 않는다.

```text
GOOGLE_CLIENT_SECRET
KAKAO_CLIENT_SECRET
JWT_SECRET
Kakao Admin Key
Apple private key
Apple team key id
refresh token
access token 고정값
DotShelf JWT 고정값
```
