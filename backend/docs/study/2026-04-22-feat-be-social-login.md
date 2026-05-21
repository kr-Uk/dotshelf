# 소셜 로그인 토큰 검증과 Unity 클라이언트 설정 흐름

- **브랜치**: `feat/be/social_login`
- **범위**: `13d9ded..HEAD` (`origin/dev`와의 merge-base 기준, 커밋 1개)
- **기간**: 2026-04-22 ~ 2026-04-22
- **티켓**: 없음

## TL;DR

DotShelf 로그인 정책을 자체 회원가입이 아닌 소셜 계정 전용 로그인으로 맞추기 위해 Google/Kakao/Apple 토큰 검증 계층을 추가했다. 클라이언트가 넘기는 임의 사용자 정보가 아니라 각 provider의 토큰을 BE가 검증한 뒤 `provider + providerUserId` 기준으로 유저를 찾거나 생성한다. Unity의 데스크탑, Android, iOS 환경을 고려해 Google은 여러 OAuth Client ID를 audience로 허용하고, 로컬 실행용 `.env.example`, Android 팀 디버그 키스토어, Google OAuth JSON도 함께 정리했다.

## 무엇을 했나

### 1. 소셜 로그인 요청 모델을 토큰 중심으로 변경

- 사용자 관점 효과: 클라이언트는 provider와 provider별 토큰만 넘기면 되고, 유저 식별 정보는 서버가 검증된 토큰에서 추출한다.
- 시스템 관점 효과: 클라이언트가 임의로 `email`, `nickname`, `providerUserId`를 조작해 가입/로그인하는 경로를 줄인다.
- 주요 파일: `src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java`, `src/main/java/com/dotshelf/domain/auth/service/AuthService.java`

### 2. Google/Kakao/Apple provider 검증기 추가

- 사용자 관점 효과: Google, Kakao, Apple 계정으로 같은 `/v1/auth/social-login` 진입점을 사용할 수 있다.
- 시스템 관점 효과: provider별 토큰 검증 방식은 분리하고, `AuthService`는 `SocialUserInfo`라는 동일한 결과 모델만 사용한다.
- 주요 파일: `src/main/java/com/dotshelf/domain/auth/service/social/*`

### 3. 소셜 계정 매핑과 사용자 생성 규칙 정리

- 사용자 관점 효과: 같은 소셜 계정으로 재로그인하면 기존 유저로 로그인되고, 신규 계정이면 자동 가입된다.
- 시스템 관점 효과: `provider`를 문자열 대신 `SocialProvider` enum으로 고정하고, 이메일은 중복 가능성을 고려해 nullable로 정리했다.
- 주요 파일: `src/main/java/com/dotshelf/domain/auth/entity/UserSocialAccount.java`, `src/main/java/com/dotshelf/domain/user/entity/User.java`

### 4. 로컬 설정과 클라이언트 개발 자산 정리

- 사용자 관점 효과: 실제 비밀값은 `.env.local`에 두고, 팀원은 `.env.example`을 보고 필요한 값을 채울 수 있다.
- 시스템 관점 효과: Google OAuth JSON과 Android 공용 debug keystore를 버전 관리해 Unity Android 테스트 환경을 맞추기 쉬워졌다.
- 주요 파일: `.env.example`, `src/main/resources/application.yml`, `../frontend/ANDROID_DEBUG_KEYSTORE.md`, `../json/client_secret_*.json`

## 왜 했나

DotShelf는 일반 이메일/비밀번호 로그인을 열지 않고 소셜 계정만으로 인증하는 방향을 잡았다. 그러면 백엔드는 클라이언트가 보내준 사용자 프로필을 신뢰하면 안 되고, Google/Kakao/Apple이 발급한 토큰을 직접 검증해 "이 provider의 이 subject가 맞다"를 확인해야 한다.

또한 Unity 클라이언트는 데스크탑, Android 태블릿/휴대폰, iOS처럼 플랫폼이 나뉜다. Google OAuth는 플랫폼별 Client ID가 다르므로 BE가 받아들일 audience 목록을 설정으로 관리해야 하고, Android 개발 중에는 같은 debug keystore를 써야 팀원 PC마다 SHA-1이 달라지는 문제를 줄일 수 있다.

## 어떻게 구현했나

### AuthService는 provider별 세부사항을 모르게 분리

기존 요청은 클라이언트가 `providerUserId`, `email`, `nickname`까지 보내는 구조였지만, 이제 `provider + token`만 받는다. 실제 사용자 정보는 `SocialTokenVerifierRegistry`가 provider에 맞는 검증기를 찾아 추출한다.

```java
public AuthTokenResponse socialLogin(SocialLoginRequest request) {
    SocialUserInfo socialUser = socialTokenVerifierRegistry.verify(request.provider(), request.token());
    UserSocialAccount socialAccount = socialAccountRepository
            .findByProviderAndProviderUserId(socialUser.provider(), socialUser.providerUserId())
            .orElse(null);
    boolean newUser = socialAccount == null;
    User user = newUser ? createUserAndSocialAccount(socialUser) : userService.getUser(socialAccount.getUserId());
    user.markLogin();
    TokenPair tokenPair = issueTokens(user.getId());
    return new AuthTokenResponse(user.getId(), tokenPair.accessToken(), tokenPair.refreshToken(), newUser, user.isOnboarded());
}
```

핵심 포인트:
- 로그인 식별 키는 이메일이 아니라 `provider + providerUserId`다.
- 이메일이 중복이면 새 유저의 `email`을 `null`로 두어 이메일 기반 자동 계정 병합을 하지 않는다.
- nickname은 비어 있거나 중복될 수 있으므로 fallback과 suffix 처리를 한다.

### JWT 기반 provider는 공통 검증 support로 묶음

Google `id_token`과 Apple `identity_token`은 JWT다. 둘 다 JWKS로 서명을 검증하고, issuer와 audience를 확인해야 하므로 `JwtSocialTokenVerifierSupport`를 만들었다.

```java
NimbusJwtDecoder decoder = NimbusJwtDecoder.withJwkSetUri(jwksUri).build();
OAuth2TokenValidator<Jwt> validator = new DelegatingOAuth2TokenValidator<>(
        JwtValidators.createDefault(),
        new JwtIssuerValidator(issuers),
        new JwtAudienceValidator(clientIds)
);
decoder.setJwtValidator(validator);
```

핵심 포인트:
- JWKS URI는 provider가 공개한 공개키 목록이다.
- `iss`는 토큰 발급자가 맞는지 확인한다.
- `aud`는 이 토큰이 DotShelf의 OAuth Client ID를 대상으로 발급됐는지 확인한다.

### Google은 id_token과 server auth code를 모두 받게 함

Unity 환경에서는 클라이언트 구현 방식에 따라 Google `id_token`을 직접 받을 수도 있고, server auth code를 받아 BE가 Google token endpoint에 교환해야 할 수도 있다. 그래서 Google 검증기는 토큰 문자열이 JWT 형태인지 확인하고, JWT가 아니면 authorization code로 간주해 `id_token`으로 교환한다.

```java
private String exchangeCodeForIdToken(String code) {
    MultiValueMap<String, String> body = new LinkedMultiValueMap<>();
    body.add("grant_type", "authorization_code");
    body.add("code", code);
    body.add("client_id", clientId);
    body.add("client_secret", clientSecret);

    Map<String, Object> response = restClient.post()
            .uri(properties.google().tokenUri())
            .contentType(MediaType.APPLICATION_FORM_URLENCODED)
            .body(body)
            .retrieve()
            .body(RESPONSE_TYPE);
    return stringValue(response.get("id_token"));
}
```

핵심 포인트:
- `GOOGLE_CLIENT_SECRET`은 server auth code 교환에만 필요하다.
- `GOOGLE_CLIENT_IDS`에는 Web, Android, Desktop, iOS Client ID를 모두 넣어 platform별 `aud`를 허용한다.
- 실제 secret 값은 `.env.local`에만 두고 git에는 `.env.example`만 올린다.

### Kakao는 access token으로 사용자 정보 API를 호출

Kakao는 Google/Apple처럼 현재 구현에서 JWT를 직접 decode하지 않고, Kakao access token을 `Authorization: Bearer`로 실어 `https://kapi.kakao.com/v2/user/me`를 호출한다. 응답의 `id`를 provider user id로 쓰고, `kakao_account`, `profile`, `properties`에서 이메일/닉네임/프로필 이미지를 꺼낸다.

핵심 포인트:
- provider별 응답 JSON 구조가 다르므로 map 추출 로직은 Kakao 검증기 내부에 둔다.
- Kakao 이메일은 사용자 동의 항목에 따라 없을 수 있으므로 nullable을 전제로 처리한다.
- 외부 API 호출 실패나 응답 누락은 `INVALID_TOKEN`으로 통일한다.

### 설정은 ConfigurationProperties로 모음

`application.yml`은 `.env`와 `.env.local`을 optional import하고, 소셜 로그인 관련 값을 `social.*` 아래로 묶었다. 실제 바인딩은 `SocialLoginProperties`가 담당한다.

```yaml
social:
  google:
    server-client-id: ${GOOGLE_SERVER_CLIENT_ID:${GOOGLE_CLIENT_ID:}}
    client-ids: ${GOOGLE_CLIENT_IDS:${GOOGLE_CLIENT_ID:}}
    client-secret: ${GOOGLE_CLIENT_SECRET:}
  kakao:
    user-info-uri: https://kapi.kakao.com/v2/user/me
  apple:
    client-ids: ${APPLE_CLIENT_IDS:${APPLE_CLIENT_ID:}}
```

핵심 포인트:
- 로컬 실행자는 `.env.local`만 채우면 된다.
- CI/운영은 환경변수로 같은 키를 주입하면 된다.
- Apple의 audience는 Google OAuth iOS client id가 아니라 실제 Apple Sign in에서 쓰는 Bundle ID 또는 Services ID를 넣어야 한다.

## 배운 것

- **ID Token과 Access Token의 역할 차이**: ID Token은 "사용자가 누구인지"를 서버가 검증하기 좋은 JWT이고, Access Token은 provider API 호출 권한을 나타내는 토큰이다.
- **OAuth Client ID와 audience 검증**: Google/Apple JWT의 `aud`는 토큰이 어떤 클라이언트를 대상으로 발급됐는지 나타내므로, BE는 허용할 Client ID 목록을 명시적으로 관리해야 한다.
- **Android SHA-1과 debug keystore**: Android OAuth Client ID는 package name과 SHA-1에 묶인다. 팀원이 같은 debug keystore를 쓰면 로컬 테스트에서 같은 SHA-1을 공유할 수 있다.
- **소셜 로그인에서 이메일은 식별자가 아니다**: provider가 이메일을 주지 않거나, 사용자가 provider별로 같은 이메일을 쓸 수 있으므로 로그인 식별자는 `provider + providerUserId`로 잡는 것이 안전하다.
- **provider별 검증 전략 분리**: Google/Apple은 JWT decode, Kakao는 user info API 호출처럼 방식이 다르므로 공통 결과 모델만 맞추고 검증기는 분리하는 편이 유지보수에 유리하다.

## 더 공부할 것

- [ ] Google Unity 로그인에서 `id_token` 직접 전달 방식과 server auth code 전달 방식 중 어떤 흐름이 데스크탑/Android/iOS에서 가장 안정적인가?
- [ ] Apple Sign in에서 Bundle ID와 Services ID를 각각 언제 audience로 사용해야 하며, Unity iOS 빌드에서는 어떤 값이 `sub` 안정성에 영향을 주는가?
- [ ] Kakao access token 검증을 user info API 호출로만 처리할 때 rate limit, timeout, retry 정책을 어떻게 잡아야 하는가?
- [ ] refresh token 만료가 24시간보다 길어져야 하는 자동 로그인 요구사항을 DB rotation/reuse detection 구조와 어떻게 맞출 것인가?
- [ ] 소셜 계정 병합을 나중에 허용한다면 이메일 기반 자동 병합 대신 어떤 사용자 확인 UX와 BE 트랜잭션이 필요한가?

## 참고

- 관련 커밋: `ae9eed3`
- 관련 코드: `backend/src/main/java/com/dotshelf/domain/auth/service/social`
- 관련 설정: `backend/.env.example`, `backend/src/main/resources/application.yml`
- 관련 문서: `frontend/ANDROID_DEBUG_KEYSTORE.md`
