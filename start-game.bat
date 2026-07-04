@echo off
rem Idle Grounds — one-click local test (Windows).
rem Pulls the latest build from git, starts the node server in its own
rem window, and opens the game in the default browser.
cd /d "%~dp0"

echo Pulling latest build...
git fetch origin
git checkout claude/idle-grounds-setup-pracof
git pull origin claude/idle-grounds-setup-pracof

echo Starting server...
start "Idle Grounds server (close to stop)" node server.js
timeout /t 1 >nul
start "" http://localhost:5174

echo Game opened at http://localhost:5174 - close the server window to stop.
pause
