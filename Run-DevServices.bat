@echo off
setlocal enabledelayedexpansion

rem Starts both halves of Numo.DevServices.App for someone who just wants to run it,
rem without needing Rider or a terminal habit. Safe to double-click.

set "ROOT=%~dp0"
set "API_DIR=%ROOT%src\Numo.DevServices.Api"
set "CLIENT_DIR=%API_DIR%\ClientApp"

echo ============================================================
echo  Numo Dev Services - local launcher
echo ============================================================
echo.

if not exist "%API_DIR%\appsettings.Development.json" (
    echo No appsettings.Development.json found - copying the template.
    copy "%API_DIR%\appsettings.Development.Template.json" "%API_DIR%\appsettings.Development.json" >nul
    echo.
    echo A new appsettings.Development.json was created from the template.
    echo It expects Postgres on localhost:5432 with user hor4_db_user / password hor4_db_psw.
    echo If your Postgres setup is different, edit that file before continuing.
    echo.
    pause
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: 'dotnet' was not found on PATH. Install the .NET SDK first:
    echo   https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
    echo ERROR: 'npm' was not found on PATH. Install Node.js first:
    echo   https://nodejs.org/
    echo.
    pause
    exit /b 1
)

if not exist "%CLIENT_DIR%\node_modules" (
    echo Frontend dependencies are not installed yet - this only happens once.
    echo Running npm install, this can take a few minutes...
    call npm --prefix "%CLIENT_DIR%" install
    if errorlevel 1 (
        echo ERROR: npm install failed - see the messages above.
        pause
        exit /b 1
    )
)

echo Starting the backend...
start "Numo Dev Services - API" cmd /k "cd /d "%ROOT%" && dotnet run --project "%API_DIR%""

echo Starting the frontend...
start "Numo Dev Services - Frontend" cmd /k "cd /d "%CLIENT_DIR%" && npm run serve"

echo.
echo Both windows are starting up - the first request can take a little while.
echo This window will open your browser to the app once it looks ready.
echo Closing either of the other two windows stops that half of the app.
echo.

rem Give the frontend a head start before trying to open it - it takes longer than the API.
timeout /t 20 /nobreak >nul
start "" "http://localhost:4200/app/dev-services/"

echo Done. You can close this window; the other two keep the app running.
pause
