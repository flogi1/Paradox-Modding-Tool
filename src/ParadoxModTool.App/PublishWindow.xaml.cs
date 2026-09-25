using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Auf Steam veröffentlichen": bearbeitet die <c>metadata.json</c> (Version), zeigt die Prüfungen (siehe <see cref="PublishReadiness"/>), richtet bei Bedarf SteamCMD ein und lädt den
/// Mod-Ordner über <see cref="WorkshopPublisher"/> hoch, entweder über den laufenden Steam-Client oder über SteamCMD (siehe <see cref="IWorkshopBackend"/>). Das Programm sieht nie ein Passwort:
/// beim Client-Weg gehört die Anmeldung Steam, bei SteamCMD läuft sie in dessen eigenem Fenster.
/// </summary>
public partial class PublishWindow : Window
{
    private const string FallbackPreview = "thumbnail.png";
    private const string Caption = "Auf Steam veröffentlichen";

    private readonly ModRegistry _registry;
    private readonly ModInfo _mod;
    private readonly string _metadataFile;
    private readonly SteamPublishHost _host;
    private readonly SteamCmdSettingsStore _settingsStore;
    private SteamCmdSettings _settings;
    private ReleaseLog _log;

    private ModMetadata? _metadata;
    private string? _metadataPath;
    private string _metaOriginal = string.Empty;
    private bool _metaHasBom;
    private bool _metaLoading;
    private bool _metaDirty;
    private bool _syncingVersion;

    private IReadOnlyList<ChangelogEntry>? _entries;
    private string? _changelogPath;
    private StagingResult? _scope;
    private PublishReadiness? _readiness;

    private bool _noteEdited;
    private bool _fillingNote;
    private bool _visibilityTouched;
    private bool _initialized;
    private string? _clientDll;
    private string? _clientProblem;
    private bool _useClient;
    private bool _publishing;
    private CancellationTokenSource? _cts;

    public PublishWindow(ModRegistry registry, ModInfo mod, string metadataFile, SteamPublishHost host)
    {
        InitializeComponent();
        _registry = registry;
        _mod = mod;
        _metadataFile = metadataFile;
        _host = host;
        _settingsStore = new SteamCmdSettingsStore(host.GameDirectory);
        _settings = _settingsStore.Load();
        RefreshClientStatus();
        MethodBox.SelectedIndex = UploadMethods.Resolve(_settings.UploadMethod, _clientProblem is null) == UploadMethods.Client ? 0 : 1;
        ApplyMethod();
        _log = ReleaseLog.Load(registry.DataDirectory(mod.Id));

        TitleText.Text = "Auf Steam veröffentlichen: " + mod.Name;
        AppIdText.Text = $"Spiel: {host.GameName}   ·   Steam-AppID: {host.SteamAppId ?? "—"}";
        WorkshopIdBox.Text = mod.SteamWorkshopId ?? string.Empty;
        UserBox.Text = _settings.UserName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(UserBox.Text) && DetectSteamAccount() is { } account)
        {
            UserBox.Text = account;
        }

        VisibilityBox.SelectedIndex = IsFirstPublish ? 3 : 0;
        ListingCheck.IsChecked = IsFirstPublish;
        ListingCheck.IsEnabled = !IsFirstPublish;
        if (_settingsStore.LoadWarning is { } warning)
        {
            ResultText.Text = warning;
        }

        _initialized = true;
        ReloadAll();

        Activated += (_, _) =>
        {
            if (!_metaDirty && !_publishing)
            {
                ReloadAll();
            }
        };
        Closing += (_, e) =>
        {
            if (_publishing)
            {
                e.Cancel = true;
                Report("Der Upload läuft noch. Brich ihn ab oder warte, bis er fertig ist.");
            }
            else if (!ConfirmLeaveMeta())
            {
                e.Cancel = true;
            }
        };
    }

    private string BackupDirectory => Path.Combine(_registry.DataDirectory(_mod.Id), "backups");

    private bool IsFirstPublish =>
        string.IsNullOrWhiteSpace(WorkshopIdBox.Text) || WorkshopIdBox.Text.Trim().All(c => c == '0');

    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    private void Report(string message) =>
        MessageBox.Show(this, message, Caption, MessageBoxButton.OK, MessageBoxImage.Information);

    private Brush Danger => TryFindResource("Theme.Danger") as Brush ?? Brushes.Firebrick;

    private Brush Muted => TryFindResource("Theme.TextMuted") as Brush ?? Brushes.Gray;

    // ---- Laden

    private void ReloadAll()
    {
        _log = ReleaseLog.Load(_registry.DataDirectory(_mod.Id));
        _metadata = ModMetadata.Find(_mod, _metadataFile);
        _metadataPath = _metadata is null ? null : Path.Combine(_metadata.Folder, _metadataFile);
        LoadMetaText();
        LoadChangelog();
        RefreshClientStatus();
        RefreshSteamStatus();
        RefreshAll();
    }

    private void LoadMetaText()
    {
        _metaLoading = true;
        try
        {
            if (_metadataPath is not null && File.Exists(_metadataPath))
            {
                var document = ModTextFile.Read(_metadataPath);
                _metaOriginal = document.Text;
                _metaHasBom = document.HasBom;
                MetaBox.Text = document.Text;
                MetaBox.IsReadOnly = _publishing;
                MetaErrorText.Text = string.Empty;
            }
            else
            {
                _metaOriginal = string.Empty;
                MetaBox.Text = string.Empty;
                MetaBox.IsReadOnly = true;
                MetaErrorText.Text = "Es wurde keine lesbare metadata.json im Ordner der Mod gefunden.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _metaOriginal = string.Empty;
            MetaBox.Text = string.Empty;
            MetaBox.IsReadOnly = true;
            MetaErrorText.Text = "metadata.json konnte nicht gelesen werden: " + ex.Message;
        }
        finally
        {
            _metaLoading = false;
        }

        SetMetaDirty(false);
        SyncVersionBox();
    }

    private void LoadChangelog()
    {
        _entries = null;
        _changelogPath = null;
        if (_mod.ChangelogFile is not { Length: > 0 } relative)
        {
            return;
        }

        var path = _mod.Paths.Select(p => Path.GetFullPath(Path.Combine(p, relative))).FirstOrDefault(File.Exists);
        if (path is null)
        {
            return;
        }

        try
        {
            _entries = ChangelogParser.Parse(ModTextFile.Read(path).Text);
            _changelogPath = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _entries = null;
        }
    }

    // ---- metadata.json

    private void SetMetaDirty(bool dirty)
    {
        _metaDirty = dirty;
        MetaSaveButton.IsEnabled = dirty && !_publishing;
        MetaRevertButton.IsEnabled = dirty && !_publishing;
    }

    private void SyncVersionBox()
    {
        _syncingVersion = true;
        VersionBox.Text = ModMetadataEditor.ReadVersion(MetaBox.Text) ?? string.Empty;
        VersionBox.IsEnabled = !MetaBox.IsReadOnly;
        NextVersionButton.IsEnabled = !MetaBox.IsReadOnly;
        _syncingVersion = false;
    }

    private void MetaBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_metaLoading || !_initialized)
        {
            return;
        }

        SetMetaDirty(true);
        SyncVersionBox();
        MetaErrorText.Text = string.Empty;
        RefreshAll();
    }

    private void MetaBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && _metaDirty)
        {
            SaveMeta();
            e.Handled = true;
        }
    }

    private void VersionBox_Commit(object sender, RoutedEventArgs e) => ApplyVersion(VersionBox.Text.Trim());

    private void VersionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyVersion(VersionBox.Text.Trim());
            e.Handled = true;
        }
    }

    private void NextVersion_Click(object sender, RoutedEventArgs e) =>
        ApplyVersion(ModMetadataEditor.NextPatch(VersionBox.Text.Trim()));

    /// <summary>Setzt das Feld <c>version</c> im Text der Datei (nur dort, Rest bleibt gleich). Gespeichert wird erst mit "Änderung speichern".</summary>
    private void ApplyVersion(string version)
    {
        if (_syncingVersion || MetaBox.IsReadOnly || version == (ModMetadataEditor.ReadVersion(MetaBox.Text) ?? string.Empty))
        {
            return;
        }

        if (!VersionNumber.TryParse(version, out _))
        {
            MetaErrorText.Text = "Das ist keine gültige Version (erwartet z. B. 2.0.6).";
            SyncVersionBox();
            return;
        }

        try
        {
            MetaBox.Text = ModMetadataEditor.SetVersion(MetaBox.Text, version);
        }
        catch (InvalidOperationException ex)
        {
            MetaErrorText.Text = ex.Message;
            SyncVersionBox();
        }
    }

    private void MetaSave_Click(object sender, RoutedEventArgs e) => SaveMeta();

    private void MetaRevert_Click(object sender, RoutedEventArgs e)
    {
        SetMetaDirty(false);
        ReloadAll();
    }

    /// <summary>Speichert die Datei, wenn der Text gültiges JSON ist. <see langword="true"/>, wenn danach nichts Ungespeichertes mehr da ist.</summary>
    private bool SaveMeta()
    {
        if (!_metaDirty || _metadataPath is null)
        {
            return true;
        }

        if (ModMetadataEditor.Validate(MetaBox.Text) is { } error)
        {
            MetaErrorText.Text = error + " Die Datei wurde nicht gespeichert.";
            return false;
        }

        try
        {
            var onDisk = ModTextFile.Read(_metadataPath).Text;
            if (onDisk != _metaOriginal
                && MessageBox.Show(this, "Die metadata.json wurde von außen geändert.\nTrotzdem überschreiben?", Caption, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return false;
            }

            var newline = _metaOriginal.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var text = MetaBox.Text.Replace("\r\n", "\n").Replace("\n", newline);
            ModTextFile.Write(_metadataPath, new ModTextDocument(text, _metaHasBom), BackupDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MetaErrorText.Text = "Die Datei konnte nicht gespeichert werden: " + ex.Message;
            return false;
        }

        SetMetaDirty(false);
        ReloadAll();
        return true;
    }

    /// <summary>Fragt bei ungespeicherter Änderung nach. <see langword="false"/> = abbrechen.</summary>
    private bool ConfirmLeaveMeta()
    {
        if (!_metaDirty)
        {
            return true;
        }

        var answer = MessageBox.Show(this, "Die metadata.json hat ungespeicherte Änderungen.\nJetzt speichern?", Caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        switch (answer)
        {
            case MessageBoxResult.Yes:
                return SaveMeta();
            case MessageBoxResult.No:
                SetMetaDirty(false);
                return true;
            default:
                return false;
        }
    }

    private void NewBanner_Click(object sender, RoutedEventArgs e)
    {
        var version = ModMetadataEditor.ReadVersion(MetaBox.Text);
        if (_changelogPath is null || !VersionNumber.TryParse(version, out var number))
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(_metadata?.Name) ? _mod.Name : _metadata!.Name;
        try
        {
            var document = ModTextFile.Read(_changelogPath);
            ModTextFile.Write(_changelogPath, document with { Text = ChangelogEditor.InsertVersion(document.Text, number.Text, name) }, BackupDirectory);
            if (_log.Find(number.Text) is null)
            {
                _log.Add(number.Text, Today);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Report(ex.Message);
            return;
        }

        LoadChangelog();
        RefreshAll();
        Report($"Das Banner für Version {number.Text} steht jetzt oben im Changelog. Trage die Änderungen im Fenster \"Releases\" ein (Textfeld rechts).");
    }

    // ---- Prüfungen, Umfang, Changenote

    private void RefreshAll()
    {
        if (!_initialized)
        {
            return;
        }

        RefreshScope();
        FillNote();
        RefreshChecks();
    }

    private void RefreshScope()
    {
        _scope = null;
        if (_metadata is not null)
        {
            try
            {
                _scope = ModStaging.Measure(_metadata.Folder, ModStagingRules.Default);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _scope = null;
            }
        }

        if (_scope is null)
        {
            ScopeText.Text = string.Empty;
            return;
        }

        var shown = string.Join(", ", _scope.Excluded.Take(6));
        var more = _scope.Excluded.Count > 6 ? $" … (+{_scope.Excluded.Count - 6} weitere)" : string.Empty;
        ScopeText.Text = $"Dateien: {_scope.FileCount:N0} ({FormatBytes(_scope.TotalBytes)}). Ausgeschlossen: {(shown.Length == 0 ? "nichts" : shown + more)}";
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:N1} MB" : $"{Math.Max(1, bytes / 1024):N0} KB";

    private void ScopeList_Click(object sender, RoutedEventArgs e)
    {
        if (_scope is null)
        {
            return;
        }

        var lines = _scope.Excluded.Take(60).ToList();
        var more = _scope.Excluded.Count > lines.Count ? $"\n… und {_scope.Excluded.Count - lines.Count} weitere" : string.Empty;
        Report(lines.Count == 0 ? "Es wird nichts ausgeschlossen." : "Nicht hochgeladen werden:\n\n" + string.Join("\n", lines) + more);
    }

    private string? PreviewFullPath()
    {
        if (_metadata is null || _metadataPath is null)
        {
            return null;
        }

        var picture = string.IsNullOrWhiteSpace(_metadata.Picture) ? FallbackPreview : _metadata.Picture;
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_metadataPath)!, picture));
    }

    /// <summary>Der Name einer anderen Mod der Verwaltung, die schon die Workshop-ID im Feld führt; sonst <see langword="null"/>. Schützt davor, versehentlich die Seite einer anderen Mod zu überschreiben.</summary>
    private string? OtherModWithWorkshopId()
    {
        var id = WorkshopIdBox.Text.Trim();
        return id.Length == 0 || id.All(c => c == '0')
            ? null
            : _registry.Mods.FirstOrDefault(m => m.Id != _mod.Id && string.Equals(m.SteamWorkshopId, id, StringComparison.Ordinal))?.Name;
    }

    private void RefreshChecks()
    {
        var versionText = ModMetadataEditor.ReadVersion(MetaBox.Text);
        var hasSection = VersionNumber.TryParse(versionText, out var number)
            && _entries?.Any(entry => VersionNumber.TryParse(entry.Version, out var other) && other.Equals(number)) == true;
        var preview = PreviewFullPath();

        _readiness = PublishReadiness.Evaluate(new PublishReadinessInput(
            AppId: _host.SteamAppId,
            Metadata: _metadata,
            MetadataUnsaved: _metaDirty,
            WorkshopIdText: WorkshopIdBox.Text,
            SteamCmdReady: SteamCmdLocator.Find(_settings) is not null,
            UserName: UserBox.Text,
            VersionForChangelog: versionText,
            ChangelogHasVersion: hasSection,
            ChangelogExists: _entries is not null,
            IsFirstPublish: IsFirstPublish,
            PreviewExists: preview is not null && File.Exists(preview),
            PublishedOnSteam: _metadata is not null && _log.Find(_metadata.Version)?.Steam is not null,
            NothingToUpload: _scope is { FileCount: 0 },
            UseSteamClient: _useClient,
            ClientProblem: _clientProblem,
            WorkshopIdOwner: OtherModWithWorkshopId()));

        HintList.ItemsSource = _readiness.Hints.Select(h => new HintRow(h.Text, h.Blocking ? Danger : Muted)).ToList();
        PublishButton.Content = IsFirstPublish ? "Veröffentlichen" : "Update";
        PublishButton.IsEnabled = _readiness.CanPublish && !_publishing;
        NewBannerButton.Visibility = _entries is not null && !hasSection && versionText is not null && VersionNumber.TryParse(versionText, out _)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FillNote()
    {
        if (_noteEdited)
        {
            UpdateNoteCount();
            return;
        }

        var text = string.Empty;
        if (VersionNumber.TryParse(ModMetadataEditor.ReadVersion(MetaBox.Text), out var number)
            && _entries?.FirstOrDefault(entry => VersionNumber.TryParse(entry.Version, out var other) && other.Equals(number)) is { } section)
        {
            text = BbCodeCheck.IsChecked == true ? ChangeNoteFormatter.ToBbCode(section.Text) : section.Text;
        }

        _fillingNote = true;
        NoteBox.Text = text;
        _fillingNote = false;
        UpdateNoteCount();
    }

    private void UpdateNoteCount()
    {
        var length = NoteBox.Text.Length;
        NoteCountText.Text = $"{length:N0} / {ChangeNoteFormatter.MaxLength:N0} Zeichen";
        NoteCountText.Foreground = ChangeNoteFormatter.IsTooLong(NoteBox.Text) ? Danger : Muted;
    }

    private void NoteBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_fillingNote && _initialized)
        {
            _noteEdited = true;
        }

        UpdateNoteCount();
    }

    private void BbCodeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        _noteEdited = false;
        FillNote();
    }

    private void VisibilityBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initialized)
        {
            _visibilityTouched = true;
        }
    }

    private void WorkshopIdBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        if (WorkshopIdText.Extract(WorkshopIdBox.Text) is { } extracted && extracted != WorkshopIdBox.Text.Trim())
        {
            WorkshopIdBox.Text = extracted;
            return;
        }

        var first = IsFirstPublish;
        ListingCheck.IsEnabled = !first;
        if (first)
        {
            ListingCheck.IsChecked = true;
        }

        if (!_visibilityTouched)
        {
            var touched = _visibilityTouched;
            VisibilityBox.SelectedIndex = first ? 3 : 0;
            _visibilityTouched = touched;
        }

        var id = WorkshopIdBox.Text.Trim();
        OpenPageButton.IsEnabled = !first && id.All(char.IsAsciiDigit);
        RefreshChecks();
    }

    private void OpenPage_Click(object sender, RoutedEventArgs e)
    {
        var id = WorkshopIdBox.Text.Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("https://steamcommunity.com/sharedfiles/filedetails/?id=" + id) { UseShellExecute = true });
    }

    // ---- Workshop-ID suchen

    /// <summary>Der Kontoname des zuletzt bei Steam angemeldeten Kontos (aus <c>loginusers.vdf</c>); <see langword="null"/>, wenn er nicht zu finden ist.</summary>
    private static string? DetectSteamAccount() => ReadLoginUser(null)?.AccountName;

    private static SteamLoginUser? ReadLoginUser(string? accountName)
    {
        try
        {
            if (SteamFolders.Find() is not { } steamFolder || !File.Exists(SteamFolders.LoginUsersFile(steamFolder)))
            {
                return null;
            }

            return SteamLoginUsers.Pick(SteamLoginUsers.Parse(File.ReadAllText(SteamFolders.LoginUsersFile(steamFolder))), accountName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async void FindId_Click(object sender, RoutedEventArgs e)
    {
        if (_metadata is null || string.IsNullOrWhiteSpace(_host.SteamAppId))
        {
            Report("Ohne lesbare metadata.json und ohne Steam-AppID im Spielprofil kann ich nicht suchen.");
            return;
        }

        FindIdButton.IsEnabled = false;
        try
        {
            await FindWorkshopIdAsync();
        }
        finally
        {
            FindIdButton.IsEnabled = true;
        }
    }

    private async Task FindWorkshopIdAsync()
    {
        var metadata = _metadata!;
        var appId = _host.SteamAppId!;

        var steamFolder = SteamFolders.Find();
        var libraries = steamFolder is null ? Array.Empty<string>() : SteamFolders.LibraryFolders(steamFolder).ToArray();
        var local = LocalWorkshopScanner.Find(libraries, appId, _metadataFile, metadata.Name, metadata.Id);
        if (local.Count == 1)
        {
            UseFound(local[0]);
            return;
        }

        if (local.Count > 1)
        {
            if (WorkshopPickWindow.Pick(this, "Mehrere abonnierte Workshop-Mods passen zu dieser Mod. Wähle die richtige:", local) is { } chosenLocal)
            {
                UseFound(chosenLocal);
            }

            return;
        }

        var user = ReadLoginUser(UserBox.Text);
        if (user is null)
        {
            Report("Lokal wurde keine passende Workshop-Seite gefunden, und auf diesem Rechner ist kein Steam-Konto bekannt.\n\nTrage die ID von Hand ein oder füge die Adresse der Workshop-Seite in das Feld ein.");
            return;
        }

        var ask = MessageBox.Show(
            this,
            $"Lokal wurde keine passende Workshop-Seite gefunden.\n\nSoll ich die öffentliche Workshop-Seite des Profils „{user.PersonaName}“ auf steamcommunity.com abrufen?\n(steamcommunity.com/profiles/{user.SteamId64}/myworkshopfiles)\n\nEs wird keine Anmeldung und kein Cookie benutzt. Als „Nur ich“ oder „Versteckt“ hochgeladene Seiten sind dort nicht sichtbar.",
            Caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes)
        {
            return;
        }

        IReadOnlyList<WorkshopMatch> items;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ParadoxModTool");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
            items = await WorkshopProfileClient.ListAsync(user.SteamId64, appId, (url, token) => http.GetStringAsync(url, token), CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Report("Die Workshop-Seite konnte nicht abgerufen werden: " + ex.Message);
            return;
        }

        if (items.Count == 0)
        {
            Report($"Auf dem öffentlichen Profil „{user.PersonaName}“ gibt es keine Workshop-Seiten für dieses Spiel. Vielleicht ist die Seite nur für dich sichtbar.\n\nTrage die ID von Hand ein oder füge die Adresse der Workshop-Seite in das Feld ein.");
            return;
        }

        var exact = WorkshopMatcher.Exact(items, metadata.Name);
        if (exact.Count == 1)
        {
            UseFound(exact[0]);
            return;
        }

        var header = exact.Count > 1
            ? "Mehrere Seiten haben genau den Titel der Mod. Wähle die richtige:"
            : "Keine Seite hat genau den Titel der Mod. Wähle die richtige oder brich ab:";
        if (WorkshopPickWindow.Pick(this, header, exact.Count > 1 ? exact : items) is { } chosen)
        {
            UseFound(chosen);
        }
    }

    private void UseFound(WorkshopMatch match)
    {
        WorkshopIdBox.Text = match.Id;
        ResultText.Text = $"Workshop-ID {match.Id} übernommen: „{match.Title}“ ({match.Source}). Sie wird gespeichert, sobald du hochlädst.";
        ResultText.Foreground = Muted;
    }

    // ---- Weg des Uploads

    private void RefreshClientStatus()
    {
        _clientDll = null;
        if (_host.SteamAppId is { Length: > 0 } appId && SteamFolders.Find() is { } steamFolder)
        {
            _clientDll = SteamworksLocator.Find(SteamFolders.LibraryFolders(steamFolder), appId);
        }

        _clientProblem = _clientDll is null
            ? "Die Steamworks-Bibliothek des Spiels wurde nicht gefunden. Ist das Spiel installiert?"
            : !SteamClientStatus.IsSteamRunning() ? "Steam läuft nicht. Starte Steam und melde dich an." : null;
        ClientStatusText.Text = _clientProblem
            ?? $"Bereit: Steam läuft. Der Upload nutzt deine bestehende Steam-Anmeldung, es ist nichts einzurichten. Steam zeigt dich nur solange der Upload läuft als \"{_host.GameName} spielend\" an; starte das Spiel nicht gleichzeitig.";
    }

    private void ApplyMethod()
    {
        _useClient = MethodBox.SelectedIndex == 0;
        SteamCmdPanel.Visibility = _useClient ? Visibility.Collapsed : Visibility.Visible;
        ClientStatusText.Visibility = _useClient ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MethodBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        ApplyMethod();
        _settings.UploadMethod = _useClient ? UploadMethods.Client : UploadMethods.SteamCmd;
        _settingsStore.Save(_settings);
        RefreshChecks();
    }

    // ---- SteamCMD

    private void RefreshSteamStatus()
    {
        var exe = SteamCmdLocator.Find(_settings);
        var user = UserBox.Text.Trim();
        SteamStatusText.Text = exe is null
            ? "SteamCMD ist nicht eingerichtet. Lade es herunter oder wähle eine vorhandene steamcmd.exe."
            : $"SteamCMD: {exe}" + (user.Length == 0 ? string.Empty : $"\nBenutzer: {user} (ob die Anmeldung gilt, zeigt sich beim Upload)");
        DownloadButton.Visibility = exe is null ? Visibility.Visible : Visibility.Collapsed;
        LoginButton.IsEnabled = exe is not null && user.Length > 0 && !user.Any(c => char.IsWhiteSpace(c) || c == '"');
        OpenPageButton.IsEnabled = !IsFirstPublish;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var folder = SteamCmdInstaller.DefaultFolder;
        var answer = MessageBox.Show(
            this,
            "SteamCMD wird von Valve heruntergeladen.\n\nDatei: steamcmd.zip\nQuelle: steamcdn-a.akamaihd.net (Valve)\nGröße: ca. 3 MB\nZiel: " + folder
            + "\n\nBeim ersten Start lädt sich SteamCMD noch selbst nach (ca. 100 MB).\n\nFortfahren?",
            "SteamCMD herunterladen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        DownloadButton.IsEnabled = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var bytes = await http.GetByteArrayAsync(SteamCmdInstaller.DownloadUrl);
            using var stream = new MemoryStream(bytes);
            _settings.SteamCmdPath = SteamCmdInstaller.Install(stream, folder);
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Report("Der Download ist fehlgeschlagen: " + ex.Message);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }

        RefreshSteamStatus();
        RefreshChecks();
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "steamcmd.exe wählen",
            Filter = "steamcmd.exe|steamcmd.exe|Programme (*.exe)|*.exe",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _settings.SteamCmdPath = dialog.FileName;
        _settingsStore.Save(_settings);
        RefreshSteamStatus();
        RefreshChecks();
    }

    private void UserBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        RefreshSteamStatus();
        RefreshChecks();
    }

    private void UserBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _settings.UserName = UserBox.Text.Trim();
        _settingsStore.Save(_settings);
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var exe = SteamCmdLocator.Find(_settings);
        var user = UserBox.Text.Trim();
        if (exe is null || user.Length == 0)
        {
            return;
        }

        _settings.UserName = user;
        _settingsStore.Save(_settings);
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                Arguments = "+login " + user,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Report("SteamCMD konnte nicht gestartet werden: " + ex.Message);
            return;
        }

        Report("SteamCMD wurde in einem eigenen Fenster gestartet.\n\nGib dort dein Passwort und den Steam-Guard-Code ein (das Programm sieht sie nicht). Warte, bis „Logged in OK“ erscheint, und schließe das Fenster mit dem Befehl quit.\n\nBeim allerersten Start lädt SteamCMD sich zuerst selbst nach; das kann einige Minuten dauern.");
    }

    // ---- Upload

    private WorkshopVisibility? SelectedVisibility() => VisibilityBox.SelectedIndex switch
    {
        1 => WorkshopVisibility.Public,
        2 => WorkshopVisibility.FriendsOnly,
        3 => WorkshopVisibility.Private,
        _ => null,
    };

    private void SetPublishing(bool publishing)
    {
        _publishing = publishing;
        CancelButton.Visibility = publishing ? Visibility.Visible : Visibility.Collapsed;
        UploadProgress.Visibility = publishing ? Visibility.Visible : Visibility.Collapsed;
        MetaBox.IsReadOnly = publishing || _metadataPath is null;
        SetMetaDirty(_metaDirty);
        PublishButton.IsEnabled = !publishing && _readiness?.CanPublish == true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (_publishing || !ConfirmLeaveMeta())
        {
            return;
        }

        RefreshChecks();
        var exe = _useClient ? null : SteamCmdLocator.Find(_settings);
        if (_readiness is not { CanPublish: true } || _metadata is null || (_useClient ? _clientDll is null : exe is null))
        {
            Report("Der Upload ist noch nicht möglich. Die roten Hinweise unter \"Prüfungen\" zeigen, was fehlt.");
            return;
        }

        var version = _metadata.Version;
        var first = IsFirstPublish;
        var note = NoteBox.Text.Trim();
        if (ChangeNoteFormatter.IsTooLong(note))
        {
            Report($"Die Changenote ist zu lang ({note.Length:N0} von {ChangeNoteFormatter.MaxLength:N0} Zeichen). Kürze sie.");
            return;
        }

        if (note.Length == 0
            && MessageBox.Show(this, "Die Changenote ist leer. Trotzdem hochladen?", Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var visibility = SelectedVisibility() ?? (first ? WorkshopVisibility.Private : null);
        var confirm = $"Version {version} von „{_metadata.Name}“ auf Steam hochladen?\n\n"
            + (first ? "Es wird eine neue Workshop-Seite angelegt." : $"Die Workshop-Seite {WorkshopIdBox.Text.Trim()} wird aktualisiert.")
            + $"\nSichtbarkeit: {(visibility is null ? "unverändert" : VisibilityBox.Text)}"
            + $"\nDateien: {_scope?.FileCount:N0}";
        if (MessageBox.Show(this, confirm, Caption, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        string? previewRelative = null;
        if (PreviewFullPath() is { } previewPath && File.Exists(previewPath))
        {
            var relative = Path.GetRelativePath(_metadata.Folder, previewPath);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                previewRelative = relative.Replace('\\', '/');
            }
        }

        var request = new WorkshopPublishRequest(
            AppId: _host.SteamAppId!,
            ModFolder: _metadata.Folder,
            WorkshopId: first ? null : WorkshopIdBox.Text.Trim(),
            ChangeNote: note,
            Visibility: visibility,
            UpdateListing: first || ListingCheck.IsChecked == true,
            Title: string.IsNullOrWhiteSpace(_metadata.Name) ? null : _metadata.Name,
            Description: string.IsNullOrWhiteSpace(_metadata.ShortDescription) ? null : _metadata.ShortDescription,
            PreviewFile: previewRelative,
            Rules: ModStagingRules.Default);

        SetPublishing(true);
        OutputBox.Clear();
        OutputExpander.IsExpanded = true;
        ResultText.Text = string.Empty;
        _cts = new CancellationTokenSource();
        var lines = new Progress<string>(line =>
        {
            OutputBox.AppendText(line + Environment.NewLine);
            OutputBox.ScrollToEnd();
        });

        WorkshopPublishResult result;
        try
        {
            IWorkshopBackend backend = _useClient
                ? new SteamClientProcessBackend(new SteamHelperLauncher(), _clientDll!, TimeSpan.FromMilliseconds(250), TimeSpan.FromMinutes(30))
                : new SteamCmdBackend(new SteamCmdProcess(), exe!, UserBox.Text.Trim(), TimeSpan.FromMinutes(30));
            result = await new WorkshopPublisher(backend).PublishAsync(request, lines, _cts.Token);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetPublishing(false);
        }

        HandleResult(result, version, first);
    }

    private void HandleResult(WorkshopPublishResult result, string version, bool first)
    {
        var problems = new List<string>();
        if (result.WorkshopId is { Length: > 0 } id && (first || result.Outcome == WorkshopPublishOutcome.Success))
        {
            try
            {
                _registry.SetSteamWorkshopId(_mod.Id, id);
                WorkshopIdBox.Text = id;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                problems.Add("Die Workshop-ID konnte nicht gespeichert werden: " + ex.Message);
            }
        }

        if (result.Outcome == WorkshopPublishOutcome.Success && result.WorkshopId is { } publishedId)
        {
            try
            {
                if (_log.Find(version) is null)
                {
                    _log.Add(version, Today);
                }

                _log.SetSteam(version, new SteamPublication { PublishedAt = DateTime.Now.ToString("s"), WorkshopId = publishedId });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                problems.Add("Der Vermerk in den Releases konnte nicht gespeichert werden: " + ex.Message);
            }
        }

        var message = result.Message;
        if (result.Outcome != WorkshopPublishOutcome.Success && first && result.WorkshopId is not null)
        {
            message += $" Die Workshop-Seite {result.WorkshopId} wurde schon angelegt; ein erneuter Versuch aktualisiert sie.";
        }

        ResultText.Text = string.Join("\n", new[] { message }.Concat(problems));
        ResultText.Foreground = result.Outcome == WorkshopPublishOutcome.Success && problems.Count == 0 ? Muted : Danger;
        RefreshAll();

        if (result.NeedsLegalAgreement
            && MessageBox.Show(
                this,
                "Steam verlangt, dass du der Workshop-Vereinbarung zustimmst, bevor die Seite sichtbar wird. Das machst du selbst auf der Steam-Seite.\n\nSeite jetzt öffnen?",
                Caption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo("https://steamcommunity.com/sharedfiles/workshoplegalagreement") { UseShellExecute = true });
        }
    }

    private sealed record HintRow(string Text, Brush Brush);
}
