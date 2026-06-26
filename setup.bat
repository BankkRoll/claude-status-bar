@echo off
rem One-click setup for Claude Status Bar: registers the Claude Code hooks against the
rem bundled executable so the widget launches with each session.
setlocal EnableDelayedExpansion
cd /d "%~dp0"
title  Claude Status Bar  -  Setup

rem Enable ANSI colors (Windows 10+).
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
echo   %C_ORANGE%      *                                            %C_RESET%
echo   %C_ORANGE%    *   *     Claude Status Bar%C_RESET%
echo   %C_ORANGE%      *       %C_DIM%live Claude Code status, on your taskbar%C_RESET%
echo   %C_ORANGE%       *                                           %C_RESET%
echo.
echo   %C_DIM%------------------------------------------------------------%C_RESET%
echo.

rem --- Step 1: Node.js ---
echo   %C_BOLD%[1/3]%C_RESET% Checking for Node.js...
where node >nul 2>&1
if errorlevel 1 (
  echo         %C_RED%not found%C_RESET%
  echo.
  echo   %C_RED%Node.js is required to run the Claude Code hooks.%C_RESET%
  echo   Install it from %C_BOLD%https://nodejs.org%C_RESET% and run setup again.
  echo.
  goto :fail
)
for /f "delims=" %%v in ('node --version') do set "_nodever=%%v"
echo         %C_GREEN%found%C_RESET% %C_DIM%(!_nodever!)%C_RESET%

rem --- Step 2: executable present ---
echo   %C_BOLD%[2/3]%C_RESET% Checking files...
if not exist "ClaudeStatusBar.exe" (
  echo         %C_RED%ClaudeStatusBar.exe missing%C_RESET%
  echo.
  echo   %C_RED%The application file is missing.%C_RESET%
  echo   Extract the entire zip, keep the files together, then run setup again.
  echo.
  goto :fail
)
if not exist "hooks\install.js" (
  echo         %C_RED%hooks\install.js missing%C_RESET%
  echo.
  echo   %C_RED%The hook scripts are missing.%C_RESET%
  echo   Extract the entire zip, keep the files together, then run setup again.
  echo.
  goto :fail
)
echo         %C_GREEN%ok%C_RESET%

rem --- Step 3: register hooks ---
echo   %C_BOLD%[3/3]%C_RESET% Registering Claude Code hooks...
node "hooks\install.js" "%cd%\ClaudeStatusBar.exe" >nul 2>&1
if errorlevel 1 (
  echo         %C_RED%failed%C_RESET%
  echo.
  echo   %C_RED%Could not register the hooks.%C_RESET%
  echo   Make sure Claude Code is installed, then run setup again.
  echo.
  goto :fail
)
echo         %C_GREEN%done%C_RESET%

echo.
echo   %C_DIM%------------------------------------------------------------%C_RESET%
echo.
echo   %C_GREEN%%C_BOLD%  All set.%C_RESET%
echo.
echo   Start a new Claude Code session in your terminal or editor and the
echo   widget appears on your taskbar. It shows what Claude is doing and
echo   hides itself when no session is running.
echo.
echo   %C_DIM%Click the widget for options. Run uninstall.bat to remove it.%C_RESET%
echo.
pause
exit /b 0

:fail
pause
exit /b 1
