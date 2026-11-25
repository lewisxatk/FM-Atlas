@echo off
setlocal
title FM Atlas - Build and Launch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-FMAtlas-TestBuild.ps1"
if errorlevel 1 (
  echo.
  echo FM Atlas could not be built. Check the logs in the artifacts folder.
  pause
  exit /b 1
)
echo.
echo FM Atlas build passed and the application was launched.
pause
