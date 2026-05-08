<#
.SYNOPSIS
DotShelf 버전 bump 헬퍼. 한 트랙(launcher 또는 unity) 의 source of truth 와
미러 위치들을 한 번에 갱신한다.

자세한 정책: launcher/docs/versioning.md

.PARAMETER Target
"launcher" | "unity"

.PARAMETER NewVersion
SemVer (예: 0.2.0). 빌드 메타 없는 X.Y.Z 형식만 받는다.

.PARAMETER ReleaseDate
unity 트랙에서만 사용. 다운로드 페이지 RELEASE.date 에 박힌다.
미지정 시 오늘 날짜 (한국어).

.EXAMPLE
.\launcher\scripts\bump-version.ps1 -Target launcher -NewVersion 0.1.1

.EXAMPLE
.\launcher\scripts\bump-version.ps1 -Target unity -NewVersion 0.2.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet("launcher", "unity")]
    [string]$Target,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$NewVersion,

    [string]$ReleaseDate
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Edit-File([string]$Path, [string]$OldPattern, [string]$NewLine, [bool]$Regex = $false) {
    $full = Join-Path $repoRoot $Path
    if (-not (Test-Path $full)) {
        throw "파일 없음: $full"
    }
    # PS 5.1 의 Get-Content / Set-Content default 인코딩이 시스템 codepage(CP-949) 라
    # BOM 없는 UTF-8 파일(예: page.tsx) 의 한글이 깨진다. .NET API 로 BOM-less UTF-8 강제.
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $content = [System.IO.File]::ReadAllText($full, $utf8NoBom)
    if ($Regex) {
        $matchCount = ([regex]::Matches($content, $OldPattern)).Count
        if ($matchCount -lt 1) { throw "패턴 매치 0건 [$Path]: $OldPattern" }
        if ($matchCount -gt 1) { throw "패턴 매치 $matchCount 건 (1건이어야 함) [$Path]: $OldPattern" }
        $new = [regex]::Replace($content, $OldPattern, $NewLine)
    }
    else {
        if ($content -notlike "*$OldPattern*") { throw "리터럴 매치 0건 [$Path]: $OldPattern" }
        $new = $content.Replace($OldPattern, $NewLine)
    }
    [System.IO.File]::WriteAllText($full, $new, $utf8NoBom)
    Write-Host "  ✓ $Path"
}

if ($Target -eq "launcher") {
    Write-Host "=== launcher $NewVersion ==="

    $csproj = "launcher/src/DotShelf.Launcher/DotShelf.Launcher.csproj"
    Edit-File -Path $csproj -OldPattern '<Version>[\d.]+</Version>' -NewLine "<Version>$NewVersion</Version>" -Regex $true
    Edit-File -Path $csproj -OldPattern '<AssemblyVersion>[\d.]+</AssemblyVersion>' -NewLine "<AssemblyVersion>$NewVersion.0</AssemblyVersion>" -Regex $true
    Edit-File -Path $csproj -OldPattern '<FileVersion>[\d.]+</FileVersion>' -NewLine "<FileVersion>$NewVersion.0</FileVersion>" -Regex $true

    $page = "web/service-web/app/(marketing)/download/page.tsx"
    Edit-File -Path $page -OldPattern '"https://dotshelf\.digital/launcher/windows/DotShelfLauncher-[\d.]+\.exe"' -NewLine "`"https://dotshelf.digital/launcher/windows/DotShelfLauncher-$NewVersion.exe`"" -Regex $true

    Write-Host
    Write-Host "다음 단계:"
    Write-Host "  1. MR 머지 후 GitLab CI launcher:build 가 exe 를 publish"
    Write-Host "  2. launcher:publish 가 exe/latest.json/events.json/Unity zip 을 운영 경로에 업로드"
    Write-Host "  3. 옛 버전은 한동안 살려둔다 (옛 다운로드 링크 누른 사용자 대비)"
}
elseif ($Target -eq "unity") {
    Write-Host "=== unity $NewVersion ==="

    $proj = "frontend/ProjectSettings/ProjectSettings.asset"
    Edit-File -Path $proj -OldPattern '(?m)^  bundleVersion: [\d.]+$' -NewLine "  bundleVersion: $NewVersion" -Regex $true

    $manifest = "launcher/manifests/dev/windows/latest.json"
    Edit-File -Path $manifest -OldPattern '"latestVersion": "[\d.]+"' -NewLine "`"latestVersion`": `"$NewVersion`"" -Regex $true
    Edit-File -Path $manifest -OldPattern 'Dotshelf-[\d.]+\.zip' -NewLine "Dotshelf-$NewVersion.zip" -Regex $true

    $page = "web/service-web/app/(marketing)/download/page.tsx"
    Edit-File -Path $page -OldPattern 'version: "[\d.]+",' -NewLine "version: `"$NewVersion`"," -Regex $true

    if (-not $ReleaseDate) {
        $ReleaseDate = (Get-Date).ToString("yyyy년 M월 d일", [Globalization.CultureInfo]::GetCultureInfo("ko-KR"))
    }
    Edit-File -Path $page -OldPattern 'date: "[^"]+",' -NewLine "date: `"$ReleaseDate`"," -Regex $true

    Write-Host
    Write-Host "다음 단계 (자동화 권장: Unity Editor 메뉴 'Tools > DotShelf > Build & Publish Launcher Package'):"
    Write-Host "  - 자동: launcher/scripts/publish-unity.ps1 가 zip/sha256/upload/MR 까지 처리"
    Write-Host "  - 수동 fallback 절차는 launcher/AGENTS.md '수동 fallback' 섹션 참고"
    Write-Host "    (요약: zip 압축 → SHA256 → manifest sha256/releaseNotes 편집 → curl PUT → feat 브랜치 + MR)"
}

Write-Host
Write-Host "git diff 로 변경 확인 후 커밋."
