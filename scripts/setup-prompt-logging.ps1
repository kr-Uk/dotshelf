# DotShelf 프롬프트 자동 로깅 부트스트랩 (Windows PowerShell).
# - Node 18+, git, codex CLI 존재 확인
# - %USERPROFILE%\.codex\config.toml 에 [features] codex_hooks = true 보장
# - LOG_USER 사용자 환경변수 setx 등록
# - Notion API ping 으로 토큰 검증

$ErrorActionPreference = "Stop"

$RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ConfigFile = Join-Path $RepoRoot ".claude\notion-config.json"

function Write-Ok    ($m) { Write-Host "✓ $m" -ForegroundColor Green }
function Write-Warn2 ($m) { Write-Host "△ $m" -ForegroundColor Yellow }
function Write-Err   ($m) { Write-Host "✗ $m" -ForegroundColor Red }
function Write-Info  ($m) { Write-Host "  $m" }

function Test-Node {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { Write-Err "node 가 PATH 에 없습니다. Node.js 18 이상을 설치하세요."; exit 1 }
    $v = (& node -v).TrimStart('v')
    $major = [int]($v.Split('.')[0])
    if ($major -lt 18) { Write-Err "Node $v 는 너무 낮습니다. 18 이상 필요."; exit 1 }
    Write-Ok "node $v"
}

function Test-Git {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) { Write-Err "git 이 없습니다."; exit 1 }
    $v = (& git --version).Split(' ')[2]
    Write-Ok "git $v"
}

function Test-Codex {
    $codex = Get-Command codex -ErrorAction SilentlyContinue
    if ($codex) {
        $v = (& codex --version 2>$null | Select-Object -First 1)
        Write-Ok "codex $v"
    } else {
        Write-Warn2 "codex CLI 미설치 — Claude Code 만 쓸 거면 무시 가능"
    }
}

function Ensure-CodexHooksFlag {
    $cfg = Join-Path $env:USERPROFILE ".codex\config.toml"
    if (-not (Test-Path $cfg)) {
        Write-Info "$cfg 가 없어 새로 만듭니다 (codex 미사용 시 그대로 둬도 무관)"
        New-Item -ItemType Directory -Force -Path (Split-Path $cfg) | Out-Null
        "[features]`r`ncodex_hooks = true`r`n" | Out-File -FilePath $cfg -Encoding utf8 -NoNewline
        Write-Ok "codex_hooks=true 추가"
        return
    }
    $content = Get-Content $cfg -Raw
    if ($content -match '(?m)^\s*codex_hooks\s*=\s*true') {
        Write-Ok "codex_hooks=true 이미 설정됨"
        return
    }
    if ($content -match '(?m)^\s*\[features\]\s*$') {
        $new = [regex]::Replace($content, '(?m)^(\s*\[features\]\s*)$', "`$1`r`ncodex_hooks = true", 1)
    } else {
        $new = $content.TrimEnd() + "`r`n`r`n[features]`r`ncodex_hooks = true`r`n"
    }
    $new | Out-File -FilePath $cfg -Encoding utf8 -NoNewline
    Write-Ok "codex_hooks=true 추가"
}

function Set-LogUser {
    $current = $env:LOG_USER
    if ([string]::IsNullOrWhiteSpace($current)) { $current = $env:USERNAME }
    $input = Read-Host "LOG_USER (Notion User Select 에 표시될 이름) [$current]"
    if ([string]::IsNullOrWhiteSpace($input)) { $input = $current }
    & setx LOG_USER $input | Out-Null
    $env:LOG_USER = $input
    Write-Ok "사용자 환경변수 LOG_USER='$input' 등록"

    $dir = Join-Path $env:USERPROFILE ".dotshelf"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $file = Join-Path $dir "log-user"
    [System.IO.File]::WriteAllText($file, $input, (New-Object System.Text.UTF8Encoding $false))
    Write-Ok "$file 작성 (환경변수 stale 시 폴백)"

    Write-Warn2 "→ setx 는 explorer 재시작 전까지 stale 일 수 있지만 폴백 파일이 즉시 적용됩니다."
}

function Ping-Notion {
    if (-not (Test-Path $ConfigFile)) { Write-Warn2 "$ConfigFile 가 없어 Notion ping 건너뜀"; return }
    $token = (Get-Content $ConfigFile -Raw | ConvertFrom-Json).token
    if ([string]::IsNullOrWhiteSpace($token)) { Write-Err "notion-config.json 에 token 이 없습니다"; exit 1 }
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

Write-Host "DotShelf 프롬프트 자동 로깅 부트스트랩"
Write-Host "--------------------------------------"
Test-Node
Test-Git
Test-Codex
Ensure-CodexHooksFlag
Set-LogUser
Ping-Notion
Write-Host ""
Write-Ok "완료. 다음 Claude Code/Codex 세션부터 자동 로깅됩니다."
$tmp = & node -e "console.log(require('os').tmpdir())"
Write-Info "로그 파일: $tmp\dotshelf-notion-hook.log"
