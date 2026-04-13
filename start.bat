@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"
set "DOCKER_DESKTOP_URL=https://docs.docker.com/desktop/setup/install/windows-install/"

where docker >nul 2>&1
if errorlevel 1 (
  echo Docker Desktop is not installed or docker.exe is not on PATH.
  echo Download and install Docker Desktop here:
  echo   %DOCKER_DESKTOP_URL%
  if /I not "%KOTOBA_COLISEUM_SKIP_HELP_LINK%"=="1" start "" "%DOCKER_DESKTOP_URL%" >nul 2>&1
  exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
  echo Docker Desktop is installed but the Docker engine is not ready.
  echo Start Docker Desktop, wait until the engine is running, and try again.
  if /I not "%KOTOBA_COLISEUM_SKIP_HELP_LINK%"=="1" start "" "%DOCKER_DESKTOP_URL%" >nul 2>&1
  exit /b 1
)

if not exist ".env" (
  copy ".env.example" ".env" >nul
  echo Created .env from .env.example.
)

set "WEB_PORT=19120"
set "HAS_WEB_PORT="
for /f "usebackq tokens=1,* delims==" %%A in (".env") do (
  if /I "%%A"=="KOTOBA_COLISEUM_WEB_PORT" (
    set "WEB_PORT=%%B"
    set "HAS_WEB_PORT=1"
  )
)

if not defined HAS_WEB_PORT (
  >> ".env" echo KOTOBA_COLISEUM_WEB_PORT=%WEB_PORT%
  echo Added KOTOBA_COLISEUM_WEB_PORT=%WEB_PORT% to .env.
)

echo Starting web container...
docker compose up --build -d
if errorlevel 1 (
  echo docker compose failed before the app became ready.
  exit /b 1
)

echo Waiting for web health check...
set /a ATTEMPT=0

:wait_loop
set /a ATTEMPT+=1
set "WEB_RUNNING="

for /f %%S in ('docker compose ps --services --status running 2^>nul') do (
  if /I "%%S"=="web" set "WEB_RUNNING=1"
)

if defined WEB_RUNNING (
  powershell -NoLogo -NoProfile -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:%WEB_PORT%/health' -UseBasicParsing -TimeoutSec 5; if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) { exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1
  if not errorlevel 1 goto ready
)

if !ATTEMPT! GEQ 45 goto failed

powershell -NoLogo -NoProfile -Command "Start-Sleep -Seconds 2" >nul 2>&1
goto wait_loop

:ready
echo KotobaColiseum is ready at http://localhost:%WEB_PORT%
if /I "%KOTOBA_COLISEUM_SKIP_BROWSER_OPEN%"=="1" exit /b 0

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\open-app-window.ps1" -Url "http://localhost:%WEB_PORT%" -Width 1280 -Height 900
exit /b %ERRORLEVEL%

:failed
echo KotobaColiseum did not become ready in time.
echo.
docker compose ps
echo.
echo Last container logs:
docker compose logs --tail 60 web
exit /b 1
