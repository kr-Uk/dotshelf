# 챕터 6 · 배포 — 내 코드가 서버에 도달하기까지

> 목표: **`git push` 한 번으로** 어떻게 SSAFY 서버에 새 버전이 올라가고 사용자가 쓸 수 있게 되는지, 그 자동화 파이프라인의 모든 단계를 이해하기.

읽고 나면 답할 수 있는 것:
- **CI, CD** 가 각각 뭐야
- **Runner** 가 뭐야
- **Docker 컨테이너** 가 뭐야
- `.gitlab-ci.yml` 이 하는 일
- 우리 프로젝트의 **정확한 배포 순서**
- 배포 실패하면 어떻게 되고 어떻게 고치나

---

## 1. 알아둘 기초

### 1-1. CI 와 CD 는 다른 개념

**CI** = **Continuous Integration (지속적 통합)**
> "내 코드 + 팀 코드를 **합쳐서 깨지는지 자동 검사**"

**CD** = **Continuous Deployment (지속적 배포)**
> "검사 통과한 코드를 **자동으로 서버에 올리기**"

비유: **음식점**
- CI = 주방장이 재료 받아서 **신선한지, 상했는지 자동 검사**
- CD = 검사 통과한 재료로 요리해서 **손님 테이블로 자동 배달**

### 1-2. "Runner" 가 뭐야?

**CI 가 시키는 일(빌드, 테스트, 배포)을 실제로 수행하는 작업자**.

GitLab 의 경우:
- **Shared Runner** — GitLab 회사가 제공하는 공용 작업자들
- **Specific Runner** — 우리가 직접 서버에 설치한 전용 작업자

작업 하나가 오면:
1. Runner 가 빈 **Docker 컨테이너** 을 띄움
2. 거기서 빌드/테스트 코드 실행
3. 결과를 보고
4. 컨테이너 버림 (다음 작업은 또 새 컨테이너)

**매번 깨끗한 환경** 이라는 게 포인트. "내 컴에선 되는데요" 방지.

### 1-3. "Docker 컨테이너" 가 뭐야?

**프로그램 하나를 담는 표준 박스**.

비유: **컵라면**
- 컵라면 = 라면 + 수프 + 컵이 한 세트
- 뜨거운 물만 부으면 어디서든 똑같이 먹을 수 있음
- 주방 환경(조리도구, 가스레인지) 필요 없음

컨테이너도 비슷:
- **이미지** = 컵라면 완제품 (실행 가능한 상태의 프로그램)
- **컨테이너** = 그 이미지를 실행한 상태 (물 부은 컵라면)
- 같은 이미지를 어디서 돌려도 **거의 동일한 동작** (호스트 OS/설치된 라이브러리 영향 없음)

### 1-4. 우리 서버에서 Docker 가 하는 일

```
SSAFY 서버 (=호스트 OS, Ubuntu)
  │
  ├─ Docker Engine (Docker 자체 실행 프로그램)
  │
  └─ 컨테이너 1개가 돌고 있음:
        │
        ├─ dotshelf-api (Java 21 + Spring Boot + 우리 JAR)
        └─ 포트 8080 으로 리스닝

영구 서버 (Ubuntu)
  │
  └─ Docker Engine
        │
        ├─ dotshelf-postgres (PostgreSQL 17)
        ├─ dotshelf-redis (Redis 7.4)
        └─ dotshelf-nginx (Nginx 1.28)
```

각 컨테이너가 **독립적**. 하나 재시작해도 다른 건 영향 없음.

---

## 2. 우리 프로젝트의 전체 배포 파이프라인

```
[개발자]                    [GitLab]                    [SSAFY 서버]
  │                            │                            │
  │ 1. git push                │                            │
  │──────────────────────────▶ │                            │
  │                            │                            │
  │                            │ 2. MR 생성 → 리뷰 → 머지       │
  │                            │                            │
  │                            │ 3. dev 브랜치에 머지됨 →      │
  │                            │    CI 파이프라인 자동 시작   │
  │                            │                            │
  │                            │  ┌─[backend:build]─┐        │
  │                            │  │ gradle check    │        │
  │                            │  │ gradle bootJar  │        │
  │                            │  │ JAR 저장         │        │
  │                            │  └────────┬────────┘        │
  │                            │           │                 │
  │                            │  ┌─[backend:deploy]┐        │
  │                            │  │ SSH 로 SSAFY 접속│        │
  │                            │  │ JAR rsync       │        │
  │                            │  │ docker compose  │────▶   │ 4. docker build
  │                            │  │ up -d --build   │        │    dotshelf-api
  │                            │  │ health 체크     │        │    컨테이너 교체
  │                            │  └────────┬────────┘        │
  │                            │           │                 │
  │                            │  ┌─ 공용 URL 검증 ─┐        │
  │                            │  │ curl https://.. │────▶   │ 5. 사용자에게
  │                            │  └─────────────────┘        │    새 버전 노출
  │                            │                            │
  │  ✅ 파이프라인 성공           │                            │
  │◀───────────────────────────                             │
```

5 단계 상세히 들어가봅시다.

---

## 3. 단계별 자세히

### 단계 1: `git push` 하면 벌어지는 일

```bash
git checkout -b feat/be/add-bio-field
# ... 코드 수정 ...
git add -p
git commit -m "[BE] Feat: User 에 bio 필드 추가"
git push -u origin feat/be/add-bio-field
```

→ GitLab 에 브랜치 + 커밋이 올라감. **아직 CI 자동 실행 안 됨**.

왜 안 돼? — 우리 `.gitlab-ci.yml` 에 **"dev 브랜치에 머지될 때만 실행"** 이라고 조건이 걸려 있음:

```yaml
rules:
  - if: '$CI_COMMIT_BRANCH == "dev"'    # ← dev 에 푸시될 때만
    changes:
      - backend/**/*
      - .gitlab-ci.yml
      - .ci/**/*
```

이래야 feature 브랜치 push 가 매번 서버 배포되는 걸 막음.

### 단계 2: MR → 리뷰 → 머지

1. GitLab 웹에서 **MR 생성** (feature 브랜치 → dev)
2. 리뷰어가 코드 보고 피드백
3. 괜찮으면 **Merge** 버튼 클릭
4. dev 브랜치에 커밋이 포함됨 → **이 순간 CI 파이프라인이 자동 기동**

### 단계 3a: `backend:build` 작업

`.gitlab-ci.yml` 의 이 부분:
```yaml
backend:build:
  image: eclipse-temurin:21-jdk
  script:
    - cd backend
    - ./gradlew check bootJar --no-daemon
  artifacts:
    paths:
      - backend/build/libs/*-SNAPSHOT.jar
    reports:
      junit: backend/build/test-results/test/TEST-*.xml
```

안에서 일어나는 일:
1. GitLab Runner 가 `eclipse-temurin:21-jdk` Docker 컨테이너 띄움 (자바 21 설치된 깨끗한 환경)
2. 우리 저장소 코드 체크아웃
3. `./gradlew check` — **테스트 + 정적 검사 실행** ([챕터 5 참고](./05-error-handling.md))
4. `./gradlew bootJar` — 실행 가능한 JAR 파일 생성
5. **artifact 로 JAR 업로드** (다음 단계가 쓸 수 있게)
6. 테스트 결과도 XML 로 업로드 → GitLab MR UI 에 "Tests 5 passed" 표시

이 단계에서 실패하면 → **배포 진행 안 됨**. SSAFY 서버의 기존 컨테이너는 그대로.

### 단계 3b: `backend:deploy` 작업

build 성공하면 이어서 deploy:
```yaml
backend:deploy:
  image: alpine:3.20
  needs: [backend:build]
  before_script:
    - apk add --no-cache openssh-client rsync curl
    - printf '%s\n' "$SSH_PRIVATE_KEY" > ~/.ssh/id_ed25519
    - printf '%s\n' "$EC2_KNOWN_HOSTS" > ~/.ssh/known_hosts
  script:
    - JAR=$(ls $JAR_ARTIFACT_PATH/*-SNAPSHOT.jar | head -1)
    - COMPOSE=backend/deploy/app/docker-compose.yml
    - ssh "$EC2_USER@$EC2_HOST" "mkdir -p ..."
    - rsync -az "$JAR" "$EC2_USER@$EC2_HOST:$EC2_DEPLOY_PATH/api/build/libs/"
    - rsync -az "$COMPOSE" "$EC2_USER@$EC2_HOST:$EC2_DEPLOY_PATH/docker-compose.yml"
    - ssh "$EC2_USER@$EC2_HOST" "cd $EC2_DEPLOY_PATH && docker compose up -d --build api"
    - sh .ci/wait_for_api_healthy.sh
    - 'ssh ... "curl http://100.74.2.103:8080/health"'
    - curl -fsS https://api.dotshelf.digital/health
```

안에서 일어나는 일:

1. `alpine:3.20` 컨테이너 (가벼운 리눅스) 에서 시작
2. SSH/rsync/curl 설치
3. **GitLab CI/CD Variables** 에서 시크릿(`SSH_PRIVATE_KEY`, `EC2_KNOWN_HOSTS`) 를 파일로 심음
4. artifact 에서 JAR 가져옴
5. **SSH 로 SSAFY 서버 접속** 해서 디렉토리 준비
6. **rsync 로 JAR + docker-compose.yml 을 SSAFY 서버로 전송**
7. **SSH 로 다시 접속해서 `docker compose up -d --build api`** 실행
8. 헬스체크 대기 (`.ci/wait_for_api_healthy.sh` 가 최대 1분 폴링)
9. SSAFY 로컬에서 `/health` 직접 호출 → UP 확인
10. Cloudflare 경유 공용 URL 로 한 번 더 호출 → 전 경로 OK 확인

### 단계 4: SSAFY 서버에서 실제로 일어나는 일

`docker compose up -d --build api` 한 줄이 뭘 하냐면:

1. **현재 있는 compose 파일** 기준으로 서비스 정의 읽음
2. `api` 서비스의 **이미지를 재빌드** (`--build`) — 새 JAR 가 포함된 이미지 생성
3. 기존 `dotshelf-api` 컨테이너 중지
4. 새 이미지로 새 컨테이너 기동
5. 기동 시 Spring Boot 가 뜨고 **Flyway 가 새 마이그레이션이 있으면 자동 실행** ([챕터 4 참고](./04-data-persistence.md))
6. Actuator 헬스체크 ([챕터 5](./05-error-handling.md)) 가 UP 이 될 때까지 Docker 가 "starting" 상태 유지
7. UP 이 되면 "healthy" 로 전환

이 전체가 ~1분 안에 끝남. 그 동안:
- **Nginx 는 계속 돌고** 있지만 upstream 이 잠시 연결 안 돼 5xx 가 몇 초 나올 수 있음
- Docker Compose 가 기본적으로 "새 컨테이너 시작 → 기존 중지" 순서는 아님 (recreate-then-start). 완벽한 무중단은 아님
- 진짜 무중단 원하면 blue/green 배포 필요 (현재 범위 초과)

### 단계 5: 공용 URL 에 노출

Nginx 의 upstream 은 여전히 `100.74.2.103:8080` 을 가리킴. 새 컨테이너가 그 포트로 준비 완료되면 자동으로 트래픽을 받음.

사용자는 **뭔가 했는지도 모른 채** 새 기능 써볼 수 있게 됨.

---

## 4. `.ci/wait_for_api_healthy.sh` 자세히

CI 가 "컨테이너 띄웠으니 끝" 하면 안 됨. 실제로 앱이 정상 기동됐는지 확인해야 함.

```bash
#!/bin/sh
STATUS="starting"
for i in 1 2 3 4 5 6 7 8 9 10 11 12; do
    STATUS=$(ssh "$EC2_USER@$EC2_HOST" "docker inspect -f '{{.State.Health.Status}}' dotshelf-api")
    echo "[$i] api health: $STATUS"
    if [ "$STATUS" = "healthy" ]; then
        break
    fi
    sleep 5
done

if [ "$STATUS" != "healthy" ]; then
    echo "api did not become healthy within timeout" >&2
    ssh ... "docker compose logs --tail 50 api" >&2
    exit 1
fi
```

5 초 간격으로 12 번 = 최대 1 분 동안 폴링. 그 사이에 `healthy` 되면 OK, 안 되면 로그 찍고 실패.

Docker `HEALTHCHECK` 이 실제 의미 있게 작동하게 된 게 [챕터 5 에서 다룬 Actuator 강화](./05-error-handling.md) 덕분.

---

## 5. 실제 실패 시나리오별 대응

### ❌ 컴파일 오류
- `backend:build` 가 실패
- GitLab MR 에 빨간 X 표시
- deploy 단계 안 돌아감 → **SSAFY 서버의 기존 컨테이너 무사**
- 개발자가 수정 → 다시 push

### ❌ 테스트 실패
- 위와 동일. `backend:build` 단계에서 멈춤
- 어느 테스트가 실패했는지 GitLab UI 에 표시됨
- `backend/build/reports/tests/test/index.html` artifact 로 상세 확인 가능

### ❌ SSH 접속 실패
- `EC2_KNOWN_HOSTS` 망가짐 / 서버가 재부팅되면서 호스트키 바뀜 등
- "Host key verification failed" 에러
- `EC2_KNOWN_HOSTS` Variable 재생성 필요 ([infra/gitlab-ci-variables.md](../infra/gitlab-ci-variables.md))

### ❌ rsync/docker 실행 실패
- SSAFY 서버 디스크 꽉 참, docker daemon 꺼짐 등
- deploy 스텝에서 에러
- SSAFY 서버 상태 확인 필요 (`ssh dotshelf-app "docker ps"`)

### ❌ 헬스체크 타임아웃
- 컨테이너는 띄웠는데 앱이 기동 실패 (Flyway 체크섬 오류, DB 연결 실패 등)
- `wait_for_api_healthy.sh` 가 1 분 후 포기 + 로그 출력
- 로그 보고 원인 파악 → 코드 수정 → 재배포

### ❌ 공용 URL 호출 실패
- SSAFY 내부 health 는 OK 인데 공용 URL 이 안 됨 → Nginx 문제 or Cloudflare 문제
- 이 경우 파이프라인은 실패지만 **새 컨테이너는 이미 up**
- 수동 개입 필요

어느 단계든 실패하면 GitLab 이 **이메일/슬랙 알림** 보냄 (설정 돼있으면).

---

## 6. GitLab Variables — 시크릿은 어디서

`.gitlab-ci.yml` 에서 `$EC2_HOST` 같은 변수를 쓰는 이유:
- **시크릿을 코드에 박지 않기** 위함
- 변수값은 **GitLab Settings → CI/CD → Variables** 에 저장

우리가 저장한 5개:
| Key | 뜻 |
|---|---|
| `SSH_PRIVATE_KEY` | CI 전용 SSH 비공개키 (SSAFY 에 접속 가능) |
| `EC2_KNOWN_HOSTS` | SSAFY 서버 SSH 지문 (man-in-the-middle 방지) |
| `EC2_HOST` | SSAFY Public IP (3.36.72.58) |
| `EC2_USER` | ubuntu |
| `EC2_DEPLOY_PATH` | /opt/dotshelf |

상세: [infra/gitlab-ci-variables.md](../infra/gitlab-ci-variables.md)

Variables 는 **Protected** 로 설정 → dev / master 같은 보호 브랜치에서만 사용 가능. feature 브랜치가 장난칠 수 없음.

---

## 7. 배포 후 확인 절차

배포 성공 후 개발자가 해보면 좋은 것:

### 7-1. 파이프라인 UI
- GitLab MR 페이지에서 초록 체크
- "Deployed to dev: https://api.dotshelf.digital" 링크 뜸

### 7-2. 공용 URL 호출
```bash
curl https://api.dotshelf.digital/health | jq
# 예상: status=UP, components.db/redis 전부 UP
```

### 7-3. Swagger
- https://api.dotshelf.digital/swagger-ui.html 가면 현재 API 스펙
- 방금 추가한 엔드포인트 나오나 확인

### 7-4. 실제 앱/프론트
- 모바일에서 새 기능 한 번 써봄
- 성공하면 끝

### 7-5. 로그 훑어보기
```bash
ssh dotshelf-app "sudo docker logs -f dotshelf-api"
```
- `[ERROR]` 찍히는 게 많으면 뭔가 문제
- MDC 의 `requestId` 덕에 특정 요청 추적 쉬움 ([챕터 1](./01-request-lifecycle.md))

---

## 8. 수동 개입이 필요한 경우

### 긴급 롤백
CI 는 자동 롤백 안 함. 방법 2가지:
- **Git revert** 로 문제 커밋 되돌리고 다시 push → CI 가 이전 코드로 배포
- 서버에서 **이전 JAR 로 수동 재기동** (백업 JAR 있다면. 보통 없음 → Git revert 가 안전)

### Flyway 체크섬 충돌
- "누군가 이미 머지된 V 파일 수정" → 앱 기동 차단
- `flyway_schema_history` 손봐야 함 ([챕터 4](./04-data-persistence.md) 참고)

### 서버 재기동 (OS 레벨)
- `sudo reboot` 하면 Docker 컨테이너도 꺼짐
- `restart: unless-stopped` 덕에 Docker 가 자동 기동
- 안 되면 `sudo systemctl restart docker` + `docker compose up -d`

---

## 9. 자주 헷갈리는 것들

### Q. CI 가 돌면 유저가 끊겨?
- 빌드 단계는 CI 서버에서만 돌아서 영향 없음
- 배포 단계에서 컨테이너 재생성 중엔 **수 초간 5xx 가 날 수 있음**
- 트래픽 많으면 Blue/Green 이나 rolling 전략으로 무중단 가능 (우리는 아직 필요 없음)

### Q. "이미지를 빌드" 와 "JAR 을 빌드" 는 다른 일?
- **JAR 빌드** = Java 소스 → 실행 가능한 `.jar` 파일 (Gradle 이 함)
- **이미지 빌드** = 그 JAR + JRE + 환경 설정을 합쳐서 Docker 이미지 만듦 (Dockerfile 이 함)
- CI 는 JAR 빌드 후 SSAFY 에 전송 → SSAFY 에서 Dockerfile 로 이미지 빌드

### Q. 왜 이미지를 **Docker Hub 같은 레지스트리** 에 안 올리고 서버에서 빌드해?
- 더 간단해서. 레지스트리 쓰려면 auth/push/pull 설정 필요
- 단점: 서버마다 빌드 환경 필요, 빌드 시간 ↑
- 규모 커지면 registry 쓰는 게 맞음 (GitLab Container Registry 무료)

### Q. CI 가 dev 브랜치 아닌데도 한 번씩 돌면 안 돼? (PR 미리 검증)
- `rules.if` 에 `$CI_PIPELINE_SOURCE == "merge_request_event"` 추가하면 MR 생성/업데이트 시마다 **테스트만** 돌게 가능
- 우리 `.backend-rules` 에 이미 MR event 포함. deploy 는 제외됨. 완벽한 세팅.

### Q. Runner 가 prod 서버에서도 돈다 했는데 지금도?
- 아직 `gitlab-runner` 서비스가 prod 에서 돌고 있음
- 다만 우리 CI 는 대부분 shared runner 사용 → prod runner 사용 비중 낮음
- 정리 TODO: 필요 없으면 끄기 ([2026-04-22 infra doc §14](../infra/2026-04-22-tailscale-app-db-split.md))

### Q. 배포가 Flyway 와 엔티티를 동시에 업데이트하면 중간 순간에 깨지지 않아?
- 배포 순서: **새 컨테이너 기동 → Flyway 실행 → Hibernate validate → 서비스 시작**
- 이 흐름이 모두 성공해야 healthy
- 중간에 깨지면 새 컨테이너가 healthy 안 되고 기존 컨테이너가 남아서 사용자에겐 영향 X (완벽한 무중단은 아니지만 심각한 장애는 드묾)

### Q. 환경변수(JWT_SECRET 등) 는 언제 컨테이너에 주입?
- SSAFY 의 `/opt/dotshelf/.env` 가 `docker-compose.yml` 에 의해 읽힘
- 컨테이너 기동 시 환경변수로 주입
- 이 파일은 **git 에 없음** — 서버에만 있음
- 상세: [infra/2026-04-20-initial-setup.md](../infra/2026-04-20-initial-setup.md)

---

## 10. 실제 코드에서 확인하기

| 개념 | 파일 |
|---|---|
| CI 전체 설정 | [`../../.gitlab-ci.yml`](../../../.gitlab-ci.yml) (레포 루트) |
| 헬스체크 스크립트 | `.ci/wait_for_api_healthy.sh` |
| SSAFY 의 docker-compose | `backend/deploy/app/docker-compose.yml` (git 에 있음, CI 가 서버로 rsync) |
| Dockerfile | `backend/deploy/app/Dockerfile` (또는 `backend/api/Dockerfile`) |
| GitLab Variables 명세 | [`docs/infra/gitlab-ci-variables.md`](../infra/gitlab-ci-variables.md) |
| 2-tier 배포 구조 배경 | [`docs/infra/2026-04-22-tailscale-app-db-split.md`](../infra/2026-04-22-tailscale-app-db-split.md) |
| Flyway 배포 통합 | [`docs/infra/2026-04-22-flyway-and-health.md §1`](../infra/2026-04-22-flyway-and-health.md) |
| CI 테스트 자동화 | [`docs/infra/2026-04-22-flyway-and-health.md §3`](../infra/2026-04-22-flyway-and-health.md) |

---

## 11. 챕터 6 정리

- ✅ CI = 검증, CD = 배포. 둘 합친 게 CI/CD 파이프라인
- ✅ Runner 가 깨끗한 Docker 컨테이너에서 작업 실행
- ✅ `.gitlab-ci.yml` 이 파이프라인 설계도
- ✅ dev 머지 → build (gradle check + bootJar) → deploy (rsync + docker compose up)
- ✅ Flyway 가 스키마 자동 적용, Actuator 가 기동 완료 알림
- ✅ 실패 시 SSAFY 기존 컨테이너는 보호됨 (대부분의 경우)
- ✅ 시크릿은 GitLab Variables 에. 코드에 박지 않음

---

# 🎉 시리즈 완주!

지금까지 6 챕터를 다 읽었다면, DotShelf 백엔드의 **전체 흐름을 이해** 한 상태입니다.

### 마스터한 지도

```
[사용자]
   │
   ├─(1)─ Cloudflare → Nginx → Tailscale → Spring Boot 도착
   │
   ├─(2)─ JWT 로 인증 검증 (JwtAuthenticationFilter)
   │
   ├─(3)─ Controller → Service → Repository 를 거침
   │      (엔티티 = DB 테이블 자바 표현)
   │
   ├─(4)─ JPA / QueryDSL / Flyway 로 DB 와 상호작용
   │
   └─(5)─ 에러 나면 GlobalExceptionHandler 가 잡아 일관된 응답
   
 이 모든 코드가 (6)─ CI/CD 로 자동 배포됨
```

### 한 번 더 봐두면 좋을 것

- [`docs/architecture/patterns.md`](../architecture/patterns.md) — 우리 팀 코드 컨벤션
- [`docs/architecture/checklist.md`](../architecture/checklist.md) — PR 올리기 전 셀프 리뷰
- [`docs/infra/`](../infra/) — 인프라 결정 이력

### 학습 팁

- **실제 코드를 열어서 한 기능을 따라가보세요** — 예: "`/api/users/me` 엔드포인트는 어디부터 시작해서 어디서 끝나나?"
- **작은 기능 하나를 직접 추가해보세요** — Flyway V 파일 + 엔티티 수정 + 테스트 작성까지. 이 시리즈의 모든 챕터를 한 번에 체험.
- **이해 안 되는 게 있으면 [README 의 용어 사전](./README.md#읽기-전-용어-미니-사전) 에서 먼저 찾아보세요**.

수고했습니다 🫡
