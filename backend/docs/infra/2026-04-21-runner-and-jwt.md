# 2026-04-21 · GitLab Runner 자립 + JWT 환경변수 도입

하루 내내 씨름한 CI/CD 문제 해결과 JWT 배포 준비 작업을 시간 순으로 정리.

## TL;DR

- 어제 푸시한 파이프라인이 "🏷️ stuck" 태그로 20분 넘게 pending → **SSAFY 공용 Runner 가 우리 프로젝트 잡을 안 받는 상태**였음
- 해결: **EC2 에 `gitlab-runner` 직접 설치**하고 프로젝트 전용 Runner 로 등록
- 그 다음 배포 단계에서 `ssh` 가 **자기 자신 EC2 의 public IP 로 접속** 시도 → AWS hairpin NAT 제약으로 타임아웃
- 해결: `EC2_HOST` 를 공용 IP `3.38.240.110` → Docker bridge IP `172.17.0.1` 로 변경
- 이후 연쇄 이슈 2개: `EC2_KNOWN_HOSTS` 줄바꿈 유실, `EC2_USER` 값 오타
- 마지막으로 `feat/be/refactoring` 브랜치의 JWT 필터 체인을 미리 대비해 **EC2 `.env` + `docker-compose.yml` 에 `JWT_SECRET` 주입 라인 추가**

## 1. GitLab Runner 를 EC2 에 직접 설치

### 배경
- 첫 머지 후 파이프라인이 `Pending` + `stuck` 태그로 멈춤
- **Settings → CI/CD → Runners** 확인 결과: Project/Group/Instance Runner 모두 **0개**
- "Other available runners" 에 표시되던 `#1743`, `#1604` 은 시계 아이콘(stale) — 활성화해도 실제로 안 잡음
- 친구/공용 Runner 에 의존하지 않고 **우리 EC2 를 Runner 호스트로 자립** 방향 선택

### 설치 절차 (Ubuntu 24.04 ARM64)

```bash
# 1) 공식 저장소 추가
curl -sL "https://packages.gitlab.com/install/repositories/runner/gitlab-runner/script.deb.sh" | sudo bash

# 2) 설치
sudo apt-get install -y gitlab-runner
# 결과: gitlab-runner 18.11.1 (arm64)

# 3) docker executor 를 쓸 것이므로 gitlab-runner 유저를 docker 그룹에 추가
sudo usermod -aG docker gitlab-runner
sudo systemctl restart gitlab-runner

# 4) Runner 등록 (GitLab UI 에서 토큰 발급 후)
sudo gitlab-runner register \
  --non-interactive \
  --url https://lab.ssafy.com \
  --token glrt-xxxxxxxxxxxxxxxxxxxx \
  --executor docker \
  --docker-image alpine:3.20 \
  --description 'DotShelf EC2 runner'

# 5) 검증
sudo gitlab-runner status   # active
sudo gitlab-runner verify   # Runner is valid
sudo systemctl is-enabled gitlab-runner  # enabled
```

### Runner 설정 메모
- 등록 시 **"Run untagged jobs" 체크** — 우리 `.gitlab-ci.yml` 에 `tags:` 가 없어도 잡을 받도록
- **"Lock to current projects" 체크** — 다른 프로젝트가 실수로 이 Runner 를 쓰는 것 방지
- **"Maximum job timeout" = 1800s** — 30분 안에 끝나지 않는 잡은 강제 종료
- `config.toml` 경로: `/etc/gitlab-runner/config.toml` (토큰 포함되므로 읽기 주의)

### 부수 효과
- EC2 가 빌드 러너 역할까지 겸함 → 빌드 중 CPU 튐. m7g.medium 에서는 버틸 만.
- **Runner 전용 서버 분리** 는 추후 트래픽·배포 빈도 커지면 고려. 현재는 단일 EC2 가 "앱 + Runner + Postgres + Redis + Nginx" 를 전부 감당 중.

## 2. EC2_HOST 를 public IP → Docker bridge 로 전환

### 증상
Runner 설치 직후 deploy 잡에서:
```
ssh: connect to host [MASKED] port 22: Operation timed out
```

### 원인
```
[EC2: dotshelf-prod]
 └─ Docker 컨테이너 (gitlab-runner 의 alpine)
     └─ ssh → 3.38.240.110:22 (자기 EC2 public EIP)
         → ❌ AWS hairpin NAT 미지원 + SG 의 admin IP 제한
```

AWS VPC 는 **EC2 가 자기 자신의 퍼블릭 IP 로 접속하는 경우 경로를 자동으로 열어주지 않음**. 덕택에 동일 머신인데도 연결 타임아웃.

### 해결
Runner 컨테이너가 호스트 EC2 에 접근할 때 **Docker bridge 게이트웨이** `172.17.0.1` 사용. 이 주소는:
- Linux 에서 Docker 설치 시 자동 생성되는 `docker0` 인터페이스
- 컨테이너 입장에서 "지금 올라타 있는 호스트"
- 트래픽이 물리적으로 EC2 밖으로 **나가지 않음** → VPC/SG/NAT 전부 우회
- 보안상도 localhost 수준

### 검증 스니펫
```bash
docker run --rm alpine:3.20 sh -c '
  apk add --no-cache openssh-client >/dev/null
  ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 -o BatchMode=yes \
    ubuntu@172.17.0.1 exit 2>&1 | tail -3
'
# 기대: "Permission denied (publickey)" = TCP/SSH 핸드셰이크 성공
```

### GitLab Variables 변경
- `EC2_HOST`: `3.38.240.110` → `172.17.0.1`
- `EC2_KNOWN_HOSTS`: 아래 재생성 방법 참조
- `EC2_USER`/`EC2_DEPLOY_PATH`/`SSH_PRIVATE_KEY` 는 변동 없음

## 3. EC2_KNOWN_HOSTS 재생성

`ssh-keyscan` 으로 `172.17.0.1` 의 호스트 키를 뽑아 GitLab Variable 에 붙여넣기.

```bash
ssh -i C:/PROJECT/DOTSHELF/server/f206_jihocompany.pem ubuntu@3.38.240.110 \
  "ssh-keyscan -t ed25519,rsa,ecdsa 172.17.0.1 2>/dev/null"
```

결과 3줄을 **GitLab Variables 의 `EC2_KNOWN_HOSTS` 값으로 그대로 붙여넣기**. 주의:
- Type: **Variable** (File 아님)
- Visibility: **Visible** (Masked 는 multiline 안 됨)
- Protect: **ON**

### 이후 다시 만났던 에러
파이프라인이 `Host key verification failed.` 를 뱉음. 원인 추정: Variable 에 붙여넣을 때 줄바꿈이 공백으로 변환되었거나, Masked 옵션이 켜져 있어 값이 잘림. **Masked 해제 + 3줄 정확히 재붙여넣기** 로 해결.

## 4. CI 에서 echo → printf 로 교체

`.gitlab-ci.yml` 의 before_script:
```diff
-    - echo "$SSH_PRIVATE_KEY" | tr -d '\r' > ~/.ssh/id_ed25519
-    - echo "$EC2_KNOWN_HOSTS" > ~/.ssh/known_hosts
+    - printf '%s\n' "$SSH_PRIVATE_KEY" | tr -d '\r' > ~/.ssh/id_ed25519
+    - printf '%s\n' "$EC2_KNOWN_HOSTS" > ~/.ssh/known_hosts
```

이유:
- alpine `echo` 는 BusyBox 구현. `-n`·`-e` 옵션 처리 + 백슬래시 해석 구현 차이로 multiline 변수 원형이 깨질 수 있음
- `printf '%s\n'` 은 POSIX 표준. 변수 값을 있는 그대로 + 끝에 개행 하나만 붙여줌
- SSH 키·known_hosts 처럼 **여러 줄 원형 유지가 중요한 값** 에 안전

## 5. EC2_USER 오타 해결

다음 실행에서 `Permission denied (publickey)`. 로그 상세:
```
Deploying ... to EC2_USER@172.17.0.1:/opt/dotshelf
```
→ `$EC2_USER` 가 치환된 결과 **문자열 `EC2_USER`** 임. Variables 에서 key 이름 그대로 value 에 입력한 실수.

해결: `EC2_USER` 값을 `ubuntu` 로 수정 후 retry → 성공.

## 6. JWT 환경변수 주입 (배포 준비)

`feat/be/refactoring` 브랜치가 `JwtProperties` 를 통해 `jwt.secret` 을 강제한다 (32바이트 미만이면 기동 실패). MR 머지 전에 EC2 `.env` + `docker-compose.yml` 을 먼저 세팅.

### 시크릿 생성

```bash
openssl rand -base64 48 | tr -d '\n'
# 결과: 64자 base64 문자열 (48 바이트)
```

### `.env` 에 추가

```bash
# EC2 /opt/dotshelf/.env 끝에 다음 3줄 append
JWT_SECRET=<방금 생성한 64자>
JWT_ACCESS_TTL=1800       # access token 30분
JWT_REFRESH_TTL=2592000   # refresh token 30일
```

### `docker-compose.yml` api 서비스 `environment:` 블록에 치환 라인 3개 추가

```yaml
services:
  api:
    environment:
      SPRING_PROFILES_ACTIVE: ${SPRING_PROFILES_ACTIVE}
      DB_HOST: ${DB_HOST}
      # ... (기존)
      REDIS_PORT: ${REDIS_PORT}
      JWT_SECRET: ${JWT_SECRET}          # ← 추가
      JWT_ACCESS_TTL: ${JWT_ACCESS_TTL}  # ← 추가
      JWT_REFRESH_TTL: ${JWT_REFRESH_TTL} # ← 추가
      TZ: Asia/Seoul
```

### 검증

```bash
cd /opt/dotshelf
sudo docker compose config 2>&1 | grep JWT_
# 기대: JWT_SECRET, JWT_ACCESS_TTL, JWT_REFRESH_TTL 세 줄 모두 값이 찍혀야 함
```

### 재기동은 아직 안 함
- 현재 돌고 있는 api 컨테이너는 **JWT 를 쓰지 않는 옛 코드**
- JWT_SECRET 이 env 에만 존재해도 지금 이미지는 무시 → 안전
- MR 머지 순간 CI 가 새 이미지로 재빌드·재기동하면서 비로소 env 를 소비

## 7. 시크릿 로테이션 절차 (미래의 본인에게)

```bash
# 1) 새 값 생성
NEW=$(openssl rand -base64 48 | tr -d '\n')

# 2) .env 교체
ssh ubuntu@3.38.240.110
cd /opt/dotshelf
sudo sed -i "s/^JWT_SECRET=.*$/JWT_SECRET=$NEW/" .env

# 3) api 만 재기동 (nginx/DB 는 영향 없음)
sudo docker compose up -d api

# 4) 기존 발급된 JWT 들은 전부 무효화됨 (서명 불일치)
#    → 클라이언트는 전부 재로그인 필요. 중요한 로테이션은 공지 후 야간 진행 권장
```

## 현재 GitLab Variables 상태 (참고)

| Key | 값 요약 | Protected |
|-----|---------|:---:|
| `SSH_PRIVATE_KEY` | CI 전용 ed25519 비공개키 (multiline) | ✅ |
| `EC2_KNOWN_HOSTS` | `172.17.0.1` 의 ed25519/rsa/ecdsa 지문 3줄 | ✅ |
| `EC2_HOST` | `172.17.0.1` | ✅ |
| `EC2_USER` | `ubuntu` | ✅ |
| `EC2_DEPLOY_PATH` | `/opt/dotshelf` | ✅ |

전 변수 Masked **OFF** (multiline 때문에 어차피 불가), Protected **ON**.

## 배운 것 요약

- "Runner 가 없네" 와 "Runner 가 잡을 안 받네" 는 다른 이야기. "stuck" 태그가 결정적 단서
- AWS 에서 EC2 → 자기 EIP 접속은 기본적으로 막힘. 같은 머신이라면 private IP 또는 docker bridge 로
- GitLab Variable 의 multiline 저장은 Masked 옵션과 상극. 키·known_hosts 는 무조건 Masked OFF
- `echo` vs `printf` 의 차이는 shell 구현체에 따라 드러난다. 키 같은 값은 `printf '%s\n'` 이 안전
- 시크릿은 애플리케이션 기동 전에 먼저 환경에 준비해두고, 배포가 그 뒤를 따라가는 순서

## 변경 이력

- 2026-04-21 · 최초 작성 (Runner 자립 + EC2_HOST 전환 + JWT env 세팅)
