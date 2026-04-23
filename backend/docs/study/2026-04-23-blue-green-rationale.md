# 2026-04-23 · Blue-Green 무중단 배포 도입 — 설계 & 회고

- **브랜치**: `feat/be/blue-green-deploy` (예정)
- **선행 작업**: [Tailscale App/DB 분리](../infra/2026-04-22-tailscale-app-db-split.md), [Flyway + Health 강화](../infra/2026-04-22-flyway-and-health.md)
- **관련 팀 규약**: [Flyway 마이그레이션 작성 규칙](../architecture/flyway-migration-rules.md)
- **기간**: 2026-04-23 ~ (진행 중)

---

## TL;DR

기존 CD(`.gitlab-ci.yml`)는 `docker compose up -d --build api` 로 **단일 컨테이너를 재기동**한다. 이 방식은 (a) 재기동 30~60초 동안 502 가 뜨고, (b) 새 jar 가 Flyway 실패로 뜨지 않으면 전체 서비스가 내려간 채 수동 복구가 필요하다.

해결 접근: **포트 기반 blue-green + Nginx upstream 스위칭**. 단일 VM 위에서 k8s 없이, Nginx include 파일 한 줄 교체로 트래픽을 전환한다. DB는 두 색이 공유하므로 **모든 DDL을 expand-contract 2-phase 로 강제**하는 팀 규약을 동시 도입한다.

현실적으로 DotShelf 의 트래픽은 거의 0이므로 **"무중단 자체가 목적"은 아니다**. 진짜 목적은:
1. `dev` 배포가 팀 개발 흐름을 끊지 않게 하는 **체감 편의**
2. Flyway 실패 시 이전 버전이 **살아있게 하는 안전망**
3. 단일 VM 제약 하에서 **최소 비용 무중단의 판단 근거**를 포트폴리오 자산으로 남기기

---

## 1. 왜 도입하는가 — 관찰한 문제

### 1-1. 현재 배포 시 실제로 벌어지는 일
[.gitlab-ci.yml](../../.gitlab-ci.yml) `backend:deploy` 가 실행하는 실제 순서:

```
1. jar rsync → /opt/dotshelf/api/build/libs/
2. docker compose up -d --build api         ← 여기서 기존 컨테이너가 stop 됨
3. wait_for_api_healthy.sh (최대 60초)       ← 이 기간 전체가 다운타임
4. curl https://api.dotshelf.digital/api/health
```

즉 **매 배포마다 최소 20~60초의 확정 다운타임**이 발생한다. 현재는 트래픽이 거의 없어 눈에 띄지 않지만, 체감 가능한 부작용 두 가지:

- `dev` 에 머지한 팀원이 즉시 Postman/Swagger 로 테스트 시 **502 Bad Gateway** 를 자주 봄 → "내 코드 버그인가 배포 중인가" 혼동
- Flyway 마이그레이션 실패 → 컨테이너가 부팅 자체를 거부 → **기존 정상 버전도 이미 stop** 된 상태 → 전면 장애

### 1-2. 왜 지금인가
- Flyway 가 이제 prod 에서 활성화됨 ([infra/2026-04-22-flyway-and-health.md](../infra/2026-04-22-flyway-and-health.md))
- 즉 **DB 스키마 변경이 본격화되기 직전**. 이 시점에 expand-contract 규약을 함께 도입해야 팀이 처음부터 올바른 버릇을 들일 수 있다.
- 스키마 V1 뿐인 지금이 규약 정착 비용이 가장 낮다.

---

## 2. 목적 재정의 — "다운타임 제거"가 아닌 "배포 안전성"

| 흔한 명분 | 이 프로젝트에서 유효? | 비고 |
|---|---|---|
| 사용자 UX (다운타임 없음) | ❌ 약함 | 트래픽 거의 0 |
| 업계 표준이라서 | ❌ | 설득력 없음 |
| **팀 개발 흐름 보호** | ✅ | `dev` CD 중 502 혼동 제거 |
| **Flyway 실패 안전망** | ✅ 강함 | 신버전 실패해도 구버전 생존 |
| **롤백 가능성 확보** | ✅ 강함 | 현재 롤백 경로 없음 |
| 포트폴리오 방어선 | ✅ | 단일 VM 제약 극복 스토리 |

→ 본 도입의 **실제 가치 중심축은 "배포 안전성 + 롤백 가능성"** 이다. 이는 수치로 증명 가능하다 (배포 윈도우 중 5xx 카운트, 롤백 소요 시간).

---

## 3. DotShelf 환경의 제약

| 제약 | 의미 |
|---|---|
| App 서버(SSAFY Lightsail) **단일 노드** | k8s / ASG / ALB 모두 비현실. 단일 VM 위에서 해결해야 함 |
| App 서버 **휘발성** (학기말 소멸) | 스크립트·설정 전부 `/opt/dotshelf/` + Git 이중화 필요 |
| RAM 15 GiB, CPU 4 core, Swap 0 | 두 색 공존 시 JVM 메모리 예산 고정 필요 (Swap 없음 = OOM 즉사) |
| prod Nginx 는 컨테이너, 설정은 bind mount | `nginx -s reload` 는 `docker exec` 로 호출해야 함 |
| Tailscale 사설망 | 색 구분은 **포트**로 (다른 IP 확보 불가) |
| Flyway 는 이미 prod 에서 돌고 있음 | 스키마 규약을 사후 도입해야 — 첫 번째 expand 시점 중요 |
| GitLab CI (self-hosted runner) | 기존 deploy job 을 대체, 신규 이미지 추가 없이 openssh-client 만으로 완결 |

---

## 4. 아키텍처 선택

### 4-1. 토폴로지
```
 Cloudflare
    │ HTTPS
    ▼
 prod:443 [dotshelf-nginx]
    │  upstream dotshelf_api { server 100.74.2.103:$ACTIVE_PORT; }
    │  (↑ upstream.active.conf 한 파일이 single switch)
    ▼
 [Tailscale]
    │
    ▼
 SSAFY app
    ├─ dotshelf-api-blue   100.74.2.103:8080
    └─ dotshelf-api-green  100.74.2.103:8081
```

**색 식별**: (포트 + 컨테이너명) 이중. 포트가 source of truth.

**왜 이 방식?**: prod Nginx 의 upstream 블록을 별도 파일로 추출한 뒤 그 한 파일만 교체하는 게 **가장 작은 변경 표면**. LB 도입, 이중 Nginx, 동적 upstream 모듈(`lua` / `upstream_zone`) 모두 자원 대비 이득 미미.

### 4-2. 스위칭 메커니즘
```nginx
# /opt/dotshelf/nginx/conf.d/default.conf  (상단)
include /etc/nginx/conf.d/upstream.active.conf;

# /opt/dotshelf/nginx/conf.d/upstream.active.conf  (1줄 짜리)
upstream dotshelf_api { server 100.74.2.103:8080; keepalive 32; }
```
배포 스크립트가 이 파일 내용을 교체 → `docker exec dotshelf-nginx nginx -t && nginx -s reload`.

Nginx reload 의 성질:
- 기존 worker 는 inflight 요청을 마치고 종료 (graceful)
- 새 worker 는 새 upstream 으로 바로 시작
- 따라서 **순간 502 없음**. 이게 핵심.

### 4-3. 상태 저장 (어느 색이 active 인가)
| 위치 | 값 |
|---|---|
| app `/opt/dotshelf/.active_color` | `blue` or `green` |
| prod `upstream.active.conf` 내 포트 | 8080(blue) / 8081(green) |
| **불일치 = 배포 중/실패 상태** | 마지막 단계에서 검증 |

### 4-4. 배포 오케스트레이션
```
ACTIVE  = cat /opt/dotshelf/.active_color
TARGET  = opposite(ACTIVE)
PORT    = 8080 if TARGET=blue else 8081

# === app 서버 ===
1. rsync jar
2. docker compose build api-$TARGET
3. docker compose up -d api-$TARGET         # Flyway 여기서 실행
4. wait healthy (max 60s)                    # 실패 시 TARGET stop, 구버전 유지
5. curl 100.74.2.103:$PORT/api/health        # 직격 smoke

# === prod 서버 ===
6. upstream.active.conf 를 TARGET 포트로 교체
7. docker exec dotshelf-nginx nginx -t
8. docker exec dotshelf-nginx nginx -s reload
9. curl https://api.dotshelf.digital/api/health

# === 유예 (2분) ===
10. echo $TARGET > /opt/dotshelf/.active_color
11. docker compose stop api-$ACTIVE          # 구 color stop (rm 은 다음 배포 때)
```

---

## 5. Flyway — expand-contract 규약

두 색이 같은 DB 에 붙는 1~2분 구간에 **구버전 코드가 새 스키마에서 돌 수 있어야** 한다. 이 제약이 모든 DDL 을 2-phase 로 강제한다.

| 변경 | 같은 PR? | 근거 |
|---|---|---|
| 컬럼 ADD (NULL 허용 or DEFAULT) | ✅ | 구버전 무시 가능 |
| 인덱스 CREATE | ✅ | |
| 테이블 ADD | ✅ | |
| 컬럼 DROP | ❌ | 구버전 SELECT/INSERT 실패 |
| NOT NULL 강화 | ❌ | 구버전 NULL INSERT 거부됨 |
| 컬럼 RENAME | ❌ | 구/신 버전이 서로 다른 이름 찾음 |
| 타입 변경 | ❌ | 락 + 호환 문제 |

**파일명 규칙**: `V{n}__expand_*.sql` / `V{n}__contract_*.sql`. 리뷰어가 위험도를 한눈에 인지.

**팀 공유**: [docs/architecture/flyway-migration-rules.md](../architecture/flyway-migration-rules.md) 에 예시 + 왜 안 되는지를 SQL 과 함께 박제. 이 문서는 **팀원용**, 본 문서는 **본인 회고용** 으로 분리.

**강제 수준**: 현재는 pre-push 경고만. CI 차단은 Flyway 파일이 많아진 뒤 재평가.

---

## 6. JVM 메모리 재배분 (blue-green 과 독립적인 선결 과제)

### 문제
현재 [Dockerfile:17](../../Dockerfile#L17):
```
ENV JAVA_TOOL_OPTIONS="... -XX:MaxRAMPercentage=60 ..."
```
컨테이너 메모리 제한이 없어 **호스트 15 GiB 의 60% = 9 GiB** 까지 heap 을 잡을 수 있다. 단일 색일 때도 과하고, 두 색 공존 시 **합계 18 GiB > 15 GiB → OOM** 가능.

### 대응
compose 에 색마다:
```yaml
api-blue:
  mem_limit: 4g
  environment:
    JAVA_TOOL_OPTIONS: "-XX:+UseContainerSupport -XX:MaxRAMPercentage=60 -Xms512m"
```
색당 최대 4 GiB → 합계 8 GiB. OS/Nginx/기타 여유 7 GiB.

### 왜 별개 MR 로 가는가
- blue-green 이 아니어도 현재 설정 자체가 위험 → 독립 가치
- 커밋 히스토리가 분리되어 "왜 JVM 옵션을 건드렸나" 설명이 깔끔
- 회귀 발생 시 범인 분리 용이

---

## 7. 트레이드오프 — 정직하게

| 얻는 것 | 잃는 것 |
|---|---|
| 배포 중 5xx 0건 | 메모리 점유 2배 (~4 GiB → ~8 GiB) |
| Flyway 실패 시 구버전 생존 | Flyway 규약이 팀 인지부담으로 존재 |
| 롤백이 "반대 방향 재배포" 로 단순화 | contract 마이그레이션이 이미 적용된 경우 롤백 불가 (여전히 존재) |
| 배포 스크립트가 자기-문서화 | 스크립트 복잡도 증가 (~50줄 → ~150줄) |
| 팀 개발 중 혼동 제거 | 실제 사용자 UX 개선은 측정 불가 (트래픽 없음) |

**정직한 인정**: 이 규모의 학생 프로젝트에서 **기술적 필요성만으로는 과잉**이다. 도입의 주 명분은 "배포 안전성 + 판단 근거 박제" 이지 "사용자 보호" 가 아니다. 면접에서 질문받으면 이대로 답하는 게 맞다.

---

## 8. 실패 모드와 대응

| 실패 지점 | 감지 | 대응 |
|---|---|---|
| TARGET 컨테이너 기동 실패 | `docker inspect` healthy 타임아웃 | 배포 스크립트 중단, ACTIVE 계속 서비스. TARGET stop. 알림(TBD) |
| Flyway 실패 | TARGET 부팅 실패 | 위 + 수동 스키마 상태 확인 (일부 DDL 이미 적용 가능) |
| `nginx -t` 실패 | 스크립트에서 감지 | `upstream.active.conf` 원복 후 reload 생략. 배포 실패로 보고 |
| 스위칭 후 TARGET 5xx | 공개 health 실패 or 모니터링 | `active_color` 를 ACTIVE 로 되돌리고 `upstream.active.conf` 재교체 + reload |
| active_color 와 upstream 불일치 | 배포 마지막 검증 단계 | 배포 실패로 마킹, 수동 개입 필요 알림 |
| 구버전 stop 중 TARGET 에서 OOM | host dmesg / 컨테이너 OOM-kill | mem_limit 재조정. 유예 기간 동안이라 ACTIVE 는 이미 down 상태 → 주의 |

---

## 9. 측정 결과 (2026-04-23 첫 성공 배포: `blue → green`)

### 9-1. 핵심 증거 — 배포 중 5xx 카운트
CI 파이프라인 deploy 잡(#339277) 의 `.ci/poll-health.sh` 마지막 JSON:

```json
{"url":"https://api.dotshelf.digital/api/health","duration":30,"attempts":28,"ok":28,"fail":0,"codes":"200:28"}
```

**30초 폴링 윈도우 내 28회 요청 전부 200. 5xx/4xx/timeout 0건.**

`attempts=28` 이 30 에 못 미친 건 `curl --max-time 2 + sleep 1` 조합의 산술 특성 (요청이 2초까지 걸리면 루프 1회가 3초) 으로 설계 내 오차. 핵심은 `fail=0`.

### 9-2. 주요 지표

| 지표 | 목표 | **실측** | 판정 |
|---|---|---|---|
| 배포 중 공개 health 5xx 카운트 | 0 건 | **0** (28/28 200) | ✅ |
| 배포 전체 소요 (build job 제외) | < 4분 | **195.6 초** | ✅ |
| 유예(sleep 120) 제외 실질 cutover 시간 | < 2분 | ~75 초 | ✅ |
| TARGET healthy → Nginx reload | < 5 초 | ~1–2 초 (direct smoke 포함) | ✅ |
| 색 당 JVM 메모리 (runtime) | < 3 GiB | **471 MiB** (컨테이너 limit 4 GiB 의 11.5%) | ✅ (한참 여유) |
| 색 공존 시 호스트 RAM 점유 추정 | < 70% | 2 × 471 = ~940 MiB / 15 GiB = **6%** | ✅ |
| 롤백 소요 (수동 리허설) | < 30 초 | 아직 실시 안 함 | ⏳ |

### 9-3. 타임라인 (CI 러너 로그 기준)

```
13:34:34  build job succeeded        (backend:build  339276, 49.5s)
13:35:30  deploy job started          (backend:deploy 339277)
13:38:46  deploy job succeeded        (195.6s 총 소요)
├─ ~30s   rsync + remote build
├─ ~15s   api-green up → healthy 대기
├─  ~2s   direct smoke
├─  ~1s   Nginx upstream 교체 + reload   ← 실제 cutover
├─  ~2s   공개 smoke OK
├─ 120s   유예 sleep
└─  ~3s   api-blue stop
14:39     poll-health.sh 30초 폴링: 28/28 ok, 0 fail
```

### 9-4. 첫 시도 실패 2회를 통한 배운 것 (포트폴리오 자산)

| 차수 | 증상 | 원인 | 수정 MR |
|---|---|---|---|
| 1차 | deploy job 2분 27초 타임아웃 (exit 255) | `PROD_HOST=3.38.240.110` (public IP) — CI 러너가 prod 호스트 안이라 hairpin NAT 로 자기 자신 못 돌아옴 | GitLab Variable 수정 (PROD_HOST → `172.17.0.1`) + known_hosts 9줄 |
| 2차 | `nginx -t` 실패: `duplicate upstream` | `nginx.conf` 가 `conf.d/*.conf` 를 자동 로드하는데 `default.conf` 가 `upstream.active.conf` 를 다시 명시 include → 2중 로드. 더불어 `upstream.{blue,green}.conf` 도 conf.d 루트에 있어 자동 로드됨 | [`fix/be/blue-green-nginx-layout`](../infra/2026-04-23-blue-green.md#7-bis-첫-배포에서-터진-두-가지-함정-2026-04-23) — 명시 include 제거 + 템플릿을 `templates/` 서브디렉토리로 이동 + rsync `--delete` |

이 두 함정을 [docs/infra/2026-04-23-blue-green.md §7-bis](../infra/2026-04-23-blue-green.md#7-bis-첫-배포에서-터진-두-가지-함정-2026-04-23) 와 [docs/infra/gitlab-ci-variables.md §6](../infra/gitlab-ci-variables.md#6-prod_host) 에 근거·수정·방지책까지 박제. 같은 함정이 나중에 다른 인프라 변경에서도 반복될 여지가 있어 규약화.

### 9-5. 아직 안 한 것

- **롤백 리허설**: 의도적으로 green → blue 로 되돌리고 동일한 JSON 폴링 증거 수집. 30분 안에 가능.
- **동일 배포 2회 반복**: blue ↔ green 양방향 안정성 확인.

---

## 10. 대안으로 고려했지만 버린 것

| 대안 | 버린 이유 |
|---|---|
| **k8s + Argo Rollouts** | 단일 VM 에 k8s 를 올리는 것 자체가 리소스 낭비. 학습 목적으로는 매력적이지만 프로젝트 맥락 이탈 |
| **HAProxy 동적 backend** | Nginx 이미 컨테이너로 운영 중. 추가 컴포넌트 도입 대비 이득 없음 |
| **Nginx `upstream_zone` / `api` 모듈** (OSS Nginx 한정 기능 제한) | 정석 OSS 빌드에선 동적 upstream 수정 제한적. include 파일 교체가 더 단순 |
| **docker compose rolling (`replicas: 2` + 순차 재시작)** | compose 에서 roll 은 image 단위로만 무중단이 보장되지 않음. health probe/라우팅 제어 부재 |
| **단일 컨테이너 내 graceful shutdown 강화** | 이미 `server.shutdown: graceful` + `timeout-per-shutdown-phase: 20s` 적용 중이지만, **새 컨테이너 부팅 시간 동안은 그대로 단절**. 근본 해결 아님 |

---

## 11. 포트폴리오 방어선 — 예상 Q&A

**Q. 트래픽도 없는데 왜 무중단?**
A. 사용자 UX 가 주 명분이 아닙니다. (1) `dev` 배포가 팀 개발 중 혼동을 만들었고, (2) Flyway 실패 시 전체 다운이라는 **실제 장애 경로** 가 있었고, (3) 롤백 경로가 없었습니다. 그 셋을 해결하는 가장 경제적인 방법으로 blue-green 을 골랐습니다.

**Q. k8s 왜 안 썼어요?**
A. App 서버가 단일 VM 이고 학기 종료 후 소멸됩니다. k8s 자체가 이 환경에서 순손실이에요. "도구를 쓸 수 있느냐" 가 아니라 "이 제약 아래에서 최소 비용 무중단이 뭔가" 가 질문이었고, Nginx include + 2-color 로 답했습니다.

**Q. 롤백 실제로 해보셨어요?**
A. 네, dev 환경에서 blue → green 스위칭 후 의도적으로 green → blue 로 되돌리는 리허설 1회 수행. 측정: TBD 초. (구현 후 채움)

**Q. DB 스키마 변경은 어떻게 다뤄요?**
A. 두 색이 같은 DB 를 보는 1~2분이 있어, 모든 DDL 을 expand-contract 2-phase 로 강제했습니다. 팀 규약으로 `V{n}__expand_*.sql` / `contract_*.sql` 접두 + 리뷰 가중. 문서: [flyway-migration-rules.md](../architecture/flyway-migration-rules.md).

**Q. 실패하면?**
A. TARGET 컨테이너 부팅 실패·health 실패·`nginx -t` 실패 각각의 대응이 스크립트에 있고, 어떤 실패든 **ACTIVE 가 살아있는 상태로 배포가 중단**됩니다. 현재 체계와 가장 큰 차이는 이 "ACTIVE 생존 보장".

**Q. 과하지 않아요?**
A. 기술적 필요성만으로는 과잉 맞습니다. 다만 `dev` CD + Flyway 가 동시에 활성화된 지금이 expand-contract 규율을 팀에 정착시킬 수 있는 가장 저비용 시점이라, 이 둘을 함께 도입했습니다.

---

## 12. 구현 순서

1. [x] 설계 박제 (**이 문서**)
2. [x] [팀 DB 마이그레이션 규칙](../architecture/flyway-migration-rules.md) 문서
3. [x] JVM 메모리 정리 — 선행 독립 MR (`chore/be/app-compose-mem-limit`, push 완료, 머지 대기 중)
4. [x] Nginx `upstream.active.conf` 분리 (`deploy/prod/nginx/conf.d/`)
5. [x] compose 에 `api-blue` / `api-green` 2 서비스 정의
6. [x] `deploy-blue-green.sh` 스크립트 (app/prod 2파트)
7. [x] `.gitlab-ci.yml` deploy 단계 교체
8. [x] 관찰 폴링 스크립트 (`poll-health.sh`)
9. [ ] 롤백 리허설 + 측정 (**post-merge, dev 복구 후**)
10. [ ] 본 문서 §9 수치 채움 (**post-merge**)

### Blockers
- **Merge 순서**: `feat/be/social_login` 핫픽스 → JVM MR 머지 → blue-green MR 머지 순으로 진행. social_login 의 `RestClient.Builder` 빈 누락 이슈로 현재 dev 가 502 상태라 MR 머지 대기 중.
- **CI 변수 추가 필요**: `PROD_HOST`, `PROD_USER`, `PROD_DEPLOY_PATH` + `EC2_KNOWN_HOSTS` 확장. [gitlab-ci-variables.md](../infra/gitlab-ci-variables.md#blue-green-도입으로-추가된-변수-3개-2026-04-23) 지침대로 사용자가 GitLab 콘솔에서 추가.
- **prod 서버 `authorized_keys`**: CI SSH 공개키를 prod `ubuntu` 계정에 추가 필요.

---

## 13. 참고

- [배포 파이프라인 초기 구축](./2026-04-20-chore-be-deploy-infra.md)
- [App/DB 분리 + Tailscale](../infra/2026-04-22-tailscale-app-db-split.md)
- [Flyway + Health 강화](../infra/2026-04-22-flyway-and-health.md)
- [아키텍처 컨벤션](../architecture/patterns.md)
