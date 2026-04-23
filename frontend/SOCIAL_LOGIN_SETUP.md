# DotShelf Social Login Frontend Setup

Unity 프론트에서 Google, Kakao 소셜 로그인을 연동할 때 필요한 공통 설정과 BE 요청 형식이다.

## Backend Endpoint

```http
POST https://api.dotshelf.digital/v1/auth/social-login
Content-Type: application/json
```

BE 응답의 `accessToken`, `refreshToken`은 Google/Kakao 토큰이 아니라 DotShelf 자체 JWT다.

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

Desktop REST OAuth 흐름에서 사용한다.

```text
https://api.dotshelf.digital/v1/auth/kakao/callback
```

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

Desktop은 Kakao REST OAuth로 받은 authorization code를 BE로 보낸다.

```json
{
  "provider": "KAKAO",
  "tokenType": "AUTHORIZATION_CODE",
  "token": "Kakao authorization code"
}
```

Desktop에서 callback URL로 직접 받는 경우:

```text
https://api.dotshelf.digital/v1/auth/kakao/callback?code={authorization_code}
```

## Platform Summary

| Platform | Google | Kakao |
| --- | --- | --- |
| Android mobile | Google Android Client ID 사용 | Kakao SDK `accessToken` 전달 |
| Android tablet | Android mobile과 동일 | Android mobile과 동일 |
| iOS | Google iOS Client ID 사용 | Kakao SDK `accessToken` 전달 |
| Desktop | Google Desktop Client ID 또는 server auth code 흐름 | Kakao REST OAuth `authorization code` 전달 |

## Do Not Put These In Frontend

아래 값은 Unity 앱, Git, 문서에 넣지 않는다.

```text
GOOGLE_CLIENT_SECRET
KAKAO_CLIENT_SECRET
JWT_SECRET
Kakao Admin Key
refresh token
access token 고정값
```
