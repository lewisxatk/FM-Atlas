@echo off
setlocal
cd /d "%~dp0"
echo ==========================================
echo     FM ATLAS COACHPRO R5 - INSTALLER
echo ==========================================

dotnet restore .\FmAtlas.sln
if errorlevel 1 goto failed

dotnet publish .\src\FmAtlas\FmAtlas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\win-x64
if errorlevel 1 goto failed

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do set "ISCC=%%I"
)
if not exist "%ISCC%" (
  echo.
  echo Inno Setup compiler (ISCC.exe) was not found.
  echo Install Inno Setup 6, then run this file again.
  echo The self-contained app is already in .\publish\win-x64\FmAtlas.exe
  goto failed
)

if not exist .\installer-output mkdir .\installer-output
"%ISCC%" .\Installer\FM-Atlas-CoachPro.iss
if errorlevel 1 goto failed

echo.
echo Installer created: .\installer-output\FM-Atlas-CoachPro-Setup-5.0.0.exe
exit /b 0

:failed
echo.
echo BUILD OR INSTALLER FAILED. Review the messages above.
exit /b 1
