@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Publish-FM-Atlas-27.bat"
if errorlevel 1 exit /b 1
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do set "ISCC=%%I"
if not exist "%ISCC%" (echo Inno Setup 6 was not found. Published app is in .\publish\win-x64.& pause& exit /b 1)
if not exist .\installer-output mkdir .\installer-output
"%ISCC%" .\Installer\FM-Atlas-CoachPro.iss
if errorlevel 1 exit /b 1
echo Installer: .\installer-output\FM-Atlas-27-Setup-7.0.0.exe
pause
