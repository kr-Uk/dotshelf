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

### Google BE Request

```json
{
  "provider": "GOOGLE",
  "token": "google id_token 또는 server auth code"
}
```

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

### Kakao Redirect URI

Desktop REST OAuth 흐름에서 Unity 데스크탑 앱이 authorization code를 받을 URI다.
BE는 이 URI를 직접 호출받지 않고, token exchange 요청의 `redirect_uri` 값으로만 사용한다.

```text
http://localhost:18080/oauth/kakao/callback
```

Kakao Developers 설정:

```text
내 애플리케이션
-> 앱 선택
-> 앱 설정
-> 앱 키
-> REST API 키 수정
-> 카카오 로그인 리다이렉트 URI
-> http://localhost:18080/oauth/kakao/callback 추가
```

API 서버 주소를 카카오 로그인 리다이렉트 URI로 등록하지 않는다.

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

Desktop 구현 순서:

1. 로그인 시작 시 랜덤 `state`를 생성해서 앱 메모리에 저장한다.
2. Kakao authorize URL에 `redirect_uri=http://localhost:18080/oauth/kakao/callback`와 `state`를 함께 넣는다.
3. Unity 데스크탑 앱이 `localhost:18080` local callback으로 `code`와 `state`를 받는다.
4. 받은 `state`가 저장한 값과 같을 때만 아래 BE 요청을 보낸다.

```json
{
  "provider": "KAKAO",
  "tokenType": "AUTHORIZATION_CODE",
  "token": "Kakao authorization code",
  "redirectUri": "http://localhost:18080/oauth/kakao/callback"
}
```

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

## Platform Summary

| Platform | Google | Kakao | Apple |
| --- | --- | --- | --- |
| Android mobile | Google Android Client ID 사용 | Kakao SDK `accessToken` 전달 | Services ID 기반 `identityToken` 전달 |
| Android tablet | Android mobile과 동일 | Android mobile과 동일 | Android mobile과 동일 |
| iOS | Google iOS Client ID 사용 | Kakao SDK `accessToken` 전달 | Apple 로그인 `identityToken` 전달 |
| Desktop | Google Desktop Client ID 또는 server auth code 흐름 | Kakao REST OAuth `authorization code` 전달 | Services ID 기반 `identityToken` 전달 |

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
