@echo off
setlocal
cd /d "%~dp0"
echo ================================================
echo       FM ATLAS COACHPRO R6 - WINDOWS x64
echo ================================================
where dotnet >nul 2>nul
if errorlevel 1 goto failed
dotnet restore .\FmAtlas.sln
if errorlevel 1 goto failed
dotnet publish .\src\FmAtlas\FmAtlas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\win-x64
if errorlevel 1 goto failed
echo.
echo Published executable: .\publish\win-x64\FmAtlas.exe
echo This is a self-contained Windows x64 build.
pause
exit /b 0
:failed
echo.
echo PUBLISH FAILED. Copy the complete output from this window.
pause
exit /b 1
