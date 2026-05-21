# Desktop Apple Services ID 분리

- **브랜치**: `feat/fe/ioslogin`
- **범위**: `origin/feat/fe/ioslogin..HEAD` (커밋 1개)
- **기간**: 2026-05-20 ~ 2026-05-20
- **관련 커밋**: `b816d845`

## TL;DR

Desktop Apple 로그인에서 iOS/mobile용 Services ID를 재사용하지 않고, Desktop/Web 전용 Services ID인 `com.jihocompany.dotshelf.desktop`을 사용하도록 Unity 기본값과 LoginScene 설정을 맞췄다.
Apple OAuth의 `invalid_client`는 Unity 로직 문제가 아니라 Apple Developer에 등록된 Services ID와 authorize 요청의 `client_id`가 맞지 않을 때 발생한다.
문서에는 mobile/desktop Services ID를 분리해 `APPLE_CLIENT_IDS`와 `APPLE_DESKTOP_CLIENT_ID`가 서로 어떤 역할을 하는지 남겼다.

## 무엇을 했나

### 1. Desktop Apple clientId 기본값 추가

- 사용자 관점 효과: LoginScene 인스펙터에 Apple 항목이 누락되어도 desktop Apple OAuth가 `client_id` 누락으로 바로 중단되지 않는다.
- 시스템 관점 효과: Desktop/Web Apple OAuth가 서버 `client_secret` JWT의 `sub`와 같은 Services ID를 사용한다.
- 주요 파일: `frontend/Assets/App/Auth/WindowsOAuthBrowserFlow.cs`

### 2. LoginScene Apple provider 항목 추가

- 사용자 관점 효과: Unity Inspector의 `Desktop Browser OAuth Providers`에서 Apple 설정을 직접 확인할 수 있다.
- 시스템 관점 효과: Google/Kakao와 같은 배열에 Apple provider가 직렬화되어 씬 단위 설정 누락 가능성이 줄었다.
- 주요 파일: `frontend/Assets/Scenes/LoginScene.unity`

### 3. Apple 설정 문서 정리

- 사용자 관점 효과: Apple Developer에서 mobile Services ID와 desktop Services ID를 구분해 설정할 수 있다.
- 시스템 관점 효과: BE audience allowlist(`APPLE_CLIENT_IDS`)와 desktop token exchange client id(`APPLE_DESKTOP_CLIENT_ID`)의 차이를 문서화했다.
- 주요 파일: `frontend/SOCIAL_LOGIN_SETUP.md`

## 왜 했나

Desktop Apple 로그인 버튼 클릭 시 Apple authorize 화면에서 `invalid_client`가 발생했다. 원인은 iOS/native와 Desktop/Web의 Apple client id 체계가 다르기 때문이다.

iOS native 로그인은 보통 Bundle ID 또는 mobile용 Services ID가 audience로 쓰이지만, desktop 브라우저 OAuth는 Apple Developer의 Services ID가 authorize 요청의 `client_id`가 된다. 기존에 mobile에서 쓰던 `.signin` 식별자가 이미 사용 중이었기 때문에 desktop 전용 `.desktop` Services ID로 분리했다.

## 어떻게 구현했나

### Desktop Services ID를 코드 기본값으로 둠

```csharp
private const string AppleDesktopClientId = "com.jihocompany.dotshelf.desktop";

// Apple Desktop/Web uses the public Services ID as client_id.
// The private key/client_secret stays server-side.
clientId = AppleDesktopClientId,
```

핵심 포인트:
- Services ID는 secret이 아니라 public client id라 Unity 앱에 포함 가능하다.
- `.p8` private key와 Apple `client_secret` 생성 책임은 계속 BE에 남긴다.
- `authorizationRedirectUri`는 기존 BE relay callback을 유지한다.

### LoginScene에 Apple provider를 명시

```yaml
- provider: 2
  authorizeEndpoint:
  clientId: com.jihocompany.dotshelf.desktop
  scope:
  responseType:
  authorizationRedirectUri:
  useNonce: 1
  extraQuery:
```

Inspector에는 client id만 명시하고, authorize endpoint/scope/response mode는 `OAuthBrowserDefaults.MergeWithDefaults`가 채운다. 이렇게 하면 씬에는 환경별로 바뀔 수 있는 값만 두고, OAuth provider의 표준 설정은 코드 기본값으로 유지할 수 있다.

## 배운 것

- **Apple Services ID는 플랫폼별로 분리할 수 있다**: mobile/web/desktop을 같은 값으로 억지로 묶지 말고, 실제 Apple Developer Identifier와 BE audience allowlist를 맞추는 것이 더 안전하다.
- **`invalid_client`는 서버 callback 이전 단계의 실패다**: Apple authorize 화면에서 바로 뜨면 BE의 token exchange나 Unity loopback 문제가 아니라 `client_id` 등록/활성화 문제부터 봐야 한다.
- **`APPLE_CLIENT_IDS`와 `APPLE_DESKTOP_CLIENT_ID`는 역할이 다르다**: 전자는 id_token audience 검증 목록이고, 후자는 desktop authorization code를 Apple token endpoint에서 교환할 때 쓰는 client id다.
- **Unity scene YAML은 공백도 diff 품질에 영향이 있다**: 빈 필드를 추가할 때 trailing whitespace가 생기기 쉬워 `git diff --check`로 잡아야 한다.

## 더 공부할 것

- [ ] Apple Developer의 Bundle ID, Services ID, Sign in with Apple Key가 각각 OAuth/OIDC의 어느 필드에 대응되는지 더 정확히 정리하기
- [ ] Apple `sub`가 Bundle ID와 Services ID를 넘나들 때 동일 사용자를 안정적으로 식별할 수 있는지 확인하기
- [ ] Desktop Services ID를 dev/prod로 분리해야 하는지, 하나의 Services ID에 여러 Return URL을 두는 방식이 충분한지 검토하기
- [ ] Unity 씬 직렬화 변경에서 빈 YAML 필드와 trailing whitespace를 자동으로 정리하는 pre-commit 규칙을 둘지 검토하기

## 참고

- 관련 커밋: `b816d845`
- 관련 문서: `frontend/SOCIAL_LOGIN_SETUP.md`
- 운영 설정: `.p8` private key, `APPLE_TEAM_ID`, `APPLE_KEY_ID`는 repo가 아니라 서버 env 또는 secret store에만 둔다.
