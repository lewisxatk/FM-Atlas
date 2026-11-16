[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "FMAtlas.sln"
$desktopProject = Join-Path $repoRoot "src/FMAtlas.Desktop/FMAtlas.Desktop.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Step
    )

    Write-Host ""
    Write-Host "== $Step ==" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE. No package was created."
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
    throw "The .NET 8 SDK is required. Installed SDKs: $($sdks -join ', ')"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$publishDirectory = Join-Path $artifactsRoot "FMAtlas-win-x64-$stamp"
$zipPath = Join-Path $artifactsRoot "FMAtlas-win-x64-$stamp.zip"

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

Push-Location $repoRoot
try {
    Invoke-DotNet -Step "Restore solution" -Arguments @(
        "restore", "FMAtlas.sln"
    )

    Invoke-DotNet -Step "Build solution ($Configuration)" -Arguments @(
        "build", "FMAtlas.sln", "--configuration", $Configuration, "--no-restore"
    )

    Invoke-DotNet -Step "Run automated tests ($Configuration)" -Arguments @(
        "test", "FMAtlas.sln", "--configuration", $Configuration, "--no-build", "--verbosity", "normal"
    )

    Invoke-DotNet -Step "Publish Windows x64 desktop app" -Arguments @(
        "publish", "src/FMAtlas.Desktop/FMAtlas.Desktop.csproj",
        "--configuration", $Configuration,
        "--runtime", "win-x64",
        "--self-contained", "false",
        "--output", $publishDirectory
    )

    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory "FMAtlas.exe"))) {
        throw "Publish completed but FMAtlas.exe was not found. The package was not created."
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

    Write-Host ""
    Write-Host "All restore, build and test steps passed." -ForegroundColor Green
    Write-Host "Published app: $publishDirectory"
    Write-Host "Test package:  $zipPath"
    Write-Host "This package is framework-dependent and requires the .NET 8 Windows Desktop Runtime."
}
finally {
    Pop-Location
}
