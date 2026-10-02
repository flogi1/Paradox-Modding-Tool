@echo off
rem Baut das Release-Werkzeug nach release-tool\PMT-Release.exe (siehe docs/superpowers/specs/2026-09-26-release-tool-design.md).
cd /d "%~dp0"
dotnet publish tools\ParadoxModTool.Release -c Release -r win-x64 --self-contained false -o release-tool
if errorlevel 1 (pause & exit /b 1)
echo Fertig: release-tool\PMT-Release.exe
pause
