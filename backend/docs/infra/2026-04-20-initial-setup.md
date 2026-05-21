# 2026-04-20 · 운영 인프라 초기 구축

빈 EC2 인스턴스 한 대에서 시작해 `https://api.dotshelf.digital` 이 외부에서 정상 응답하기까지의 모든 설정을 기록한다.

## TL;DR

- 경로: **브라우저 → Cloudflare(엣지 SSL) → EC2 Nginx(오리진 SSL) → Spring Boot(`:8080`)**, 같은 도커 네트워크의 **PostgreSQL / Redis**
- 도메인 `dotshelf.digital` 은 **가비아에서 구매 → 네임서버를 Cloudflare 로 위임**
- EC2 는 **ARM64(m7g.medium) + Ubuntu 24.04**, Elastic IP 고정, SG 는 `22/80/443`만 오픈
- 모든 앱은 **`/opt/dotshelf/docker-compose.yml`** 한 방에서 올라감 (api · nginx · postgres · redis)
- 배포는 **GitLab CI** 가 `dev` 브랜치 푸시에 한해 `rsync + ssh` 로 EC2 에 JAR 전달 → `docker compose build && up -d api`
- 헬스체크는 `GET /api/health` 하나로 컨테이너 · Nginx · Cloudflare 3단계 검증

## 1. 네트워크 / 도메인 구성

### 1-1. 최종 토폴로지

```
 Client
   │  HTTPS (443)
   ▼
 Cloudflare (Proxied, Full strict)
   │  HTTPS (443)  — Cloudflare Origin Certificate (15y, *.dotshelf.digital)
   ▼
 EC2 Nginx (dotshelf-nginx 컨테이너, 80/443 publish)
   │  HTTP (proxy_pass)
   ▼
 Spring Boot (dotshelf-api 컨테이너, 8080, 네트워크 내부만 노출)
   │
   ├── PostgreSQL (dotshelf-postgres, 5432, 내부 전용)
   └── Redis      (dotshelf-redis,   6379, 내부 전용)
```

### 1-2. DNS (Cloudflare)

| Type | Name | Content | Proxy | 용도 |
|------|------|---------|-------|------|
| A | `@` | 3.38.240.110 | Proxied | 루트 도메인 (현재 placeholder) |
| A | `www` | 3.38.240.110 | Proxied | 웹 프론트용 예약 |
| A | `api` | 3.38.240.110 | Proxied | 백엔드 API (실사용) |
| A | `grafana` | 3.38.240.110 | Proxied | 모니터링(추후) |

- 가비아 네임서버 → `emely.ns.cloudflare.com`, `james.ns.cloudflare.com` 로 변경
- SSL/TLS 모드: **Full (strict)**
- Always Use HTTPS: **On**
- Universal SSL: **Active** (엣지-클라이언트 구간)

### 1-3. Cloudflare Origin Certificate

- 대상: `dotshelf.digital`, `*.dotshelf.digital`
- 유효기간: 15년
- EC2 저장 위치 (컨테이너에 마운트):
  - `/etc/ssl/cloudflare/origin.pem`  (0644, root:root)
  - `/etc/ssl/cloudflare/origin.key`  (0600, root:root)
- 로컬 백업: `C:\PROJECT\DOTSHELF\server\origin-cert.pem`, `origin-key.pem` (git 제외)

## 2. AWS (EC2)

| 항목 | 값 |
|------|----|
| 리전 | ap-northeast-2 (Seoul) |
| 인스턴스 ID | `i-0f8dff761caf9393e` |
| 타입 | `m7g.medium` (Graviton, **ARM64**) |
| OS | Ubuntu 24.04 LTS Noble |
| Elastic IP | `3.38.240.110` |
| 루트 볼륨 | gp3 |
| 키페어 | `f206_jihocompany.pem` (로컬: `C:\PROJECT\DOTSHELF\server\`) |
| 보안 그룹 | 인바운드 `22/tcp (관리자 IP)`, `80/tcp (0.0.0.0/0)`, `443/tcp (0.0.0.0/0)` |

> **ARM64 주의**: Docker 이미지를 고를 때 multi-arch 또는 `linux/arm64` 태그가 있는지 반드시 확인. (eclipse-temurin, alpine, nginx 공식 이미지는 OK)

## 3. EC2 OS 기본 설정

- **시스템 업데이트**: `apt update && apt full-upgrade -y`
- **타임존/로케일**: `Asia/Seoul`, `ko_KR.UTF-8`
- **스왑**: 4 GiB 파일 스왑 (`/swapfile`, `vm.swappiness=10`)
- **UFW 방화벽**: default deny incoming, 허용 `OpenSSH`, `80`, `443`
- **fail2ban**: sshd jail 활성
- **unattended-upgrades**: 보안 업데이트 자동 적용
- **SSH**: `/etc/ssh/sshd_config.d/99-dotshelf.conf`
  - `PasswordAuthentication no`
  - `PermitRootLogin no`
  - `PubkeyAuthentication yes`
- **배포 전용 SSH 키**: `ed25519` (GitLab CI 전용, admin pem 과 분리)
  - 공개키는 EC2 `ubuntu` 계정의 `~/.ssh/authorized_keys` 에 추가
  - 비공개키 로컬 백업: `C:\PROJECT\DOTSHELF\server\gitlab-ci_ed25519{,.pub}`

## 4. Docker 런타임

- Docker Engine **29.4**, Compose plugin **v5.1.3** (apt 공식 저장소)
- 사용자 `ubuntu` 를 `docker` 그룹에 추가
- 로그 드라이버: json-file, `max-size=10m`, `max-file=3` (data root 기본)

## 5. 앱 스택 (`/opt/dotshelf/`)

```
/opt/dotshelf/
├── docker-compose.yml
├── .env                      # 시크릿 (mode 600, root:root)
├── api/
│   └── build/libs/*.jar      # GitLab CI 가 rsync 로 덮어씀
└── nginx/
    └── conf.d/
        └── default.conf
```

### 5-1. docker-compose 서비스

| 컨테이너 | 이미지 | 역할 |
|----------|--------|------|
| `dotshelf-postgres` | `postgres:17-alpine` | 주 DB. `DB_*` 환경변수로 초기화. 내부 네트워크만 노출 |
| `dotshelf-redis` | `redis:7.4-alpine` | 캐시/세션 |
| `dotshelf-api` | 로컬 빌드 (`backend/Dockerfile`) | Spring Boot `prod` 프로필, `:8080` |
| `dotshelf-nginx` | `nginx:1.28-alpine` | `80/443` publish, 오리진 인증서 마운트 |

- `depends_on` 은 **healthcheck condition** 기반 (postgres/redis/ api healthy 후 nginx 기동)
- named volume: `dotshelf_postgres_data`, `dotshelf_redis_data`

### 5-2. `.env` 에 들어가는 키 (값은 서버에만 존재)

```
DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD
REDIS_HOST, REDIS_PORT
SPRING_PROFILES_ACTIVE=prod
```

비밀번호는 `openssl rand -base64 32` 로 생성, 필요 시 `/opt/dotshelf/.env` 직접 편집 후 `docker compose up -d` 로 재반영.

### 5-3. Spring Boot `application-prod.yml`

- `server.forward-headers-strategy: framework` — Cloudflare/Nginx 프록시 체인에서 원 IP/스킴 복원
- `server.shutdown: graceful` — 롤링 재기동 안전성
- Hikari `maximum-pool-size: 10` — m7g.medium 기준 보수적으로 시작
- JPA `ddl-auto: update` — 초기 운영 편의, 추후 Flyway 도입 예정

### 5-4. Dockerfile 포인트

- `eclipse-temurin:21-jre-noble` (ARM64 공식 지원)
- 비루트 `app` 유저 실행
- `-XX:+UseContainerSupport -XX:MaxRAMPercentage=60`
- `HEALTHCHECK` 이 컨테이너 단에서 `/api/health` 를 확인 → compose `depends_on` 과 CI 의 `wait_for_api_healthy.sh` 가 이걸 공유

## 6. Nginx 리버스 프록시

`/opt/dotshelf/nginx/conf.d/default.conf` 에 4 개 서버 블록:

1. **`:80` 전체** → `return 301 https://$host$request_uri;` (HTTP→HTTPS 강제)
2. **`api.dotshelf.digital:443`** → `proxy_pass http://dotshelf_api:8080;`
3. **`grafana.dotshelf.digital:443`** → 추후 Grafana 컨테이너 upstream (placeholder)
4. **기본 `:443 default_server`** → 루트 도메인 placeholder 응답 (404 아닌 200)

공통:

- `ssl_certificate /etc/ssl/cloudflare/origin.pem;`
- Cloudflare IP 대역을 `set_real_ip_from` 으로 신뢰, `real_ip_header CF-Connecting-IP;` 로 접속자 IP 복원
- `proxy_set_header` 로 `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Real-IP` 전달

## 7. 배포 파이프라인 (GitLab CI)

- 모노레포이므로 `.gitlab-ci.yml` 은 **레포 루트**에 둠
- 변경 필터: `rules:changes` 로 `backend/**/*`, `.gitlab-ci.yml`, `.ci/**/*` 가 바뀔 때만 파이프라인 실행
- `extends:` 로 build / deploy 공통 rules 블록 재사용 (YAML merge key 대신)
- 단계:
  1. `backend:build` — `eclipse-temurin:21-jdk` 이미지에서 `./gradlew bootJar`, `*-SNAPSHOT.jar` 를 artifact 로 업로드
  2. `backend:deploy` (`dev` 브랜치 전용) — `alpine:3.20` 에서 `openssh-client + rsync + curl` 설치 → EC2 로 JAR rsync → `docker compose build api && up -d api` → `wait_for_api_healthy.sh` → Nginx 경유 + Cloudflare 경유 헬스체크
- 롤백/재기동 시 CI 파이프라인을 다시 돌리는 것보다, 서버에서 직접 `docker compose up -d api` 가 더 빠름

### 7-1. GitLab CI/CD 변수 (Settings → CI/CD → Variables)

| Key | Masked | Protected | 값 요약 |
|-----|--------|-----------|---------|
| `SSH_PRIVATE_KEY` | ✅ | ✅ | CI 전용 ed25519 비공개키 원문 |
| `EC2_KNOWN_HOSTS` | ❌ | ✅ | `ssh-keyscan 3.38.240.110` 결과 |
| `EC2_HOST` | ❌ | ✅ | `3.38.240.110` |
| `EC2_USER` | ❌ | ✅ | `ubuntu` |
| `EC2_DEPLOY_PATH` | ❌ | ✅ | `/opt/dotshelf` |

> Protected Variables 는 **Protected Branches** 에서만 전달된다. `dev` 는 Protected 로 설정됨.

### 7-2. 헬스체크 스크립트

`.ci/wait_for_api_healthy.sh` — 컨테이너 health status 를 최대 60초 폴링, 실패 시 `docker compose logs --tail 50 api` 출력 후 exit 1. CI `script:` 에 한 줄로 호출한다.

## 8. 로컬 아티팩트 위치 (git 에 올리지 않는 것들)

`C:\PROJECT\DOTSHELF\server\`

| 파일 | 용도 |
|------|------|
| `f206_jihocompany.pem` | EC2 admin SSH |
| `gitlab-ci_ed25519` / `.pub` | GitLab CI 배포 전용 SSH 키쌍 |
| `origin-cert.pem` / `origin-key.pem` | Cloudflare Origin 인증서 백업 |

백엔드 저장소의 `.gitignore` 는 `.env`, `.env.*`(`.env.example` 제외) 를 무시한다.

## 9. 운영 치트시트

서버 SSH:

```bash
ssh -i C:/PROJECT/DOTSHELF/server/f206_jihocompany.pem ubuntu@3.38.240.110
```

상태 확인:

```bash
cd /opt/dotshelf
docker compose ps
docker compose logs --tail 100 -f api
```

재기동 (무중단에 가깝게):

```bash
docker compose up -d api        # api 만
docker compose up -d nginx      # nginx 만
```

이미지 용량 정리:

```bash
docker image prune -f
```

외부 헬스체크:

```bash
curl -fsS https://api.dotshelf.digital/api/health
```

## 10. 재구축 런북 (IaC 가 아직 아닌 부분)

| 항목 | 재현 방법 |
|------|-----------|
| EC2 인스턴스 / EIP / SG | 현재는 **수동 콘솔 작업**. 추후 Terraform 후보 |
| Cloudflare DNS / 인증서 | 수동 대시보드 작업 (계정 자체가 IaC 소스) |
| EC2 OS 하드닝 | 재현용 bootstrap 스크립트 필요 (TODO) |
| Docker/Compose 설치 | 공식 `get-docker.sh` |
| `/opt/dotshelf/` 트리 | `docker-compose.yml`, `nginx/conf.d/default.conf` 는 저장소에 올릴지 검토 (민감 X) |
| `.env` | 서버에서만 생성. 값은 1Password 등 별도 저장소에 보관 (TODO) |
| API 컨테이너 이미지 | 저장소 `backend/Dockerfile` 에서 **GitLab CI 파이프라인**이 재빌드 |

> 지금은 "EC2 가 사라지면 수동 복구 1일 소요" 수준. Terraform/Ansible 도입 전까지 이 문서가 런북 역할을 한다.

## 11. 보안 메모

- Cloudflare Proxied 상태에서 EC2 Origin IP 는 SG/UFW 로만 노출 — 추후 **Cloudflare IP 대역만 허용**하도록 UFW 축소 고려
- Spring `ddl-auto: update` 는 초기 단계만 허용. 운영 안정화 이후 **Flyway 마이그레이션**으로 전환
- GitLab CI deploy 키는 ssh `command=` 제한을 붙여 `rsync/docker compose` 만 허용하도록 강화 예정
- `.env` 값은 어디에도 커밋 금지 — `.gitignore` 로 차단하고 있지만 리뷰 시 2차 확인

## 12. 다음에 해야 할 일 (TODO)

- [ ] CI 파이프라인 최초 성공 (현재 파서 오류 수정 커밋 `177897e` 푸시 완료, 파이프라인 재실행 검증 필요)
- [ ] Grafana + Loki + Promtail 스택 추가 (현재 DNS/Nginx 레인만 예약)
- [ ] UFW 인바운드를 Cloudflare IP 대역으로 한정
- [ ] DB 마이그레이션을 Flyway 로 전환
- [ ] EC2/SG/EIP Terraform 화
- [ ] 백업: PostgreSQL 정기 dump + S3 업로드 cron

## 변경 이력

- 2026-04-20 · 최초 작성 (EC2/Cloudflare/Nginx/Docker/GitLab CI 초기 구축)
