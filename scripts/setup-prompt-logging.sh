#!/usr/bin/env bash
# DotShelf 프롬프트 자동 로깅 활성화 (Mac/Linux/Git Bash).
# fresh clone 상태에서는 로깅이 비활성. 이 스크립트를 명시적으로 실행해야만 hook 이 켜진다.
#
# 동작:
# - Node 18+, git, codex CLI 존재 확인
# - 활성화 의사 확인 (opt-in)
# - Notion integration token 입력 받아 .claude/notion-config.json 생성 (gitignored)
# - 5개 settings.local.json + .codex/hooks.json 생성 (모두 gitignored)
# - LOG_USER 환경변수 + ~/.dotshelf/log-user 폴백 설정
# - codex_hooks 피처 플래그 보장
# - Notion API ping 으로 토큰 검증

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXAMPLE_CFG="$REPO_ROOT/.claude/notion-config.example.json"
LOCAL_CFG="$REPO_ROOT/.claude/notion-config.json"
HOOKS_TEMPLATE="$REPO_ROOT/.codex/hooks.json.example"
HOOKS_LOCAL="$REPO_ROOT/.codex/hooks.json"

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
info()   { printf '  %s\n' "$*"; }

confirm_opt_in() {
  cat <<'EOF'

DotShelf 프롬프트 자동 로깅 시스템

이 스크립트는 다음을 활성화한다:
  - Claude Code/Codex 가 응답을 끝낼 때마다 (Stop 훅) 사용자 프롬프트와 어시스턴트 응답을
    팀 Notion DB 에 자동 기록.
  - 기록 위치: backend/apple/front 워크스페이스별로 라우팅.
  - 시크릿 마스킹은 패턴 기반이라 누락 가능성 있음. 민감 정보는 처음부터 입력하지 말 것.

EOF
  printf "활성화하시겠습니까? [y/N]: "
  read -r ans
  case "$ans" in
    y|Y|yes|YES) ;;
    *) yellow "취소됨"; exit 0 ;;
  esac
}

check_node() {
  if ! command -v node >/dev/null 2>&1; then red "✗ node 없음 (18+ 필요)"; exit 1; fi
  local v major
  v="$(node -v | sed 's/^v//')"
  major="${v%%.*}"
  if [ "${major:-0}" -lt 18 ]; then red "✗ Node $v 너무 낮음 (18+ 필요)"; exit 1; fi
  green "✓ node $v"
}

check_git() {
  command -v git >/dev/null 2>&1 || { red "✗ git 없음"; exit 1; }
  green "✓ git $(git --version | awk '{print $3}')"
}

check_codex() {
  if command -v codex >/dev/null 2>&1; then
    green "✓ codex $(codex --version 2>/dev/null | head -1 || echo '')"
  else
    yellow "△ codex CLI 미설치 — Claude Code 만 쓸 거면 무시 가능"
  fi
}

ensure_codex_hooks_flag() {
  local cfg="${HOME}/.codex/config.toml"
  mkdir -p "${HOME}/.codex"
  if [ ! -f "$cfg" ]; then
    printf '[features]\ncodex_hooks = true\n' > "$cfg"
    green "✓ ~/.codex/config.toml 생성 (codex_hooks=true)"
    return
  fi
  if grep -qE '^\s*codex_hooks\s*=\s*true' "$cfg"; then
    green "✓ codex_hooks=true 이미 설정됨"
    return
  fi
  if grep -qE '^\s*\[features\]' "$cfg"; then
    awk '/^\s*\[features\]\s*$/ {print; print "codex_hooks = true"; next} {print}' "$cfg" > "${cfg}.new" && mv "${cfg}.new" "$cfg"
  else
    printf '\n[features]\ncodex_hooks = true\n' >> "$cfg"
  fi
  green "✓ codex_hooks=true 추가"
}

write_notion_config() {
  if [ ! -f "$EXAMPLE_CFG" ]; then red "✗ $EXAMPLE_CFG 없음"; exit 1; fi
  local existing_token=""
  if [ -f "$LOCAL_CFG" ]; then
    existing_token="$(node -e "try { console.log(JSON.parse(require('fs').readFileSync('$LOCAL_CFG','utf-8')).token || '') } catch(e){}")"
  fi

  local prompt_label="Notion integration token (https://www.notion.so/profile/integrations 의 Internal Integration Secret)"
  if [ -n "$existing_token" ]; then prompt_label="$prompt_label [기존값 유지하려면 enter]"; fi
  printf "%s\n  > " "$prompt_label"
  read -rs token_input
  echo
  local token="${token_input:-$existing_token}"
  if [ -z "$token" ]; then red "✗ 토큰 입력 필수"; exit 1; fi

  printf "Anthropic API key (선택, Phase 5-1/5-3 활성화) [enter 로 건너뛰기]\n  > "
  read -rs llm_input
  echo
  local llm_token="$llm_input"

  node - "$EXAMPLE_CFG" "$LOCAL_CFG" "$token" "$llm_token" <<'NODE'
const fs = require('fs');
const [, , examplePath, localPath, token, llm] = process.argv;
const cfg = JSON.parse(fs.readFileSync(examplePath, 'utf-8'));
delete cfg._help;
cfg.token = token;
cfg.llm_token = llm || "";
fs.writeFileSync(localPath, JSON.stringify(cfg, null, 2) + "\n");
NODE
  green "✓ $LOCAL_CFG 생성 (gitignored)"
}

write_settings_local() {
  local hook_command='node "$(git rev-parse --show-toplevel)/.claude/log-to-notion.mjs"'
  local content
  content=$(cat <<EOF
{
  "hooks": {
    "Stop": [
      {
        "hooks": [
          { "type": "command", "command": "$hook_command" }
        ]
      }
    ]
  }
}
EOF
)
  for dir in .claude apple/.claude backend/.claude frontend/.claude; do
    local target="$REPO_ROOT/$dir/settings.local.json"
    mkdir -p "$REPO_ROOT/$dir"
    printf '%s\n' "$content" > "$target"
    green "✓ $dir/settings.local.json"
  done

  if [ -f "$HOOKS_TEMPLATE" ]; then
    cp "$HOOKS_TEMPLATE" "$HOOKS_LOCAL"
    green "✓ .codex/hooks.json (gitignored)"
  fi
}

resolve_rc_file() {
  case "${SHELL:-}" in
    *zsh) echo "${HOME}/.zshrc" ;;
    *bash)
      if [ -f "${HOME}/.bashrc" ]; then echo "${HOME}/.bashrc"
      else echo "${HOME}/.bash_profile"; fi ;;
    *) echo "${HOME}/.profile" ;;
  esac
}

set_log_user() {
  local current="${LOG_USER:-${USER:-${LOGNAME:-user}}}"
  printf "LOG_USER (Notion User Select 에 표시될 이름) [%s]: " "$current"
  read -r input
  local value="${input:-$current}"
  local rc; rc="$(resolve_rc_file)"
  touch "$rc"
  if grep -qE '^\s*export\s+LOG_USER=' "$rc"; then
    if [ "$(uname -s)" = "Darwin" ]; then
      sed -i '' -E "s|^\s*export\s+LOG_USER=.*|export LOG_USER=\"${value}\"|" "$rc"
    else
      sed -i -E "s|^\s*export\s+LOG_USER=.*|export LOG_USER=\"${value}\"|" "$rc"
    fi
    green "✓ ${rc} 의 LOG_USER 갱신"
  else
    printf '\n# DotShelf prompt logging\nexport LOG_USER="%s"\n' "$value" >> "$rc"
    green "✓ ${rc} 에 LOG_USER 추가"
  fi
  export LOG_USER="$value"

  mkdir -p "${HOME}/.dotshelf"
  printf '%s' "$value" > "${HOME}/.dotshelf/log-user"
  green "✓ ~/.dotshelf/log-user 작성 (환경변수 stale 폴백)"

  yellow "→ 새 셸을 열거나 'source ${rc}' 로 환경변수도 적용하세요"
}

ping_notion() {
  local token; token="$(node -e "console.log(JSON.parse(require('fs').readFileSync('$LOCAL_CFG','utf-8')).token||'')")"
  if [ -z "$token" ]; then red "✗ $LOCAL_CFG 에 token 없음"; exit 1; fi
  local code
  code="$(curl -s -o /dev/null -w '%{http_code}' \
    -H "Authorization: Bearer ${token}" \
    -H "Notion-Version: 2022-06-28" \
    https://api.notion.com/v1/users/me)"
  if [ "$code" = "200" ]; then green "✓ Notion API ping OK"
  else red "✗ Notion API 응답 ${code} — 토큰 확인"; exit 1; fi
}

echo "DotShelf 프롬프트 자동 로깅 활성화"
echo "==================================="
confirm_opt_in
check_node
check_git
check_codex
ensure_codex_hooks_flag
write_notion_config
write_settings_local
set_log_user
ping_notion

echo
green "완료. 새 Claude Code/Codex 세션부터 자동 로깅됩니다."
info "로그 파일: $(node -e "console.log(require('os').tmpdir())")/dotshelf-notion-hook.log"
info "끄려면 5개 settings.local.json + .codex/hooks.json + ~/.dotshelf/log-user 삭제"
