@echo off
cd /d "%~dp0"
if not exist build-logs mkdir build-logs
start "" explorer.exe "%CD%\build-logs"
