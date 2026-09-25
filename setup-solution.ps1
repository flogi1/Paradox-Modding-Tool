# Einmalig ausführen, um ParadoxModTool.sln zu erzeugen und alle Projekte
# einzubinden. Vermeidet eine handgeschriebene .sln-Datei, deren Syntax in
# der Cloud-Umgebung, in der dieses Gerüst entstanden ist, nicht gegen die
# dotnet-CLI geprüft werden konnte.
#
# Voraussetzung: .NET 8 SDK installiert (prüfen mit: dotnet --version)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet new sln -n ParadoxModTool --force

$projects = @(
    "src/ParadoxModTool.App/ParadoxModTool.App.csproj",
    "src/ParadoxModTool.Editor/ParadoxModTool.Editor.csproj",
    "src/ParadoxModTool.GameProfiles/ParadoxModTool.GameProfiles.csproj",
    "src/ParadoxModTool.Syntax/ParadoxModTool.Syntax.csproj",
    "src/ParadoxModTool.ErrorLog/ParadoxModTool.ErrorLog.csproj",
    "src/ParadoxModTool.VersionManager/ParadoxModTool.VersionManager.csproj",
    "src/ParadoxModTool.VanillaDiff/ParadoxModTool.VanillaDiff.csproj",
    "src/ParadoxModTool.ModCompat/ParadoxModTool.ModCompat.csproj",
    "src/ParadoxModTool.GitIntegration/ParadoxModTool.GitIntegration.csproj",
    "src/ParadoxModTool.SteamIntegration/ParadoxModTool.SteamIntegration.csproj",
    "src/ParadoxModTool.LaunchControl/ParadoxModTool.LaunchControl.csproj",
    "src/ParadoxModTool.Tools/ParadoxModTool.Tools.csproj",
    "src/ParadoxModTool.Localization/ParadoxModTool.Localization.csproj",
    "src/ParadoxModTool.UndoRedo/ParadoxModTool.UndoRedo.csproj",
    "src/ParadoxModTool.Session/ParadoxModTool.Session.csproj",
    "src/ParadoxModTool.SearchReplace/ParadoxModTool.SearchReplace.csproj",
    "src/ParadoxModTool.Notes/ParadoxModTool.Notes.csproj",
    "tests/ParadoxModTool.ErrorLog.Tests/ParadoxModTool.ErrorLog.Tests.csproj",
    "tests/ParadoxModTool.VersionManager.Tests/ParadoxModTool.VersionManager.Tests.csproj",
    "tests/ParadoxModTool.VanillaDiff.Tests/ParadoxModTool.VanillaDiff.Tests.csproj",
    "tests/ParadoxModTool.ModCompat.Tests/ParadoxModTool.ModCompat.Tests.csproj",
    "tests/ParadoxModTool.UndoRedo.Tests/ParadoxModTool.UndoRedo.Tests.csproj",
    "tests/ParadoxModTool.Session.Tests/ParadoxModTool.Session.Tests.csproj",
    "tests/ParadoxModTool.SearchReplace.Tests/ParadoxModTool.SearchReplace.Tests.csproj",
    "tests/ParadoxModTool.Notes.Tests/ParadoxModTool.Notes.Tests.csproj"
)

foreach ($p in $projects) {
    dotnet sln add $p
}

Write-Host ""
Write-Host "Solution erstellt. Weiter mit: dotnet build"
