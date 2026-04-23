# prod Nginx conf.d

이 디렉토리는 **prod 서버(100.86.247.88) 의 `/opt/dotshelf/nginx/conf.d/` 의 영속 소스**다.
CI (`backend:deploy`) 가 매 배포마다 이 디렉토리의 파일들을 prod 서버로 rsync 한다.

## 파일 역할

| 파일 | 역할 | git 체크인? |
|---|---|---|
| `default.conf` | 가상호스트 정의 + Cloudflare real_ip + `include upstream.active.conf` | ✅ |
| `upstream.blue.conf` | Blue 색의 upstream 정의 (port 8080) | ✅ |
| `upstream.green.conf` | Green 색의 upstream 정의 (port 8081) | ✅ |
| `upstream.active.conf` | **runtime 상태**. 현재 활성 색의 upstream 복사본. | ❌ |

## upstream.active.conf 관리

- 배포 스크립트 `.ci/deploy-blue-green.sh` 가 `cp upstream.$TARGET.conf upstream.active.conf` 후 `nginx -s reload`
- 초기 bootstrap: 수동으로 `cp upstream.blue.conf upstream.active.conf` 한 번 실행 필요
- rsync 시 `--exclude=upstream.active.conf` 로 덮어쓰기 방지

## 수정 시 주의

- 이 파일들은 `server_name api.dotshelf.digital` 의 동작에 직결된다. 실수 시 API 전체 다운.
- 수정 후 반드시 로컬에서라도 `nginx -t` 검증 (도커로: `docker run --rm -v $PWD:/etc/nginx/conf.d:ro nginx:1.28-alpine nginx -t` — 단, include 파일 경로는 절대 경로라 완벽 재현 불가).
- 실 검증은 배포 파이프라인에서 스크립트가 `docker exec dotshelf-nginx nginx -t` 를 수행한 뒤에만 `nginx -s reload` 한다.
