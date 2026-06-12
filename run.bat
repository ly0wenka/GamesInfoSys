@echo off
setlocal

cd /d "%~dp0GamesInfoSys"

set "PORT=5112"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$port = 5112; while (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { $port++ }; [Console]::Write($port)" > "%TEMP%\\gamesinfosys-port.txt"

set /p PORT=<"%TEMP%\\gamesinfosys-port.txt"
del "%TEMP%\\gamesinfosys-port.txt" >nul 2>&1

echo Starting GamesInfoSys on http://localhost:%PORT%
dotnet run --no-launch-profile --urls "http://localhost:%PORT%"
