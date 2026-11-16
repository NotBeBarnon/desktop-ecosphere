@echo off
cd /d "%~dp0"
if not exist "dist\DesktopEcosphere-v0.6\DesktopEcosphere.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0windows\build.ps1"
  if errorlevel 1 (pause & exit /b 1)
)
start "" "%~dp0dist\DesktopEcosphere-v0.6\DesktopEcosphere.exe"
