# 2026-04-24 · 모니터링 스택 도입 (Prometheus + Loki + Grafana + Promtail)

## TL;DR

- **목적**: Blue-Green 배포 중 **색별 RPS/지연/에러/JVM heap** + 로그를 실시간 관측. Grafana 한 화면에서 `color=blue/green` 라벨로 분리.
- **배치**:
  - prod 호스트 (100.86.247.88) — Prometheus + Loki + Grafana
  - SSAFY 앱 호스트 (100.74.2.103) — Promtail (blue/green 컨테이너 로그 수집)
- **외부 노출**: `https://grafana.dotshelf.digital` (Nginx reverse proxy → docker bridge 172.17.0.1:3000). Prometheus/Loki 는 외부 비노출.
- **보안**: 공개 Nginx 에서 `/actuator` 는 404 차단 → `/actuator/prometheus` 는 Tailscale 내부 스크레이프만 허용.
- **핵심 쿼리**: `sum(rate(http_server_requests_seconds_count{job="dotshelf-api"}[1m])) by (color)` — Blue-Green 전환이 그래프로 가시화.

## 1. 구조

```
┌────────────────────────── SSAFY 앱 호스트 (100.74.2.103) ───────────────────────┐
│                                                                                 │
│   dotshelf-api-blue   :8080  ──────────────┐                                    │
│   dotshelf-api-green  :8081  ──────────────┤  /actuator/prometheus              │
│                                            │  (Tailscale 내부 노출만)           │
│   dotshelf-promtail ────► /var/lib/docker/containers/*-json.log                 │
│                                            │                                    │
└────────────────────────────────────────────┼────────────────────────────────────┘
                                             │ Tailscale
                                             │
┌──────────────────────── prod 호스트 (100.86.247.88) ────────────────────────────┐
│                                            ▼                                    │
│   dotshelf-prometheus  :9090 (internal) ──► scrape blue + green                 │
│                                                                                 │
│   dotshelf-loki        :3100 ◄──────────────── Promtail push (Tailscale)        │
│                                                                                 │
│   dotshelf-grafana     172.17.0.1:3000 ◄─── dotshelf-nginx ◄── HTTPS 443        │
│                         (docker bridge 바인딩)      │                           │
│                                                     └─ grafana.dotshelf.digital │
│                                                                                 │
│   dotshelf-nginx       api.dotshelf.digital   → dotshelf_api (blue|green)       │
│                        grafana.dotshelf.digital → dotshelf_grafana              │
│                        /actuator → 404 (보안)                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## 2. 새 파일 / 수정 파일

### 2-1. 새 파일

| 경로 | 역할 |
|---|---|
| [deploy/prod/monitoring/docker-compose.yml](../../deploy/prod/monitoring/docker-compose.yml) | prod 호스트 모니터링 스택 (Prometheus + Loki + Grafana) |
| [deploy/prod/monitoring/prometheus/prometheus.yml](../../deploy/prod/monitoring/prometheus/prometheus.yml) | Prometheus scrape config (blue/green 각각 `color` 라벨) |
| [deploy/prod/monitoring/loki/loki-config.yml](../../deploy/prod/monitoring/loki/loki-config.yml) | Loki single-binary 설정 (retention 7d) |
| [deploy/prod/monitoring/grafana/provisioning/datasources/datasources.yml](../../deploy/prod/monitoring/grafana/provisioning/datasources/datasources.yml) | Grafana datasource (Prometheus + Loki) |
| [deploy/prod/monitoring/grafana/provisioning/dashboards/dashboards.yml](../../deploy/prod/monitoring/grafana/provisioning/dashboards/dashboards.yml) | 대시보드 provider 설정 |
| [deploy/prod/monitoring/grafana/dashboards/dotshelf-bluegreen.json](../../deploy/prod/monitoring/grafana/dashboards/dotshelf-bluegreen.json) | DotShelf 전용 Blue-Green 대시보드 (9 패널) |
| [deploy/prod/monitoring/.env.example](../../deploy/prod/monitoring/.env.example) | Grafana 관리자 계정 변수 예시 |
| [deploy/app/promtail/promtail-config.yml](../../deploy/app/promtail/promtail-config.yml) | SSAFY 호스트 Promtail 설정 (blue/green 컨테이너만 수집, color 라벨 추출) |

### 2-2. 수정 파일

| 경로 | 변경 |
|---|---|
| [backend/build.gradle.kts](../../build.gradle.kts) | `io.micrometer:micrometer-registry-prometheus` runtimeOnly 추가 |
| [backend/src/main/resources/application-prod.yml](../../src/main/resources/application-prod.yml) | actuator exposure 에 `prometheus,info` 추가, `metrics.tags.color=${DEPLOY_COLOR}` 공통 태그, p95/p99 히스토그램 설정 |
| [backend/src/main/resources/application-local.yml](../../src/main/resources/application-local.yml) | 로컬에서도 `/actuator/prometheus` 접근 가능하도록 exposure 확장 |
| [backend/deploy/app/docker-compose.yml](../../deploy/app/docker-compose.yml) | `promtail` 서비스 추가 (docker.sock + containers 로그 마운트) |
| [backend/deploy/prod/nginx/conf.d/default.conf](../../deploy/prod/nginx/conf.d/default.conf) | (1) `api.dotshelf.digital/actuator → 404` 차단, (2) `grafana.dotshelf.digital` placeholder 를 실제 Grafana reverse proxy 로 교체, (3) WebSocket upgrade map 추가 |
| [.gitlab-ci.yml](../../../.gitlab-ci.yml) | `deploy/app/promtail/` 를 SSAFY 호스트로 rsync 하는 단계 추가 |

## 3. 최초 배포 절차 (prod 호스트 수동 1회)

CI 는 `deploy/prod/monitoring/` 을 자동 rsync 하지 않는다 (인프라 성격 · 드물게 변경). 수동 1회 세팅 후 유지보수.

### 3-1. 모니터링 파일 업로드

로컬에서 prod 호스트로:

```bash
# rsync 로 전체 트리 업로드 (실행 전 dry-run 권장: --dry-run 추가)
rsync -az --delete backend/deploy/prod/monitoring/ dotshelf:/opt/dotshelf/monitoring/
```

### 3-2. `.env` 생성

```bash
ssh dotshelf "cp /opt/dotshelf/monitoring/.env.example /opt/dotshelf/monitoring/.env"
ssh dotshelf "chmod 600 /opt/dotshelf/monitoring/.env"
# 에디터로 GRAFANA_ADMIN_PASSWORD 변경
ssh dotshelf "vi /opt/dotshelf/monitoring/.env"
```

### 3-3. 스택 기동

```bash
ssh dotshelf "cd /opt/dotshelf/monitoring && docker compose up -d"
ssh dotshelf "docker compose -f /opt/dotshelf/monitoring/docker-compose.yml ps"
```

예상 출력:
```
NAME                    STATUS                    PORTS
dotshelf-prometheus     Up (healthy)              9090/tcp
dotshelf-loki           Up (healthy)              100.86.247.88:3100->3100/tcp
dotshelf-grafana        Up (healthy)              172.17.0.1:3000->3000/tcp
```

### 3-4. Nginx 재시작 (새 grafana server block + WebSocket map 적용)

`.gitlab-ci.yml` 이 이미 `deploy/prod/nginx/conf.d/default.conf` 를 rsync 하므로 **다음 배포 시 자동 반영**. 즉시 적용하려면:

```bash
ssh dotshelf "docker exec dotshelf-nginx nginx -t && docker exec dotshelf-nginx nginx -s reload"
```

### 3-5. DNS / TLS

`grafana.dotshelf.digital` 이 Cloudflare (orange-cloud) 로 prod 호스트 public IP 에 연결돼 있어야 한다.  
TLS 는 기존 `origin.pem` / `origin.key` (Cloudflare Origin Cert) 공유 사용 — 별도 발급 불필요.

> Cloudflare DNS 에 `grafana` A 레코드 없다면 추가: `grafana → 3.38.240.110 (proxied)`.

## 4. 검증

### 4-1. Prometheus 가 blue/green 을 잘 긁는지

```bash
ssh dotshelf "curl -fsS http://localhost:9090/api/v1/targets" | jq '.data.activeTargets[] | {job: .labels.job, color: .labels.color, health: .health}'
```

기대 출력 (양쪽 `healthy`):
```json
{"job":"dotshelf-api","color":"blue","health":"up"}
{"job":"dotshelf-api","color":"green","health":"up"}
```

(배포 직전/직후엔 한쪽이 `down` 일 수 있음 — 이게 정상.)

### 4-2. Spring Boot 메트릭 엔드포인트 직접 확인

```bash
# SSAFY 호스트에서 직접
ssh dotshelf-app "curl -fsS http://localhost:8080/actuator/prometheus | head -30"
```

기대: `# HELP jvm_memory_used_bytes ...` 같은 prom 포맷 라인. `color="blue"` / `color="green"` 이 태그로 붙어있어야 함.

### 4-3. Loki 가 로그를 받고 있는지

```bash
ssh dotshelf "curl -fsS 'http://localhost:3100/loki/api/v1/labels'"
# labels 에 color, container_name, compose_service 가 보여야 정상
```

### 4-4. Grafana 접속

브라우저: https://grafana.dotshelf.digital

- 로그인: `.env` 의 `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD`
- 좌측 Dashboards → DotShelf → **"DotShelf · Blue-Green 관측"** 클릭
- 1시간 윈도우에서 Blue/Green 상태 · RPS · p95 · 5xx · JVM heap · 로그 panel 표시 확인

### 4-5. 공개 경로에서 `/actuator` 차단 확인

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://api.dotshelf.digital/actuator/prometheus
# 기대: 404
```

## 5. 배포 중 관측 시나리오

Blue-Green 배포를 돌리는 동안 Grafana 대시보드를 열어두면:

1. **"Blue 상태" / "Green 상태"** 패널 — cutover 순간 하나가 DOWN → UP 토글
2. **"색별 RPS"** 패널 — blue 라인이 꺾이는 순간 green 라인이 상승
3. **"색별 5xx 에러"** 패널 — 이상적으로 **평평한 0 라인** 이 유지되어야 성공
4. **"색별 JVM heap"** 패널 — 새 컨테이너가 기동하면서 heap 이 0 부터 정상 수준까지 상승

발표 시 이 대시보드를 **배포 직전에 캡처** 해서 "5xx=0 으로 무중단 증명" 스토리로 활용 가능.

## 6. 알려진 한계 / TODO

- **CI 자동화**: prod 호스트 `monitoring/` 는 수동 배포. 변경 빈도 낮지만 향후 별도 stage 로 자동화 가능.
- **Alert rules 없음**: Prometheus alertmanager 미구성. 지금은 대시보드 시각 확인만. 알림 필요 시 Grafana Alerting 으로 대체 가능.
- **Loki retention 7일**: 디스크 여유 있으면 14일로 확장. `loki-config.yml` 의 `retention_period: 168h` 수정.
- **Promtail positions 유실**: 컨테이너 재시작 시 중복 log ingestion 발생할 수 있음 — `promtail-positions` 볼륨으로 방지했지만 볼륨 삭제 시 reset.
- **grafana-data 백업 없음**: 대시보드 JSON 은 repo 에 있으므로 DataSource/유저만 날아감. 중요하면 주기적 `docker compose cp` 백업.
- **외부 `api.dotshelf.digital/actuator`** 차단은 했지만, 내부 Tailscale 경로(`100.74.2.103:8080/actuator/*`) 는 열려있음. Tailscale ACL 로 Prometheus 컨테이너만 접근하도록 제한하는 추가 강화 여지 있음.

## 7. 롤백

### 7-1. 모니터링만 내리기
```bash
ssh dotshelf "cd /opt/dotshelf/monitoring && docker compose down"
```
→ Grafana 404 가 되지만 **Blue-Green 운영은 영향 없음**.

### 7-2. Spring Boot 의 Prometheus 엔드포인트만 끄기
`application-prod.yml` 에서:
```yaml
management:
  endpoints:
    web:
      exposure:
        include: health  # prometheus, info 제거
```
→ 재배포 필요. Prometheus 는 `up==0` 표시하고 메트릭 수집 중단.

### 7-3. Nginx grafana 서브도메인만 내리기
`deploy/prod/nginx/conf.d/default.conf` 에서 `grafana.dotshelf.digital` server 블록을 제거 또는 `return 444;` 로 변경 후 nginx reload.

### 7-4. 전체 revert
이 MR 을 `git revert`. `.gitlab-ci.yml` 변경이 포함되므로 CI 가 정상 동작. prod 호스트의 `/opt/dotshelf/monitoring/` 는 수동으로 `docker compose down && rm -rf` 필요.

## 8. 관련 문서

- [Blue-Green 배포 (선행 MR)](./2026-04-23-blue-green.md)
- [Tailscale App/DB 분리 (prod/app 분리 배경)](./2026-04-22-tailscale-app-db-split.md)
- [GitLab CI Variables](./gitlab-ci-variables.md)
