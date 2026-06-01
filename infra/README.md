# infra/ — DotShelf 인프라 (서버 단위)

서버 단위로 IaC 를 분리한다. 기존 `backend/deploy/` 를 역할이 아닌 **호스트 기준**으로 재구성한 것.

## 구조

```
infra/
├── app/        # 앱/컴퓨트 티어  (현재 SSAFY 호스트, 휘발성 → jihocompany 통합 예정)
│   ├── docker-compose.yml         # api-blue/green, service-web, admin-web, promtail
│   ├── docker-compose.test.yml    # api-test, service-web-test (dev 환경)
│   ├── promtail/promtail-config.yml
│   └── .env.test.example
└── db/         # 데이터/인프라 티어 (jihocompany / dotshelf-prod 호스트)
    ├── docker-compose.yml         # postgres, redis, nginx (코어)
    ├── nginx/conf.d/...           # 공개 진입점 vhost / upstream 템플릿
    ├── monitoring/...             # prometheus, loki, grafana
    └── scripts/backup-pg.sh       # pg 자동 백업 (14d 보존, cron)
```

## 배포 방식

| 대상 | 자동/수동 | 트리거 |
|---|---|---|
| `app/` (api·web) | **CI 자동** | `.gitlab-ci.yml` → 호스트로 rsync 후 `docker compose up` (api 는 blue-green) |
| `db/nginx/conf.d` | **CI 자동** | nginx config 변경 시 rsync + reload |
| `db/docker-compose.yml` (코어) | **수동** | postgres/redis/nginx 본체. 변경 빈도 낮음, infra 성격 |
| `db/monitoring/` | **수동** | `docs/infra/2026-04-24-monitoring-stack.md` 참조 |
| `db/scripts/backup-pg.sh` | **수동(cron)** | jihocompany 호스트 cron 등록 |

> `.env` 류(비밀번호·키)는 git 밖. 각 디렉터리의 `*.example` 만 버전 관리.

## 모노서버 이전 메모

SSAFY 호스트 회수 예정 → `app/` 컨테이너를 jihocompany(현재 `db/` 호스트)로 통합 진행 중.
선결: 앱 이미지를 **arm64 로 재빌드**(jihocompany=aarch64). 자세한 제약/계획은 메모리
`dotshelf_server_migration` 및 단계별 작업 참고.
