<#
.SYNOPSIS
Unity Windows 빌드 산출물을 GitLab Generic Packages 로 올리고 manifest/소스 갱신
+ feat/launcher/unity-<ver> 브랜치 commit/push/MR 까지 일괄 처리.

.DESCRIPTION
Unity Editor 의 "Tools/DotShelf/Build & Publish Launcher Package" 메뉴에서 호출된다.
사람이 직접 호출해도 동작 (자동화 fallback).

순서:
  0. Pre-flight (모두 외부 변경 전):
     - GITLAB_TOKEN / BuildDir / Dotshelf.exe 존재
     - Git: dev 브랜치 위 + clean working tree + 신규 feat 브랜치 사용 가능
  1. bump-version.ps1 -Target unity 로 ProjectSettings/manifest/page.tsx 동기 갱신
  2. Build/Windows/* → Dotshelf-<ver>.zip 압축
  3. SHA256 → manifest sha256/releaseNotes JSON parse 방식 patch
  4. PUT https://lab.ssafy.com/.../packages/generic/dotshelf-unity/<ver>/Dotshelf-<ver>.zip
  5. HEAD 검증
  6. feat/launcher/unity-<ver> 신규 브랜치 생성, commit/push, MR 생성

릴리스 자동화는 dev 브랜치 위에서만 실행 가능.
master 또는 feat/* 위에서 실행하면 즉시 중단 (master 절대 건드리지 말 것 정책 +
dev 직푸시 금지 정책 + release commit 에 unrelated 변경 섞임 방지).

실패 시 recovery:
- 0~3 단계에서 실패: 외부 영향 없음. ProjectSettings/manifest 만 dirty 가능 →
  `git checkout -- frontend/ProjectSettings/ProjectSettings.asset
   launcher/manifests/dev/windows/latest.json
   web/service-web/app/(marketing)/download/page.tsx`
- 4 단계 (PUT) 이후 실패: GitLab Generic Packages 에 zip 이 이미 올라간 상태.
  같은 버전 재시도 시 PUT 이 idempotent (덮어쓰기) 라 OK. 단 manifest commit/push
  단계까지 갔다면 그 브랜치 재사용 또는 `git branch -D feat/launcher/unity-<ver>` 후 재시도.

.PARAMETER Version
SemVer (X.Y.Z)

.PARAMETER BuildDir
Unity Standalone Windows64 빌드 출력 디렉터리 (Dotshelf.exe + Dotshelf_Data 등 위치).

.PARAMETER ReleaseNotesBase64
UTF-8 + Base64 로 인코딩된 release notes. 한 줄 = 한 bullet. 빈 문자열 허용.

.NOTES
필요 환경변수: GITLAB_TOKEN (lab.ssafy.com PAT, scope=api)
필요 도구    : git, curl.exe (Windows 11 기본 포함)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$BuildDir,

    [Parameter(Mandatory)]
    [string]$ReleaseNotesBase64
)

$ErrorActionPreference = "Stop"

# stdout/stderr 를 UTF-8 로 출력 (Unity Editor 가 stdout 을 UTF-8 로 읽음 — PS 5.1
# 의 default OutputEncoding 은 시스템 codepage 라 한글 메시지가 깨짐).
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectId = 1304436
$gitlabApi = "https://lab.ssafy.com/api/v4/projects/$projectId"
$packageName = "dotshelf-unity"
$zipName = "Dotshelf-$Version.zip"
$uploadUrl = "$gitlabApi/packages/generic/$packageName/$Version/$zipName"
$branch = "feat/launcher/unity-$Version"

# --- 0a. Pre-flight: token / build artifact ----------------------------
if (-not $env:GITLAB_TOKEN) {
    throw "환경변수 GITLAB_TOKEN 이 설정되어 있지 않습니다. (lab.ssafy.com PAT, scope=api)"
}
if (-not (Test-Path $BuildDir)) {
    throw "BuildDir 가 없습니다: $BuildDir"
}
$exePath = Join-Path $BuildDir "Dotshelf.exe"
if (-not (Test-Path $exePath)) {
    throw "Unity 빌드 산출물 (Dotshelf.exe) 을 찾을 수 없습니다: $exePath"
}

$notesPlain = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ReleaseNotesBase64))
$notesLines = @(
    ($notesPlain -split "`r?`n") |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ }
)
if ($notesLines.Count -eq 0) {
    $notesLines = @("Unity build $Version.")
}

# --- 0b. Pre-flight: git state (release artifact 업로드 전 모두 검증) --
Push-Location $repoRoot
try {
    $currentBranch = ([string](& git rev-parse --abbrev-ref HEAD)).Trim()
    if ($LASTEXITCODE -ne 0) { throw "git rev-parse 실패 — repo root 가 아닐 수 있습니다: $repoRoot" }

    if ($currentBranch -eq "master") {
        throw "master 브랜치 위에서 release 자동화 금지 (정책: master 절대 건드리지 말 것). dev 로 checkout 후 다시 시도하세요."
    }
    if ($currentBranch -ne "dev") {
        throw "release 자동화는 dev 브랜치 위에서만 실행 가능합니다. 현재: '$currentBranch'. 작업 브랜치를 먼저 머지하고 dev 로 checkout (+ git pull) 후 다시 시도하세요."
    }

    & git diff --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "working tree 에 unstaged 변경이 있습니다. release 자동화 전 commit/stash/checkout 으로 정리하세요. ('git status' 로 확인)"
    }
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "index 에 staged 변경이 있습니다. release 자동화 전 commit/reset 으로 정리하세요. ('git status' 로 확인)"
    }

    & git show-ref --verify --quiet "refs/heads/$branch"
    if ($LASTEXITCODE -eq 0) {
        throw "로컬 브랜치 '$branch' 가 이미 존재합니다. 같은 버전 재시도면 'git branch -D $branch' 후 다시 시도하세요."
    }
    $remoteRef = & git ls-remote --heads origin $branch
    if ($remoteRef) {
        throw "원격 브랜치 'origin/$branch' 가 이미 존재합니다. 같은 버전 재시도면 GitLab 에서 해당 브랜치/MR 정리 후 다시 시도하세요."
    }
}
finally {
    Pop-Location
}

Write-Host "===== publish-unity.ps1  (version=$Version) ====="
Write-Host "  repo  : $repoRoot"
Write-Host "  build : $BuildDir"
Write-Host "  notes : $($notesLines.Count) bullet(s)"
Write-Host "  branch: $branch  (from dev)"

# --- 1. bump-version.ps1 ------------------------------------------------
Write-Host ""
Write-Host "▶ bump-version.ps1 -Target unity -NewVersion $Version"
& (Join-Path $PSScriptRoot "bump-version.ps1") -Target unity -NewVersion $Version
if ($LASTEXITCODE -ne 0) { throw "bump-version.ps1 실패" }

# --- 2. Zip --------------------------------------------------------------
$distDir = Join-Path $repoRoot "launcher\dist"
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
$zipPath = Join-Path $distDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host ""
Write-Host "▶ Compress-Archive  $BuildDir\*  →  $zipPath"
Compress-Archive -Path (Join-Path $BuildDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
$zipSize = (Get-Item $zipPath).Length
Write-Host ("  zip size: {0:N0} bytes" -f $zipSize)

# --- 3. SHA256 + manifest patch (JSON parse 방식) -----------------------
$sha256 = (Get-FileHash -Algorithm SHA256 -Path $zipPath).Hash.ToUpper()
Write-Host "  sha256  : $sha256"

$manifestPath = Join-Path $repoRoot "launcher\manifests\dev\windows\latest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.sha256 = $sha256
$manifest.releaseNotes = @($notesLines)
$manifestJson = $manifest | ConvertTo-Json -Depth 10

# PS 5.1 의 ConvertTo-Json 은 비ASCII 문자(한글, & < > ' 등)를 \uXXXX 로 escape.
# 사람이 읽기 쉽게 unescape — JSON 의 다른 escape (\", \\, \n, \t) 는 안 건드림.
$manifestJson = [regex]::Replace(
    $manifestJson,
    '\\u([0-9a-fA-F]{4})',
    { param($m) [char][int]("0x$($m.Groups[1].Value)") }
)
[System.IO.File]::WriteAllText(
    $manifestPath,
    $manifestJson + "`n",
    (New-Object System.Text.UTF8Encoding $false)
)
Write-Host "  manifest patched: $manifestPath"

# --- 4. PUT to GitLab Generic Packages ----------------------------------
Write-Host ""
Write-Host "▶ PUT $uploadUrl"
& curl.exe --fail --silent --show-error --request PUT `
    --header "PRIVATE-TOKEN: $env:GITLAB_TOKEN" `
    --upload-file $zipPath `
    $uploadUrl
if ($LASTEXITCODE -ne 0) { throw "GitLab Generic Packages 업로드 실패 (exit=$LASTEXITCODE)" }
Write-Host "  upload OK"

# --- 5. HEAD verify -----------------------------------------------------
& curl.exe --fail --silent --show-error --head `
    --header "PRIVATE-TOKEN: $env:GITLAB_TOKEN" `
    $uploadUrl | Out-Null
if ($LASTEXITCODE -ne 0) { throw "업로드 직후 HEAD 검증 실패: $uploadUrl" }
Write-Host "  HEAD OK"

# --- 6. Git workflow ----------------------------------------------------
Push-Location $repoRoot
try {
    Write-Host ""
    Write-Host "▶ git checkout -b $branch  (dev 위에서 갈래)"
    & git checkout -b $branch
    if ($LASTEXITCODE -ne 0) { throw "git checkout -b $branch 실패" }

    & git add `
        "frontend/ProjectSettings/ProjectSettings.asset" `
        "launcher/manifests/dev/windows/latest.json" `
        "web/service-web/app/(marketing)/download/page.tsx"
    if ($LASTEXITCODE -ne 0) { throw "git add 실패" }

    & git commit -m "[LAUNCHER] Build: Unity $Version"
    if ($LASTEXITCODE -ne 0) { throw "git commit 실패 (변경사항이 없거나 hook 실패)" }

    & git push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw "git push 실패" }

    $bullets = ($notesLines | ForEach-Object { "- $_" }) -join "`n"
    $desc = @"
Automated Unity build $Version (Editor 'Build & Publish Launcher Package').

## Release notes

$bullets

## Files changed

- ``frontend/ProjectSettings/ProjectSettings.asset`` — bundleVersion
- ``launcher/manifests/dev/windows/latest.json`` — latestVersion / packageUrl / sha256 / releaseNotes
- ``web/service-web/app/(marketing)/download/page.tsx`` — version / date

## Artifact

GitLab Generic Packages: ``$packageName/$Version/$zipName``
- size: $zipSize bytes
- sha256: ``$sha256``

머지되면 ``launcher:publish`` CI 잡이 자동으로 prod 정적 호스트에 배포한다.
"@

    $payload = [ordered]@{
        source_branch        = $branch
        target_branch        = "dev"
        title                = "[LAUNCHER] Unity build $Version"
        description          = $desc
        remove_source_branch = $true
        squash               = $true
    } | ConvertTo-Json -Depth 10

    $mrBodyFile = Join-Path $env:TEMP "dotshelf-mr-$Version.json"
    [System.IO.File]::WriteAllText(
        $mrBodyFile,
        $payload,
        (New-Object System.Text.UTF8Encoding $false)
    )

    Write-Host ""
    Write-Host "▶ POST MR  $branch → dev"
    $mrResp = & curl.exe --silent --show-error --fail --request POST `
        --header "PRIVATE-TOKEN: $env:GITLAB_TOKEN" `
        --header "Content-Type: application/json" `
        --data-binary "@$mrBodyFile" `
        "$gitlabApi/merge_requests"
    if ($LASTEXITCODE -ne 0) { throw "MR 생성 실패 (exit=$LASTEXITCODE)" }
    $mr = $mrResp | ConvertFrom-Json
    Write-Host ""
    Write-Host "✅ MR created: $($mr.web_url)"
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "===== Done. ====="
