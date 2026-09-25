# PMT-Release (siehe docs/superpowers/specs/2026-09-25-self-update-design.md, Abschnitt "release.bat").
# Ohne Parameter: echtes Release auf GitHub. Mit -LocalFeed <Ordner>: Debug-Paket in einen lokalen Test-Feed, ohne Git und ohne Upload.
param([string]$LocalFeed)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csproj = Join-Path $PSScriptRoot 'src\ParadoxModTool.App\ParadoxModTool.App.csproj'
$signer = 'tools/ParadoxModTool.ReleaseSigner/ParadoxModTool.ReleaseSigner.csproj'
$keyFile = Join-Path $env:APPDATA 'ParadoxModTool-Release\release-key.p8'
$repo = 'flogi1/Paradox-Modding-Tool'
# Öffentlicher Verlauf: ein Commit pro Release, verborgene GitHub-Adresse, ohne docs/. Der lokale master (voller Verlauf) wird nie gepusht.
$publicName = 'Florian'
$publicEmail = '152910095+flogi1@users.noreply.github.com'
$publicExclude = @('docs')
$local = [bool]$LocalFeed
$failed = $false
$config = if ($local) { 'Debug' } else { 'Release' }
$csprojChanged = $false

# Jeder Fehler wirft; das finally am Ende nimmt eine noch nicht committete Versionsänderung zurück.
function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

# Nur für Prüfungen, deren Ausgabe egal ist: PowerShell 5.1 wirft unter 'Stop', sobald ein Programm bei umgeleiteter Ausgabe
# etwas auf stderr schreibt (gh auth status tut das auch bei Erfolg). Liefert den Exitcode.
function Invoke-Quiet([scriptblock]$command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $command *> $null } finally { $ErrorActionPreference = $previous }
    return $LASTEXITCODE
}

# gh.exe in derselben Reihenfolge wie PMT (GhLocator): PATH, Programme\GitHub CLI, zuletzt das von PMT selbst heruntergeladene gh.
function Find-Gh {
    $onPath = Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($onPath) { return $onPath.Source }
    foreach ($candidate in @(
            (Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'),
            (Join-Path $env:APPDATA 'ParadoxModTool\tools\gh\bin\gh.exe'))) {
        if (Test-Path $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}

function Invoke-Signer([string[]]$arguments) {
    Invoke-Checked "ReleaseSigner $($arguments[0])" { dotnet run --project $signer -c Release --no-launch-profile -- @arguments }
}

try {
    # 1. Vorbedingungen
    if (-not (Test-Path $keyFile)) { throw "Schlüsseldatei $keyFile fehlt. Erst: dotnet run --project $signer -- keygen" }
    if (-not $local) {
        if ((git branch --show-current) -ne 'master') { throw 'Releases nur von master.' }
        if (git status --porcelain) { throw 'Arbeitsbaum ist nicht sauber. Erst committen oder aufräumen.' }
        if ((Invoke-Quiet { git remote get-url origin }) -ne 0) { throw "Kein Git-Remote 'origin'. Einrichten: git remote add origin https://github.com/$repo.git" }
        $gh = Find-Gh
        if (-not $gh) { throw 'GitHub CLI (gh.exe) nicht gefunden: weder im PATH noch unter Programme\GitHub CLI noch im PMT-Werkzeugordner. In PMT einmal eine Mod über GitHub veröffentlichen (lädt gh herunter) oder gh installieren.' }
        if ((Invoke-Quiet { & $gh auth status }) -ne 0) { throw "gh ist nicht angemeldet. Erst: & '$gh' auth login" }
    }

    # 2. Version
    $current = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    $version = Read-Host "Neue Version (aktuell $current)"
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version im Format x.y.z angeben.' }
    if (-not $local -and [version]$version -le [version]$current) { throw "Version muss größer als $current sein." }

    # 3. Notizen: immer als Datei an gh geben. Als Argument zerlegt PowerShell 5.1 Text mit Anführungszeichen, und leerer Text fällt weg.
    $notesFile = $null
    if (-not $local) {
        $notes = Read-Host 'Release-Notizen (Text oder Pfad zu einer Datei)'
        if ($notes -and (Test-Path $notes -PathType Leaf)) {
            $notesFile = (Resolve-Path $notes).Path
        }
        else {
            $notesFile = Join-Path $env:TEMP "pmt-release-notes-$PID.md"
            [IO.File]::WriteAllText($notesFile, [string]$notes, (New-Object Text.UTF8Encoding($false)))
        }
    }

    # 4. Version in der csproj (nur echtes Release; ohne BOM schreiben wie die Datei heute)
    if (-not $local) {
        $text = [IO.File]::ReadAllText($csproj)
        $text = $text -replace "<Version>$([regex]::Escape($current))</Version>", "<Version>$version</Version>"
        [IO.File]::WriteAllText($csproj, $text, (New-Object Text.UTF8Encoding($false)))
        $csprojChanged = $true
    }

    # 5. Sauber bauen und testen (Test-Lauf nur beim echten Release)
    Get-ChildItem src, tests, tools -Directory -Recurse -Include bin, obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Invoke-Checked 'Build' { dotnet build ParadoxModTool.slnx -c $config }
    if (-not $local) { Invoke-Checked 'Tests' { dotnet test ParadoxModTool.slnx -c $config --no-build } }

    # 6. Publish und Paket
    $out = Join-Path $PSScriptRoot "release-output\$version"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    Invoke-Checked 'Publish' { dotnet publish src/ParadoxModTool.App -c $config -r win-x64 --self-contained true "-p:Version=$version" -o "$out\app" }
    Invoke-Signer @('package', '--folder', "$out\app", '--version', $version, '--out', $out)

    # 7. Signieren und prüfen (Passwort nur einmal abfragen, nur für diesen Aufruf in der Umgebung)
    $secure = Read-Host 'Passwort des Release-Schlüssels' -AsSecureString
    $env:PMT_RELEASE_KEY_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    try {
        Invoke-Signer @('sign', "$out\update-manifest.json", '--key', $keyFile)
    }
    finally {
        Remove-Item Env:PMT_RELEASE_KEY_PASSWORD -ErrorAction SilentlyContinue
    }
    Invoke-Signer @('verify', "$out\update-manifest.json")

    $files = @("$out\ParadoxModTool-$version-win-x64.zip", "$out\update-manifest.json", "$out\update-manifest.json.sig")

    if ($local) {
        New-Item -ItemType Directory -Force $LocalFeed | Out-Null
        # Nur die eigenen Feed-Dateien ersetzen: ein falsch angegebener Ordner darf keine fremden Dateien verlieren.
        Get-ChildItem $LocalFeed -File | Where-Object { $_.Name -like 'ParadoxModTool-*-win-x64.zip' -or $_.Name -in 'update-manifest.json', 'update-manifest.json.sig', 'release.json' } | Remove-Item -Force
        Copy-Item $files $LocalFeed
        Invoke-Signer @('feed', '--folder', $LocalFeed, '--version', $version)
        Write-Host "Lokaler Feed fertig: $LocalFeed (Debug-Build, Programmordner: $out\app)" -ForegroundColor Green
        exit 0
    }

    # 8. Bestätigen
    Write-Host ''
    Write-Host "Release v$version nach $repo mit:" -ForegroundColor Cyan
    $files | ForEach-Object { Write-Host "  $_ ($([math]::Round((Get-Item $_).Length / 1MB, 1)) MB)" }
    Write-Host "Öffentlicher Commit: ein Commit 'Release v$version' als $publicName <$publicEmail>, ohne $($publicExclude -join ', ')."
    if ((Read-Host 'Commit, Tag, Push und Veröffentlichung jetzt ausführen? (j/n)') -ne 'j') {
        throw 'Abgebrochen. Die Dateien bleiben in release-output.'
    }

    # 9. Lokal committen (nur master, wird nie gepusht; ab hier bleibt die csproj-Änderung)
    Invoke-Checked 'Commit' { git commit -am "chore(release): v$version" }
    $csprojChanged = $false

    # 10. Öffentlichen Commit bauen: Dateistand von HEAD ohne $publicExclude, in einem eigenen Index (Arbeitsbaum bleibt unberührt)
    $publicIndex = Join-Path $env:TEMP "pmt-public-index-$PID"
    $env:GIT_INDEX_FILE = $publicIndex
    try {
        Invoke-Checked 'read-tree' { git read-tree HEAD }
        foreach ($excluded in $publicExclude) {
            Invoke-Checked "Ausschluss $excluded" { git rm -r --cached --quiet --ignore-unmatch -- $excluded }
        }
        $tree = git write-tree
        if ($LASTEXITCODE -ne 0) { throw 'write-tree fehlgeschlagen.' }
    }
    finally {
        Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
        Remove-Item $publicIndex -ErrorAction SilentlyContinue
    }

    $env:GIT_AUTHOR_NAME = $publicName
    $env:GIT_AUTHOR_EMAIL = $publicEmail
    $env:GIT_COMMITTER_NAME = $publicName
    $env:GIT_COMMITTER_EMAIL = $publicEmail
    try {
        $parent = git rev-parse --verify --quiet refs/heads/public
        $parentArgs = if ($LASTEXITCODE -eq 0 -and $parent) { @('-p', $parent) } else { @() }
        $commit = git commit-tree $tree @parentArgs -m "Release v$version"
        if ($LASTEXITCODE -ne 0) { throw 'commit-tree fehlgeschlagen.' }
    }
    finally {
        'GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL' | ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
    }

    # Letzte Sicherung vor dem Push: nichts Ausgeschlossenes im öffentlichen Commit, richtige Adresse
    $leaked = git ls-tree -r --name-only $commit | Where-Object { $name = $_; $publicExclude | Where-Object { $name -like "$_/*" } }
    if ($leaked) { throw "Öffentlicher Commit enthält ausgeschlossene Dateien: $($leaked | Select-Object -First 3)" }
    if ((git log -1 --format='%ae|%ce' $commit) -ne "$publicEmail|$publicEmail") { throw 'Öffentlicher Commit hat die falsche E-Mail-Adresse.' }

    # 11. Branch public fortschreiben, taggen, pushen (public -> main auf GitHub)
    Invoke-Checked 'Branch public' { git update-ref refs/heads/public $commit }
    Invoke-Checked 'Tag' { git tag "v$version" $commit }
    Invoke-Checked 'Push' { git push origin refs/heads/public:refs/heads/main }
    Invoke-Checked 'Push Tag' { git push origin "refs/tags/v$version" }

    # 12. GitHub-Release
    $ghArgs = @('release', 'create', "v$version") + $files + @('--repo', $repo, '--title', "v$version", '--notes-file', $notesFile)
    & $gh @ghArgs
    if ($LASTEXITCODE -ne 0) {
        # Commit und Tag sind schon auf GitHub; nur die Release-Seite fehlt. Nicht mit neuer Version wiederholen, sondern nachholen.
        throw "gh release create fehlgeschlagen. Commit und Tag v$version sind schon auf GitHub. Nachholen mit: & '$gh' $($ghArgs -join ' ')"
    }
    Write-Host "Release v$version veröffentlicht." -ForegroundColor Green
}
catch {
    Write-Host "FEHLER: $_" -ForegroundColor Red
    $failed = $true
}
finally {
    # Im finally statt im catch: auch Strg+C an einer Eingabe läuft am catch vorbei, und die Versionsänderung bliebe sonst stehen.
    if ($csprojChanged) {
        git checkout -- $csproj
        Write-Host 'Versionsänderung in der csproj zurückgenommen.'
    }
}

if ($failed) { exit 1 }
