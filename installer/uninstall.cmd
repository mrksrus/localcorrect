@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0LocalCorrect.exe" (
  echo LocalCorrect.exe was not found next to this script.
  pause
  exit /b 1
)
"%~dp0LocalCorrect.exe" --uninstall
if errorlevel 1 (
  echo.
  echo LocalCorrect did not finish uninstalling.
  pause
  exit /b 1
)
exit /b 0
