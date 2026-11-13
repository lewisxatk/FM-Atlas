@echo off
setlocal EnableExtensions
cd /d "%~dp0"
for /f %%A in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set STAMP=%%A
if not exist build-logs mkdir build-logs
set "LOG=build-logs\FM-Atlas-build-%STAMP%.log"
set "LATEST=build-logs\latest-build.log"
(
 echo ================================================
 echo          FM ATLAS 27 - LOGGED BUILD
 echo ================================================
 echo Started: %DATE% %TIME%
 echo Folder: %CD%
 echo Target: Release / .NET 8 WPF
 echo.
) > "%LOG%"
where dotnet >> "%LOG%" 2>&1
if errorlevel 1 (
 echo .NET SDK was not found. Install the .NET 8 SDK for Windows.>> "%LOG%"
 goto failed
)
dotnet --info >> "%LOG%" 2>&1
echo.>> "%LOG%"
echo [1/2] RESTORE>> "%LOG%"
dotnet restore .\FmAtlas.sln >> "%LOG%" 2>&1
if errorlevel 1 goto failed
echo.>> "%LOG%"
echo [2/2] RELEASE BUILD>> "%LOG%"
dotnet build .\FmAtlas.sln -c Release --no-restore >> "%LOG%" 2>&1
if errorlevel 1 goto failed
(
 echo.
 echo BUILD SUCCEEDED.
 echo Executable: .\src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
 echo Finished: %DATE% %TIME%
) >> "%LOG%"
copy /y "%LOG%" "%LATEST%" >nul
cls
type "%LOG%"
echo.
echo ================================================
echo BUILD SUCCEEDED
echo ================================================
echo Full build log: %CD%\%LOG%
echo A copy is also saved as: %CD%\%LATEST%
echo Press C to copy the latest build log to clipboard, or any other key to close.
choice /c CN /n /m "Copy log? [C/N] "
if errorlevel 2 goto success_end
powershell -NoProfile -Command "Get-Content -Raw '%CD%\%LATEST%' | Set-Clipboard"
echo Build log copied to clipboard.
:success_end
pause
exit /b 0
:failed
(
 echo.
 echo BUILD FAILED.
 echo Finished: %DATE% %TIME%
) >> "%LOG%"
copy /y "%LOG%" "%LATEST%" >nul
cls
type "%LOG%"
echo.
echo ================================================
echo BUILD FAILED - SEND build-logs\latest-build.log
echo ================================================
echo Press C to copy the complete build log to clipboard, or any other key to close.
choice /c CN /n /m "Copy log? [C/N] "
if errorlevel 2 goto failed_end
powershell -NoProfile -Command "Get-Content -Raw '%CD%\%LATEST%' | Set-Clipboard"
echo Build log copied to clipboard.
:failed_end
pause
exit /b 1
