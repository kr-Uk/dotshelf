#!/usr/bin/env bash
# Re-scan frontend/.claude/skills/ + backend/.claude/skills/ 와 루트 .claude/skills/
# 심링크를 동기화. frontend/backend에 새 스킬을 추가했을 때 한 번만 실행하면 루트
# 자동 검색용 심링크가 갱신됨. 기존 심링크는 삭제 후 재생성 (depth-1 idempotent).
#
# Usage:
#   ./scripts/sync-claude-skills.sh
#
# 새 스킬 추가 워크플로:
#   1. frontend/.claude/skills/<new-skill>/SKILL.md 또는 backend/... 작성
#   2. ./scripts/sync-claude-skills.sh 실행
#   3. git add .claude/skills/<new-skill> && commit
#
# Windows 사용자: WSL/Git Bash에서 실행 + git config core.symlinks=true 필요.
# macOS/Linux는 그대로 동작.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT_SKILLS_DIR="$REPO_ROOT/.claude/skills"
SOURCES=(
  "$REPO_ROOT/frontend/.claude/skills"
  "$REPO_ROOT/backend/.claude/skills"
)

mkdir -p "$ROOT_SKILLS_DIR"

# 기존 심링크 모두 제거 (orphan 정리 + 명확한 desync 회피).
# 일반 파일/디렉터리는 건드리지 않음.
echo "[sync-claude-skills] Cleaning existing symlinks in $ROOT_SKILLS_DIR"
find "$ROOT_SKILLS_DIR" -maxdepth 1 -type l -delete

CREATED=0
SKIPPED=0
for SOURCE_DIR in "${SOURCES[@]}"; do
  if [ ! -d "$SOURCE_DIR" ]; then
    echo "[sync-claude-skills] $SOURCE_DIR — 디렉터리 없음, skip"
    continue
  fi
  for SKILL_DIR in "$SOURCE_DIR"/*/; do
    [ -d "$SKILL_DIR" ] || continue
    NAME="$(basename "$SKILL_DIR")"
    TARGET="$ROOT_SKILLS_DIR/$NAME"
    # 상대 경로로 심링크 (clone 위치 무관 동작). ROOT_SKILLS_DIR 는 항상 $REPO_ROOT/.claude/skills,
    # SKILL_DIR 는 $REPO_ROOT/<frontend|backend>/.claude/skills/<name>/ 이므로 relpath 는
    # 결정적 — ../../ + (repo root 기준 SKILL_DIR 상대경로). python3 의존 제거 (bash 네이티브).
    SKILL_REL="${SKILL_DIR#"$REPO_ROOT"/}"
    REL_PATH="../../${SKILL_REL%/}"
    ln -s "$REL_PATH" "$TARGET"
    CREATED=$((CREATED + 1))
    echo "  [+] $NAME -> $REL_PATH"
  done
done

echo "[sync-claude-skills] 완료: $CREATED 심링크 생성"
echo ""
echo "다음 단계:"
echo "  git add .claude/skills/"
echo "  git status  # 새/제거된 심링크 확인"
echo "  git commit -m '[META] sync .claude/skills'"
