# Android Debug Keystore

Android 개발 빌드용 keystore는 각 개발자가 로컬에서 생성한다. 파일과 비밀번호는 Git에 커밋하지 않는다.

```shell
keytool -genkeypair -keystore frontend/keystores/dotshelf-debug.keystore -alias dotshelfdebug -keyalg RSA -keysize 2048 -validity 10000
```

`keytool`이 요청하는 keystore/key 비밀번호를 입력하고 안전하게 보관한다. Unity의 `Edit > Project Settings > Player > Android > Publishing Settings`에서 `Custom Keystore`를 선택하고 로컬 파일, alias, 비밀번호를 입력한다.

Google Android OAuth 클라이언트에는 이 keystore의 SHA-1과 앱 패키지명을 등록한다. SHA-1은 다음 명령으로 확인한다.

```shell
keytool -list -v -keystore frontend/keystores/dotshelf-debug.keystore -alias dotshelfdebug
```

Kakao Developers에도 이 keystore에서 구한 Android key hash를 등록한다. 이전에 저장소에 포함된 공용 debug keystore의 SHA-1과 key hash는 더 이상 새 빌드의 기준으로 사용하지 않는다.

Google Play 배포용 release signing key는 별도로 관리한다.
