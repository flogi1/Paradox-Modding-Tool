@echo off
rem Startet release.ps1 (PMT-Release). Parameter werden durchgereicht, z. B. release.bat -LocalFeed C:\pmt-feed
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "release.ps1" %*
pause
