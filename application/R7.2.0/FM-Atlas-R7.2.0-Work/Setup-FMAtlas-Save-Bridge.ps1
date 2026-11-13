$ErrorActionPreference = 'Stop'
Write-Host 'FM Atlas 27 - local save reader setup' -ForegroundColor Cyan
Write-Host 'Installs the read-only fmsave package for the current Windows user.'
Write-Host 'The selected save remains on your computer and is not uploaded or modified.'
if (-not (Get-Command py -ErrorAction SilentlyContinue)) {
    throw 'Python Launcher (py.exe) was not found. Install Python 3.12 or newer from https://www.python.org/downloads/windows/ and enable the Python Launcher.'
}
$version = & py -3 -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')" 2>$null
if ($LASTEXITCODE -ne 0) { throw 'No Python 3 interpreter was found. Install Python 3.12 or newer and retry.' }
$parts = $version.Trim().Split('.')
if ($parts.Count -lt 2 -or [int]$parts[0] -lt 3 -or ([int]$parts[0] -eq 3 -and [int]$parts[1] -lt 12)) {
    throw "Python $version is too old. fmsave requires Python 3.12 or newer."
}
Write-Host "Using Python $version" -ForegroundColor Gray
& py -3 -m pip install --upgrade fmsave==0.12.3
if ($LASTEXITCODE -ne 0) { throw 'Could not install fmsave. Check your internet connection and pip setup.' }
$installedFmsave = & py -3 -c "import fmsave; print(getattr(fmsave, '__version__', 'unknown'))"
if ($LASTEXITCODE -ne 0) { throw 'fmsave installed but could not be imported.' }
if ($installedFmsave.Trim() -ne '0.12.3') { throw "FM Atlas requires fmsave 0.12.3, but Python reports $installedFmsave." }
Write-Host "Verified fmsave $installedFmsave" -ForegroundColor Gray
Write-Host 'Setup complete. Restart FM Atlas and choose Import Career Save.' -ForegroundColor Green
