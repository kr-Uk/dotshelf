# prod Nginx conf.d

prod 서버(100.86.247.88) 의 `/etc/nginx/conf.d/` (호스트 `/opt/dotshelf/nginx/conf.d/` bind mount) 영속 소스.
CI 가 매 배포마다 이 디렉토리 트리를 prod 로 rsync 한다.

## 파일 역할

| 파일/경로 | 역할 | nginx 자동 로드? | git 체크인? |
|---|---|---|---|
| `default.conf` | 가상호스트 + Cloudflare real_ip | ✅ (`conf.d/*.conf`) | ✅ |
| `upstream.active.conf` | **runtime**. 현재 활성 색의 upstream 사본. | ✅ (`conf.d/*.conf`) | ❌ |
| `templates/upstream.blue.conf` | Blue 템플릿 (port 8080) | ❌ (서브디렉토리는 스캔 안 됨) | ✅ |
| `templates/upstream.green.conf` | Green 템플릿 (port 8081) | ❌ | ✅ |

## 루트 도메인 라우팅

`dotshelf.digital` / `www.dotshelf.digital` 은 2026-04-29 부터 정적 파일 서빙을 중단하고
SSAFY app 서버의 `service-web` Next.js 컨테이너로 프록시한다(`upstream dotshelf_service` → `100.74.2.103:3002`).
배포는 GitLab CI 의 `service-web:deploy` job — 소스 rsync + `docker compose up -d --build service-web` + nginx reload 를 한 파이프라인에서 처리.
이전 게임 hub(`/usr/share/nginx/html/root/`) 는 더 이상 사용하지 않는다.

## 왜 templates/ 서브디렉토리인가

`nginx.conf` 의 `include /etc/nginx/conf.d/*.conf` 는 **디렉토리 바로 아래 `.conf` 파일만** 스캔한다. 서브디렉토리는 스캔하지 않는다. 만약 `upstream.blue.conf` 와 `upstream.green.conf` 가 conf.d 루트에 있으면 `upstream.active.conf` 와 함께 **같은 이름의 upstream 을 여러 번 선언** 해서 `duplicate upstream` 에러가 난다. 따라서 템플릿은 반드시 서브디렉토리에 둔다.

## `upstream.active.conf` 관리

- 배포 스크립트 `.ci/deploy-blue-green.sh` 가 `cp templates/upstream.$TARGET.conf upstream.active.conf` 후 `docker exec dotshelf-nginx nginx -s reload`
- 초기 bootstrap: 스크립트가 파일 없으면 blue 템플릿으로 자동 생성
- CI rsync 시 `--exclude=upstream.active.conf` 로 덮어쓰기 방지

## 수정 시 주의

- **`default.conf` 에 `include upstream.active.conf` 쓰지 말 것.** nginx 가 `conf.d/*.conf` 로 이미 로드하므로 명시 include 는 중복 선언 에러를 유발한다. 이 함정은 2026-04-23 에 실전에서 한 번 터졌다.
- 수정 후 가능하면 로컬에서 syntax 검증:
  ```bash
  # 먼저 active 파일을 임시로 만들고
  cp backend/deploy/prod/nginx/conf.d/templates/upstream.blue.conf \
     backend/deploy/prod/nginx/conf.d/upstream.active.conf
  # 그 다음 docker 로 nginx -t
  docker run --rm \
    -v $PWD/backend/deploy/prod/nginx/conf.d:/etc/nginx/conf.d:ro \
    nginx:1.28-alpine nginx -t
  # 검증 끝나면 임시 파일은 커밋하지 말고 삭제
  rm backend/deploy/prod/nginx/conf.d/upstream.active.conf
  ```
- 실배포 검증은 `.ci/deploy-blue-green.sh` 가 `docker exec dotshelf-nginx nginx -t` 수행 후에만 `nginx -s reload` 한다.
