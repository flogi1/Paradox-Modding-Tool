using System.IO;
using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Mods;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Releases" einer Mod: erfasste Versionen (siehe <see cref="ReleaseLog"/>), der Changelog-Text der gewählten Version und die Abgleich-Hinweise
/// (siehe <see cref="ReleaseStatus"/>). Der Changelog im Mod-Ordner ist die einzige Quelle für den Text; jede Änderung liest ihn frisch, sichert ihn vorher
/// in die Programmdaten und schreibt ihn atomar zurück.
/// </summary>
public partial class ReleasesWindow : Window
{
    private readonly ModRegistry _registry;
    private readonly ModInfo _mod;
    private readonly string _metadataFile;
    private readonly SteamPublishHost _steam;
    private ReleaseLog _log;
    private IReadOnlyList<ChangelogEntry>? _entries;
    private ModMetadata? _metadata;

    /// <summary>Der Text im Editor weicht vom Changelog ab.</summary>
    private bool _dirty;

    /// <summary>Wird der Editor gerade vom Programm gefüllt (dann ist die Änderung keine des Nutzers)?</summary>
    private bool _loading;

    private bool _reverting;

    /// <summary>Die Version, deren Text im Editor steht (die Auswahl in der Liste kann schon weitergesprungen sein).</summary>
    private string? _editingVersion;

    public ReleasesWindow(ModRegistry registry, ModInfo mod, string metadataFile, SteamPublishHost steam)
    {
        InitializeComponent();
        _registry = registry;
        _mod = mod;
        _metadataFile = metadataFile;
        _steam = steam;
        _log = ReleaseLog.Load(registry.DataDirectory(mod.Id));
        TitleText.Text = PublishTexts.ReleasesTitleFor(mod.Name);

        if (mod.ChangelogFile is null && mod.Paths.Select(ChangelogFile.Suggest).FirstOrDefault(s => s is not null) is { } suggestion)
        {
            _registry.SetChangelogFile(mod.Id, suggestion);
        }

        Reload();
        Activated += (_, _) =>
        {
            if (!_dirty)
            {
                Reload();
            }
        };
        Closing += (_, e) => e.Cancel = !ConfirmLeave();
    }

    private string BackupDirectory => Path.Combine(_registry.DataDirectory(_mod.Id), "backups");

    private ReleaseRow? SelectedRow => ReleaseList.SelectedItem as ReleaseRow;

    /// <summary>Der volle Pfad der Changelog-Datei im ersten Mod-Ordner, in dem sie liegt; <see langword="null"/>, wenn keine festgelegt oder keine vorhanden ist.</summary>
    private string? ChangelogPath =>
        _mod.ChangelogFile is { Length: > 0 } relative
            ? _mod.Paths.Select(p => Path.GetFullPath(Path.Combine(p, relative))).FirstOrDefault(File.Exists)
            : null;

    private void Reload()
    {
        var keep = SelectedRow?.Version;
        _log = ReleaseLog.Load(_registry.DataDirectory(_mod.Id));
        _metadata = ModMetadata.Find(_mod, _metadataFile);

        string? problem = null;
        _entries = null;
        if (ChangelogPath is { } path)
        {
            try
            {
                _entries = ChangelogParser.Parse(ModTextFile.Read(path).Text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                problem = ex.Message;
            }
        }

        ChangelogPathText.Text = _mod.ChangelogFile is null
            ? PublishTexts.NoChangelogSet
            : ChangelogPath is null ? PublishTexts.ChangelogFileMissing(_mod.ChangelogFile) : _mod.ChangelogFile;

        var status = ReleaseStatus.Evaluate(_metadata, _entries, _log.Releases);
        StatusText.Text = PublishTexts.StatusLine(status.MetadataVersion ?? "—", status.ChangelogTop ?? "—", status.LatestRelease ?? "—");
        var hints = status.Hints.ToList();
        if (problem is not null)
        {
            hints.Insert(0, PublishTexts.ChangelogReadFailed(problem));
        }

        if (_log.LoadWarning is { } warning)
        {
            hints.Insert(0, warning);
        }

        HintList.ItemsSource = hints;

        var rows = _log.Releases.Select(r => new ReleaseRow(r, Find(r.Version) is not null)).ToList();
        ReleaseList.ItemsSource = rows;
        ReleaseList.SelectedItem = rows.FirstOrDefault(r => r.Version == keep) ?? rows.FirstOrDefault();
        UpdateButtons();
        ShowPreview();
    }

    private ChangelogEntry? Find(string version) =>
        VersionNumber.TryParse(version, out var wanted)
            ? _entries?.FirstOrDefault(e => VersionNumber.TryParse(e.Version, out var v) && v.Equals(wanted))
            : null;

    private void UpdateButtons()
    {
        var hasSelection = SelectedRow is not null;
        var hasChangelog = ChangelogPath is not null && _entries is not null;
        RecordButton.IsEnabled = _metadata is not null;
        ImportButton.IsEnabled = hasChangelog;
        NewVersionButton.IsEnabled = hasChangelog;
        PublishButton.IsEnabled = _metadata is not null && !string.IsNullOrWhiteSpace(_steam.SteamAppId);
        GitHubPublishButton.IsEnabled = _metadata is not null;
        DateButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    /// <summary>Füllt den Editor mit dem Changelog-Text der gewählten Version. Steht die Version nicht im Changelog, ist der Editor schreibgeschützt und zeigt einen Hinweis.</summary>
    private void ShowPreview()
    {
        var entry = SelectedRow is { } row ? Find(row.Version) : null;
        _loading = true;
        PreviewBox.IsReadOnly = entry is null;
        PreviewBox.Text = SelectedRow is null
            ? string.Empty
            : entry is not null
                ? entry.Text
                : _entries is null ? PublishTexts.NoChangelogAvailable : PublishTexts.VersionNotInChangelog;
        _loading = false;
        _editingVersion = entry is null ? null : SelectedRow!.Version;
        SetDirty(false);
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        SaveTextButton.IsEnabled = dirty;
        RevertTextButton.IsEnabled = dirty;
    }

    private void PreviewBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading && _editingVersion is not null)
        {
            SetDirty(true);
        }
    }

    private void PreviewBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && _dirty)
        {
            SaveText();
            e.Handled = true;
        }
    }

    /// <summary>Fragt bei ungespeicherter Änderung nach. <see langword="false"/> = abbrechen (Fenster, Auswahl oder Aktion bleiben, wie sie sind).</summary>
    private bool ConfirmLeave()
    {
        if (!_dirty)
        {
            return true;
        }

        var answer = MessageBox.Show(this, PublishTexts.TextChanged(_editingVersion), "Releases", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        switch (answer)
        {
            case MessageBoxResult.Yes:
                return SaveText();
            case MessageBoxResult.No:
                SetDirty(false);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Schreibt den Editor-Text in den Changelog. <see langword="true"/>, wenn danach nichts Ungespeichertes mehr da ist.</summary>
    private bool SaveText()
    {
        if (_editingVersion is not { } version)
        {
            return true;
        }

        var newText = PreviewBox.Text;
        if (!Try(() => ModifyChangelog(fileText => ChangelogEditor.SetText(fileText, version, newText))))
        {
            return false;
        }

        SetDirty(false);
        Reload();
        return true;
    }

    private void SaveText_Click(object sender, RoutedEventArgs e) => SaveText();

    private void RevertText_Click(object sender, RoutedEventArgs e)
    {
        SetDirty(false);
        Reload();
    }

    private void ReleaseList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_reverting)
        {
            return;
        }

        if (_dirty && e.RemovedItems.Count == 1 && !ConfirmLeave())
        {
            _reverting = true;
            ReleaseList.SelectedItem = e.RemovedItems[0];
            _reverting = false;
            return;
        }

        UpdateButtons();
        ShowPreview();
    }

    private void Report(string message) =>
        MessageBox.Show(this, message, "Releases", MessageBoxButton.OK, MessageBoxImage.Information);

    private bool Try(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, "Releases", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Liest den Changelog frisch, wendet die Änderung an und schreibt ihn mit Sicherungskopie zurück, damit eine Änderung von außen nicht überschrieben wird.</summary>
    private void ModifyChangelog(Func<string, string> change)
    {
        var path = ChangelogPath ?? throw new InvalidOperationException(PublishTexts.NoChangelogFile);
        var document = ModTextFile.Read(path);
        ModTextFile.Write(path, document with { Text = change(document.Text) }, BackupDirectory);
    }

    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    private void PickChangelog_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var folder = _mod.Paths.FirstOrDefault(Directory.Exists);
        if (folder is null)
        {
            Report(PublishTexts.ModFolderMissing);
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = PublishTexts.PickChangelogTitle,
            InitialDirectory = Path.GetFullPath(folder),
            Filter = PublishTexts.PickChangelogFilter,
        };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("releases.file");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(folder), dialog.FileName);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            Report(PublishTexts.ChangelogOutsideMod);
            return;
        }

        if (Try(() => _registry.SetChangelogFile(_mod.Id, relative.Replace('\\', '/'))))
        {
            Reload();
        }
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var status = ReleaseStatus.Evaluate(_metadata, _entries, _log.Releases);
        if (status.MetadataVersion is null)
        {
            Report(PublishTexts.MetadataNoVersion);
            return;
        }

        if (_log.Find(status.MetadataVersion) is not null)
        {
            Report(PublishTexts.AlreadyRecorded(status.MetadataVersion));
            return;
        }

        if (Try(() => _log.Add(status.MetadataVersion, Today)))
        {
            Reload();
            ReleaseList.SelectedItem = ReleaseList.Items.OfType<ReleaseRow>().FirstOrDefault(r => r.Version == status.MetadataVersion);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var missing = (_entries ?? new List<ChangelogEntry>())
            .Where(entry => _log.Find(entry.Version) is null)
            .Select(entry => entry.Version)
            .Distinct()
            .ToList();
        if (missing.Count == 0)
        {
            Report(PublishTexts.AllRecorded);
            return;
        }

        if (ChangelogImportWindow.Pick(this, missing) is not { Count: > 0 } chosen)
        {
            return;
        }

        if (Try(() =>
            {
                foreach (var version in chosen)
                {
                    _log.Add(version, string.Empty);
                }
            }))
        {
            Reload();
        }
    }

    private void NewVersion_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var status = ReleaseStatus.Evaluate(_metadata, _entries, _log.Releases);
        var proposal = NextVersion(status);
        if (TextPromptWindow.Ask(this, PublishTexts.NewVersion, PublishTexts.NewVersionPrompt, proposal) is not { } text)
        {
            return;
        }

        var version = text.Trim();
        var modName = string.IsNullOrWhiteSpace(_metadata?.Name) ? _mod.Name : _metadata!.Name;
        var created = Try(() =>
        {
            if (!VersionNumber.TryParse(version, out _))
            {
                throw new ArgumentException(PublishTexts.NotAVersion(version));
            }

            ModifyChangelog(fileText => ChangelogEditor.InsertVersion(fileText, version, modName));
            if (_log.Find(version) is null)
            {
                _log.Add(version, Today);
            }
        });
        if (created)
        {
            Reload();
            ReleaseList.SelectedItem = ReleaseList.Items.OfType<ReleaseRow>().FirstOrDefault(r => r.Version == version);
            InfoText.Text = PublishTexts.ReleasesBannerCreated(version);
        }
    }

    /// <summary>Vorschlag für die nächste Version: die höchste bekannte Nummer, letzter Teil plus eins.</summary>
    private static string NextVersion(ReleaseStatus status)
    {
        var known = new[] { status.MetadataVersion, status.ChangelogTop, status.LatestRelease }
            .Where(v => VersionNumber.TryParse(v, out _))
            .OrderByDescending(v =>
            {
                VersionNumber.TryParse(v, out var number);
                return number;
            })
            .FirstOrDefault();
        if (known is null)
        {
            return "1.0.0";
        }

        var parts = known.Split('.');
        parts[^1] = (int.Parse(parts[^1]) + 1).ToString();
        return string.Join(".", parts);
    }

    private void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave() || !ConfirmNoTraces())
        {
            return;
        }

        new PublishWindow(_registry, _mod, _metadataFile, _steam) { Owner = this }.Show();
    }

    private void GitHubPublish_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave() || !ConfirmNoTraces())
        {
            return;
        }

        new GitHubPublishWindow(_registry, _mod, _metadataFile, _steam) { Owner = this }.Show();
    }

    /// <summary>
    /// Scope-Verfolgung: Enthält die Mod noch Spuren (<c>PMT-TRACE</c>), fragt PMT vor dem Veröffentlichen nach. Entfernen geht im
    /// Fehlerprotokoll unter "Aktive Spuren" (dort mit Sicherung und Rückgängig).
    /// </summary>
    private bool ConfirmNoTraces()
    {
        var count = _mod.Paths
            .Where(Directory.Exists)
            .Sum(path => ParadoxModTool.ErrorLog.TraceMarkers.Scan(new ParadoxModTool.ErrorLog.ModSource(_mod.Name, path, IsOwn: true, LoadOrder: 0), ParadoxModTool.ErrorLog.TraceRules.Empty).Count);
        return count == 0
            || MessageBox.Show(this, ParadoxModTool.ErrorLog.ErrorLogTexts.PublishTracesWarning(count), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static string Marks(ReleaseEntry entry)
    {
        var marks = new List<string>();
        if (entry.Steam is not null)
        {
            marks.Add("Steam");
        }

        if (entry.GitHub is { Count: > 0 })
        {
            marks.Add("GitHub");
        }

        return marks.Count == 0 ? string.Empty : string.Join(" ", marks) + "  ";
    }

    private void ChangeDate_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        if (SelectedRow is not { } row)
        {
            return;
        }

        if (TextPromptWindow.Ask(this, PublishTexts.ChangeDate, PublishTexts.DatePrompt(row.Version), row.Date) is { } date
            && Try(() => _log.SetDate(row.Version, date.Trim())))
        {
            Reload();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        if (SelectedRow is not { } row)
        {
            return;
        }

        if (MessageBox.Show(this, PublishTexts.DeleteConfirm(row.Version), "Releases", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        if (Try(() => _log.Remove(row.Version)))
        {
            Reload();
        }
    }

    private sealed class ReleaseRow
    {
        public ReleaseRow(ReleaseEntry entry, bool inChangelog)
        {
            Version = entry.Version;
            Date = entry.Date;
            Display = $"{entry.Version,-12}{(entry.Date.Length == 0 ? PublishTexts.NoDate : entry.Date),-14}{Marks(entry)}{(inChangelog ? string.Empty : PublishTexts.NotInChangelog)}";
        }

        public string Version { get; }

        public string Date { get; }

        public string Display { get; }
    }
}
