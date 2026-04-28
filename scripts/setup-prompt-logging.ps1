# DotShelf 프롬프트 자동 로깅 활성화 (Windows PowerShell).
# fresh clone 상태에서는 로깅이 비활성. 이 스크립트를 명시적으로 실행해야만 hook 이 켜진다.
#
# 동작:
# - Node 18+, git, codex CLI 존재 확인
# - 활성화 의사 확인 (opt-in)
# - Notion integration token 입력 받아 .claude/notion-config.json 생성 (gitignored)
# - 5개 settings.local.json + .codex/hooks.json 생성 (모두 gitignored)
# - LOG_USER 사용자 환경변수 + ~/.dotshelf/log-user 폴백 설정
# - codex_hooks 피처 플래그 보장
# - Notion API ping 으로 토큰 검증

$ErrorActionPreference = "Stop"

$RepoRoot     = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ExampleCfg   = Join-Path $RepoRoot ".claude\notion-config.example.json"
$LocalCfg     = Join-Path $RepoRoot ".claude\notion-config.json"
$HooksTemplate = Join-Path $RepoRoot ".codex\hooks.json.example"
$HooksLocal    = Join-Path $RepoRoot ".codex\hooks.json"

function Write-Ok    ($m) { Write-Host "✓ $m" -ForegroundColor Green }
function Write-Warn2 ($m) { Write-Host "△ $m" -ForegroundColor Yellow }
function Write-Err   ($m) { Write-Host "✗ $m" -ForegroundColor Red }
function Write-Info  ($m) { Write-Host "  $m" }

function Confirm-OptIn {
    Write-Host ""
    Write-Host "DotShelf 프롬프트 자동 로깅 시스템"
    Write-Host ""
    Write-Host "이 스크립트는 다음을 활성화한다:"
    Write-Host "  - Claude Code/Codex 가 응답을 끝낼 때마다 (Stop 훅) 사용자 프롬프트와 어시스턴트 응답을"
    Write-Host "    팀 Notion DB 에 자동 기록."
    Write-Host "  - 기록 위치: backend/apple/front 워크스페이스별로 라우팅."
    Write-Host "  - 시크릿 마스킹은 패턴 기반이라 누락 가능성 있음. 민감 정보는 처음부터 입력하지 말 것."
    Write-Host ""
    $ans = Read-Host "활성화하시겠습니까? [y/N]"
    if ($ans -notmatch '^(y|yes)$') { Write-Warn2 "취소됨"; exit 0 }
}

function Test-Node {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { Write-Err "node 없음 (18+ 필요)"; exit 1 }
    $v = (& node -v).TrimStart('v')
    $major = [int]($v.Split('.')[0])
    if ($major -lt 18) { Write-Err "Node $v 너무 낮음 (18+ 필요)"; exit 1 }
    Write-Ok "node $v"
}

function Test-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Write-Err "git 없음"; exit 1 }
    Write-Ok ("git " + ((& git --version).Split(' ')[2]))
}

function Test-Codex {
    if (Get-Command codex -ErrorAction SilentlyContinue) {
        $v = (& codex --version 2>$null | Select-Object -First 1)
        Write-Ok "codex $v"
    } else {
        Write-Warn2 "codex CLI 미설치 — Claude Code 만 쓸 거면 무시 가능"
    }
}

function Ensure-CodexHooksFlag {
    $cfg = Join-Path $env:USERPROFILE ".codex\config.toml"
    New-Item -ItemType Directory -Force -Path (Split-Path $cfg) | Out-Null
    if (-not (Test-Path $cfg)) {
        "[features]`r`ncodex_hooks = true`r`n" | Out-File -FilePath $cfg -Encoding utf8 -NoNewline
        Write-Ok "~/.codex/config.toml 생성 (codex_hooks=true)"
        return
    }
    $content = Get-Content $cfg -Raw
    if ($content -match '(?m)^\s*codex_hooks\s*=\s*true') { Write-Ok "codex_hooks=true 이미 설정됨"; return }
    if ($content -match '(?m)^\s*\[features\]\s*$') {
        $new = [regex]::Replace($content, '(?m)^(\s*\[features\]\s*)$', "`$1`r`ncodex_hooks = true", 1)
    } else {
        $new = $content.TrimEnd() + "`r`n`r`n[features]`r`ncodex_hooks = true`r`n"
    }
    $new | Out-File -FilePath $cfg -Encoding utf8 -NoNewline
    Write-Ok "codex_hooks=true 추가"
}

function Write-NotionConfig {
    if (-not (Test-Path $ExampleCfg)) { Write-Err "$ExampleCfg 없음"; exit 1 }
    $existingToken = ""
    if (Test-Path $LocalCfg) {
        try {
            $existingToken = (Get-Content $LocalCfg -Raw | ConvertFrom-Json).token
            if ($null -eq $existingToken) { $existingToken = "" }
        } catch { $existingToken = "" }
    }

    $label = "Notion integration token (https://www.notion.so/profile/integrations 의 Internal Integration Secret)"
    if ($existingToken) { $label = "$label [기존값 유지하려면 enter]" }
    $secure = Read-Host -AsSecureString $label
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $tokenInput = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) | Out-Null
    $token = if ($tokenInput) { $tokenInput } else { $existingToken }
    if (-not $token) { Write-Err "토큰 입력 필수"; exit 1 }

    $secureLlm = Read-Host -AsSecureString "Anthropic API key (선택, Phase 5-1/5-3 활성화) [enter 로 건너뛰기]"
    $bstr2 = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureLlm)
    $llmInput = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr2)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr2) | Out-Null

    $cfg = Get-Content $ExampleCfg -Raw | ConvertFrom-Json
    if ($cfg.PSObject.Properties.Name -contains '_help') { $cfg.PSObject.Properties.Remove('_help') }
    $cfg.token = $token
    $cfg.llm_token = if ($llmInput) { $llmInput } else { "" }
    ($cfg | ConvertTo-Json -Depth 10) + "`n" | Out-File -FilePath $LocalCfg -Encoding utf8 -NoNewline
    Write-Ok "$LocalCfg 생성 (gitignored)"
}

function Write-SettingsLocal {
    $hookCommand = 'node "$(git rev-parse --show-toplevel)/.claude/log-to-notion.mjs"'
    $content = @"
{
  "hooks": {
    "Stop": [
      {
        "hooks": [
          { "type": "command", "command": "$hookCommand" }
        ]
      }
    ]
  }
}
"@
    foreach ($d in @(".claude", "apple/.claude", "backend/.claude", "frontend/.claude")) {
        $dir = Join-Path $RepoRoot $d
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $target = Join-Path $dir "settings.local.json"
        $content | Out-File -FilePath $target -Encoding utf8 -NoNewline
        Write-Ok "$d/settings.local.json"
    }
    if (Test-Path $HooksTemplate) {
        Copy-Item $HooksTemplate $HooksLocal -Force
        Write-Ok ".codex/hooks.json (gitignored)"
    }
}

function Set-LogUser {
    $current = $env:LOG_USER
    if ([string]::IsNullOrWhiteSpace($current)) { $current = $env:USERNAME }
    $userInput = Read-Host "LOG_USER (Notion User Select 에 표시될 이름) [$current]"
    if ([string]::IsNullOrWhiteSpace($userInput)) { $userInput = $current }
    & setx LOG_USER $userInput | Out-Null
    $env:LOG_USER = $userInput
    Write-Ok "사용자 환경변수 LOG_USER='$userInput' 등록"

    $dir = Join-Path $env:USERPROFILE ".dotshelf"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $file = Join-Path $dir "log-user"
    [System.IO.File]::WriteAllText($file, $userInput, (New-Object System.Text.UTF8Encoding $false))
    Write-Ok "$file 작성 (환경변수 stale 시 폴백)"

    Write-Warn2 "→ setx 는 explorer 재시작 전까지 stale 일 수 있지만 폴백 파일이 즉시 적용됩니다."
}

function Ping-Notion {
    $token = (Get-Content $LocalCfg -Raw | ConvertFrom-Json).token
    if ([string]::IsNullOrWhiteSpace($token)) { Write-Err "$LocalCfg 에 token 없음"; exit 1 }
    try {
        $resp = Invoke-WebRequest -Uri https://api.notion.com/v1/users/me `
            -Headers @{ Authorization = "Bearer $token"; "Notion-Version" = "2022-06-28" } `
            -UseBasicParsing -ErrorAction Stop
        if ($resp.StatusCode -eq 200) { Write-Ok "Notion API ping OK" }
        else { Write-Err "Notion API 응답 $($resp.StatusCode)"; exit 1 }
    } catch {
        Write-Err "Notion API ping 실패: $($_.Exception.Message)"
        exit 1
    }
}

Write-Host "DotShelf 프롬프트 자동 로깅 활성화"
Write-Host "==================================="
Confirm-OptIn
Test-Node
Test-Git
Test-Codex
Ensure-CodexHooksFlag
Write-NotionConfig
Write-SettingsLocal
Set-LogUser
Ping-Notion

Write-Host ""
Write-Ok "완료. 새 Claude Code/Codex 세션부터 자동 로깅됩니다."
$tmp = & node -e "console.log(require('os').tmpdir())"
Write-Info "로그 파일: $tmp\dotshelf-notion-hook.log"
Write-Info "끄려면 5개 settings.local.json + .codex/hooks.json + ~/.dotshelf/log-user 삭제"
