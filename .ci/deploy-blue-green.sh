#!/bin/sh
# Blue-Green 배포 오케스트레이션.
#
# 전제:
# - JAR 과 compose 가 이미 app 서버 $EC2_DEPLOY_PATH 로 rsync 되어 있음
# - Nginx 설정 (default.conf, upstream.*.conf) 이 이미 prod 서버 $PROD_DEPLOY_PATH/nginx/conf.d/ 로 rsync 되어 있음
# - SSH 키가 양쪽 서버 모두에 등록되어 있음 (EC2_KNOWN_HOSTS 에 prod/app 둘 다 포함)
#
# 상태 저장:
# - app:$EC2_DEPLOY_PATH/.active_color    — 현재 트래픽 받는 색 (blue|green)
# - prod:$PROD_DEPLOY_PATH/nginx/conf.d/upstream.active.conf — 현재 Nginx upstream (runtime 파일, git 밖)
#
# 실행 흐름:
#  1. ACTIVE 색 읽기 → TARGET = 반대 색 (최초 실행이면 bootstrap 모드로 blue 진입)
#  2. app: docker compose build + up api-$TARGET
#  3. app: TARGET 컨테이너 healthy 될 때까지 대기
#  4. app: 직격 smoke (Tailscale 바인딩 포트)
#  5. prod: upstream.active.conf 를 TARGET 으로 교체, nginx -t, nginx -s reload
#  6. 공개 smoke (https://api.dotshelf.digital/health)
#  7. .active_color 갱신
#  8. 유예 시간 후 구 ACTIVE 색 stop
#
# 어떤 단계든 실패하면:
#  - Nginx 스위칭 전 실패 → TARGET stop, ACTIVE 그대로 서비스 지속
#  - Nginx 스위칭 중 실패 → upstream.active.conf 원복 시도, TARGET stop
#  - 스위칭 후 실패 → 다음 배포를 반대 방향으로 돌려 롤백 가능
#
set -eu

: "${EC2_USER:?EC2_USER required}"
: "${EC2_HOST:?EC2_HOST required (app server Tailscale IP)}"
: "${EC2_DEPLOY_PATH:?EC2_DEPLOY_PATH required}"
: "${PROD_USER:?PROD_USER required}"
: "${PROD_HOST:?PROD_HOST required (prod server Tailscale IP)}"
: "${PROD_DEPLOY_PATH:?PROD_DEPLOY_PATH required}"

HEALTH_URL_PUBLIC=${HEALTH_URL_PUBLIC:-https://api.dotshelf.digital/health}
HEALTH_TIMEOUT_SECONDS=${HEALTH_TIMEOUT_SECONDS:-60}
GRACE_PERIOD_SECONDS=${GRACE_PERIOD_SECONDS:-120}

APP="$EC2_USER@$EC2_HOST"
PROD="$PROD_USER@$PROD_HOST"

ssh_app()  { ssh "$APP" "$@"; }
ssh_prod() { ssh "$PROD" "$@"; }

log()  { printf '[%s] %s\n' "$(date +%H:%M:%S)" "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; exit 1; }

now_secs() { date +%s; }

# ---------- 1. ACTIVE / TARGET 결정 ----------
log "Detecting active color..."
ACTIVE=$(ssh_app "cat $EC2_DEPLOY_PATH/.active_color 2>/dev/null || echo none")
BOOTSTRAP=false
case "$ACTIVE" in
  blue)  TARGET=green; TARGET_PORT=8081 ;;
  green) TARGET=blue;  TARGET_PORT=8080 ;;
  none)
    log "First blue-green deploy — bootstrapping as BLUE"
    TARGET=blue
    TARGET_PORT=8080
    BOOTSTRAP=true
    ;;
  *) fail "Invalid .active_color value: $ACTIVE" ;;
esac
log "ACTIVE=$ACTIVE  TARGET=$TARGET  TARGET_PORT=$TARGET_PORT  BOOTSTRAP=$BOOTSTRAP"

# ---------- 2. Bootstrap cleanup (최초 1회) ----------
if [ "$BOOTSTRAP" = "true" ]; then
  log "Bootstrap: removing legacy single-container dotshelf-api if present"
  ssh_app "docker rm -f dotshelf-api 2>/dev/null || true"
  log "Bootstrap: ensuring prod upstream.active.conf exists (default = blue)"
  ssh_prod "test -f $PROD_DEPLOY_PATH/nginx/conf.d/upstream.active.conf || cp $PROD_DEPLOY_PATH/nginx/conf.d/templates/upstream.blue.conf $PROD_DEPLOY_PATH/nginx/conf.d/upstream.active.conf"
fi

# ---------- 2-bis. Promtail (로그 수집기) 보장 ----------
# Promtail 은 api-blue/green 과 독립된 lifecycle 로 돌아야 한다 (로그는 배포와 무관하게 수집).
# --force-recreate 로 매 배포마다 재기동 → promtail-config.yml 변경사항 즉시 반영.
# --no-deps 는 연쇄로 다른 서비스까지 재시작 안 하게.
# positions 는 named volume(promtail-positions) 에 저장되므로 재기동해도 offset 유지 → 중복 적재 없음.
# 실패해도 배포는 진행 (모니터링 스택 장애가 앱 배포를 막지 않도록).
log "Ensuring Promtail is running (log shipper to Loki)"
ssh_app "cd $EC2_DEPLOY_PATH && docker compose up -d --force-recreate --no-deps promtail" || log "WARN: promtail 기동 실패 (배포는 계속 진행). 수동 확인 필요."

# ---------- 3. TARGET 컨테이너 빌드 + 기동 ----------
log "Building api-$TARGET"
ssh_app "cd $EC2_DEPLOY_PATH && docker compose build api-$TARGET"
log "Starting api-$TARGET"
ssh_app "cd $EC2_DEPLOY_PATH && docker compose up -d api-$TARGET"

# ---------- 4. Healthcheck 대기 ----------
log "Waiting for api-$TARGET healthy (timeout ${HEALTH_TIMEOUT_SECONDS}s)"
deadline=$(( $(now_secs) + HEALTH_TIMEOUT_SECONDS ))
status=starting
while [ "$(now_secs)" -lt "$deadline" ]; do
  status=$(ssh_app "docker inspect -f '{{.State.Health.Status}}' dotshelf-api-$TARGET 2>/dev/null || echo starting")
  log "  dotshelf-api-$TARGET: $status"
  if [ "$status" = "healthy" ]; then
    break
  fi
  sleep 5
done

if [ "$status" != "healthy" ]; then
  log "Target not healthy. Dumping recent logs:"
  ssh_app "docker compose -f $EC2_DEPLOY_PATH/docker-compose.yml logs --tail 80 api-$TARGET" >&2 || true
  log "Stopping failed target"
  ssh_app "docker compose -f $EC2_DEPLOY_PATH/docker-compose.yml stop api-$TARGET" || true
  fail "api-$TARGET healthcheck timeout"
fi

# ---------- 5. 직격 smoke (Nginx 우회) ----------
log "Direct smoke: http://100.74.2.103:$TARGET_PORT/health"
if ! ssh_app "curl -fsS --max-time 5 http://100.74.2.103:$TARGET_PORT/health" >/dev/null; then
  ssh_app "docker compose -f $EC2_DEPLOY_PATH/docker-compose.yml stop api-$TARGET" || true
  fail "direct smoke failed on $TARGET_PORT"
fi

# ---------- 6. Nginx upstream 스위칭 ----------
log "Switching Nginx upstream to $TARGET"
ssh_prod "cp $PROD_DEPLOY_PATH/nginx/conf.d/templates/upstream.$TARGET.conf $PROD_DEPLOY_PATH/nginx/conf.d/upstream.active.conf"

nginx_test_out=$(ssh_prod "docker exec dotshelf-nginx nginx -t" 2>&1) || {
  log "nginx -t FAILED:"
  printf '%s\n' "$nginx_test_out" >&2
  if [ "$BOOTSTRAP" = "false" ]; then
    log "Reverting upstream.active.conf to $ACTIVE"
    ssh_prod "cp $PROD_DEPLOY_PATH/nginx/conf.d/templates/upstream.$ACTIVE.conf $PROD_DEPLOY_PATH/nginx/conf.d/upstream.active.conf" || true
  fi
  ssh_app "docker compose -f $EC2_DEPLOY_PATH/docker-compose.yml stop api-$TARGET" || true
  fail "nginx configuration invalid"
}
printf '%s\n' "$nginx_test_out"

ssh_prod "docker exec dotshelf-nginx nginx -s reload"
CUTOVER_TS=$(now_secs)
log "Nginx reloaded at epoch $CUTOVER_TS"

# ---------- 7. 공개 smoke ----------
log "Public smoke: $HEALTH_URL_PUBLIC"
public_ok=false
i=0
while [ $i -lt 5 ]; do
  i=$((i + 1))
  if curl -fsS --max-time 5 "$HEALTH_URL_PUBLIC" >/dev/null; then
    log "  public health ok (attempt $i)"
    public_ok=true
    break
  fi
  log "  public health fail (attempt $i), retry in 2s"
  sleep 2
done

if [ "$public_ok" = "false" ]; then
  log "Public health did not recover after switch — this is NOT auto-reverted."
  log "Manual action may be required. api-$TARGET stays running; consider re-running with ACTIVE=$TARGET."
  fail "public health check failed post-switch"
fi

# ---------- 8. 상태 persist ----------
log "Recording .active_color = $TARGET"
ssh_app "echo $TARGET > $EC2_DEPLOY_PATH/.active_color"

# ---------- 9. 유예 후 구 색 stop ----------
if [ "$BOOTSTRAP" = "false" ]; then
  log "Grace period ${GRACE_PERIOD_SECONDS}s before stopping api-$ACTIVE"
  sleep "$GRACE_PERIOD_SECONDS"
  log "Stopping api-$ACTIVE"
  ssh_app "docker compose -f $EC2_DEPLOY_PATH/docker-compose.yml stop api-$ACTIVE" || true
fi

log "Deploy complete: $ACTIVE -> $TARGET"
