# FM Atlas test-build launcher. Keep this file ASCII-only for Windows PowerShell 5.1 compatibility.
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot 'Build-Test-Package.ps1'
$artifactsRoot = Join-Path $repoRoot 'artifacts'

if (-not (Test-Path -LiteralPath $buildScript)) {
    Write-Host 'ERROR: Build-Test-Package.ps1 was not found.' -ForegroundColor Red
    Write-Host 'Please extract the complete FM Atlas ZIP before running this file.'
    exit 1
}

$startedAt = Get-Date
Write-Host 'FM Atlas - building and launching the Windows test version' -ForegroundColor Cyan
Write-Host ('Repository: ' + $repoRoot)

try {
    & $buildScript -Configuration $Configuration
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw ('Build/package script returned exit code ' + $LASTEXITCODE + '.')
    }

    if (-not (Test-Path -LiteralPath $artifactsRoot)) {
        throw 'The artifacts folder was not created.'
    }

    $publish = Get-ChildItem -LiteralPath $artifactsRoot -Directory -Filter 'FMAtlas-win-x64-*' |
        Where-Object { $_.LastWriteTime -ge $startedAt.AddMinutes(-1) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $publish) {
        throw 'The build completed without a new publish directory. Check artifacts\logs.'
    }

    $exe = Join-Path $publish.FullName 'FMAtlas.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw 'FMAtlas.exe was not produced. Check the build logs under artifacts\logs.'
    }

    Write-Host ''
    Write-Host 'Build and tests passed. Launching FM Atlas...' -ForegroundColor Green
    Write-Host ('Application: ' + $exe)
    Start-Process -FilePath $exe -WorkingDirectory $publish.FullName
}
catch {
    Write-Host ''
    Write-Host ('BUILD FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'No new test package should be used. Review artifacts\logs and artifacts\test-results.' -ForegroundColor Yellow
    exit 1
}
