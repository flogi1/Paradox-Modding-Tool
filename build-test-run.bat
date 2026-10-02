@echo off
setlocal enabledelayedexpansion

rem Wechselt immer in den Ordner, in dem diese Datei liegt (Projekt-Hauptordner),
rem egal von wo aus die .bat gestartet wird.
cd /d "%~dp0"

echo ============================================
echo  Paradox Modding Tool - Build, Test, Start
echo ============================================
echo.

rem Je nach installierter .NET-SDK-Version erzeugt "dotnet new sln" entweder
rem die klassische ParadoxModTool.sln oder das neuere ParadoxModTool.slnx -
rem daher hier auf beide Namen pruefen statt einen fest anzunehmen (wie in build.bat).
set "SLNFILE="
if exist "ParadoxModTool.sln" set "SLNFILE=ParadoxModTool.sln"
if exist "ParadoxModTool.slnx" set "SLNFILE=ParadoxModTool.slnx"

if not defined SLNFILE (
    echo Projektmappendatei fehlt noch - wird einmalig erzeugt...
    powershell -NoProfile -ExecutionPolicy Bypass -File "setup-solution.ps1"
    if errorlevel 1 (
        echo.
        echo FEHLER beim Erzeugen der Solution-Datei - siehe Ausgabe oben.
        pause
        exit /b 1
    )
    echo.
    if exist "ParadoxModTool.sln" set "SLNFILE=ParadoxModTool.sln"
    if exist "ParadoxModTool.slnx" set "SLNFILE=ParadoxModTool.slnx"
)

if not defined SLNFILE (
    echo.
    echo FEHLER: Weder ParadoxModTool.sln noch ParadoxModTool.slnx wurde gefunden,
    echo obwohl setup-solution.ps1 durchgelaufen ist. Bitte Ausgabe oben pruefen.
    pause
    exit /b 1
)

echo Verwende Projektmappe: !SLNFILE!
echo.

rem Stellt sicher, dass auch neu hinzugekommene Projekte (z. B. ParadoxModTool.Notes ab Runde 4)
rem in einer BEREITS VORHANDENEN Projektmappendatei eingebunden sind - siehe build.bat für die
rem ausführliche Begründung.
echo Stelle sicher, dass alle Projekte in der Projektmappe eingebunden sind...
dotnet sln "!SLNFILE!" add "src\ParadoxModTool.Notes\ParadoxModTool.Notes.csproj" >nul
dotnet sln "!SLNFILE!" add "tests\ParadoxModTool.Notes.Tests\ParadoxModTool.Notes.Tests.csproj" >nul
rem ParadoxModTool.Tools.Tests (Runde 7, Formatierungs-Werkzeug) - ParadoxModTool.Tools selbst
rem stand bereits in der urspruenglichen Liste von setup-solution.ps1.
dotnet sln "!SLNFILE!" add "tests\ParadoxModTool.Tools.Tests\ParadoxModTool.Tools.Tests.csproj" >nul
rem ParadoxModTool.LaunchControl.Tests (Runde 8, Spiel starten) - ParadoxModTool.LaunchControl
rem selbst stand bereits in der urspruenglichen Liste von setup-solution.ps1.
dotnet sln "!SLNFILE!" add "tests\ParadoxModTool.LaunchControl.Tests\ParadoxModTool.LaunchControl.Tests.csproj" >nul
rem ParadoxModTool.Explorer/.Explorer.Tests (Runde 10, Verzeichnis-Übersicht) - beide Projekte
rem sind komplett neu und standen noch in keiner bisherigen Projektmappe.
dotnet sln "!SLNFILE!" add "src\ParadoxModTool.Explorer\ParadoxModTool.Explorer.csproj" >nul
dotnet sln "!SLNFILE!" add "tests\ParadoxModTool.Explorer.Tests\ParadoxModTool.Explorer.Tests.csproj" >nul
rem ParadoxModTool.Desync/.Desync.Tests (Runde 10.1, Desync-Analyse) - beide Projekte
rem sind komplett neu und standen noch in keiner bisherigen Projektmappe.
dotnet sln "!SLNFILE!" add "src\ParadoxModTool.Desync\ParadoxModTool.Desync.csproj" >nul
dotnet sln "!SLNFILE!" add "tests\ParadoxModTool.Desync.Tests\ParadoxModTool.Desync.Tests.csproj" >nul
echo.

echo [1/3] dotnet clean ...
dotnet clean "!SLNFILE!"
if errorlevel 1 (
    echo.
    echo FEHLER beim "dotnet clean" - siehe Ausgabe oben.
    pause
    exit /b 1
)

echo.
echo [2/3] dotnet build ...
dotnet build "!SLNFILE!"
if errorlevel 1 (
    echo.
    echo FEHLER beim Build - siehe Ausgabe oben.
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "create-shortcut.ps1"

echo.
echo [3/3] Alle Tests ...
set FAILED=0
call :run_test tests\ParadoxModTool.ErrorLog.Tests
call :run_test tests\ParadoxModTool.VersionManager.Tests
call :run_test tests\ParadoxModTool.VanillaDiff.Tests
call :run_test tests\ParadoxModTool.ModCompat.Tests
call :run_test tests\ParadoxModTool.UndoRedo.Tests
call :run_test tests\ParadoxModTool.Session.Tests
call :run_test tests\ParadoxModTool.SearchReplace.Tests
call :run_test tests\ParadoxModTool.Notes.Tests
call :run_test tests\ParadoxModTool.Tools.Tests
call :run_test tests\ParadoxModTool.LaunchControl.Tests
call :run_test tests\ParadoxModTool.Explorer.Tests
call :run_test tests\ParadoxModTool.Desync.Tests

if "%FAILED%"=="1" (
    echo.
    echo ============================================
    echo  FEHLER: Mindestens ein Testprojekt ist fehlgeschlagen - siehe Ausgabe oben.
    echo ============================================
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Build und Tests erfolgreich - Programm wird gestartet...
echo ============================================
echo.
dotnet run --project src\ParadoxModTool.App
if errorlevel 1 (
    echo.
    echo ============================================
    echo  Das Programm wurde mit einem Fehler beendet - siehe Ausgabe oben.
    echo ============================================
    pause
    exit /b 1
)

rem Kein "pause" hier: lief alles fehlerfrei durch (Build, Tests, Programmstart samt normalem
rem Beenden), darf sich das Konsolenfenster automatisch schliessen (Nutzer-Feedback: "sonst
rem kann das Konsolenfenster automatisch geschlossen werden, wenn alles durchlaeuft").
exit /b 0

:run_test
echo.
echo --- dotnet test %1 ---
dotnet test %1
if errorlevel 1 set FAILED=1
exit /b 0
