# 배포 파이프라인과 운영 프로필 구축 — EC2 + Cloudflare + GitLab CI

- **브랜치**: `chore/be/deploy-infra`
- **범위**: `origin/dev..HEAD` (커밋 1개, 178줄 추가)
- **기간**: 2026-04-20
- **티켓**: (해당 없음)
- **커밋**: `dbb98af [BE] Chore: 배포 파이프라인 및 운영 프로필 추가`

---

## TL;DR

EC2에 수동 기동해서 돌고 있던 Spring Boot 앱을 **dev 브랜치 push → 자동 배포**로 전환하기 위한 파이프라인 골격을 모두 잡았다. 핵심 산출물은 (a) 모노레포 루트의 `.gitlab-ci.yml`(build/deploy 2단계, `backend/**` 경로 필터), (b) ARM64 Graviton을 타깃으로 한 `Dockerfile`, (c) 환경변수 주입 기반의 `application-prod.yml`이다. Cloudflare Full (strict) + Origin 인증서 + Nginx 리버스 프록시까지 엮여있어 네트워크 경로 자체가 운영 모드로 바뀌었다.

---

## 무엇을 했나

### 1. GitLab CI/CD 파이프라인 (`/.gitlab-ci.yml`, 85줄)
- **사용자 관점 효과**: 백엔드 코드를 `dev`에 머지하면 몇 분 안에 `https://api.dotshelf.digital`에 자동 반영된다.
- **시스템 관점 효과**: `backend:build` job이 `eclipse-temurin:21-jdk` 이미지에서 Gradle로 jar를 빌드해 artifact로 남기고, `backend:deploy` job이 SSH+rsync로 EC2에 jar를 올리고 `docker compose build api && up -d api`를 실행한 뒤 헬스체크까지 확인한다.
- **주요 포인트**: 모노레포라 `rules:changes`로 `backend/**/*` 또는 `.gitlab-ci.yml` 수정 시에만 파이프라인이 돈다. 프론트엔드 디렉토리만 수정하면 백엔드 파이프라인은 아예 생성되지 않는다.

### 2. Spring Boot 컨테이너 이미지 (`backend/Dockerfile`, `backend/.dockerignore`)
- **베이스 이미지**: `eclipse-temurin:21-jre-noble` — JRE만 포함한 가벼운 이미지. ARM64 지원 확인됨.
- **보안**: 전용 `app` 유저로 드롭. root로 Java 프로세스를 띄우지 않는다.
- **메모리 튜닝**: `JAVA_TOOL_OPTIONS=-XX:+UseContainerSupport -XX:MaxRAMPercentage=60` — 컨테이너 메모리 제한의 60%까지 힙 사용. EC2 `m7g.medium`(RAM 4GB)에서 다른 컨테이너와 공존하는 구조를 염두에 둔 값.
- **Healthcheck**: `curl /api/health`를 20초 간격으로 타격. `docker compose`의 `depends_on: condition: service_healthy`와 맞물려 배포 성공/실패 판정에 쓰인다.
- **`.dockerignore`**: `build/libs/*-SNAPSHOT.jar`만 허용하고 `.git`, `src/test`, `.claude`, `.env*` 등은 전부 제외. 빌드 컨텍스트 축소 + 우발적 시크릿 포함 방지.

### 3. 운영 프로필 (`backend/src/main/resources/application-prod.yml`, 46줄)
- DB/Redis 접속 정보를 전부 `${DB_HOST}`, `${DB_PASSWORD}` 식으로 환경변수에서 주입. 기본값은 `postgres`, `redis`(docker compose 서비스명) 로.
- `server.forward-headers-strategy: framework` — Cloudflare→Nginx→API로 이어지는 프록시 체인에서 실제 클라이언트 스킴/IP를 신뢰한다.
- `spring.lifecycle.timeout-per-shutdown-phase: 20s` + `server.shutdown: graceful` — 재배포 시 inflight 요청 20초까지 기다린 뒤 종료.
- `ddl-auto: update` — 초기 단계용. 엔티티가 추가될 때마다 자동 반영.

### 4. `.gitignore` 보강
- `.env`, `.env.*` 커밋 차단, `.env.example`만 예외 허용. 실제로 EC2 `/opt/dotshelf/.env`에는 강력 랜덤 비번이 들어있어서 레포에 섞이면 사고다.

---

## 왜 했나

1. 지금까지 배포가 "로컬에서 bootJar → scp → ssh로 `docker compose up -d`"라는 **수동 절차**였다. 팀에 들어올 다른 개발자가 동일 절차를 밟아야 하고, 중간에 한 단계 빼먹으면 배포가 실패한다. 초기 프로젝트라도 배포 스크립트는 코드로 박아두는 편이 회귀가 적다.
2. Cloudflare Full (strict)를 이미 붙인 상황이라 **오리진까지 HTTPS가 강제**된다. 다시 말해 Nginx에 적절한 인증서/헤더 처리가 있어야 하고, 그 Nginx가 돌려면 Spring Boot 컨테이너가 안정적으로 올라와야 한다. 배포 자동화가 없으면 이 체인이 깨졌을 때 원인 파악 시간이 길어진다.
3. `application-local.yml`에 박혀있던 `dotshelf/dotshelf` 비밀번호가 그대로 운영에 올라갈 경우를 막기 위해, **환경변수 기반 prod 프로필을 먼저 만들고** EC2 `.env`에서 강력 랜덤 비번으로 대체했다.

---

## 어떻게 구현했나

### 주제 1: GitLab CI monorepo — `rules:changes` + YAML anchor 재사용

모노레포라 backend / frontend / 기타가 한 `.gitlab-ci.yml`을 공유해야 한다. 그대로 `rules`를 매 job에 복붙하면 금방 지저분해진다. **YAML anchor**로 룰을 뽑아뒀다.

```yaml
.backend-rules: &backend-rules
  rules:
    - if: '$CI_COMMIT_BRANCH == "dev"'
      changes:
        - backend/**/*
        - .gitlab-ci.yml
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
      changes:
        - backend/**/*
        - .gitlab-ci.yml

.deploy-rules: &deploy-rules
  rules:
    - if: '$CI_COMMIT_BRANCH == "dev"'
      changes:
        - backend/**/*
        - .gitlab-ci.yml

backend:build:
  # ...
  <<: *backend-rules     # build는 MR에도 실행

backend:deploy:
  # ...
  <<: *deploy-rules      # deploy는 dev 브랜치에만
```

핵심 포인트:
- **`build`는 MR 이벤트에도 실행**해서 "이 브랜치의 코드가 컴파일 되나?"를 미리 검증. 단 deploy는 돌지 않음.
- **`deploy`는 `dev` 브랜치 push에만**. MR이 dev로 머지되면 dev에 새 커밋이 생기고 그 때 deploy가 발동한다.
- `.gitlab-ci.yml` 자체도 `changes` 목록에 추가 → CI 파일만 고쳐도 CI가 돌게 해서 CI 수정이 실제로 잘 동작하는지 즉시 확인 가능.

### 주제 2: SSH 배포 스크립트 — 키, known_hosts, rsync

Deploy job에서 가장 신경 쓴 부분은 **SSH 자동화**다.

```yaml
before_script:
  - apk add --no-cache openssh-client rsync curl
  - mkdir -p ~/.ssh && chmod 700 ~/.ssh
  - echo "$SSH_PRIVATE_KEY" | tr -d '\r' > ~/.ssh/id_ed25519
  - chmod 600 ~/.ssh/id_ed25519
  - echo "$EC2_KNOWN_HOSTS" > ~/.ssh/known_hosts
  - chmod 600 ~/.ssh/known_hosts
```

핵심 포인트:
- **`tr -d '\r'`**: Windows에서 생성된 키를 GitLab 변수에 붙여넣으면 `\r\n`이 섞여 들어가 `Load key "id_ed25519": invalid format` 에러가 뜬다. 이걸 방어하는 한 줄.
- **`known_hosts` 고정**: `StrictHostKeyChecking=no` 같은 걸 넣고 싶어지지만, **MITM 방어를 포기하는 셈**이다. 대신 `ssh-keyscan -H 3.38.240.110`로 EC2 호스트 키를 미리 뽑아 변수로 등록해서 검증한다.
- **CI 전용 키 분리**: 관리용 pem(`f206_jihocompany.pem`)을 CI에 재사용하지 않고, ED25519 키 페어(`dotshelf-deploy`)를 새로 만들어 공개키만 EC2 `authorized_keys`에 추가. 키가 유출되면 그 키 하나만 회수하면 됨.

배포 본체는 이렇게 짧다:

```yaml
script:
  - JAR=$(ls $JAR_ARTIFACT_PATH/*-SNAPSHOT.jar | head -1)
  - ssh "$EC2_USER@$EC2_HOST" "mkdir -p $EC2_DEPLOY_PATH/api/build/libs"
  - rsync -az --delete "$JAR" "$EC2_USER@$EC2_HOST:$EC2_DEPLOY_PATH/api/build/libs/"
  - ssh "$EC2_USER@$EC2_HOST" "cd $EC2_DEPLOY_PATH && docker compose build api && docker compose up -d api && docker image prune -f"
```

`rsync -a` 대신 `scp`도 됐지만, `--delete`를 붙여 과거 빌드 jar가 남아 Dockerfile의 `COPY *-SNAPSHOT.jar`에서 잘못된 파일이 선택되는 사고를 방지.

### 주제 3: Dockerfile — fat jar + 컨테이너-aware JVM 옵션

```dockerfile
FROM eclipse-temurin:21-jre-noble

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && ln -fs /usr/share/zoneinfo/Asia/Seoul /etc/localtime \
    && echo "Asia/Seoul" > /etc/timezone

RUN groupadd --system app && useradd --system --gid app --home /app app
WORKDIR /app

ARG JAR_FILE=build/libs/*-SNAPSHOT.jar
COPY --chown=app:app ${JAR_FILE} app.jar

USER app

ENV JAVA_TOOL_OPTIONS="-XX:+UseContainerSupport -XX:MaxRAMPercentage=60 -Djava.security.egd=file:/dev/./urandom -Duser.timezone=Asia/Seoul"
ENV SPRING_PROFILES_ACTIVE=prod

EXPOSE 8080
HEALTHCHECK --interval=20s --timeout=5s --start-period=45s --retries=3 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["java", "-jar", "app.jar"]
```

결정 근거:
- **`21-jre-noble`** (not `-slim`, not `-alpine`): Alpine은 musl libc라 가끔 QueryDSL/Hibernate 바이트코드 쪽에서 미묘한 이슈가 있고, `-slim`은 `curl`이 빠져있어 `HEALTHCHECK`가 안 돈다. `noble`(Ubuntu 24.04)는 용량이 좀 더 크지만 툴 가용성이 좋다.
- **fat jar 한 번에 COPY**: layered jar로 쪼개면 디펜던시와 애플리케이션 클래스를 분리해서 레이어 캐시 효율이 올라가지만, 지금은 최초 구축이라 단순함을 우선. (더 공부할 것에 이 주제 포함)
- **`MaxRAMPercentage=60`**: 컨테이너 메모리 제한의 60%를 힙으로. `-Xmx` 고정값 대신 `MaxRAMPercentage`를 쓰는 이유는 compose에서 `mem_limit`을 올리거나 내리면 자동으로 따라가기 때문.
- **`/dev/./urandom`**: 오래된 JVM urandom 블로킹 이슈 회피 관용구. Java 21에선 사실 기본값이 괜찮지만 관습적으로 붙여둠.
- **`HEALTHCHECK --start-period=45s`**: Spring Boot + Hibernate + Redis 연결까지 합치면 첫 부팅은 30초 이상 걸림. start-period 동안은 실패해도 unhealthy로 치지 않게 해서 compose의 depends_on이 조기에 끊기지 않도록.

### 주제 4: `application-prod.yml` — 환경변수 주입 + 프록시 인지

```yaml
server:
  forward-headers-strategy: framework
  shutdown: graceful

spring:
  datasource:
    url: jdbc:postgresql://${DB_HOST:postgres}:${DB_PORT:5432}/${DB_NAME:dotshelf}
    username: ${DB_USER:dotshelf}
    password: ${DB_PASSWORD}                 # 기본값 의도적으로 없음 — 주입 못 하면 앱이 뜨지 않음
    hikari:
      maximum-pool-size: 10
      minimum-idle: 2
      connection-timeout: 10000
  jpa:
    hibernate:
      ddl-auto: update
    properties:
      hibernate:
        dialect: org.hibernate.dialect.PostgreSQLDialect
        format_sql: false
        jdbc:
          time_zone: Asia/Seoul
```

포인트:
- **`${DB_PASSWORD}` 기본값 없음**: `.env`가 누락되면 앱이 즉시 실패한다. "빈 비번으로 어거지로 뜨는" 사고를 원천 차단.
- **`forward-headers-strategy: framework`**: Cloudflare가 `X-Forwarded-Proto: https`를 보내고 Nginx가 그걸 그대로 보존(또는 재작성)해서 넘긴다. 이걸 Spring이 신뢰해야 `HttpServletRequest.isSecure()`, OAuth2 redirect URL 생성, 쿠키 `Secure` 플래그 결정 등이 맞게 동작.
- **Hikari pool 10**: `vCPU × 2 + effective_spindle_count` 같은 고전 공식으로 추정. Graviton m7g.medium은 1 vCPU라 2 근처여도 되지만, 네트워크 대기가 섞이는 API 특성상 10 근처가 보통. 부하테스트 하면서 재조정 예정.
- **`ddl-auto: update`**: 경고등. 엔티티 컬럼을 "삭제"해도 update 모드에선 DB 컬럼이 남는다. 스키마 drift는 쌓인다. 초기 단계라 수용하지만 곧 Flyway 도입 예정.

---

## 배운 것

### 1. GitLab의 Protected Variables는 **브랜치 보호가 선행 조건**
- "Protect variable" 체크만 하면 끝나는 줄 알았는데, 해당 브랜치가 **Protected Branch로 등록돼 있어야** 변수가 주입된다.
- 우리는 `dev`/`master`를 Protected로 등록해서 해결. 등록 안 하면 deploy job이 `SSH_PRIVATE_KEY: unbound variable`로 죽는다.

### 2. SSH MultiLine 변수와 `\r` 트랩
- Windows 파일을 "메모장에서 복사" 하면 `\r\n`이 끼어든다. GitLab에 붙여넣으면 그대로 저장되고, 리눅스 컨테이너에서 `chmod 600; ssh -i`로 쓸 때 키 포맷 에러가 난다.
- `tr -d '\r'`로 before_script에서 한 번 걷어내는 방어가 관용적.

### 3. Cloudflare "Full (strict)"는 오리진 인증서를 Cloudflare가 신뢰할 수 있어야 성립
- Let's Encrypt(공인)도 되지만, **Cloudflare Origin CA 인증서**(15년짜리)가 무료고 설치도 간편.
- Cloudflare에서 한 번 발급할 때 `*.dotshelf.digital` + `dotshelf.digital` 두 줄을 넣어 **와일드카드 한 장**으로 모든 서브도메인 커버.

### 4. `rules:changes`의 "변경 없음" 판정
- 첫 push로 브랜치가 새로 생기면 `changes`는 "모든 파일 변경"으로 간주되어 파이프라인이 무조건 돈다. 중간 push들에서는 그 push의 diff만 본다.
- 그래서 "backend만 안 건드렸는데 파이프라인 도네?"가 첫 push에서는 **정상**. 두 번째부터 필터가 유효.

### 5. ARM64 & Multi-arch 이미지
- EC2 `m7g.medium`은 Graviton(ARM). `docker pull nginx:1.28-alpine`은 자동으로 `linux/arm64` manifest를 고른다.
- 우리 스택(postgres:17, redis:7.4, nginx:1.28-alpine, eclipse-temurin:21)은 모두 multi-arch. 가끔 old/niche 이미지는 amd64만 있어 `no matching manifest` 에러가 난다. 사전 확인 필요.

### 6. `docker image prune -f`를 deploy 끝에 실행
- 매 배포마다 새 `dotshelf/api:latest` 이미지가 만들어지고, 이전 같은 태그는 dangling(`<none>`)으로 남는다. 쌓이면 디스크 고갈.
- CI에서 `docker image prune -f`를 돌려 dangling만 정리. 태그 붙은 이미지(e.g. postgres:17)는 건드리지 않음.

---

## 더 공부할 것

- [ ] **Spring Boot layered jar** — `build.gradle.kts`에 `bootBuildImage` 또는 `-Pbootjar.layered=true` 설정 + Dockerfile에서 `jarmode tools extract`로 레이어 추출. fat jar 대비 이미지 캐시 히트율과 재배포 속도 차이가 얼마나 되는지 측정해볼 가치가 있음. 참고: `BP_JVM_VERSION`, Paketo buildpacks.
- [ ] **Gradle `bootBuildImage` vs 직접 Dockerfile** — Spring Boot 자체가 Paketo buildpacks 기반 이미지 빌드를 제공. Dockerfile 없이도 `./gradlew bootBuildImage`로 production-grade 이미지가 나옴. 무엇을 포기하고 무엇을 얻는가.
- [ ] **Flyway vs Liquibase 도입 시점** — `ddl-auto: update`의 한계(컬럼 삭제 무반영, 인덱스 drift, 외래키 관리 어려움). 언제 마이그레이션 툴로 전환해야 하는가, 첫 마이그레이션을 어떻게 부트스트랩할 것인가(베이스라인 전략).
- [ ] **Hikari pool sizing** — `maximum-pool-size=10`의 근거를 감각이 아닌 데이터로. `SHOW max_connections` vs API 동시 접속 vs 요청 평균 지연. PostgreSQL의 connection overhead(프로세스 기반)와 풀 크기 관계.
- [ ] **JVM `MaxRAMPercentage` 세부 동작** — `-XX:+UseContainerSupport`가 cgroups v2에서 어떻게 메모리 제한을 감지하는지. 60%는 어느 범위에서 안전한 값인가(metaspace + GC overhead + 스레드 스택).
- [ ] **GitLab CI artifact vs Container Registry** — 현재는 jar를 artifact로 넘기는데, 규모 커지면 `docker push`로 레지스트리에 이미지 밀고 deploy는 `docker pull`만 하는 게 일반적. 전환 임계점은 팀 크기/빌드 빈도/이미지 크기 어디에서 결정되는가.
- [ ] **Cloudflare Zero Trust (Access)** — Grafana/Prometheus 같은 내부 UI 서브도메인을 공개 없이 팀원만 접근하게 하려면 Cloudflare Access(무료 50user)가 유력. Google SSO와 연결.
- [ ] **`.env` 관리 패턴** — EC2에 `.env`가 있는 지금 상태는 작동하지만, 서버 재구축 시 복구가 수작업. AWS SSM Parameter Store / Secrets Manager로 올리고 compose 기동 시 fetch하는 패턴을 언제 도입할지.
- [ ] **GitLab `environments` 와 수동 배포 승인** — 지금은 dev 브랜치 push가 자동 배포. 나중에 `master` 도입하면 `when: manual`로 수동 승인 + `environments: staging/production` 분리 구조 필요. 어떻게 짜면 롤백까지 쉽게 나오나.

---

## 참고

- **커밋**: `dbb98af`
- **관련 인프라(레포 밖 상태)**:
  - EC2: `3.38.240.110`, Ubuntu 24.04 ARM64, `/opt/dotshelf/` 배치
  - Cloudflare: `dotshelf.digital` 관리, Full (strict), Origin 인증서 `*.dotshelf.digital`
  - GitLab CI Variables: `SSH_PRIVATE_KEY`, `EC2_KNOWN_HOSTS`, `EC2_HOST`, `EC2_USER`, `EC2_DEPLOY_PATH`
  - Protected Branches: `dev`, `master`
- **로컬 파일(레포 밖, 분실 시 복구 어려움)**:
  - `C:\PROJECT\DOTSHELF\server\f206_jihocompany.pem` (EC2 관리용 키, AWS에서 발급)
  - `C:\PROJECT\DOTSHELF\server\dotshelf-deploy` / `.pub` (CI 배포 전용 ED25519)
  - `C:\PROJECT\DOTSHELF\server\origin-cert.pem` / `origin-key.pem` (Cloudflare Origin 인증서 사본)
- **운영 명령어 치트시트**:
  ```bash
  ssh dotshelf 'cd /opt/dotshelf && docker compose logs -f api'
  ssh dotshelf 'cd /opt/dotshelf && docker compose ps'
  ssh dotshelf 'cd /opt/dotshelf && docker compose restart api'
  ```
