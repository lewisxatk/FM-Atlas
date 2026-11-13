@echo off
setlocal
cd /d "%~dp0"
dotnet build .\FmAtlas.sln -c Release
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Your existing FM Atlas installation and database have not been changed.
  pause
  exit /b 1
)
echo.
echo BUILD SUCCEEDED.
echo Launch: src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
pause
