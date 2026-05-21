#!/bin/sh
# Blue-Green 스위칭 전후로 공개 health 엔드포인트를 1초 간격으로 폴링.
# "배포 중 5xx 카운트" 증거 수집 목적.
#
# Usage:
#   poll-health.sh [URL] [DURATION_SECONDS]
#
# Outputs (JSON, 단일 라인):
#   {"url":"...","duration":60,"attempts":60,"ok":60,"fail":0,"codes":"200:60"}
#
# Exit code:
#   0  — fail == 0
#   1  — fail > 0 (배포 중 에러 발생)
#
set -u

URL=${1:-${HEALTH_URL_PUBLIC:-https://api.dotshelf.digital/health}}
DURATION=${2:-60}

end_ts=$(( $(date +%s) + DURATION ))
attempts=0
ok=0
fail=0
codes_log=""

while [ "$(date +%s)" -lt "$end_ts" ]; do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 "$URL" 2>/dev/null || echo "000")
  attempts=$((attempts + 1))
  if [ "$code" = "200" ]; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
  fi
  codes_log="$codes_log $code"
  sleep 1
done

hist=$(printf '%s\n' $codes_log | grep -v '^$' | sort | uniq -c | awk '{printf "%s:%s ", $2, $1}' | sed 's/ $//')

printf '{"url":"%s","duration":%d,"attempts":%d,"ok":%d,"fail":%d,"codes":"%s"}\n' \
  "$URL" "$DURATION" "$attempts" "$ok" "$fail" "$hist"

[ "$fail" -eq 0 ]
