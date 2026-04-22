# 2026-04-22 · App/DB 서버 분리 + Tailscale 사설망 구축

기존 단일 EC2 풀스택(api + nginx + postgres + redis) 구성을 **2-tier** 로 쪼갰다. SSAFY 에서 지급받은 추가 서버 한 대를 App 서버로 투입하고, 두 서버를 **Tailscale WireGuard 사설망**으로 연결한 뒤 DB/Redis 만 공용 인터넷에서 차단했다.

## TL;DR

- **새 토폴로지**: `Cloudflare → prod Nginx → (Tailscale) → SSAFY api → (Tailscale) → prod postgres/redis`
- 두 서버 사이 DB 연결은 **Tailscale VPN** 로만 허용. PG/Redis 포트는 **Tailscale IP(`100.86.247.88`) 에만 bind** — 공용 인터넷 접근 불가
- SSAFY 서버(Lightsail t2.xlarge, **amd64**)는 **수명이 유한**해 상태 없는 앱만 올림. DB/Nginx/TLS 는 영구 서버(prod, m7g.medium ARM64)에 그대로 유지
- CI(`/gitlab-ci.yml`) deploy target 을 prod → SSAFY 로 교체. ARM64 이미지는 SSAFY 에서 사용 불가라 **amd64 로 재빌드**
- Gerrit 은 SSAFY 서버에서 그대로 돌되 **8989 포트 외부 차단** (ufw 레벨)
- PostgreSQL 자동 일일 백업 cron 세팅 (아직 DB 비어있음 → 우선순위 낮지만 선제적으로)

## 1. 왜 분리했나

### 1-1. 배경
- 기존: prod EC2(`m7g.medium`, 1 vCPU / 3.7 GiB) 한 대에 api + nginx + postgres + redis + GitLab Runner 전부 탑재
- 방금 SSAFY 에서 **Lightsail t2.xlarge(4 vCPU / 15 GiB, amd64)** 추가 서버 받음
- 이 SSAFY 서버엔 이미 **Gerrit(`java`, `httpd`) 가 running 중** — 쉽게 없앨 수 없는 상태

### 1-2. 선택지 비교

| 안 | 구조 | 문제 |
|----|------|------|
| A | 전부 그대로 + SSAFY 는 놀림 | prod 가 CPU/RAM 타이트. SSAFY 자원 낭비 |
| B | 전부 SSAFY 로 이전 | **DB 데이터가 휘발성 서버에 얹힘**. 학기말 소멸 시 전부 증발 |
| C | **App 만 SSAFY 로, DB 는 prod 유지** | 역할 분리로 리소스 균형 + 영속 데이터는 장기 서버에 고정 |

→ **C 선택**. "변하면 안 되는 것(DNS/TLS/데이터)은 오래 사는 서버에, swappable 한 것(앱)은 휘발성 서버에" 원칙.

### 1-3. 네트워크 설계 의도
- 두 서버 사이 DB/Redis 트래픽은 **공용 인터넷에 노출해선 안 됨**
- 후보 2개 비교: AWS Security Group IP whitelist vs Tailscale VPN
- Tailscale 채택 이유:
  - IP 변동 내성 (SSAFY 서버 IP 가 Lightsail 특성상 언제든 바뀔 수 있음)
  - 로컬 DBeaver 접근까지 같은 방식으로 커버 가능
  - 팀 3명 이하에선 무료 티어 충분
  - WireGuard 기반이라 direct peer path 확보 시 1–2 ms RTT

## 2. 새 토폴로지

```
 Client
   │  HTTPS (443)
   ▼
 Cloudflare (Proxied, Full strict)            ← DNS/TLS 고정
   │  HTTPS (443) — Origin Cert
   ▼
 [prod]  dotshelf-nginx  :80/:443            ← 기존 영구 서버
   │  proxy_pass → 100.74.2.103:8080         ← Tailscale peer 로 변경
   ▼
 [Tailscale WireGuard direct peer, ~2 ms RTT]
   │
   ▼
 [SSAFY]  dotshelf-api  100.74.2.103:8080    ← 새 앱 서버, amd64
   │  JDBC → dotshelf-prod:5432
   │  Redis → dotshelf-prod:6379
   ▼
 [Tailscale]
   │
   ▼
 [prod]  dotshelf-postgres  100.86.247.88:5432   ← Tailscale IP 에만 bind
         dotshelf-redis     100.86.247.88:6379   ← Tailscale IP 에만 bind
```

단일 DB 쿼리 요청의 실측 end-to-end: **~13 ms** (Cloudflare 제외, prod→SSAFY→PG→돌아옴 왕복).

## 3. Tailscale 구성

### 3-1. 설치

```bash
# 양쪽 서버에서 동일
curl -fsSL https://tailscale.com/install.sh | sudo sh
sudo tailscale up --hostname=<dotshelf-prod|dotshelf-app>
# 인증 URL 을 브라우저에서 열어 같은 계정으로 로그인
```

### 3-2. 할당된 주소

| 노드 | Tailscale hostname | Tailscale IP |
|------|--------------------|--------------|
| prod | `dotshelf-prod` | `100.86.247.88` |
| SSAFY | `dotshelf-app` | `100.74.2.103` |

### 3-3. 검증

```bash
# 양방향 direct peer path 확인
tailscale ping -c 2 dotshelf-app
# → pong ... via <peer-public-ip>:41641 in 1ms   ← 'via' 가 찍혀야 direct

# MagicDNS 이름 해석 (컨테이너 밖에서만 동작, 주의)
getent hosts dotshelf-prod
```

### 3-4. 주의 — 컨테이너에서의 Tailscale 접근

- Tailscale 은 호스트의 `tailscale0` 인터페이스에 올라감
- Docker 컨테이너는 기본적으로 bridge 네트워크 격리
- **실제 테스트 결과**: 컨테이너에서 `100.x.x.x` 로 요청 시, Tailscale 이 알아서 masquerade → **동작함** (`docker exec dotshelf-nginx wget http://100.74.2.103:8080/api/health` OK)
- 즉 추가 라우팅 설정 없이 그냥 쓰면 됨. MagicDNS 는 컨테이너에 안 들어가니 **IP 로 직접 지정** (nginx upstream 등)

## 4. 방화벽 (ufw) — SSAFY 서버

SSAFY Lightsail 이라 AWS SG 가 `1024-65535` 까지 전부 오픈. 실효 방화벽은 **OS 레벨 ufw** 가 담당.

### 4-1. 기존 상태 (SSAFY 기본 제공)

```
22/tcp       ALLOW   # SSH
443/tcp      ALLOW   # (미사용, 예비)
8989/tcp     ALLOW   # Gerrit 웹
```

Gerrit SSH(`29418`) / 내부(`8988`) 는 원래 ufw 로 막혀있었음 — 최초 진단 때 "공개 노출" 로 오판했다가 수정.

### 4-2. 오늘 적용한 변경

```bash
sudo ufw allow in on tailscale0                        # Tailscale 내부 전부 허용
sudo ufw allow 41641/udp comment 'Tailscale WireGuard' # direct peer path 안정화
sudo ufw delete allow 8989/tcp                          # Gerrit 외부 차단 (프로세스는 유지)
```

### 4-3. 최종 상태

```
22/tcp                     ALLOW IN  Anywhere
443/tcp                    ALLOW IN  Anywhere      # 아직 미사용, 유지
Anywhere on tailscale0     ALLOW IN                # ← DB/앱 간 통신
41641/udp                  ALLOW IN  Anywhere      # Tailscale WireGuard
```

### 4-4. Gerrit 롤백 메모
필요해지면 즉시:
```bash
ssh dotshelf-app "sudo ufw allow 8989/tcp"
```
프로세스는 계속 돌고 있어 포트만 열면 복구 완료.

### 4-5. prod 는 왜 안 건드렸나
- AWS EC2 (Lightsail 아님) 라 Security Group 이 이미 `22/80/443` 만 허용 — 1차 방화벽 역할 충분
- fail2ban(sshd jail) 가동 중
- Docker 가 container 포트 publish 시 iptables 를 직접 조작해 **ufw 를 우회**하는 특성이 있음 → ufw 쓰더라도 Docker 앞에 있으면 한계
- 따라서 **ufw 대신 Docker 바인드 주소를 Tailscale IP 로 명시 제한** 하는 방식으로 노출 통제 (아래 §5)

## 5. PostgreSQL / Redis Tailscale IP 바인드 (prod)

### 5-1. 변경 전
```
dotshelf-postgres :5432   (publish 없음, docker 내부 네트워크만)
dotshelf-redis    :6379   (publish 없음, docker 내부 네트워크만)
```
같은 호스트의 api 만 접근 가능. 외부 호스트(SSAFY) 에서 접근할 방법 없음.

### 5-2. 변경 — `docker-compose.yml` 패치

```diff
 services:
   postgres:
     image: postgres:17
     container_name: dotshelf-postgres
     restart: unless-stopped
+    ports:
+      - "100.86.247.88:5432:5432"   # Tailscale IP 에만 bind
     environment:
       ...

   redis:
     ...
     command: ["redis-server", "--appendonly", "yes"]
+    ports:
+      - "100.86.247.88:6379:6379"
```

> **중요**: 반드시 `<Tailscale-IP>:5432:5432` 형태로 IP 명시. `"5432:5432"` 만 쓰면 `0.0.0.0` 에 붙어 공용 인터넷에 노출된다.

### 5-3. 적용

```bash
cd /opt/dotshelf
sudo docker compose up -d postgres redis   # recreate (ports 변경은 recreate 필요)
```

- 기존 `dotshelf-api`(이 시점엔 prod 에 아직 있던 버전) 는 HikariCP 가 자동 재연결 → 수동 재시작 불필요
- PG/Redis data volume 은 그대로 유지 → 데이터 손실 없음

### 5-4. 인증(`pg_hba.conf`) 확인

postgres:17 공식 이미지 기본값으로 `host all all all scram-sha-256` 가 깔려있어 외부 IP 에서 오는 연결도 비밀번호만 맞으면 허용. **listen_addresses = \*** 역시 기본값.

→ 별도 수정 없이 Tailscale 경유 접속 가능.

### 5-5. 검증

```bash
# SSAFY 에서
timeout 5 bash -c '</dev/tcp/dotshelf-prod/5432' && echo OK
# → OK

# 실제 psql
PGPASSWORD=... psql -h dotshelf-prod -U dotshelf -d dotshelf -c 'SELECT version();'
# → PostgreSQL 17.9 ... 응답
```

공용 인터넷 경로 차단 확인:
```bash
# SSAFY 에서 prod public IP 로 시도
timeout 5 bash -c '</dev/tcp/3.38.240.110/5432' && echo OK || echo BLOCKED
# → BLOCKED
```

## 6. SSAFY 서버 세팅

### 6-1. Docker 설치

```bash
# Docker 공식 저장소 → docker-ce 29.4 + compose plugin v5.1.3
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update && sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker ubuntu   # 새 세션부터 sudo 없이 docker
```

prod 의 버전과 **완전 일치 (Docker 29.4 / Compose v5.1.3)** — compose yaml 호환성 확보.

### 6-2. 디렉토리 스캐폴딩

```
/opt/dotshelf/
├── docker-compose.yml           # api 만 정의
├── .env                         # DB_HOST=dotshelf-prod, REDIS_HOST=dotshelf-prod 로 변경
└── api/
    ├── Dockerfile               # prod 원본과 동일
    └── build/libs/*.jar         # CI rsync 타겟
```

`/opt/dotshelf/api/` 는 prod 에서 tar 파이프로 초기 복사:

```bash
ssh dotshelf "sudo tar -cf - -C /opt/dotshelf api" | ssh dotshelf-app "tar -xf - -C /opt/dotshelf"
```

### 6-3. `docker-compose.yml` (SSAFY 버전)

```yaml
services:
  api:
    build:
      context: ./api
      dockerfile: Dockerfile
    image: dotshelf/api:latest
    container_name: dotshelf-api
    restart: unless-stopped
    ports:
      - "100.74.2.103:8080:8080"     # Tailscale IP 에만 bind
    environment:
      SPRING_PROFILES_ACTIVE: ${SPRING_PROFILES_ACTIVE}
      DB_HOST: ${DB_HOST}             # = dotshelf-prod
      DB_PORT: ${DB_PORT}
      DB_NAME: ${DB_NAME}
      DB_USER: ${DB_USER}
      DB_PASSWORD: ${DB_PASSWORD}
      REDIS_HOST: ${REDIS_HOST}       # = dotshelf-prod
      REDIS_PORT: ${REDIS_PORT}
      JWT_SECRET: ${JWT_SECRET}
      JWT_ACCESS_TTL: ${JWT_ACCESS_TTL}
      JWT_REFRESH_TTL: ${JWT_REFRESH_TTL}
      TZ: Asia/Seoul
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

- `build:` 와 `image:` 를 **둘 다 둠** — CI 는 `up -d --build` 로 재빌드, 평소엔 image 참조로 기동
- api 서비스 하나뿐이라 `networks:` 블록 생략 (Docker default bridge 사용)
- `depends_on:` 없음 (DB 가 다른 호스트에 있으니 의미 없음)
- **`extra_hosts: - "dotshelf-prod:100.86.247.88"`** 필수 — Tailscale MagicDNS 는 호스트 `/etc/hosts` 까지만 업데이트하고 컨테이너까진 전파 안 됨. JDBC(PostgreSQL) 는 Java 기본 InetAddress 리졸버가 /etc/hosts 의 FQDN 부분 매칭으로 운 좋게 해결하지만, **Lettuce(Redis) 가 쓰는 Netty DNS 리졸버는 /etc/hosts 를 무시**하고 DNS 서버만 조회 → `dotshelf-prod` SERVFAIL → Redis health DOWN. extra_hosts 로 컨테이너 /etc/hosts 에 고정 매핑하면 해결.

### 6-4. 아키텍처 주의 — amd64 재빌드

- prod 의 기존 `dotshelf/api:latest` 이미지는 **ARM64** 로 빌드되어 있어 SSAFY(x86_64) 에서 동작 불가
- SSAFY 에서 `docker build -t dotshelf/api:latest ./api` 로 **네이티브 amd64 빌드** → 문제 해결
- 다중 아키텍처 지원이 필요해지면 `docker buildx` + registry push 방식으로 전환 (현재는 불필요)

### 6-5. 최초 기동

```bash
cd /opt/dotshelf
sudo docker compose up -d
# 대기 후:
sudo docker ps
# dotshelf-api  Up (healthy)   100.74.2.103:8080->8080/tcp
curl http://100.74.2.103:8080/api/health
# {"code":"SUCCESS","httpStatus":200,...}
```

## 7. prod Nginx upstream 전환

### 7-1. 변경

`/opt/dotshelf/nginx/conf.d/default.conf`:

```diff
 upstream dotshelf_api {
-    server api:8080;
+    server 100.74.2.103:8080;   # via Tailscale to dotshelf-app
     keepalive 32;
 }
```

- 호스트명 `dotshelf-app` 대신 **IP 직접 지정** — Docker 컨테이너의 DNS resolver 는 Docker bridge 내부만 봄. MagicDNS 가 닿지 않음
- (IP 바뀔 가능성은 매우 낮음 — Tailscale 은 한번 할당하면 영구)

### 7-2. 적용

```bash
sudo docker exec dotshelf-nginx nginx -t    # syntax OK 확인
sudo docker exec dotshelf-nginx nginx -s reload
```

### 7-3. 검증

```bash
curl -k -H 'Host: api.dotshelf.digital' https://127.0.0.1/api/health
# HTTP 200 / ~24 ms
curl https://api.dotshelf.digital/api/health
# HTTP 200 / Cloudflare 경유까지 정상
```

## 8. prod 의 api 서비스 제거

### 8-1. 컨테이너 정지 + 제거

```bash
cd /opt/dotshelf
sudo docker compose stop api
sudo docker compose rm -f api
```

### 8-2. `docker-compose.yml` 정리

`api:` 서비스 블록 전체 제거 + `nginx:` 의 `depends_on: - api` 라인 제거.

prod 에 남은 서비스: `postgres`, `redis`, `nginx` 3개.

### 8-3. 이미지 정리

```bash
sudo docker rmi dotshelf/api:latest   # ARM64 구 이미지
sudo docker rmi hello-world           # 테스트 잔존
sudo docker image prune -f
```

## 9. GitLab CI 파이프라인 업데이트

### 9-1. `.gitlab-ci.yml` 변경 내용

```diff
   script:
     - JAR=$(ls $JAR_ARTIFACT_PATH/*-SNAPSHOT.jar | head -1)
     - ssh "$EC2_USER@$EC2_HOST" "mkdir -p $EC2_DEPLOY_PATH/api/build/libs && rm -f $EC2_DEPLOY_PATH/api/build/libs/*.jar"
     - rsync -az "$JAR" "$EC2_USER@$EC2_HOST:$EC2_DEPLOY_PATH/api/build/libs/"
-    - ssh "$EC2_USER@$EC2_HOST" "cd $EC2_DEPLOY_PATH && docker compose build api && docker compose up -d api && docker image prune -f"
+    - ssh "$EC2_USER@$EC2_HOST" "cd $EC2_DEPLOY_PATH && docker compose up -d --build api && docker image prune -f"
     - sh .ci/wait_for_api_healthy.sh
-    - 'ssh "$EC2_USER@$EC2_HOST" "curl -fsS -H ''Host: api.dotshelf.digital'' -k https://localhost/api/health"'
+    - 'ssh "$EC2_USER@$EC2_HOST" "curl -fsS http://100.74.2.103:8080/api/health"'
     - curl -fsS https://api.dotshelf.digital/api/health
```

### 9-2. 의미

- `build + up` 을 한 줄로 → `--build` 플래그 하나로 빌드+재기동
- 로컬 헬스체크 경로 변경: SSAFY 엔 nginx 가 없으므로 `https://localhost/...` 체크 불가 → Tailscale IP 로 api 직접 호출
- 공용 URL 체크는 그대로 유지 (Cloudflare → prod nginx → Tailscale → SSAFY → prod PG 전 경로 실증)

### 9-3. SSH 키 복사

CI 전용 공개키(`gitlab-ci-dotshelf-deploy` ed25519) 를 SSAFY `~/.ssh/authorized_keys` 에 추가. 
→ 기존 `SSH_PRIVATE_KEY` 시크릿 그대로 사용 가능 (GitLab Variables 수정 불필요).

### 9-4. GitLab Variables 수정 — `EC2_HOST`, `EC2_KNOWN_HOSTS`

상세 값은 별도 문서 `docs/infra/gitlab-ci-variables.md` 참조.

| Key | Before | After |
|-----|--------|-------|
| `EC2_HOST` | `172.17.0.1` (prod 내부 hairpin 회피용) | `3.36.72.58` (SSAFY Public IP) |
| `EC2_KNOWN_HOSTS` | `172.17.0.1` 의 호스트키 3줄 | `3.36.72.58` 의 호스트키 3줄 |
| `EC2_USER` | `ubuntu` | (변경 없음) |
| `EC2_DEPLOY_PATH` | `/opt/dotshelf` | (변경 없음) |
| `SSH_PRIVATE_KEY` | ed25519 CI 키 | (변경 없음 — SSAFY 에도 같은 공개키 등록) |

### 9-5. 과거 트러블슈팅과의 관계

[2026-04-21-runner-and-jwt.md](./2026-04-21-runner-and-jwt.md) 에서 "EC2 가 자기 자신 public IP 로 SSH 해 hairpin NAT 에 막혔다" 이슈는 **이제 해당 없음**. deploy target 이 "다른 호스트(SSAFY)" 가 되었기 때문에 일반 외부 SSH 와 동일한 경로.

→ 이제 deploy 단계에서 Docker bridge IP(`172.17.0.1`) 를 쓸 이유가 사라짐. 공용 IP 그대로 쓰면 됨.

### 9-6. 미검증 포인트

오늘은 실제 push 테스트까지 가지 않음. **다음 backend 관련 커밋 push 시 파이프라인 재검증 필요**.

## 10. PostgreSQL 자동 백업 cron

### 10-1. 스크립트 — `/opt/dotshelf/scripts/backup-pg.sh`

```bash
#!/bin/bash
set -euo pipefail

BACKUP_DIR="/opt/dotshelf/backups/pg"
RETENTION_DAYS=14
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/dotshelf-${TIMESTAMP}.sql.gz"

mkdir -p "${BACKUP_DIR}"
set -a; . /opt/dotshelf/.env; set +a

docker exec -i dotshelf-postgres pg_dump \
    -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" \
    --clean --if-exists --no-owner --no-privileges \
    | gzip > "${BACKUP_FILE}"

echo "[$(date -Iseconds)] Backup OK: ${BACKUP_FILE} ($(du -h ${BACKUP_FILE} | cut -f1))"
find "${BACKUP_DIR}" -name 'dotshelf-*.sql.gz' -mtime +${RETENTION_DAYS} -delete
```

### 10-2. cron — `/etc/cron.d/dotshelf-pg-backup`

```
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
MAILTO=""

0 3 * * * root /opt/dotshelf/scripts/backup-pg.sh >> /var/log/dotshelf-backup.log 2>&1
```

매일 **03:00 KST**, 14일 보관.

### 10-3. 한계 (의식하고 있을 것)

- 백업이 **DB 와 같은 물리 서버에 저장됨** → prod 디스크 fail 시 백업도 같이 증발
- 현재 DB 비어있어 immediate impact 없지만, 데이터 쌓이면 다음 중 하나 추가 필수:
  - pg_dump 후 외부 스토리지(S3/R2) 업로드
  - 주 1회 개발자 로컬에 `scp` 로 복사
  - Tailscale 을 통해 다른 노드로 푸시 (단, SSAFY 는 수명 유한하니 부적합)
- TODO: 실제 데이터 생기기 전에 **외부 백업 스토리지** 정하기

### 10-4. 수동 실행 / 복구

```bash
# 즉시 백업
sudo /opt/dotshelf/scripts/backup-pg.sh

# 최신 덤프로 복구
LATEST=$(ls -t /opt/dotshelf/backups/pg/*.sql.gz | head -1)
zcat "$LATEST" | sudo docker exec -i dotshelf-postgres psql -U dotshelf -d dotshelf
```

## 11. 로컬 SSH alias 정리

`~/.ssh/config`:

```ssh-config
Host dotshelf
    HostName 3.38.240.110
    User ubuntu
    IdentityFile C:\PROJECT\DOTSHELF\server\f206_jihocompany.pem
    ServerAliveInterval 60

Host dotshelf-app              # ← 기존 dotshelf-db 에서 이름 변경
    HostName k14f206.p.ssafy.io
    User ubuntu
    IdentityFile C:\PROJECT\DOTSHELF\server\K14F206T.pem
    ServerAliveInterval 60
```

기존 `dotshelf-db` 는 "SSAFY 서버 = DB 서버" 라는 초기 오해에서 나온 이름. 이제는 app 서버가 됐으니 `dotshelf-app` 으로 정정.

## 12. 운영 치트시트 (업데이트)

### 12-1. 접속

```bash
ssh dotshelf        # 영구 서버 (DB 역할) - 3.38.240.110
ssh dotshelf-app    # SSAFY 서버 (App 역할) - k14f206.p.ssafy.io
```

### 12-2. 상태

```bash
# prod: DB/nginx
ssh dotshelf "sudo docker ps"
ssh dotshelf "sudo docker logs -f dotshelf-nginx"

# SSAFY: app
ssh dotshelf-app "sudo docker ps"
ssh dotshelf-app "sudo docker logs -f dotshelf-api"

# Tailscale 상태
ssh dotshelf "tailscale status"
ssh dotshelf "tailscale ping -c 2 dotshelf-app"
```

### 12-3. 재기동

```bash
# app 만 수동 재기동 (CI 안 돌리고 현장 수정 시)
ssh dotshelf-app "cd /opt/dotshelf && sudo docker compose up -d --force-recreate api"

# DB 재기동 — 앱 끊김 주의
ssh dotshelf "cd /opt/dotshelf && sudo docker compose restart postgres"
```

### 12-4. 외부 헬스체크

```bash
curl https://api.dotshelf.digital/api/health
```

### 12-5. DB 접근 (로컬에서 직접)

본인 PC 에서도 Tailscale 만 설치하면 `dotshelf-prod` 로 바로 psql 가능 — DBeaver 연결도 동일:

```
Host: dotshelf-prod   (또는 100.86.247.88)
Port: 5432
DB:   dotshelf
User: dotshelf
```

## 13. 롤백 포인트

| 항목 | 롤백 방법 |
|------|-----------|
| PG Tailscale bind | `/opt/dotshelf/docker-compose.yml.bak.20260422-103056` 로 복원 |
| Nginx upstream | `/opt/dotshelf/nginx/conf.d/default.conf.bak.20260422-104547` 로 복원 |
| `.gitlab-ci.yml` | git revert |
| ufw 8989 차단 | `sudo ufw allow 8989/tcp` (SSAFY 에서) |
| prod api 재가동 | git 에 과거 `docker-compose.yml` 있음 → `api:` 블록 복구 + `api/build/libs/*.jar` 재전송 + `compose up -d api` |

## 14. 남은 숙제 (TODO)

- [ ] **CI 파이프라인 실제 push 로 검증** — 오늘 수정만 하고 실전 돌리지 않음
- [ ] **DB 외부 백업** — S3/R2 또는 정기 scp. 데이터 쌓이기 전에 결정
- [ ] **SSAFY 서버 소멸 D-day 확정** + 앱 이전 대안 서버 선정
- [ ] 영구 서버의 gitlab-runner.service 용도 정리 — 이제 prod 에서 빌드 안 함 (SSAFY 는 CI 가 deploy 만 수행). runner 자체가 불필요해졌는지 검토
- [ ] Tailscale ACL 제한 설정 (현재는 전노드 풀 오픈 상태)
- [ ] DB 마이그레이션을 **Flyway** 로 전환 (이전 문서 TODO 유지)
- [ ] UFW 인바운드를 Cloudflare IP 대역으로 한정 (prod 80/443) — 여전히 TODO

## 15. 배운 것

- **상태(state) 가 있는 것 vs 없는 것을 물리적으로 분리**하면 "이사" 비용이 극적으로 줄어든다. 앱은 컨테이너 한 번 restart 로 끝나지만 DB 이전은 다운타임 + 마이그레이션 전략이 필요
- **Tailscale 은 학생/소규모 팀에 과할 정도로 편한 사설망** — 5분 셋업, IP 불변, MagicDNS, ACL, 인터페이스까지 전부 포함. 굳이 AWS VPC peering 같은 거 안 써도 됨
- Docker container 에서 host 의 Tailscale peer 접근은 **masquerade 가 알아서 처리** — 추가 설정 없이 동작
- Docker `ports:` 에 `<IP>:<port>:<port>` 형태로 IP 명시하면 **공용 노출을 선택적으로 피할 수 있다**. `0.0.0.0` 바인드는 ufw 를 우회할 수 있으므로 반드시 IP 고정
- "이 서버는 언제까지 살 거냐?" 는 architecture 설계 때 1번 질문이다. 답이 "유한하다" 면 데이터는 절대 안 올린다
- 다른 아키텍처(ARM64 ↔ amd64) 서버 간 이미지 이전은 **반드시 재빌드**. `docker save | load` 는 편하지만 플랫폼 태그를 복제할 뿐 실행 가능성을 보장하지 않음

## 변경 이력

- 2026-04-22 · 최초 작성 (App/DB 분리 + Tailscale VPN 도입 + CI deploy target 전환 + PG 백업 cron)
