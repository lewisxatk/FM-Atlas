@echo off
setlocal
cd /d "%~dp0"
echo ==========================================
echo    FM ATLAS COACHPRO R5 - SELF CONTAINED
echo ==========================================
dotnet restore .\FmAtlas.sln
if errorlevel 1 goto failed
dotnet publish .\src\FmAtlas\FmAtlas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\win-x64
if errorlevel 1 goto failed
echo Published app: .\publish\win-x64\FmAtlas.exe
echo.
echo This is a self-contained app build. It includes the .NET runtime.
pause
exit /b 0
:failed
echo PUBLISH FAILED - copy the complete error output.
pause
exit /b 1
