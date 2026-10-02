# Legt im Projekt-Hauptordner die Verknuepfung "ParadoxModTool.lnk" auf die gebaute exe an
# (Nutzerwunsch: die exe nicht erst unter src\ParadoxModTool.App\bin\... suchen muessen).
# Wird von build.bat und build-test-run.bat nach einem erfolgreichen Build aufgerufen.
$root = $PSScriptRoot
$exe = Join-Path $root 'src\ParadoxModTool.App\bin\Debug\net8.0-windows\ParadoxModTool.exe'
if (-not (Test-Path $exe)) {
    Write-Host "Verknuepfung nicht angelegt: $exe fehlt."
    exit 0
}

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $root 'ParadoxModTool.lnk'))
$link.TargetPath = $exe
$link.WorkingDirectory = Split-Path $exe
$link.IconLocation = "$exe,0"
$link.Description = 'Paradox Modding Tool'
$link.Save()
Write-Host 'Verknuepfung ParadoxModTool.lnk im Hauptordner aktualisiert.'
