# Android Debug Keystore

Unity Android 개발 빌드에서 모든 개발자가 같은 Google OAuth SHA-1을 쓰기 위한 공용 debug keystore다.

## 파일

```text
frontend/keystores/dotshelf-debug.keystore
```

## Keystore 정보

```text
Alias: dotshelfdebug
Keystore password: android
Key password: android
SHA-1: 40:BB:69:92:B3:77:19:1F:99:6F:37:08:0B:E5:F4:92:4C:FC:D1:7E
```

Google Cloud Console의 Android OAuth Client ID에는 아래 조합을 등록한다.

```text
Package name: Unity Player Settings의 Android Package Name
SHA-1: 40:BB:69:92:B3:77:19:1F:99:6F:37:08:0B:E5:F4:92:4C:FC:D1:7E
```

## Unity 설정

```text
Unity
→ Edit
→ Project Settings
→ Player
→ Android
→ Publishing Settings
→ Custom Keystore 체크
→ Keystore: frontend/keystores/dotshelf-debug.keystore
→ Keystore password: android
→ Alias: dotshelfdebug
→ Key password: android
```

## 주의

- 이 keystore는 개발/디버그 빌드 전용이다.
- Google Play 배포용 release signing key 또는 Play App Signing SHA-1은 별도로 등록해야 한다.
- 이미 Google Console에 등록한 다른 SHA-1을 계속 쓰려면, 그 SHA-1을 만든 원본 keystore 파일을 사용해야 한다.
