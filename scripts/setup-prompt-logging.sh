#!/usr/bin/env bash
# DotShelf 프롬프트 자동 로깅 부트스트랩 (Mac/Linux/Git Bash).
# - Node 18+, git, codex CLI 존재 확인
# - ~/.codex/config.toml 에 [features] codex_hooks = true 보장
# - LOG_USER 환경변수를 셸 rc 파일에 추가
# - Notion API ping 으로 토큰 검증

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG_FILE="$REPO_ROOT/.claude/notion-config.json"

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
info()   { printf '  %s\n' "$*"; }

check_node() {
  if ! command -v node >/dev/null 2>&1; then
    red "✗ node 가 PATH 에 없습니다. Node.js 18 이상을 설치하세요."
    exit 1
  fi
  local v
  v="$(node -v | sed 's/^v//')"
  local major="${v%%.*}"
  if [ "${major:-0}" -lt 18 ]; then
    red "✗ Node ${v} 는 너무 낮습니다. 18 이상 필요."
    exit 1
  fi
  green "✓ node ${v}"
}

check_git() {
  if ! command -v git >/dev/null 2>&1; then
    red "✗ git 이 없습니다."
    exit 1
  fi
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
  if [ ! -f "$cfg" ]; then
    info "~/.codex/config.toml 가 없어 새로 만듭니다 (codex 미사용 시 그대로 둬도 무관)"
    mkdir -p "${HOME}/.codex"
    printf '[features]\ncodex_hooks = true\n' > "$cfg"
    green "✓ codex_hooks=true 추가"
    return
  fi
  if grep -qE '^\s*codex_hooks\s*=\s*true' "$cfg"; then
    green "✓ codex_hooks=true 이미 설정됨"
    return
  fi
  if grep -qE '^\s*\[features\]' "$cfg"; then
    awk '
      BEGIN { added=0 }
      /^\s*\[features\]\s*$/ { print; print "codex_hooks = true"; added=1; next }
      { print }
      END { if (!added) { print "[features]"; print "codex_hooks = true" } }
    ' "$cfg" > "${cfg}.new" && mv "${cfg}.new" "$cfg"
  else
    printf '\n[features]\ncodex_hooks = true\n' >> "$cfg"
  fi
  green "✓ codex_hooks=true 추가"
}

resolve_rc_file() {
  case "${SHELL:-}" in
    *zsh) echo "${HOME}/.zshrc" ;;
    *bash)
      if [ -f "${HOME}/.bashrc" ]; then echo "${HOME}/.bashrc"
      else echo "${HOME}/.bash_profile"; fi
      ;;
    *) echo "${HOME}/.profile" ;;
  esac
}

set_log_user() {
  local current="${LOG_USER:-}"
  local prompt_default="$current"
  if [ -z "$prompt_default" ]; then
    prompt_default="${USER:-${LOGNAME:-user}}"
  fi
  printf "LOG_USER (Notion User Select 에 표시될 이름) [%s]: " "$prompt_default"
  read -r input
  local value="${input:-$prompt_default}"
  local rc
  rc="$(resolve_rc_file)"
  touch "$rc"
  if grep -qE '^\s*export\s+LOG_USER=' "$rc"; then
    if [ "$(uname -s)" = "Darwin" ]; then
      sed -i '' -E "s|^\s*export\s+LOG_USER=.*|export LOG_USER=\"${value}\"|" "$rc"
    else
      sed -i -E "s|^\s*export\s+LOG_USER=.*|export LOG_USER=\"${value}\"|" "$rc"
    fi
    green "✓ ${rc} 의 LOG_USER 를 '${value}' 로 갱신"
  else
    printf '\n# DotShelf prompt logging\nexport LOG_USER="%s"\n' "$value" >> "$rc"
    green "✓ ${rc} 에 LOG_USER='${value}' 추가"
  fi
  export LOG_USER="$value"

  local file="${HOME}/.dotshelf/log-user"
  mkdir -p "${HOME}/.dotshelf"
  printf '%s' "$value" > "$file"
  green "✓ ${file} 작성 (환경변수 stale 시 폴백)"

  yellow "→ 새 셸을 열거나 'source ${rc}' 로 환경변수도 적용하세요"
}

ping_notion() {
  if [ ! -f "$CONFIG_FILE" ]; then
    yellow "△ ${CONFIG_FILE} 가 없어 Notion ping 건너뜀"
    return
  fi
  local token
  token="$(node -e "console.log(JSON.parse(require('fs').readFileSync('${CONFIG_FILE}','utf-8')).token||'')")"
  if [ -z "$token" ]; then
    red "✗ notion-config.json 에 token 이 없습니다"
    exit 1
  fi
  local code
  code="$(curl -s -o /dev/null -w '%{http_code}' \
    -H "Authorization: Bearer ${token}" \
    -H "Notion-Version: 2022-06-28" \
    https://api.notion.com/v1/users/me)"
  if [ "$code" = "200" ]; then
    green "✓ Notion API ping OK"
  else
    red "✗ Notion API 응답 ${code} — 토큰/네트워크 확인 필요"
    exit 1
  fi
}

echo "DotShelf 프롬프트 자동 로깅 부트스트랩"
echo "--------------------------------------"
check_node
check_git
check_codex
ensure_codex_hooks_flag
set_log_user
ping_notion
echo
green "완료. 다음 Claude Code/Codex 세션부터 자동 로깅됩니다."
info "로그 파일: $(node -e "console.log(require('os').tmpdir())")/dotshelf-notion-hook.log"
