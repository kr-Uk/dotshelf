[CmdletBinding()]
param(
    [switch]$Force,
    [ValidateSet("Junction", "Copy")]
    [string]$Mode = "Junction"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendRoot = Split-Path -Parent $scriptDir
$sourceRoot = Join-Path $backendRoot ".claude\skills"
$targetRoot = Join-Path $env:USERPROFILE ".codex\skills"

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Source skill directory was not found: $sourceRoot"
}

if (-not (Test-Path -LiteralPath $targetRoot)) {
    New-Item -ItemType Directory -Path $targetRoot | Out-Null
}

$skillDirs = Get-ChildItem -LiteralPath $sourceRoot -Directory | Sort-Object Name
if (-not $skillDirs) {
    throw "No skill directories were found under $sourceRoot"
}

foreach ($skillDir in $skillDirs) {
    $skillFile = Join-Path $skillDir.FullName "SKILL.md"
    if (-not (Test-Path -LiteralPath $skillFile)) {
        Write-Warning "Skipping $($skillDir.Name) because SKILL.md is missing."
        continue
    }

    $targetPath = Join-Path $targetRoot $skillDir.Name
    if (Test-Path -LiteralPath $targetPath) {
        if (-not $Force) {
            Write-Warning "Skipping $($skillDir.Name) because target already exists: $targetPath"
            continue
        }

        Remove-Item -LiteralPath $targetPath -Recurse -Force
    }

    if ($Mode -eq "Copy") {
        Copy-Item -LiteralPath $skillDir.FullName -Destination $targetPath -Recurse
        Write-Host "Copied  $($skillDir.Name) -> $targetPath"
        continue
    }

    New-Item -ItemType Junction -Path $targetPath -Target $skillDir.FullName | Out-Null
    Write-Host "Linked  $($skillDir.Name) -> $targetPath"
}

Write-Host ""
Write-Host "Codex skill installation completed."
Write-Host "Source: $sourceRoot"
Write-Host "Target: $targetRoot"
Write-Host "Restart Codex or open a new thread to refresh the discovered skill list."
