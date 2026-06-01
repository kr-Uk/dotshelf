#!/bin/bash
# DotShelf PostgreSQL 자동 백업 (jihocompany 호스트 cron).
#
# 서버 위치: /opt/dotshelf/scripts/backup-pg.sh
# cron 예: 0 3 * * *  /opt/dotshelf/scripts/backup-pg.sh >> /var/log/dotshelf-pg-backup.log 2>&1
# 산출물: /opt/dotshelf/backups/pg/dotshelf-YYYYMMDD-HHMMSS.sql.gz (14일 보존)
set -euo pipefail

BACKUP_DIR="/opt/dotshelf/backups/pg"
RETENTION_DAYS=14
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/dotshelf-${TIMESTAMP}.sql.gz"

mkdir -p "${BACKUP_DIR}"

set -a
. /opt/dotshelf/.env
set +a

docker exec -i dotshelf-postgres pg_dump \
  -U "${POSTGRES_USER}" \
  -d "${POSTGRES_DB}" \
  --clean --if-exists --no-owner --no-privileges \
  | gzip > "${BACKUP_FILE}"

SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "[$(date -Iseconds)] Backup OK: ${BACKUP_FILE} (${SIZE})"

find "${BACKUP_DIR}" -name 'dotshelf-*.sql.gz' -mtime +${RETENTION_DAYS} -delete
echo "[$(date -Iseconds)] Cleanup done (retention=${RETENTION_DAYS}d)"
