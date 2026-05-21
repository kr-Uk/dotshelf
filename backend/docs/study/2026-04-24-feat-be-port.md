# 로컬 프론트 3001 CORS 허용

- **브랜치**: `feat/be/port`
- **범위**: `origin/dev..HEAD` 기준 push 예정 변경
- **기간**: 2026-04-24
- **티켓**: 없음

## TL;DR

프론트엔드가 `http://localhost:3001` 개발 서버에서 백엔드 API를 호출할 수 있도록 CORS 허용 origin을 추가했다.
같은 로컬 테스트 환경에서 흔히 쓰는 `localhost:3000`, `127.0.0.1:3000`, `127.0.0.1:3001`도 함께 열어 개발 환경 차이를 줄였다.
서버가 3001 포트로 직접 listen하도록 바꾼 것은 아니며, 브라우저의 cross-origin 요청을 백엔드가 허용하도록 만든 변경이다.

## 무엇을 했나

### 1. 로컬 프론트 개발 서버 origin 허용

- 사용자 관점 효과: 프론트 개발자가 `localhost:3001`에서 실행한 화면으로 백엔드 API를 직접 호출해 테스트할 수 있다.
- 시스템 관점 효과: Spring MVC CORS 매핑에 로컬 개발 origin을 명시해 preflight 요청과 실제 API 요청이 차단되지 않도록 했다.
- 주요 파일: `backend/src/main/java/com/dotshelf/global/config/WebConfig.java`

### 2. 개발 편의용 origin 범위 정리

- 사용자 관점 효과: 브라우저 주소를 `localhost` 또는 `127.0.0.1`로 열어도 같은 방식으로 테스트할 수 있다.
- 시스템 관점 효과: 허용 origin을 와일드카드로 풀지 않고 필요한 개발 포트만 열어 범위를 제한했다.
- 주요 파일: `backend/src/main/java/com/dotshelf/global/config/WebConfig.java`

## 왜 했나

프론트엔드에서 3001 포트를 열어달라는 요청이 있었다. 이 요청은 일반적으로 백엔드가 3001 포트로 동작해야 한다는 뜻이 아니라, `http://localhost:3001`에서 실행되는 프론트 개발 서버가 `http://localhost:8080` 또는 운영 API 도메인으로 요청할 때 CORS에 막히지 않게 해달라는 의미다.

기존 `SecurityConfig`에는 `http.cors()`가 켜져 있었지만, 실제 허용 origin을 정의하는 `CorsConfigurationSource` 또는 MVC CORS 매핑은 없었다. 따라서 브라우저 기반 테스트에서는 preflight 요청이 실패할 수 있었다.

## 어떻게 구현했나

### WebConfig에 CORS 매핑 추가

기존 `WebConfig`는 `CurrentUserArgumentResolver`만 등록하고 있었다. 여기에 `WebMvcConfigurer#addCorsMappings`를 추가해 모든 API 경로에 대해 로컬 프론트 개발 origin을 허용했다.

```java
@Override
public void addCorsMappings(CorsRegistry registry) {
    registry.addMapping("/**")
            .allowedOrigins(
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://127.0.0.1:3000",
                    "http://127.0.0.1:3001")
            .allowedMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .allowedHeaders("*")
            .exposedHeaders("Authorization")
            .allowCredentials(true)
            .maxAge(3600);
}
```

핵심 포인트:
- `allowedOrigins("*")`를 쓰지 않고 개발에 필요한 origin만 명시했다.
- credential이 필요한 요청을 고려해 `allowCredentials(true)`를 유지할 수 있는 형태로 origin을 제한했다.
- JWT 응답/갱신 흐름에서 프론트가 `Authorization` 헤더를 읽을 가능성을 고려해 `exposedHeaders("Authorization")`를 열었다.
- `OPTIONS`를 허용해 브라우저 preflight 요청이 통과할 수 있게 했다.

## 검증

- `./gradlew.bat build -x test --console=plain` 성공
- `./gradlew.bat test --console=plain` 성공
- Docker Desktop 데몬이 실행 중이 아니어서 PostgreSQL/Redis 의존 부팅 smoke test는 수행하지 못했다.

## 배운 것

- **포트 오픈과 CORS 허용은 다르다**: 서버 방화벽에서 3001을 여는 것은 네트워크 접근 제어 문제이고, 이번 변경은 `localhost:3001` origin에서 오는 브라우저 요청을 백엔드가 허용하는 문제다.
- **Spring Security의 `http.cors()`만으로는 충분하지 않을 수 있다**: CORS 필터를 활성화하더라도 실제 허용 origin, method, header 정책이 별도로 정의되어야 브라우저 요청이 통과한다.
- **credentials와 wildcard origin은 같이 쓰기 어렵다**: 쿠키/인증 정보를 포함하는 요청까지 고려하면 `*` 대신 명시적인 origin 목록을 유지하는 편이 안전하다.

## 더 공부할 것

- [ ] Spring Security 6/Boot 4에서 `CorsConfigurationSource` Bean과 `WebMvcConfigurer#addCorsMappings` 중 어떤 방식을 우선 적용하는가?
- [ ] 운영 환경에서 프론트 도메인이 확정되면 CORS origin을 환경변수로 외부화하는 구조가 필요한가?
- [ ] JWT를 Authorization 헤더로만 주고받을 때 `allowCredentials(true)`가 실제로 필요한가?
- [ ] WebSocket, SSE 등 실시간 기능이 들어오면 CORS와 origin 검증 정책을 어떻게 분리해야 하는가?

## 참고

- 관련 파일: `backend/src/main/java/com/dotshelf/global/config/WebConfig.java`
- 관련 요청: 프론트 로컬 테스트를 위한 3001 포트 허용
