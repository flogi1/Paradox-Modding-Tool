@echo off
setlocal

cd /d "%~dp0"

echo ============================================
echo  Paradox Modding Tool - Starten
echo ============================================
echo.

dotnet run --project src\ParadoxModTool.App
if errorlevel 1 (
    echo.
    echo Das Programm wurde mit einem Fehler beendet - siehe Ausgabe oben.
    pause
    exit /b 1
)
