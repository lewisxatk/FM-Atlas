[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "Run this package script on Windows 10/11."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "FMAtlas.sln"
$desktopProject = Join-Path $repoRoot "src/FMAtlas.Desktop/FMAtlas.Desktop.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$logDirectory = Join-Path $artifactsRoot "logs"
$testResultsDirectory = Join-Path $artifactsRoot "test-results"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Step
    )

    $safeStep = $Step -replace '[^A-Za-z0-9]+', '-'
    $logPath = Join-Path $logDirectory "$safeStep.log"

    Write-Host ""
    Write-Host "== $Step ==" -ForegroundColor Cyan
    Write-Host "Log: $logPath" -ForegroundColor DarkGray

    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Step failed with exit code $exitCode. No package was created. See $logPath."
    }
}

if (-not (Test-Path -LiteralPath $solution)) {
    throw "FMAtlas.sln was not found. Run this script from a checked-out FM-Atlas repository."
}
if (-not (Test-Path -LiteralPath $desktopProject)) {
    throw "The WPF desktop project was not found at $desktopProject."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the .NET 8 SDK and run this script again."
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks | Where-Object { $_ -match '^8\.' })) {
    throw ".NET 8 SDK is required. Installed SDKs: $($sdks -join ', ')"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$publishDirectory = Join-Path $artifactsRoot "FMAtlas-win-x64-$stamp"
$zipPath = Join-Path $artifactsRoot "FMAtlas-win-x64-$stamp.zip"

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $testResultsDirectory -Force | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Push-Location $repoRoot
try {
    Invoke-DotNet -Step "Restore solution" -Arguments @(
        "restore", "FMAtlas.sln"
    )

    Invoke-DotNet -Step "Build solution ($Configuration)" -Arguments @(
        "build", "FMAtlas.sln", "--configuration", $Configuration, "--no-restore"
    )

    Invoke-DotNet -Step "Run automated tests ($Configuration)" -Arguments @(
        "test", "FMAtlas.sln", "--configuration", $Configuration, "--no-build",
        "--verbosity", "normal",
        "--logger", "trx;LogFileName=FMAtlas.Tests.trx",
        "--results-directory", $testResultsDirectory
    )

    Invoke-DotNet -Step "Publish self-contained Windows x64 desktop app" -Arguments @(
        "publish", "src/FMAtlas.Desktop/FMAtlas.Desktop.csproj",
        "--configuration", $Configuration,
        "--runtime", "win-x64",
        "--self-contained", "true",
        "--output", $publishDirectory
    )

    $exePath = Join-Path $publishDirectory "FMAtlas.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Publish completed but FMAtlas.exe was not found. The package was not created."
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

    $gitCommit = "not available"
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $candidateCommit = & git -C $repoRoot rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($candidateCommit)) {
            $gitCommit = $candidateCommit.Trim()
        }
    }

    $reportPath = Join-Path $artifactsRoot "FMAtlas-Test-Report-$stamp.txt"
    @"
FM ATLAS — BUILD AND TEST REPORT
Created: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")
Configuration: $Configuration
Git commit: $gitCommit
Automated restore: PASSED
Release build: PASSED
Automated tests: PASSED
Windows x64 self-contained publish: PASSED
Executable: $exePath
Package: $zipPath
Package SHA-256: $hash
Logs: $logDirectory
Test results: $testResultsDirectory

The package is self-contained and does not require a separate .NET Desktop Runtime installation.
Live FM27 connection is still limited to the documented Game Pass build and must be checked against a disposable career.
Save Live Changes remains disabled. Save Atlas writes only local FM Atlas database state.
"@ | Set-Content -LiteralPath $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "All restore, build, test and publish steps passed." -ForegroundColor Green
    Write-Host "Published app: $publishDirectory"
    Write-Host "Test package:  $zipPath"
    Write-Host "Test report:   $reportPath"
    Write-Host "SHA-256:       $hash"
    Write-Host "The package is self-contained; no separate .NET Desktop Runtime is required."
}
finally {
    Pop-Location
}
