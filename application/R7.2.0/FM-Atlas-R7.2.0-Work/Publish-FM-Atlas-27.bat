@echo off
setlocal EnableExtensions
cd /d "%~dp0"
for /f %%A in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set STAMP=%%A
if not exist publish-logs mkdir publish-logs
set "LOG=publish-logs\FM-Atlas-publish-%STAMP%.log"
set "LATEST=publish-logs\latest-publish.log"
(
 echo ================================================
 echo        FM ATLAS 27 - WINDOWS x64 PUBLISH
 echo ================================================
 echo Started: %DATE% %TIME%
 echo Folder: %CD%
 echo.
) > "%LOG%"
where dotnet >> "%LOG%" 2>&1
if errorlevel 1 (
 echo .NET SDK was not found. Install .NET 8 SDK for Windows.>> "%LOG%"
 goto failed
)
dotnet --info >> "%LOG%" 2>&1
echo.>> "%LOG%"
echo [1/2] RESTORE>> "%LOG%"
dotnet restore .\FmAtlas.sln >> "%LOG%" 2>&1
if errorlevel 1 goto failed
echo.>> "%LOG%"
echo [2/2] SELF-CONTAINED WINDOWS x64 PUBLISH>> "%LOG%"
dotnet publish .\src\FmAtlas\FmAtlas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\win-x64 >> "%LOG%" 2>&1
if errorlevel 1 goto failed
(
 echo.
 echo PUBLISH SUCCEEDED.
 echo Published executable: .\publish\win-x64\FmAtlas.exe
 echo Finished: %DATE% %TIME%
) >> "%LOG%"
copy /y "%LOG%" "%LATEST%" >nul
cls
type "%LOG%"
echo.
echo PUBLISH SUCCEEDED - publish\win-x64\FmAtlas.exe
echo Full log: %CD%\%LOG%
echo Latest copy: %CD%\%LATEST%
echo Press C to copy the publish log to clipboard, or any other key to close.
choice /c CN /n /m "Copy log? [C/N] "
if errorlevel 2 goto success_end
powershell -NoProfile -Command "Get-Content -Raw '%CD%\%LATEST%' | Set-Clipboard"
echo Publish log copied to clipboard.
:success_end
pause
exit /b 0
:failed
(
 echo.
 echo PUBLISH FAILED.
 echo Finished: %DATE% %TIME%
) >> "%LOG%"
copy /y "%LOG%" "%LATEST%" >nul
cls
type "%LOG%"
echo.
echo PUBLISH FAILED - SEND publish-logs\latest-publish.log
echo Press C to copy the complete publish log to clipboard, or any other key to close.
choice /c CN /n /m "Copy log? [C/N] "
if errorlevel 2 goto failed_end
powershell -NoProfile -Command "Get-Content -Raw '%CD%\%LATEST%' | Set-Clipboard"
echo Publish log copied to clipboard.
:failed_end
pause
exit /b 1
