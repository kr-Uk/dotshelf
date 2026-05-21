# 헬스체크 엔드포인트 /health 전환

- **브랜치**: `fix/be/health-endpoint`
- **범위**: `origin/dev..HEAD` 기준 push 예정 변경
- **기간**: 2026-04-24
- **티켓**: 없음

## TL;DR

백엔드 헬스체크 공개 경로를 `/api/health`에서 `/health`로 통일했다.
`HealthController`의 클래스 레벨 `/api` prefix를 제거하고, `SecurityConfig`, Docker `HEALTHCHECK`, GitLab CI, Blue-Green 배포 smoke test가 모두 새 경로를 사용하도록 맞췄다.
검증 스킬과 현재 학습/아키텍처 문서도 `/health` 기준으로 갱신해 이후 자동 점검이 예전 경로를 다시 사용하지 않도록 했다.

## 무엇을 했나

### 1. HealthController 공개 경로 변경

- 사용자 관점 효과: `https://api.dotshelf.digital/health`로 서비스 상태를 확인할 수 있다.
- 시스템 관점 효과: API prefix가 붙지 않는 표준적인 health endpoint를 제공해 배포/모니터링 경로를 단순화했다.
- 주요 파일: `backend/src/main/java/com/dotshelf/global/common/HealthController.java`

### 2. 인증 예외 경로 동기화

- 사용자 관점 효과: 헬스체크 호출이 JWT 없이 정상 응답한다.
- 시스템 관점 효과: `SecurityConfig` whitelist가 실제 컨트롤러 경로와 일치한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/global/config/SecurityConfig.java`

### 3. 배포/컨테이너 헬스체크 경로 동기화

- 사용자 관점 효과: 배포 파이프라인과 컨테이너 healthcheck가 새 엔드포인트 기준으로 성공/실패를 판정한다.
- 시스템 관점 효과: Dockerfile, Blue-Green direct smoke, public smoke, post-cutover polling이 모두 `/health`를 사용한다.
- 주요 파일: `backend/Dockerfile`, `.ci/deploy-blue-green.sh`, `.ci/poll-health.sh`, `.gitlab-ci.yml`

### 4. 현재 문서와 검증 스킬 갱신

- 사용자 관점 효과: 이후 개발자가 문서나 스킬을 따라 검증할 때 오래된 `/api/health` 경로를 사용하지 않는다.
- 시스템 관점 효과: `dotshelf-verify`의 smoke test 예시와 architecture 기준 문서가 실제 운영 경로와 일치한다.
- 주요 파일: `backend/.claude/skills/dotshelf-verify/SKILL.md`, `backend/docs/architecture/patterns.md`, `backend/docs/learn/*`

## 왜 했나

요청 사항은 현재 헬스체크가 `/api/health`로 되어 있는 것을 `/health`로 바꾸는 것이었다. 컨트롤러만 바꾸면 Docker healthcheck나 Blue-Green smoke가 여전히 옛 경로를 호출해 배포 실패로 이어질 수 있다.

따라서 "엔드포인트 노출", "인증 예외", "컨테이너 헬스체크", "배포 스모크", "검증 문서"를 하나의 변경 단위로 맞췄다.

## 어떻게 구현했나

### 클래스 레벨 prefix 제거

기존에는 클래스에 `@RequestMapping("/api")`, 메서드에 `@GetMapping("/health")`가 있어 최종 경로가 `/api/health`였다. 클래스 레벨 prefix를 제거해 최종 경로를 `/health`로 만들었다.

```java
@Tag(name = "Health", description = "서비스 상태 확인")
@RestController
@RequiredArgsConstructor
public class HealthController {

    @GetMapping("/health")
    public ResponseEntity<ApiResponse<Map<String, Object>>> health() {
        ...
    }
}
```

핵심 포인트:
- 응답 형태와 Actuator 위임 로직은 그대로 유지했다.
- `Status.UP`이 아니면 503을 반환하는 기존 배포 안전장치도 유지했다.

### 배포 경로 일괄 변경

Dockerfile과 CI 스크립트는 헬스체크 실패 시 배포 안정성에 직접 영향을 주므로 함께 수정했다.

```dockerfile
HEALTHCHECK --interval=20s --timeout=5s --start-period=45s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1
```

```sh
HEALTH_URL_PUBLIC=${HEALTH_URL_PUBLIC:-https://api.dotshelf.digital/health}
curl -fsS --max-time 5 "http://100.74.2.103:$TARGET_PORT/health"
```

핵심 포인트:
- 컨테이너 내부 healthcheck와 배포 smoke가 같은 경로를 본다.
- `api.dotshelf.digital/actuator` 차단 정책은 그대로 두고, 공개 health는 `HealthController`만 사용한다.

## 검증

- `./gradlew.bat build -x test --console=plain` 성공
- `./gradlew.bat test --console=plain` 성공
- Docker Desktop 데몬이 실행 중이 아니어서 PostgreSQL/Redis 의존 부팅 smoke test는 수행하지 못했다.

## 배운 것

- **헬스체크 경로는 컨트롤러만의 문제가 아니다**: 컨테이너, CI, Blue-Green smoke, 문서화된 검증 절차까지 같은 경로를 바라봐야 배포 중 오탐이 줄어든다.
- **Security whitelist와 endpoint mapping은 같이 움직여야 한다**: 컨트롤러 경로를 바꿔도 인증 예외가 예전 경로에 남아 있으면 health endpoint가 401/403으로 막힐 수 있다.
- **과거 회고 문서와 현재 운영 문서는 구분해야 한다**: 날짜가 붙은 회고 문서의 `/api/health` 기록은 당시 이력이라 보존하고, 현재 기준 문서와 스킬만 갱신했다.

## 더 공부할 것

- [ ] Kubernetes readiness/liveness probe를 도입할 때 `/health`, `/ready`, `/live`를 어떻게 나눌 것인가?
- [ ] Actuator `/actuator/health/readiness`와 커스텀 `/health` 중 운영 로드밸런서가 봐야 할 경로는 무엇인가?
- [ ] Redis나 DB 장애 시 health endpoint가 503을 반환하는 정책이 Blue-Green 배포에 어떤 롤백 시그널을 주는가?
- [ ] 공개 health endpoint에 상세 dependency 정보를 그대로 내려도 되는지, 운영에서는 summary만 노출해야 하는지 검토가 필요한가?

## 참고

- 관련 파일: `backend/src/main/java/com/dotshelf/global/common/HealthController.java`
- 관련 파일: `backend/Dockerfile`, `.ci/deploy-blue-green.sh`, `.gitlab-ci.yml`
