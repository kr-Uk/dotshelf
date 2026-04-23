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
| [deploy/prod/nginx/conf.d/default.conf](../../deploy/prod/nginx/conf.d/default.conf) | prod Nginx 설정을 repo 편입. `upstream.active.conf` 를 `include` 하는 구조로 변경 |
| [deploy/prod/nginx/conf.d/upstream.blue.conf](../../deploy/prod/nginx/conf.d/upstream.blue.conf) | Blue 색 upstream (port 8080) |
| [deploy/prod/nginx/conf.d/upstream.green.conf](../../deploy/prod/nginx/conf.d/upstream.green.conf) | Green 색 upstream (port 8081) |
| [deploy/prod/nginx/conf.d/README.md](../../deploy/prod/nginx/conf.d/README.md) | Nginx conf.d 운영 규칙 |
| [.ci/deploy-blue-green.sh](../../../.ci/deploy-blue-green.sh) | 배포 오케스트레이션 (6506 bytes) |
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

### 4-2. `EC2_KNOWN_HOSTS` 에 prod 호스트 키 3줄 추가
로컬에서:
```
ssh-keyscan -t ed25519,rsa,ecdsa <prod-ec2-public-ip> 2>/dev/null
```
결과 3줄을 `EC2_KNOWN_HOSTS` 기존 값 뒤에 append.

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
cp upstream.blue.conf upstream.active.conf   # blue 로 되돌리려면
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
- **CI 러너 → prod 는 공용 IP 경유**. Tailscale 이 아니라서 IP 화이트리스트/방화벽 정책과 맞물림.

## 8. 측정 결과 (첫 배포 후 채움)

| 지표 | 목표 | 실측 |
|---|---|---|
| 배포 중 공개 5xx 카운트 | 0 | TBD |
| TARGET healthy → cutover 간격 | < 5s | TBD |
| 배포 전체 소요 (build 제외) | < 4분 (유예 2분 포함) | TBD |
| 색당 heap 사용 피크 | < 3 GiB | TBD |
| 롤백 소요 (다음 MR 유도 시) | < 배포 전체 시간과 동일 | TBD |

`.ci/poll-health.sh` 결과의 JSON 한 줄을 이 표에 박제 예정. [rationale §9](../study/2026-04-23-blue-green-rationale.md#9-측정-계획-구현-후-채움) 와 동기화.

## 9. 관련 문서

- [Blue-Green 설계 rationale](../study/2026-04-23-blue-green-rationale.md)
- [Flyway 마이그레이션 작성 규칙](../architecture/flyway-migration-rules.md)
- [JVM 메모리 제한 + app compose repo 편입 (선행 MR)](./2026-04-23-jvm-mem-limit.md)
- [Tailscale App/DB 분리](./2026-04-22-tailscale-app-db-split.md)
- [GitLab CI Variables](./gitlab-ci-variables.md)
