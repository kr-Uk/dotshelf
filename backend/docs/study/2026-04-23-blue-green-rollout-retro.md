# 2026-04-23 · Blue-Green 무중단 배포 전면 도입 — 하루 회고

- **범위**: `origin/dev..HEAD` 전반의 2026-04-23 커밋들 (5개 MR, 11개 non-merge 커밋)
- **브랜치**:
  - `chore/be/app-compose-mem-limit` — JVM 메모리 제한 + app compose 를 repo 로 편입 (선행 MR)
  - `fix/be/social-login-restclient-builder` — 팀원 MR 머지 후 발생한 부팅 실패 핫픽스
  - `feat/be/blue-green-deploy` — Blue-Green 무중단 배포 본 구현
  - `fix/be/blue-green-nginx-layout` — 첫 배포 실패 2건 수정
  - `docs/be/blue-green-measurements` — 배포/롤백 실측값 박제
- **티켓**: (없음)
- **관련 문서**:
  - 설계 rationale (왜/어떻게/트레이드오프): [2026-04-23-blue-green-rationale.md](./2026-04-23-blue-green-rationale.md)
  - 인프라 변경 상세 + 실패 로그: [../infra/2026-04-23-blue-green.md](../infra/2026-04-23-blue-green.md)
  - 팀 DDL 규약: [../architecture/flyway-migration-rules.md](../architecture/flyway-migration-rules.md)

---

## TL;DR

하루에 5개 MR 을 스택해서 `dotshelf-api` 를 **단일 컨테이너 배포 → 2-color Blue-Green 무중단 배포** 로 전환했다. 설계 문서부터 팀 DDL 규약, 핫픽스, 본 구현, 실전 실패 2건 수정, 측정값 박제까지 원샷.

핵심 수치:
- 첫 성공 배포: **195.6초**, 공개 health **5xx=0 / 28회** (30초 폴링)
- 수동 롤백 리허설: **27초**, 공개 health **0/45**
- 색당 JVM 메모리: 471 MiB (limit 4 GiB 의 11.5%)

첫 배포는 두 번 연속 실패했다:
1. `PROD_HOST=3.38.240.110` (public IP) 로 SSH → EC2 hairpin NAT 타임아웃
2. Nginx `conf.d/*.conf` 자동 로드 + `default.conf` 의 명시 include → `duplicate upstream`

둘 다 설계 단계에서 예상하지 못한 함정이었고, 실전에서 디버깅한 경험이 오히려 본 작업의 가장 큰 학습 자산.

---

## 무엇을 했나

### 1. JVM 메모리 제한 + App Compose 를 repo 편입 (`chore/be/app-compose-mem-limit`)
- **사용자 관점**: 영향 없음 (내부 설정 변경)
- **시스템 관점**:
  - 서버 `/opt/dotshelf/docker-compose.yml` 을 `backend/deploy/app/docker-compose.yml` 로 이전 → 변경 이력이 git 에 남음
  - `api` 서비스에 `mem_limit: 4g` 추가 → `MaxRAMPercentage=60` 기반 heap 상한이 호스트(15 GiB) 가 아닌 컨테이너(4 GiB) 기준으로 고정
  - `.gitlab-ci.yml` deploy 단계가 매 배포마다 compose 파일도 rsync 하도록 수정
- **핵심 파일**: `backend/deploy/app/docker-compose.yml` (신규), `.gitlab-ci.yml`

### 2. 팀원 소셜 로그인 MR 핫픽스 (`fix/be/social-login-restclient-builder`)
- **사용자 관점**: `https://api.dotshelf.digital` 502 상태 복구
- **시스템 관점**:
  - `GoogleTokenVerifier` / `KakaoTokenVerifier` 가 `RestClient.Builder` 를 생성자 주입받으려는데 Spring Boot 4.0.5 classpath 에 해당 빈이 자동 등록되지 않음
  - `APPLICATION FAILED TO START` → `restart: unless-stopped` 로 컨테이너 재시작 루프 5회
  - `RestClientConfig` 에 `@Bean RestClient.Builder` 명시 등록하여 해결 (3줄)
- **핵심 파일**: `backend/src/main/java/com/dotshelf/global/config/RestClientConfig.java` (신규)

### 3. Blue-Green 무중단 배포 본 구현 (`feat/be/blue-green-deploy`)
- **사용자 관점**: (정상 시점엔) 배포 중에도 502 없이 응답
- **시스템 관점**:
  - `api` 단일 서비스 → `api-blue`(8080) + `api-green`(8081) 2-color
  - prod Nginx 설정 repo 편입 (`default.conf` + `upstream.{blue,green}.conf`)
  - `.ci/deploy-blue-green.sh` 9단계 오케스트레이션 (ACTIVE 감지 → TARGET 빌드 → healthy 대기 → direct smoke → Nginx cutover → public smoke → 2분 유예 → 구 색 stop)
  - `.ci/poll-health.sh` 30초 폴링으로 JSON 증거 수집
- **핵심 파일**: `.ci/deploy-blue-green.sh`, `.ci/poll-health.sh`, `backend/deploy/app/docker-compose.yml` (2-color), `backend/deploy/prod/nginx/conf.d/*`

### 4. 첫 배포 실전 실패 2건 수정 (`fix/be/blue-green-nginx-layout`)
- **사용자 관점**: 다시 502 였다가 복구됨
- **시스템 관점**:
  - 실패 1: `PROD_HOST=3.38.240.110` (public IP) 에서 EC2 hairpin NAT 로 SSH 타임아웃 → `PROD_HOST=172.17.0.1` (docker bridge) 로 GitLab Variable 수정
  - 실패 2: `default.conf` 에서 `include /etc/nginx/conf.d/upstream.active.conf` 로 인해 `conf.d/*.conf` 자동 로드와 이중 로드 → `upstream dotshelf_api` 중복 선언 에러
  - 수정: 명시 `include` 삭제 + `upstream.{blue,green}.conf` 를 `templates/` 서브디렉토리로 이동 + rsync `--delete` 로 과거 오염 파일 자동 청소
- **핵심 파일**: `backend/deploy/prod/nginx/conf.d/default.conf`, `backend/deploy/prod/nginx/conf.d/templates/*`, `.ci/deploy-blue-green.sh`, `.gitlab-ci.yml`

### 5. 배포/롤백 실측값 박제 (`docs/be/blue-green-measurements`)
- **사용자 관점**: 없음 (문서)
- **시스템 관점**:
  - 첫 성공 배포의 `poll-health.sh` JSON (`fail:0 / ok:28`) 을 rationale §9 와 infra §8 에 박제
  - 수동 롤백 리허설 (`green → blue`) 27초 소요, 45/45 폴링 성공 박제
- **핵심 파일**: `backend/docs/study/2026-04-23-blue-green-rationale.md §9`, `backend/docs/infra/2026-04-23-blue-green.md §8`

---

## 왜 했나

설계 근거는 [rationale 문서](./2026-04-23-blue-green-rationale.md) 에 상세히 박제되어 있다. 여기서는 **"왜 하루에 이 순서로 묶였나"** 만.

1. **Blue-Green 을 본 구현 전에 JVM 메모리 제한이 선행되어야 했다** — `MaxRAMPercentage=60` 이 컨테이너 메모리 기준으로 heap 을 잡으므로, 컨테이너 제한이 없으면 호스트 전체(15 GiB) 의 60% = 9 GiB 까지 heap 이 커질 수 있다. 두 색 공존 시 2 × 9 = 18 GiB > 15 GiB 호스트 RAM → OOM 위험. **먼저 `mem_limit: 4g` 을 걸어야 blue-green 을 안전하게 시도 가능**.

2. **팀원 소셜 로그인 MR 이 하필 오늘 dev 에 머지되면서 부팅이 실패했다** — 사전 로컬 부팅 검증 없이 올라온 MR 이라 api.dotshelf.digital 이 502. 본 작업(blue-green) 을 진행하기 전에 먼저 복구가 필요했고, 핫픽스 3줄로 해결.

3. **Blue-Green MR 이 첫 번째 배포에서 연속 실패했다** — 설계 단계에선 안 보였던 2개의 함정이 실전에서 터졌다. 이 둘을 잡는 fix MR 이 바로 이어서 필요했다.

4. **성공 배포 + 롤백 리허설까지 해야 포트폴리오 가치가 완성** — "배포했다" 만으론 부족하고 "실제로 중단 없었음을 JSON 으로 증명" + "롤백까지 해봤음" 이 필요. 이게 측정값 박제 MR 의 목적.

즉 **설계 → 선결 과제 → 긴급 복구 → 본 구현 → 실전 수정 → 검증** 이라는 강제된 순서가 그대로 하루의 MR 순서가 됐다.

---

## 어떻게 구현했나

### 주제 1: Spring Boot 4.x 에서 `RestClient.Builder` 자동 등록 문제

Spring Boot 3.2 이후 `spring-boot-starter-web` 이 classpath 에 있으면 `RestClientAutoConfiguration` 이 `RestClient.Builder` 를 자동 등록한다. 그런데 **4.0.5 에서는 그 자동 등록이 이 프로젝트 설정과 맞물려 활성화되지 않는다** (정확한 원인은 아직 파지 못함).

`GoogleTokenVerifier` / `KakaoTokenVerifier` 는 별 고민 없이 관용적인 패턴을 썼다:
```java
public GoogleTokenVerifier(RestClient.Builder restClientBuilder, SocialLoginProperties properties) {
    this.restClient = restClientBuilder.build();
    ...
}
```

빈이 없으면 컨텍스트 초기화 자체가 실패해서 앱이 아예 뜨지 못한다. 해결은 3줄:
```java
@Configuration
public class RestClientConfig {
    @Bean
    public RestClient.Builder restClientBuilder() {
        return RestClient.builder();
    }
}
```

**교훈**: "보통 자동 등록된다" 를 가정하지 말고, **새 API 를 주입받을 때는 해당 빈이 현 Spring Boot 버전에서 자동 등록되는지 명시 확인**. 또는 명시 `@Bean` 으로 의존성을 확실히 하는 게 안전.

### 주제 2: Nginx `conf.d/*.conf` 자동 로드 × 명시 `include` = 중복 선언

실패 로그:
```
nginx: [emerg] duplicate upstream "dotshelf_api" in /etc/nginx/conf.d/upstream.active.conf:1
```

원인 추적:
- `nginx.conf` 는 `include /etc/nginx/conf.d/*.conf` 로 conf.d 의 모든 `.conf` 를 자동 로드
- `default.conf` 에서 또 `include /etc/nginx/conf.d/upstream.active.conf` → **upstream.active.conf 가 2번 파싱됨**
- 추가로 `upstream.blue.conf` 와 `upstream.green.conf` 가 conf.d 루트에 있어 **같은 이름의 upstream 을 3번 선언**

해결:
1. `default.conf` 에서 명시 `include` 라인 제거 (nginx 의 자동 로드에 맡김)
2. 블루/그린 템플릿을 `conf.d/templates/` 서브디렉토리로 이동 (서브디렉토리는 `*.conf` 와일드카드에 안 걸림)
3. rsync 에 `--delete --exclude='upstream.active.conf' --exclude='README.md' --exclude='*.bak*'` 를 추가 → conf.d 루트에 남은 오염 파일을 다음 배포가 자동 청소

핵심 포인트:
- `include` 지시어는 **자동 로드되는 디렉토리 안의 파일을 명시 include 하면 이중 파싱** 된다. systemd drop-in, Apache `sites-enabled`, nginx `conf.d` 다 같은 함정.
- 방지책은 코드로 박지 않고 **운영 규칙 문서에 박제** 했다 ([conf.d/README.md](../../deploy/prod/nginx/conf.d/README.md)). 다음에 누가 default.conf 를 편집하다 include 를 다시 넣는 순간 규칙을 기억해야 한다.

### 주제 3: GitLab Runner 가 prod 호스트 내부에 있을 때의 hairpin NAT

deploy job 로그:
```
$ ssh "$PROD_USER@$PROD_HOST" "mkdir -p $PROD_DEPLOY_PATH/nginx/conf.d"
ssh: connect to host 3.38.240.110 port 22: Operation timed out
```

원인 추적:
- GitLab runner 가 prod 호스트 위의 Docker 컨테이너 안에서 돈다
- 그 컨테이너가 자기 호스트의 **Elastic IP(`3.38.240.110`)** 로 돌아가려 하면 EC2 NAT 게이트웨이가 hairpin(자기 자신으로 되돌아오는 경로) 을 허용하지 않아 TCP 연결 자체가 떨어진다
- 이건 AWS/EC2 네트워킹의 잘 알려진 제약이지만, **"러너가 target 호스트 안에 있다"** 는 토폴로지 구도를 모르면 설계 시점에 안 보인다

해결:
- `PROD_HOST=172.17.0.1` (Docker 기본 bridge) 로 변경
- 컨테이너 → bridge → 호스트 sshd(`0.0.0.0:22`) 경로는 hairpin 문제 없음
- `EC2_KNOWN_HOSTS` 에 bridge IP 용 호스트 키 3줄 추가 (실제는 같은 호스트 키, IP 접두만 다름) → 총 9줄

핵심 포인트:
- **러너 위치가 설계 변수다**. 같은 스크립트도 러너가 어디에 있느냐에 따라 성공/실패가 갈린다.
- hairpin NAT 은 로컬 Docker 에서는 재현 안 되고 AWS 특정 조건에서만 터진다. 실전 배포 전에 예상하기 어렵다.
- 과거 `docs/infra/gitlab-ci-variables.md` 변경 이력에 "2026-04-21 Runner 를 EC2 로 옮기며 `172.17.0.1` 쓰다가 2026-04-22 에 app 을 SSAFY 로 분리하며 public IP 로 바꿈" 흔적이 있었다. **히스토리를 먼저 읽었으면 재현 가능한 패턴임을 미리 알 수 있었다**.

### 주제 4: Blue-Green 배포 오케스트레이션 (deploy-blue-green.sh)

9단계 순서:
```
1. ACTIVE 읽기 (app:.active_color)
2. TARGET = 반대 색 (첫 실행이면 bootstrap = blue)
3. bootstrap 시 legacy dotshelf-api 컨테이너 rm -f
4. docker compose build + up -d api-$TARGET
5. healthy 대기 (max 60초)
6. direct smoke (Tailscale:8080 or 8081)
7. Nginx upstream.active.conf 교체 + nginx -t + nginx -s reload
8. 공개 health smoke (5회 retry)
9. 2분 유예 → 구 ACTIVE 색 stop
```

각 실패 지점별 안전망:
| 실패 지점 | 대응 |
|---|---|
| TARGET 기동 실패 / healthy timeout | TARGET stop, ACTIVE 계속 서비스 |
| direct smoke 실패 | TARGET stop |
| `nginx -t` 실패 | upstream.active.conf 를 ACTIVE 로 원복, TARGET stop |
| 공개 smoke 실패 (스위칭 후) | CI FAIL. 자동 롤백 없음 (의도적 — 운영자 수동 결정) |

핵심 포인트:
- **롤백이 "반대 방향 배포" 로 일반화** — 별도 롤백 경로를 유지하지 않고 같은 스크립트의 다음 실행이 롤백. 스크립트가 ACTIVE 를 상태로 읽으므로 자연스럽게 동작.
- **구 색을 `rm` 이 아닌 `stop` 만** — 롤백 리허설에서 이 설계가 빛났다. `docker compose up -d` 만으로 재빌드 없이 19초 만에 healthy. 만약 `rm` 했으면 이미지 pull 필요해서 더 느렸을 것.

### 주제 5: poll-health.sh 로 "배포 중 0 중단" 증명

30초 간 1초 간격으로 curl + HTTP 상태코드 히스토그램:
```json
{"url":"...","duration":30,"attempts":28,"ok":28,"fail":0,"codes":"200:28"}
```

`attempts=28` 이 30 에 못 미친 건 `curl --max-time 2 + sleep 1` 이 요청당 최대 3초 걸려서. 설계 내 오차.

이 JSON 한 줄이 포트폴리오 의 핵심 증거. "배포 중 5xx 없었다" 가 **감각이 아니라 실측** 이 됐다.

### 주제 6: 첫 bootstrap 의 본질적 한계 (롤백이 안전하지 않은 유일한 케이스)

일반 배포:
- ACTIVE 가 살아있는 상태에서 TARGET 기동 → healthy 확인 → cutover
- 어디서 실패해도 ACTIVE 가 살아있음 → 다운타임 0

Bootstrap (최초 1회):
- `dotshelf-api` (단일 컨테이너) 를 `docker rm -f` 로 제거한 후 blue 기동
- **이 구간엔 아무 컨테이너도 서비스하지 않음** → 일시 502
- 이번에 `nginx -t` 가 실패하면서 이 구간이 더 길어져 서비스 다운이 2~3분

결론: **bootstrap 은 무중단 보장 경로가 아니다**. 그러니 **Blue-Green 은 "이미 Blue-Green 으로 동작 중인 서비스에 대한 다음 배포" 부터 진짜 가치가 있다**. 첫 도입 시점엔 예고된 짧은 다운타임 1회를 감수.

---

## 배운 것

### 인프라 / 네트워킹
- **EC2 hairpin NAT**: 컨테이너가 자기 호스트의 Elastic IP 로 들어올 수 없다. Docker bridge IP 로 우회해야 함. 로컬 Docker 에선 재현 안 됨.
- **Nginx `conf.d/*.conf` 자동 로드**: 모든 `.conf` 파일이 자동 파싱된다. 서브디렉토리는 스캔 안 됨. 이 규칙을 모르고 `include` 를 섞으면 중복 선언.
- **Nginx graceful reload 의 메커니즘**: `nginx -s reload` 는 worker 를 fork 하고 기존 worker 는 inflight 요청을 끝까지 처리. 그래서 upstream 교체 중에도 5xx 가 0.
- **rsync `--delete` 와 `--exclude` 조합**: runtime 파일(`upstream.active.conf`) 은 exclude 해서 살리고, 과거 오염 파일은 delete 로 자동 청소.

### Java / Spring
- **Spring Boot 4.x 자동설정의 미묘한 변화**: 3.x 에서 자동 등록되던 빈이 4.x classpath/조건 변화로 안 걸릴 수 있음. 새 API 주입 시 "자동 등록된다" 를 가정하지 말고 명시 확인.
- **`MaxRAMPercentage` 는 컨테이너 메모리 제한 기준**: compose 에 `mem_limit` 없으면 호스트 RAM 기준으로 heap 이 커진다.
- **`@Bean RestClient.Builder`**: 프로토타입 스코프로 `return RestClient.builder()` 만 해도 의존성 주입 동작.

### 배포 / 운영
- **CI 러너 위치는 설계 변수**: 같은 스크립트도 러너가 어디에 있느냐에 따라 네트워크 경로가 달라진다. 설계 시점에 이 변수를 인식하지 못하면 실전에서 터진다.
- **"drop-in 디렉토리" + "명시 include" 는 위험 조합**: systemd / apache sites-enabled / nginx conf.d 다 동일.
- **상태 파일은 rsync --exclude 로 보호**: `upstream.active.conf`, `.active_color` 같은 runtime 상태가 실수로 덮어쓰이면 배포가 꼬인다.
- **`stop` vs `rm`**: 구 버전 컨테이너를 `stop` 만 하면 롤백 시 재빌드 없이 빠르게 복귀. `rm` 은 자원 정리에는 좋지만 롤백 비용 증가.
- **"무중단"은 일반 배포 경로에만 해당**: bootstrap(최초 전환) 과 기반 인프라 변경(Nginx 재시작 등) 은 여전히 다운타임을 가진다.

### 디버깅 / 워크플로우
- **exit code 255 + 긴 duration ≈ SSH 타임아웃**: 짧게 떨어지는 auth fail 과는 증상이 다르다. duration 으로 원인군을 좁힌다.
- **러너의 systemd 로그(journalctl)** 는 트레이스 자체가 아니라 메타(job 성공/실패/duration) 만 보관. 실제 에러 로그는 GitLab UI 의 job 페이지에서 확인.
- **hotfix 는 forward-fix 가 revert 보다 빠를 때가 많다**: 3줄 수정이면 revert 보다 3줄 짜기 + 머지가 빠름. 다만 규모가 커지면 revert 가 더 안전.
- **MR 스택 순서는 의존성 그래프에 매달린다**: A → B → C 머지 순서를 어기면 conflict 나 rebase 비용이 커진다. 스택 구조를 미리 의식.

### DB / 스키마
- **expand-contract 도입 시점의 중요성**: Flyway migration 이 V1 하나뿐인 지금이 팀 규약을 정착시키기 가장 저비용인 시점. 파일이 많아지면 규약 설득 비용 급증.
- **Blue-Green 과 expand-contract 는 같이 도입**: 두 색이 같은 DB 를 공유하는 1~2분 구간이 있으므로, 파괴적 DDL 을 같은 PR 에 넣으면 구 버전이 실패한다.

### 포트폴리오 / 증명
- **감각 대신 수치**: "배포 중 에러 없었다" 는 감각이 아니라 `poll-health.sh` JSON 한 줄이 증명. `fail=0 / ok=28`.
- **실패 경험이 자산**: 예상 Q&A 의 "실패 해본 적?" 에 "hairpin NAT 과 nginx duplicate 2건 실전 디버깅" 으로 답 가능. **첫 시도에 성공한 깔끔한 이야기보다 실패 → 추적 → 수정 → 검증 으로 이어지는 서사가 면접에서 더 강하다**.

---

## 더 공부할 것

- [ ] **Spring Boot 4.0.5 에서 `RestClientAutoConfiguration` 이 왜 안 걸렸나?** — 3.x 에서 4.x 로 올라오며 `@ConditionalOnXxx` 변화가 있었는지, 실제 자동설정 리포트를 뽑아 확인해야 한다. `-Ddebug=true` 로 auto-config 조건 리포트 활성화. 참고: `spring.factories` → `AutoConfiguration.imports` 변경, `spring-boot-autoconfigure` 4.x 소스.
- [ ] **JVM `MaxRAMPercentage` + cgroup v2 + `UseContainerSupport`** — 컨테이너 메모리 인식이 cgroup v1/v2 에서 어떻게 달라지나. 실제 heap 상한이 언제 어떻게 결정되는지 `-Xlog:gc*` + `jcmd VM.info` 로 추적해 보기.
- [ ] **Nginx upstream keepalive + reload 시 연결 재사용** — `keepalive 32` 가 유지되는 연결이 reload 이후 어떻게 동작하는가? 기존 worker 의 연결 풀과 새 worker 의 연결 풀이 분리되는지 실험 필요.
- [ ] **Tailscale vs WireGuard 직접 구성** — 현재 Tailscale 매직은 편하지만 코디네이터 의존. 팀이 커지거나 단가 고려 시 WireGuard 직접 구성은 어떻게? Direct peer path 확립 조건.
- [ ] **EC2 hairpin NAT 의 정확한 조건** — VPC / NAT Gateway / Elastic IP / security group 조합 중 어느 것이 hairpin 을 막는지. AWS 문서 vs 실험.
- [ ] **GitLab CI job trace 원격 취득** — glab CLI 가 설치되지 않았을 때 job trace 를 받는 방법 (GitLab API + project token). 디버깅 속도 개선.
- [ ] **`docker compose --delete-orphans`** — 서비스 이름이 바뀔 때 (이번처럼 `api` → `api-blue`) 구 컨테이너가 orphan 으로 남는다. `--delete-orphans` 를 CI 에 넣어야 하는가?
- [ ] **자동 롤백 전략** — 현재는 공개 smoke 실패 시 CI FAIL 까지만. 유예 2분 동안 poll-health 가 실패 임계 넘으면 자동으로 반대 색으로 되돌리는 옵션 설계. 트리거 메트릭 선정이 핵심.
- [ ] **expand-contract 자동 검증** — 파괴적 DDL(ALTER ... DROP, NOT NULL 강화) 을 포함한 Flyway 파일이 올라오면 CI 에서 차단하는 규칙. Flyway 파일 static 분석으로 가능할까?
- [ ] **Observability 로 blue/green 식별** — 각 요청이 어느 색을 통했는지 응답 헤더나 메트릭으로 노출. `DEPLOY_COLOR` env 를 Actuator info 나 응답 헤더에 반영하는 패턴.

---

## 참고

- **관련 MR (머지 순서)**:
  1. `chore/be/app-compose-mem-limit` — 49b2cfc, 5b0711c → merge f6d4912
  2. `fix/be/social-login-restclient-builder` — 71ef597 → merge b847fb9
  3. `feat/be/blue-green-deploy` — 57bd6d3, 9dbc857 → merge 1d3f7e4
  4. `fix/be/blue-green-nginx-layout` — 7947cb6 → merge d1c1931
  5. `docs/be/blue-green-measurements` — 6d5dd10, 0ae3a47 (머지 대기)
- **관련 문서**:
  - [Blue-Green rationale (설계)](./2026-04-23-blue-green-rationale.md)
  - [Blue-Green infra (상세 + 실패 로그)](../infra/2026-04-23-blue-green.md)
  - [Flyway migration rules (팀 규약)](../architecture/flyway-migration-rules.md)
  - [JVM 메모리 제한 선행 MR 기록](../infra/2026-04-23-jvm-mem-limit.md)
  - [GitLab CI Variables (172.17.0.1 근거 박제)](../infra/gitlab-ci-variables.md)
- **관련 연관 이슈** (dev 머지 이전):
  - `badb437` — 팀원(`성경훈`) 의 소셜 로그인 MR 머지. 여기서 `RestClient.Builder` 의존성 누락이 새어나갔다. 재발 방지로 dotshelf-verify 스킬 제안 예정.
