using System.IO;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.App;

/// <summary>Ergebnis eines Einlesevorgangs der Script-Datenbank.</summary>
/// <param name="WikiFileFound">Nur bei <paramref name="IsWikiSourced"/>: existierte die mitgelieferte Wiki-Datei? <see langword="false"/>
/// bedeutet eine fehlende/beschädigte Installation, nicht "noch nichts geändert" - braucht eine eigene Nutzerhinweis-Meldung.</param>
public sealed record ScriptDocsRefreshResult(
    ScriptDocDatabase Database,
    bool Imported,
    bool HasLogs,
    string? Folder,
    bool IsWikiSourced,
    DateTime? WikiFetchedUtc,
    bool WikiFileFound = true,
    bool IsInstallSourced = false);

/// <summary>
/// Hält die Script-Datenbank des geöffneten Spiels (Ablage <c>games\&lt;id&gt;\script-docs</c>) und liest sie bei Bedarf neu ein: aus den
/// Doku-Logs des Spiels, oder - wenn das aktive Spielprofil <see cref="GameProfile.ScriptDocsSourceKind"/> "wiki" setzt - aus einer mit
/// PMT mitgelieferten, vorab aus der Spiel-Wiki erzeugten Datei (siehe tools/wiki-scraper). Laden, Einlesen und Speichern laufen im
/// Hintergrund; das aufrufende Fenster tauscht danach nur die Datenbank aus.
/// </summary>
public sealed class ScriptDocsService
{
    private readonly Func<string?> _getDocsFolder;
    private readonly Action<string?> _setDocsFolder;
    private readonly Func<GameProfile?> _getActiveProfile;

    // Ein Einlesen zur Zeit: Start-Import im Hintergrund und "Neu einlesen" im Fenster dürfen nicht gleichzeitig dieselben Dateien schreiben.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScriptDocsService(
        string directory,
        Func<string?> getDocsFolder,
        Action<string?> setDocsFolder,
        Func<GameProfile?> getActiveProfile)
    {
        Storage = new ScriptDocStorage(directory);
        SearchStore = new UsageSearchStore(Path.Combine(directory, "searches"));
        _getDocsFolder = getDocsFolder;
        _setDocsFolder = setDocsFolder;
        _getActiveProfile = getActiveProfile;
    }

    public ScriptDocStorage Storage { get; }

    public UsageSearchStore SearchStore { get; }

    /// <summary>Die zuletzt geladene Datenbank (leer, solange noch nichts geladen wurde).</summary>
    public ScriptDocDatabase Database { get; private set; } = new();

    /// <summary>Ordner mit den Doku-Logs des Spiels (<c>docs</c>), oder <see langword="null"/>, wenn er sich nicht ableiten lässt.</summary>
    public string? DocsFolder => _getDocsFolder();

    /// <summary>Wird ausgelöst (im Kontext des Aufrufers von <see cref="RefreshAsync"/>), wenn <see cref="Database"/> ausgetauscht wurde.</summary>
    public event EventHandler? DatabaseChanged;

    public void SetDocsFolder(string? folder) => _setDocsFolder(folder);

    /// <summary>
    /// Lädt die Datenbank von der Platte und liest sie neu ein, wenn die Quelle neu oder geändert ist. Ein Fehler beim Lesen oder
    /// Speichern bricht nicht ab: die Datenbank bleibt so weit nutzbar, wie sie geladen werden konnte.
    /// </summary>
    public async Task<ScriptDocsRefreshResult> RefreshAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await RefreshCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ScriptDocsRefreshResult> RefreshCoreAsync()
    {
        var profile = _getActiveProfile();
        if (profile?.ScriptDocsSourceKind == "wiki" && profile.ScriptDocsWikiFile is { Length: > 0 } relativePath)
        {
            return await RefreshFromWikiAsync(relativePath);
        }

        var folder = DocsFolder;
        var fileNames = profile?.ScriptDocsFiles;
        var result = await Task.Run(() =>
        {
            var database = Storage.Load();
            var hasLogs = folder is not null && Directory.Exists(folder) && ScriptDocImporter.ReadWriteTicks(folder, fileNames).Count > 0;
            var imported = false;
            if (hasLogs && ScriptDocImporter.NeedsImport(database, folder!, fileNames))
            {
                if (ScriptDocImporter.Import(database, folder!, DateTime.UtcNow, fileNames) is not null)
                {
                    try
                    {
                        Storage.Save(database);
                        imported = true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Der Stand gilt für diese Sitzung, wird aber nicht dauerhaft gespeichert; der nächste Start liest neu ein.
                        imported = true;
                    }
                }
            }

            return new ScriptDocsRefreshResult(
                database, imported, hasLogs, folder, IsWikiSourced: false, WikiFetchedUtc: null, IsInstallSourced: profile?.ScriptDocsBase == "install");
        });

        Database = result.Database;
        DatabaseChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private async Task<ScriptDocsRefreshResult> RefreshFromWikiAsync(string relativeWikiFile)
    {
        var wikiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles", relativeWikiFile);
        var result = await Task.Run(() =>
        {
            var database = Storage.Load();
            var fileFound = File.Exists(wikiPath);
            var imported = false;
            if (WikiScriptDocImporter.NeedsImport(database, wikiPath))
            {
                if (WikiScriptDocImporter.Import(database, wikiPath, DateTime.UtcNow) is not null)
                {
                    try
                    {
                        Storage.Save(database);
                        imported = true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        imported = true;
                    }
                }
            }

            var fetchedUtc = WikiScriptDocImporter.ReadFetchedUtc(wikiPath);
            return new ScriptDocsRefreshResult(database, imported, HasLogs: false, Folder: null, IsWikiSourced: true, WikiFetchedUtc: fetchedUtc, WikiFileFound: fileFound);
        });

        Database = result.Database;
        DatabaseChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }
}
