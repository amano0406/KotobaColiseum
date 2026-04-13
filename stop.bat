@echo off
setlocal EnableExtensions

cd /d "%~dp0"

where docker >nul 2>&1
if errorlevel 1 (
  echo docker.exe was not found on PATH.
  exit /b 1
)

docker compose down --remove-orphans
exit /b %ERRORLEVEL%
