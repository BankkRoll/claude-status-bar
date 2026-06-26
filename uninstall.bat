@echo off
rem Removes the Claude Code hooks and stops the running widget.
setlocal EnableDelayedExpansion
cd /d "%~dp0"
title  Claude Status Bar  -  Uninstall

set "ESC="
for /f %%e in ('"prompt $E& for %%a in (1) do rem"') do set "ESC=%%e"
set "C_DIM=%ESC%[90m"
set "C_ORANGE=%ESC%[38;2;217;119;87m"
set "C_GREEN=%ESC%[92m"
set "C_RED=%ESC%[91m"
set "C_BOLD=%ESC%[1m"
set "C_RESET=%ESC%[0m"

cls
echo.
echo   %C_ORANGE%  Claude Status Bar%C_RESET%  %C_DIM%- Uninstall%C_RESET%
echo.
echo   %C_DIM%------------------------------------------------------------%C_RESET%
echo.

where node >nul 2>&1
if errorlevel 1 (
  echo   %C_RED%Node.js was not found, so the hooks cannot be removed automatically.%C_RESET%
  echo.
  echo   Install Node.js from %C_BOLD%https://nodejs.org%C_RESET% and run this again, or remove
  echo   the status-bar hooks from %%USERPROFILE%%\.claude\settings.json by hand.
  echo.
  pause
  exit /b 1
)

echo   Removing hooks and stopping the widget...
node "hooks\uninstall.js" >nul 2>&1
echo   %C_GREEN%done%C_RESET%
echo.
echo   %C_DIM%------------------------------------------------------------%C_RESET%
echo.
echo   %C_GREEN%%C_BOLD%  Removed.%C_RESET%  You can now delete this folder.
echo.
pause
exit /b 0
