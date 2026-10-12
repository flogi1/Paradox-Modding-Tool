@echo off
setlocal

cd /d "%~dp0"

echo ============================================
echo  Paradox Modding Tool - Alle Tests
echo ============================================
echo.

set FAILED=0

call :run_test tests\ParadoxModTool.ErrorLog.Tests
call :run_test tests\ParadoxModTool.VersionManager.Tests
call :run_test tests\ParadoxModTool.VanillaDiff.Tests
call :run_test tests\ParadoxModTool.ModCompat.Tests
call :run_test tests\ParadoxModTool.UndoRedo.Tests
call :run_test tests\ParadoxModTool.Session.Tests
call :run_test tests\ParadoxModTool.Localization.Tests
call :run_test tests\ParadoxModTool.Settings.Tests
call :run_test tests\ParadoxModTool.SettingsUi.Tests
call :run_test tests\ParadoxModTool.Shell.Tests
call :run_test tests\ParadoxModTool.Editor.Tests
call :run_test tests\ParadoxModTool.SearchReplace.Tests
call :run_test tests\ParadoxModTool.Notes.Tests
call :run_test tests\ParadoxModTool.BugTracking.Tests
call :run_test tests\ParadoxModTool.Tools.Tests
call :run_test tests\ParadoxModTool.LaunchControl.Tests
call :run_test tests\ParadoxModTool.Explorer.Tests
call :run_test tests\ParadoxModTool.Desync.Tests
call :run_test tests\ParadoxModTool.GuiDesign.Tests
call :run_test tests\ParadoxModTool.Updates.Tests
call :run_test tests\ParadoxModTool.Generators.Tests

echo.
if "%FAILED%"=="1" (
    echo ============================================
    echo  Mindestens ein Testprojekt ist fehlgeschlagen - siehe oben.
    echo ============================================
) else (
    echo ============================================
    echo  Alle Testprojekte erfolgreich.
    echo ============================================
)
pause
exit /b 0

:run_test
echo.
echo --- dotnet test %1 ---
dotnet test %1
if errorlevel 1 set FAILED=1
exit /b 0
