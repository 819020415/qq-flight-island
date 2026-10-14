@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Internet.ps1" -PublicAddress "127.0.0.1"
pause