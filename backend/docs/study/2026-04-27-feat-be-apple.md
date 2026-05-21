# Apple 로그인 1차 연동 준비

- **브랜치**: `feat/be/apple`
- **범위**: `origin/dev..HEAD`
- **기간**: 2026-04-27 ~ 2026-04-27
- **티켓**: 없음

## TL;DR

Apple Sign in에서 발급되는 `identityToken`을 기존 공통 소셜 로그인 엔드포인트(`/v1/auth/social-login`)로 받을 수 있도록 운영 설정과 연동 문서를 맞췄다.
백엔드 Java 검증기는 이미 존재했으므로, 이번 작업은 `APPLE_CLIENT_ID(S)`가 컨테이너까지 전달되도록 배포 compose를 보강하고 실제 값은 source-controlled 예시가 아니라 app 서버 `/opt/dotshelf/.env`에 넣는 절차를 문서화하는 데 초점을 뒀다.
또한 로컬 JWKS 서버와 테스트용 RSA 키로 Apple JWT 검증 경로를 단위 테스트해 issuer/audience/signature/nonce 검증이 실제로 동작하는지 확인했다.

## 무엇을 했나

### 1. Apple audience 설정을 배포 경로에 연결

- 사용자 관점 효과: Apple 로그인 후 받은 `identityToken`이 백엔드에서 `aud` 검증을 통과할 수 있다.
- 시스템 관점 효과: blue/green 컨테이너가 `APPLE_CLIENT_ID`, `APPLE_CLIENT_IDS` 환경변수를 Spring Boot 설정으로 받을 수 있다.
- 주요 파일: `backend/deploy/app/docker-compose.yml`, `backend/.env.example`

### 2. Apple identityToken 검증 테스트 추가

- 사용자 관점 효과: Apple 로그인 연동이 배포 전 테스트에서 깨지는지 확인할 수 있다.
- 시스템 관점 효과: `AppleTokenVerifier`가 JWKS 서명 검증, issuer 검증, audience 검증, nonce 검증, `sub` 추출, email nullable 처리를 모두 타는 테스트를 갖게 됐다.
- 주요 파일: `backend/src/test/java/com/dotshelf/domain/auth/service/social/AppleTokenVerifierTest.java`

### 3. 프론트/운영 문서 정리

- 사용자 관점 효과: Unity/프론트 담당자가 Apple 로그인 성공 후 어떤 body로 백엔드에 보내야 하는지 명확해졌다.
- 시스템 관점 효과: Bundle ID와 Services ID가 `APPLE_CLIENT_IDS`에 같이 들어가야 하는 이유, 그리고 현재 배포에서 값의 source가 app 서버 `/opt/dotshelf/.env`라는 점을 남겨 운영 설정 누락 가능성을 줄였다.
- 주요 파일: `frontend/SOCIAL_LOGIN_SETUP.md`, `backend/docs/infra/gitlab-ci-variables.md`

## 왜 했나

기존 소셜 로그인 구조는 Google/Kakao/Apple을 모두 `/v1/auth/social-login` 하나로 받고, `provider` 값에 따라 검증기를 선택한다. 따라서 Apple 전용 callback API를 새로 만드는 대신 클라이언트가 Apple 로그인으로 받은 `identityToken`을 기존 공통 API로 보내는 방식이 현재 코드와 가장 잘 맞는다.

Apple JWT는 `aud`가 실제 로그인 클라이언트 식별자와 일치해야 한다. iOS 네이티브 로그인은 Bundle ID가 audience가 될 수 있고, Web/Android/Desktop용 Services ID 흐름은 Services ID가 audience가 될 수 있다. 그래서 source-controlled Unity/Xcode 앱 식별자와 Apple Developer 설정이 확정된 뒤, 그 값을 모두 허용 목록에 넣어야 플랫폼별 토큰을 안전하게 검증할 수 있다.

또한 Apple 로그인은 nonce를 identity token에 포함할 수 있다. 백엔드가 요청 nonce와 토큰 claim을 비교해야 다른 로그인 시도에서 발급된 토큰을 잘못 받아들이는 상황을 줄일 수 있다.

## 어떻게 구현했나

### 배포 compose에서 Apple env 전달

`application.yml`은 이미 아래 형태로 Apple client id 목록을 읽고 있었다.

```yaml
social:
  apple:
    client-ids: ${APPLE_CLIENT_IDS:${APPLE_CLIENT_ID:}}
```

하지만 컨테이너 실행 환경에 값이 들어가지 않으면 설정은 빈 목록이 된다. 그래서 `api-blue`, `api-green` 양쪽에 동일하게 Apple 변수를 전달했다.

```yaml
APPLE_CLIENT_ID: ${APPLE_CLIENT_ID}
APPLE_CLIENT_IDS: ${APPLE_CLIENT_IDS}
```

핵심 포인트:
- blue/green 어느 색이 떠도 같은 audience 설정을 받는다.
- 서버 `/opt/dotshelf/.env`에 값이 있으면 `docker compose`가 컨테이너 환경변수로 주입한다.
- `.env.example`에는 실제 Bundle ID/Services ID를 고정하지 않고 placeholder만 남겼다. 실제 값은 repo의 앱 식별자와 맞는지 확인한 뒤 서버 `.env`에 넣는다.

### Apple nonce 검증

공통 로그인 요청 DTO에 `nonce` 필드를 추가하고, Apple verifier에서 `identityToken`의 `nonce` claim과 요청 `nonce`를 비교한다.

```java
private void validateNonce(Jwt jwt, String expectedNonce) {
    String actualNonce = jwt.getClaimAsString("nonce");
    if (actualNonce == null || !actualNonce.equals(expectedNonce)) {
        throw new CustomException(ErrorCode.INVALID_TOKEN);
    }
}
```

핵심 포인트:
- `provider=APPLE`에서는 nonce가 필수다.
- Google/Kakao는 기존 요청과 호환되도록 nonce를 받더라도 사용하지 않는다.
- 클라이언트는 Apple 요청에 넣은 nonce와 같은 값을 `/v1/auth/social-login`에도 보내야 한다.

### 로컬 JWKS 서버로 JWT 검증 경로 테스트

Apple의 실제 JWKS를 테스트에서 직접 호출하면 네트워크 상태와 외부 키 회전에 영향을 받는다. 대신 테스트 안에서 RSA 키를 만들고, 공개키를 로컬 HTTP 서버의 `/keys`로 노출했다.

```java
jwksServer.createContext("/keys", exchange -> {
    byte[] response = new JWKSet(rsaKey.toPublicJWK())
            .toString()
            .getBytes(StandardCharsets.UTF_8);
    exchange.getResponseHeaders().add("Content-Type", "application/json");
    exchange.sendResponseHeaders(200, response.length);
    try (OutputStream outputStream = exchange.getResponseBody()) {
        outputStream.write(response);
    }
});
```

테스트 JWT는 같은 RSA 키의 private key로 서명하고, issuer는 `https://appleid.apple.com`, audience는 실제 Bundle ID로 맞췄다. 이렇게 하면 `NimbusJwtDecoder`가 실제 운영과 같은 흐름으로 JWKS를 가져와 서명과 claim을 검증한다.

핵심 포인트:
- `tokenType = ID_TOKEN` 성공 케이스
- `tokenType = null` 기본 처리 케이스
- `tokenType = ACCESS_TOKEN` 거부 케이스
- Apple email이 없을 수 있음을 테스트로 명시

## 배운 것

- **Apple Client ID의 의미**: Apple의 `aud`는 항상 하나의 값이지만, 플랫폼에 따라 Bundle ID 또는 Services ID가 들어올 수 있다. 백엔드는 허용할 audience 목록을 명시적으로 관리해야 하고, repo의 실제 앱 식별자와 불일치하면 로그인은 실패한다.
- **Return URL과 API 엔드포인트의 차이**: Apple Developer의 Return URL은 브라우저 redirect 대상이고, `/v1/auth/social-login`은 JSON POST API다. 현재 구조에서는 Return URL을 백엔드 API로 연결하지 않는다.
- **JWKS 기반 JWT 테스트 방법**: 외부 provider의 공개키 엔드포인트를 테스트에서 직접 쓰기보다 로컬 JWKS 서버를 띄우면 빠르고 결정적인 테스트가 가능하다.
- **nonce 검증의 API 계약성**: nonce는 프론트가 Apple 요청에 넣고 백엔드가 토큰 claim과 대조해야 하므로 DTO, verifier, 프론트 문서가 함께 바뀌어야 한다.
- **서버 `.env`와 GitLab Variables의 역할 차이**: 현재 DotShelf 배포에서는 GitLab Variables가 주로 SSH/배포 경로를 제공하고, 앱 런타임 OAuth 설정은 SSAFY 서버의 `/opt/dotshelf/.env`에서 읽힌다.

## 더 공부할 것

- [ ] Apple Services ID 기반 웹 로그인에서 `response_mode=form_post`와 `identityToken` 수신 방식은 Unity/Desktop에서 어떻게 연결하는가?
- [ ] Apple `sub`는 Team ID와 client_id 조합에 따라 안정성이 달라지는지, Bundle ID와 Services ID를 동시에 쓸 때 계정 연결 전략은 어떻게 잡아야 하는가?
- [ ] 현재처럼 클라이언트가 `identityToken`을 직접 보내는 방식과 서버가 authorization code를 교환하는 방식의 보안/운영 차이는 무엇인가?
- [ ] Apple 이메일이 최초 로그인 이후 누락될 때 온보딩/프로필 보완 플로우를 백엔드 응답 모델에 어떻게 표현하는 게 좋은가?

## 참고

- 관련 커밋: Apple 로그인 1차 연동 및 리뷰 반영 커밋
- 관련 API: `POST /v1/auth/social-login`
- 관련 설정: `APPLE_CLIENT_ID={apple-bundle-id-or-services-id}`, `APPLE_CLIENT_IDS={apple-bundle-id},{apple-services-id}`
