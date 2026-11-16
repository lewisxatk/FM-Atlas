[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "Build-Test-Package.ps1"
$artifactsRoot = Join-Path $repoRoot "artifacts"

if (-not (Test-Path -LiteralPath $buildScript)) {
    throw "Build-Test-Package.ps1 was not found. Run this from a complete FM-Atlas checkout."
}

$startedAt = Get-Date
Write-Host "FM Atlas — building and launching the Windows test version" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"

try {
    & $buildScript -Configuration $Configuration
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Build/package script returned exit code $LASTEXITCODE."
    }

    $publish = Get-ChildItem -LiteralPath $artifactsRoot -Directory -Filter "FMAtlas-win-x64-*" |
        Where-Object { $_.LastWriteTime -ge $startedAt.AddMinutes(-1) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $publish) {
        throw "The build completed without a new publish directory. Check artifacts\logs."
    }

    $exe = Join-Path $publish.FullName "FMAtlas.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "FMAtlas.exe was not produced. Check the build logs under artifacts\logs."
    }

    Write-Host ""
    Write-Host "Build and tests passed. Launching FM Atlas..." -ForegroundColor Green
    Write-Host "Application: $exe"
    Start-Process -FilePath $exe -WorkingDirectory $publish.FullName
}
catch {
    Write-Error $_
    Write-Host ""
    Write-Host "No new test package should be used. Review artifacts\logs and artifacts\test-results." -ForegroundColor Yellow
    exit 1
}
