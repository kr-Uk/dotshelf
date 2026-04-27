# 2026-04-23 · app 서버 compose repo 편입 + JVM 메모리 제한

## TL;DR

- App 서버(`/opt/dotshelf/docker-compose.yml`) 가 Git 바깥에서만 살아있던 상태를 **repo 편입** ([backend/deploy/app/docker-compose.yml](../../deploy/app/docker-compose.yml))
- `api` 서비스에 **`mem_limit: 4g`** 추가 — JVM heap 자동 산정(`MaxRAMPercentage=60`)이 호스트 15 GiB 기준으로 커지는 것을 컨테이너 메모리 기준으로 제한
- [`.gitlab-ci.yml`](../../../.gitlab-ci.yml) `backend:deploy` 가 매 배포마다 compose 파일도 함께 rsync 하도록 수정
- 이 변경은 **Blue-Green 도입의 선행 조건** ([2026-04-23-blue-green-rationale.md](../study/2026-04-23-blue-green-rationale.md) §6)

---

## 왜 했나

### 1. Compose 파일이 Git 밖이라 변경 이력이 남지 않았다
- 최초 서버 세팅 시 수동으로 `/opt/dotshelf/docker-compose.yml` 작성
- 이후 수정은 서버에서 직접 — `docker-compose.yml.bak.20260422-103056` 같은 흔적만 남음
- 리뷰 불가, 롤백 불가, 변경 근거 추적 불가

### 2. JVM heap 이 호스트 전체를 기준으로 산정되고 있었다
현 [Dockerfile:17](../../Dockerfile#L17) 설정:
```
ENV JAVA_TOOL_OPTIONS="-XX:+UseContainerSupport -XX:MaxRAMPercentage=60 ..."
```
`UseContainerSupport` + `MaxRAMPercentage=60` 은 **컨테이너 메모리 제한의 60%** 를 heap max 로 잡는다. 그런데 compose 에 `mem_limit` 이 없어 컨테이너 메모리 제한이 **호스트 전체(15 GiB)** 와 같은 취급 → heap 이 **최대 ~9 GiB** 까지 커질 수 있었다.

현 단일 컨테이너 구조에서도 과한 값이고, blue-green 공존 시 **2 × 9 = 18 GiB > 15 GiB → OOM** 가능.

### 3. Blue-Green 본 구현의 전제
Blue-Green 배포 시 CI 가 compose 에 정의된 `api-blue` / `api-green` 서비스를 대상으로 `docker compose up -d` 해야 한다. **compose 가 repo 에 있어야 리뷰 가능**하고, CI 가 rsync 로 동기화할 수 있다.

---

## 무엇을 했나

### 1. `backend/deploy/app/docker-compose.yml` 추가
서버 기존 내용을 그대로 옮긴 뒤 `mem_limit: 4g` 한 줄만 추가. 기능 동작은 동일.

```yaml
services:
  api:
    ...
    mem_limit: 4g    # ← 신규
    environment:
      ...
```

**왜 `4g` 인가**:
- Spring Boot API 한 인스턴스의 정상 heap 점유: [`free -h`] 기준 현재 502 MiB
- 4 GiB 는 **heap + metaspace + direct buffer + thread stack + native 를 다 합쳐도 2× 여유**
- 두 색 공존 시 합계 8 GiB → 호스트 15 GiB 중 OS/Nginx/etc 여유 7 GiB

### 2. `.gitlab-ci.yml` deploy 단계 수정
```diff
 script:
   - JAR=$(ls $JAR_ARTIFACT_PATH/*-SNAPSHOT.jar | head -1)
+  - COMPOSE=backend/deploy/app/docker-compose.yml
   - ssh ... "mkdir -p $EC2_DEPLOY_PATH/api/build/libs && rm -f ..."
   - rsync -az "$JAR" "...:$EC2_DEPLOY_PATH/api/build/libs/"
+  - rsync -az "$COMPOSE" "...:$EC2_DEPLOY_PATH/docker-compose.yml"
   - ssh ... "cd $EC2_DEPLOY_PATH && docker compose up -d --build api && docker image prune -f"
```

매 배포마다 compose 도 함께 갱신되어 **서버 파일과 repo 가 자동으로 동기화**된다.

---

## 검증

### 로컬 syntax check
```
$ docker compose -f deploy/app/docker-compose.yml config --quiet
# (환경변수 경고만, 에러 없음)
OK
```

### 배포 후 런타임 확인 (MR 머지 후)
```bash
# 메모리 제한 적용 확인
ssh dotshelf-app "docker inspect dotshelf-api --format '{{.HostConfig.Memory}}'"
# → 4294967296 (= 4 GiB)

# 실제 JVM heap max 확인
ssh dotshelf-app "docker exec dotshelf-api jcmd 1 VM.flags | grep MaxHeapSize"
# → 2.4g 근처 (= 4g × 60%)

# 컨테이너 메모리 실사용
ssh dotshelf-app "docker stats --no-stream dotshelf-api"
```

---

## 의도하지 않은 부작용 예상

| 경로 | 영향 |
|---|---|
| 첫 배포 시 서버 compose 가 덮어씌워짐 | 내용 동일 + `mem_limit` 만 추가 → 기능 변화 없음 |
| JVM heap 상한이 줄어듦 (9 GiB → 2.4 GiB) | 실제 사용량(~500 MiB) 의 약 5배 여유. 문제 없음 |
| 배포 중 기존 컨테이너 재시작 | 현 단일 컨테이너 구조상 기존과 동일 (~30초 다운타임, blue-green 이전) |

---

## 다음 단계

1. 이 MR 머지 → 첫 자동 배포에서 `mem_limit` 반영 확인
2. 이후 본 구현: [Blue-Green rationale §12](../study/2026-04-23-blue-green-rationale.md#12-구현-순서) 의 4번부터 (Nginx upstream 파일 분리)

---

## 관련 문서

- [Blue-Green 무중단 배포 도입 설계](../study/2026-04-23-blue-green-rationale.md)
- [Flyway 마이그레이션 작성 규칙](../architecture/flyway-migration-rules.md)
- [App/DB 분리 + Tailscale](./2026-04-22-tailscale-app-db-split.md)
