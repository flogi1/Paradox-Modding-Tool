@echo off
setlocal enabledelayedexpansion

rem Wechselt immer in den Ordner, in dem diese Datei liegt (Projekt-Hauptordner),
rem egal von wo aus die .bat gestartet wird.
cd /d "%~dp0"

echo ============================================
echo  Paradox Modding Tool - Clean Build
echo ============================================
echo.

rem Je nach installierter .NET-SDK-Version erzeugt "dotnet new sln" entweder
rem die klassische ParadoxModTool.sln oder das neuere ParadoxModTool.slnx -
rem daher hier auf beide Namen pruefen statt einen fest anzunehmen.
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
rem in einer BEREITS VORHANDENEN Projektmappendatei eingebunden sind - setup-solution.ps1 oben
rem wird nur einmalig beim allerersten Start ausgeführt und erzeugt keine bereits vorhandene
rem Projektmappe erneut. "dotnet sln add" ist idempotent (meldet ein bereits eingebundenes
rem Projekt nur, bricht aber nicht ab), daher hier bei JEDEM Build unbedingt erneut aufgerufen,
rem statt eine eigene Prüflogik zu benötigen.
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
echo.

echo [1/2] dotnet clean ...
dotnet clean "!SLNFILE!"
if errorlevel 1 (
    echo.
    echo FEHLER beim "dotnet clean" - siehe Ausgabe oben.
    pause
    exit /b 1
)

echo.
echo [2/2] dotnet build ...
dotnet build "!SLNFILE!"
if errorlevel 1 (
    echo.
    echo FEHLER beim Build - siehe Ausgabe oben.
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Build erfolgreich.
echo ============================================
pause
