@echo off
setlocal
cd /d "%~dp0"
echo ==========================================
echo       FM ATLAS COACHPRO R5 - BUILD
echo ==========================================
dotnet restore .\FmAtlas.sln
if errorlevel 1 goto failed
dotnet build .\FmAtlas.sln -c Release
if errorlevel 1 goto failed
echo.
echo Build completed. Executable:
echo .\src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
pause
exit /b 0
:failed
echo.
echo BUILD FAILED - copy the complete error output.
pause
exit /b 1
