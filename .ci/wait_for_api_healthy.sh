#!/bin/sh
set -eu

: "${EC2_USER:?EC2_USER required}"
: "${EC2_HOST:?EC2_HOST required}"
: "${EC2_DEPLOY_PATH:?EC2_DEPLOY_PATH required}"

STATUS="starting"
for i in 1 2 3 4 5 6 7 8 9 10 11 12; do
    STATUS=$(ssh "$EC2_USER@$EC2_HOST" "docker inspect -f '{{.State.Health.Status}}' dotshelf-api 2>/dev/null || echo starting")
    echo "[$i] api health: $STATUS"
    if [ "$STATUS" = "healthy" ]; then
        break
    fi
    sleep 5
done

if [ "$STATUS" != "healthy" ]; then
    echo "api did not become healthy within timeout" >&2
    ssh "$EC2_USER@$EC2_HOST" "cd $EC2_DEPLOY_PATH && docker compose logs --tail 50 api" >&2 || true
    exit 1
fi
