# 2026-04-23 · Blue-Green 무중단 배포 도입

## TL;DR

- **토폴로지**: 단일 `dotshelf-api` 컨테이너 → `dotshelf-api-blue`(8080) + `dotshelf-api-green`(8081) 2-color 공존. 항상 둘 중 하나만 트래픽 수신.
- **스위칭**: prod Nginx `upstream.active.conf` 한 파일을 `cp upstream.$TARGET.conf` 로 교체 후 `nginx -s reload`. 1~5초 내 graceful cutover.
- **오케스트레이션**: `.ci/deploy-blue-green.sh` 가 현재 활성 색 감지 → 반대 색 기동 → healthy 대기 → Nginx 스위칭 → 공개 smoke → 2분 유예 → 구 색 stop.
- **증거 수집**: `.ci/poll-health.sh` 가 30초간 1초 간격으로 공개 health 폴링. 5xx 발생 시 배포 로그에 경고.
- **관련 설계 문서**: [Blue-Green rationale](../study/2026-04-23-blue-green-rationale.md), [Flyway migration rules](../architecture/flyway-migration-rules.md)

## 1. 무엇이 바뀌었나

### 1-1. 새 파일
| 경로 | 역할 |
|---|---|
| [deploy/prod/nginx/conf.d/default.conf](../../deploy/prod/nginx/conf.d/default.conf) | prod Nginx 가상호스트 설정을 repo 편입. `upstream dotshelf_api` 는 별도 파일(nginx 자동 로드) |
| [deploy/prod/nginx/conf.d/templates/upstream.blue.conf](../../deploy/prod/nginx/conf.d/templates/upstream.blue.conf) | Blue 색 upstream 템플릿 (port 8080). 서브디렉토리라 nginx 가 자동 로드하지 않음 |
| [deploy/prod/nginx/conf.d/templates/upstream.green.conf](../../deploy/prod/nginx/conf.d/templates/upstream.green.conf) | Green 색 upstream 템플릿 (port 8081) |
| [deploy/prod/nginx/conf.d/README.md](../../deploy/prod/nginx/conf.d/README.md) | Nginx conf.d 운영 규칙 |
| [.ci/deploy-blue-green.sh](../../../.ci/deploy-blue-green.sh) | 배포 오케스트레이션 |
| [.ci/poll-health.sh](../../../.ci/poll-health.sh) | 공개 health 폴링 증거 수집 |

### 1-2. 수정된 파일
| 경로 | 변경 |
|---|---|
| [deploy/app/docker-compose.yml](../../deploy/app/docker-compose.yml) | `api` 단일 서비스 → `api-blue` + `api-green` 2 서비스. 각각 Tailscale IP 의 다른 포트, `mem_limit: 4g`, `DEPLOY_COLOR` env 주입 |
| [.gitlab-ci.yml](../../../.gitlab-ci.yml) | `backend:deploy` 스크립트가 `docker compose up --build api` 대신 Nginx config rsync → `deploy-blue-green.sh` → `poll-health.sh` 로 구성 |
| [docs/infra/gitlab-ci-variables.md](./gitlab-ci-variables.md) | 신규 변수 `PROD_HOST`, `PROD_USER`, `PROD_DEPLOY_PATH` 와 `EC2_KNOWN_HOSTS` 확장 지침 |

### 1-3. 삭제된 파일
| 경로 | 이유 |
|---|---|
| `.ci/wait_for_api_healthy.sh` | 로직이 `deploy-blue-green.sh` 내부로 흡수됨 |

## 2. 새 배포 흐름

```
GitLab CI (alpine:3.20 runner)
  │
  ├─ 1. build:bootJar → dotshelf-api-*-SNAPSHOT.jar (artifact)
  │
  ├─ 2. rsync jar + app compose → app 서버 /opt/dotshelf/
  ├─ 3. rsync nginx conf.d → prod 서버 /opt/dotshelf/nginx/conf.d/
  │       (upstream.active.conf, README.md 는 --exclude)
  │
  ├─ 4. deploy-blue-green.sh (SSH 로 app + prod 양쪽 제어)
  │     │
  │     ├─ ACTIVE = cat app:/opt/dotshelf/.active_color
  │     ├─ TARGET = opposite(ACTIVE)  (최초엔 bootstrap = blue)
  │     ├─ (bootstrap만) 기존 dotshelf-api 컨테이너 rm -f
  │     ├─ docker compose build api-$TARGET
  │     ├─ docker compose up -d api-$TARGET
  │     ├─ wait healthy (max 60s)
  │     ├─ 직격 smoke: curl 100.74.2.103:$TARGET_PORT/api/health
  │     ├─ prod: cp upstream.$TARGET.conf upstream.active.conf
  │     ├─ prod: docker exec dotshelf-nginx nginx -t
  │     ├─ prod: docker exec dotshelf-nginx nginx -s reload
  │     ├─ 공개 smoke: curl https://api.dotshelf.digital/api/health (최대 5회 재시도)
  │     ├─ app: echo $TARGET > /opt/dotshelf/.active_color
  │     ├─ sleep 120  (유예)
  │     └─ docker compose stop api-$ACTIVE
  │
  └─ 5. poll-health.sh → 30초 폴링, JSON 한 줄로 {"attempts":30,"ok":30,"fail":0,"codes":"200:30"} 형태 증거 출력
```

## 3. 실패 모드별 안전망

| 실패 지점 | 대응 |
|---|---|
| TARGET healthcheck timeout | 컨테이너 로그 80줄 덤프 → TARGET stop → CI FAIL. ACTIVE 그대로 서비스 지속 |
| 직격 smoke 실패 | TARGET stop → CI FAIL |
| `nginx -t` 실패 | `upstream.active.conf` 를 ACTIVE 값으로 원복 → TARGET stop → CI FAIL |
| 공개 smoke 실패 (스위칭 후) | CI FAIL + 경고 로그. **자동 롤백 없음** — 다음 배포를 반대 방향으로 돌려 수동 롤백 필요 |
| `stop api-$ACTIVE` 실패 | 경고만 (배포는 이미 성공 처리) |

## 4. 최초 배포 전 사전 작업

이 MR 머지 직후 자동 배포로 전환되기 전에 **반드시** 아래 사항을 확인/조치.

### 4-1. GitLab CI Variables 추가 (3개)
[gitlab-ci-variables.md §6~8](./gitlab-ci-variables.md) 참조.
- `PROD_HOST` = prod 서버 public IP
- `PROD_USER` = `ubuntu`
- `PROD_DEPLOY_PATH` = `/opt/dotshelf`

### 4-2. `EC2_KNOWN_HOSTS` 에 호스트 키 6줄 추가 (총 9줄)
prod 는 **docker bridge(`172.17.0.1`)** 로 접속하므로 bridge IP 용 호스트 키 3줄이 반드시 필요하다. 자세한 배경은 [gitlab-ci-variables.md §6](./gitlab-ci-variables.md#6-prod_host) 참조. 로컬에서:
```
# prod public (참고용 보관)
ssh-keyscan -t ed25519,rsa,ecdsa 3.38.240.110 2>/dev/null
# prod docker bridge (실제 사용)
ssh-keyscan -t ed25519,rsa,ecdsa 3.38.240.110 2>/dev/null | sed 's/^3\.38\.240\.110/172.17.0.1/'
```
→ 결과 6줄을 기존 app 3줄 뒤에 append. 총 9줄이 된다.

### 4-3. SSH 공개키를 prod `authorized_keys` 에 등록
```
ssh dotshelf "cat >> ~/.ssh/authorized_keys" < ~/.ssh/gitlab-ci-dotshelf-deploy.pub
```

### 4-4. prod 에 Nginx 설정 초기 배치 (bootstrap 1회)
CI 가 rsync 하는 디렉토리 구조가 존재해야 함. 보통 첫 배포에서 자동 생성되지만 선제적으로:
```
ssh dotshelf "mkdir -p /opt/dotshelf/nginx/conf.d"
```

### 4-5. 현재 운영 중인 legacy `dotshelf-api` 는 건드리지 않기
`deploy-blue-green.sh` 의 bootstrap 분기(`BOOTSTRAP=true`)가 첫 배포 시 자동으로 `docker rm -f dotshelf-api` 를 수행. 사전에 수동으로 stop 할 필요 없음.

## 5. 검증 (머지 후 첫 배포에서 수행)

### 5-1. 양 색 컨테이너 존재 확인
```
ssh dotshelf-app "docker ps -a --filter name=dotshelf-api --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"
```
결과 예시:
```
NAMES                  STATUS              PORTS
dotshelf-api-blue      Up 30 seconds       100.74.2.103:8080->8080/tcp
dotshelf-api-green     Exited (143) 1min   (없음)
```

### 5-2. 현재 활성 색 확인
```
ssh dotshelf-app "cat /opt/dotshelf/.active_color"
ssh dotshelf      "grep -E '8080|8081' /opt/dotshelf/nginx/conf.d/upstream.active.conf"
```
두 값이 일치해야 정상.

### 5-3. 공개 health
```
curl https://api.dotshelf.digital/api/health
```
HTTP 200 + JSON `status: "UP"`.

### 5-4. 폴링 증거 확인
CI 로그 마지막 부분에 JSON 한 줄:
```
{"url":"https://api.dotshelf.digital/api/health","duration":30,"attempts":30,"ok":30,"fail":0,"codes":"200:30"}
```
`fail:0` 이면 배포 중 5xx 없었다는 뜻 → 본 도입의 **핵심 증거**.

## 6. 롤백 시나리오

### 6-1. 이 배포 직후 문제 발견
**다음 커밋을 dev 에 push** 하면 자동으로 반대 색으로 배포 → 롤백 효과.
- 주의: contract 마이그레이션이 포함된 경우 롤백 실패 가능 ([flyway-migration-rules.md](../architecture/flyway-migration-rules.md) 참조)

### 6-2. 수동 급 롤백 (CI 통하지 않고)
prod 서버에서:
```
cd /opt/dotshelf/nginx/conf.d
cp templates/upstream.blue.conf upstream.active.conf   # blue 로 되돌리려면
docker exec dotshelf-nginx nginx -t && docker exec dotshelf-nginx nginx -s reload
```
app 서버에서:
```
cd /opt/dotshelf
docker compose up -d api-blue   # 정지돼 있었다면 다시 띄움
echo blue > .active_color
```

### 6-3. Blue-Green 자체를 롤백하고 싶다면
이 MR 자체를 `git revert` → CI 가 기존 `wait_for_api_healthy.sh` 가 사라진 상태로 시도 → 배포 실패. **CI 도 같이 revert** 해야 함. 권장하지 않음.

## 7. 알려진 한계

- **contract 마이그레이션 + 롤백 조합은 지원 불가**. [flyway-migration-rules.md](../architecture/flyway-migration-rules.md) 규약을 어기면 blue-green 이 롤백 수단으로 작동하지 않음.
- **단일 호스트 제약**. app 서버가 죽으면 blue/green 둘 다 죽음. 무중단 배포는 **애플리케이션 배포 다운타임**만 제거하지 **호스트 장애**는 해결하지 않음.
- **배포 중 CD 잡 전체 시간 증가**. 유예 2분 sleep 이 CI 시간을 잡아먹음. 급할 때 수동 bypass 옵션 필요하면 추가 예정.
- **CI 러너 → prod 는 docker bridge(`172.17.0.1`) 경유**. 러너가 prod 호스트 내부에 있으므로 public IP(`3.38.240.110`) 로 접속하면 EC2 hairpin NAT 에 막힌다. 러너가 다른 호스트로 이사하면 이 값을 바꿔야 한다.

## 7-bis. 첫 배포에서 터진 두 가지 함정 (2026-04-23)

> 설계/코드는 맞다고 믿고 있어도 실전에서 한번은 꼭 깨진다는 걸 보여주는 사례. 둘 다 repo 에 **방지책** 이 들어갔다.

### 함정 1 — `PROD_HOST` 에 public IP 를 쓰면 hairpin NAT 로 타임아웃

- **증상**: `ssh: connect to host 3.38.240.110 port 22: Operation timed out` → exit code 255
- **근본 원인**: GitLab runner 가 **prod 호스트 내부 Docker 컨테이너** 에서 돈다. 그 컨테이너가 자신을 감싼 호스트의 Elastic IP (`3.38.240.110`) 로 돌아오려 하면 EC2 NAT Gateway 가 라우팅을 거부 (hairpin NAT 제한).
- **수정**: `PROD_HOST = 172.17.0.1` (docker bridge). 컨테이너 → bridge → 호스트 sshd(`0.0.0.0:22`) 경로로 해결.
- **방지책**: [gitlab-ci-variables.md §6](./gitlab-ci-variables.md#6-prod_host) 에 근거 박제 + 체크리스트에 "PROD_HOST 가 172.17.0.1 인지" 명시.

### 함정 2 — `upstream.active.conf` 를 명시 `include` 하면 이중 로드

- **증상**: `nginx: [emerg] duplicate upstream "dotshelf_api" in /etc/nginx/conf.d/upstream.active.conf:1`
- **근본 원인**: Nginx 기본 `nginx.conf` 가 `include /etc/nginx/conf.d/*.conf` 로 conf.d 의 모든 .conf 를 이미 로드한다. `default.conf` 에서 다시 `include upstream.active.conf` 를 쓰면 같은 파일이 두 번 파싱됨 → `upstream dotshelf_api` 가 두 번 선언됨 → 에러.
- 추가로 `upstream.blue.conf`, `upstream.green.conf` 를 conf.d 루트에 두면 그것들도 자동 로드돼서 같은 문제.
- **수정 2가지**:
  1. `default.conf` 에서 명시 `include upstream.active.conf` 라인 삭제 (auto-load 에 맡김)
  2. 블루/그린 템플릿을 **`templates/` 서브디렉토리** 로 이동 (서브디렉토리는 `conf.d/*.conf` 와일드카드에 안 걸림)
- **방지책**: [conf.d/README.md](../../deploy/prod/nginx/conf.d/README.md) 에 "include 절대 쓰지 말 것" 경고 박제. rsync 에 `--delete --exclude='upstream.active.conf' --exclude='README.md' --exclude='*.bak*'` 로 과거 오염 파일 자동 청소.

### 배운 것
- **"자동 로드되는 디렉토리" 는 명시 include 와 조합하면 위험**. nginx/apache/systemd 다 동일 패턴. drop-in 디렉토리의 스캔 규칙을 먼저 확인.
- **CI 러너 위치는 설계 변수다**. runner 가 target 호스트 안에 있으면 hairpin 이슈가 따라온다. 이걸 모르면 설계 시점엔 안 보이다가 실전에서 터진다.
- 첫 blue-green 배포는 **bootstrap 경로** 라 일반 경로와 다르다. 이 순간이 제일 깨지기 쉬우므로 수동 리허설을 먼저 하는 게 정답이었다.

## 8. 측정 결과 (2026-04-23 첫 성공 배포: `blue → green`)

### 핵심 증거
CI deploy job `#339277` 의 `poll-health.sh` 최종 출력:

```json
{"url":"https://api.dotshelf.digital/api/health","duration":30,"attempts":28,"ok":28,"fail":0,"codes":"200:28"}
```

**30초 폴링 윈도우 내 28 회 요청 전부 200, 5xx/timeout 0 건.**

### 지표 표

| 지표 | 목표 | 실측 | 판정 |
|---|---|---|---|
| 배포 중 공개 5xx 카운트 | 0 | **0** (28/28 200) | ✅ |
| TARGET healthy → Nginx reload | < 5 초 | ~1–2 초 | ✅ |
| 배포 전체 소요 (build 제외) | < 4분 (유예 2분 포함) | **195.6 초** | ✅ |
| 유예 제외 실질 cutover 시간 | - | ~75 초 | - |
| 색당 컨테이너 메모리 사용 | < 3 GiB | **471 MiB** (limit 4 GiB 의 11.5%) | ✅ |
| 호스트 RAM 점유 (색 공존 추정) | < 70% | ~6% (940 MiB / 15 GiB) | ✅ |
| 롤백 소요 (수동 리허설) | < 배포 전체 시간과 동일 | 아직 실시 안 함 | ⏳ |

자세한 타임라인/배운 것은 [rationale §9](../study/2026-04-23-blue-green-rationale.md#9-측정-결과-2026-04-23-첫-성공-배포-blue--green) 참조.

## 9. 관련 문서

- [Blue-Green 설계 rationale](../study/2026-04-23-blue-green-rationale.md)
- [Flyway 마이그레이션 작성 규칙](../architecture/flyway-migration-rules.md)
- [JVM 메모리 제한 + app compose repo 편입 (선행 MR)](./2026-04-23-jvm-mem-limit.md)
- [Tailscale App/DB 분리](./2026-04-22-tailscale-app-db-split.md)
- [GitLab CI Variables](./gitlab-ci-variables.md)
