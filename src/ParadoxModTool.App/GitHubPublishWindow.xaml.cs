using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.Mods;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Auf GitHub veröffentlichen": Repositories wählen, Titel und Release-Notes prüfen, ZIP-Varianten wählen und über <see cref="GitHubPublisher"/> als Release veröffentlichen; dazu die
/// Reiter für Git (committen, pushen) und Aufräumen. Alles läuft über <c>gh</c> und <c>git</c>; das Programm sieht nie ein Token.
/// </summary>
public partial class GitHubPublishWindow : Window
{
    private static string Caption => PublishTexts.GitHubTitle;

    private readonly ModRegistry _registry;
    private readonly ModInfo _mod;
    private readonly string _metadataFile;
    private readonly SteamPublishHost _host;
    private readonly GhSettingsStore _settingsStore;
    private readonly IProcessRunner _runner = new ProcessRunner();
    private readonly GitClient _git;

    private GhSettings _settings;
    private ReleaseLog _log;
    private ModMetadata? _metadata;
    private IReadOnlyList<ChangelogEntry>? _entries;
    private string? _ghPath;
    private GhClient? _gh;
    private bool _ghLoggedIn;
    private GitState? _gitState;
    private bool _initialized;
    private bool _publishing;
    private bool _filling;
    private bool _titleEdited;
    private bool _notesEdited;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<string> _zipProblems = Array.Empty<string>();
    private readonly Dictionary<string, GitHubRepoCheck> _checks = new(StringComparer.OrdinalIgnoreCase);
    private int _checkGeneration;
    private PublishReadiness? _readiness;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _gitBusy;

    public GitHubPublishWindow(ModRegistry registry, ModInfo mod, string metadataFile, SteamPublishHost host)
    {
        InitializeComponent();
        _registry = registry;
        _mod = mod;
        _metadataFile = metadataFile;
        _host = host;
        _git = new GitClient(_runner);
        _settingsStore = new GhSettingsStore(host.GameDirectory);
        _settings = _settingsStore.Load();
        _log = ReleaseLog.Load(registry.DataDirectory(mod.Id));

        TitleText.Text = PublishTexts.GitHubTitleFor(mod.Name);
        if (_settingsStore.LoadWarning is { } warning)
        {
            ResultText.Text = warning;
        }

        _initialized = true;
        ReloadAll();

        Activated += (_, _) =>
        {
            if (!_publishing && DateTime.Now - _lastRefresh > TimeSpan.FromSeconds(20))
            {
                ReloadAll();
            }
        };
        Closing += (_, e) =>
        {
            if (_publishing)
            {
                e.Cancel = true;
                Report(PublishTexts.PublishRunning);
            }
        };
    }

    private string? ModFolder => _metadata?.Folder ?? _mod.Paths.FirstOrDefault();

    private string? Version => _metadata?.Version;

    private bool VersionValid => VersionNumber.TryParse(Version, out _);

    private bool Classic => ModMetadata.IsClassic(_metadataFile);

    private Brush Danger => TryFindResource("Theme.Danger") as Brush ?? Brushes.Firebrick;

    private Brush Muted => TryFindResource("Theme.TextMuted") as Brush ?? Brushes.Gray;

    private void Report(string message) =>
        MessageBox.Show(this, message, Caption, MessageBoxButton.OK, MessageBoxImage.Information);

    private string TagFor(GitHubRepoEntry entry) => GitHubNames.ExpandTag(entry.TagFormat, Version ?? string.Empty, _mod.Name);

    // ---- Laden

    private void ReloadAll()
    {
        _lastRefresh = DateTime.Now;
        _log = ReleaseLog.Load(_registry.DataDirectory(_mod.Id));
        _metadata = ModMetadata.Find(_mod, _metadataFile);
        LoadChangelog();
        VersionText.Text = _metadata is null
            ? ModsTexts.NoReadableMetadataFor(Classic)
            : VersionValid ? $"Version {Version}" : ModsTexts.NoValidVersionFor(Classic);
        if (string.IsNullOrWhiteSpace(CommitMessageBox.Text))
        {
            CommitMessageBox.Text = VersionValid ? $"Release {Version}" : string.Empty;
        }

        FillReleaseFields();
        RefreshRepos();
        RefreshVariants();
        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await RefreshGitStateAsync();
        await RefreshGhAsync();
    }

    private void LoadChangelog()
    {
        _entries = null;
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
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _entries = null;
        }
    }

    private ChangelogEntry? CurrentSection() =>
        VersionNumber.TryParse(Version, out var number)
            ? _entries?.FirstOrDefault(e => VersionNumber.TryParse(e.Version, out var other) && other.Equals(number))
            : null;

    private void FillReleaseFields()
    {
        _filling = true;
        if (!_titleEdited)
        {
            TitleBox.Text = VersionValid ? $"{_mod.Name} {Version}" : _mod.Name;
        }

        if (!_notesEdited)
        {
            NotesBox.Text = ReleaseNotesFormatter.ToMarkdown(CurrentSection()?.Text);
        }

        _filling = false;
        UpdateNotesCount();
    }

    private void UpdateNotesCount()
    {
        NotesCountText.Text = PublishTexts.NotesCount(NotesBox.Text.Length, ReleaseNotesFormatter.MaxLength);
        NotesCountText.Foreground = ReleaseNotesFormatter.IsTooLong(NotesBox.Text) ? Danger : Muted;
    }

    private void TitleBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized || _filling)
        {
            return;
        }

        _titleEdited = true;
        RefreshChecks();
    }

    private void NotesBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized || _filling)
        {
            return;
        }

        _notesEdited = true;
        UpdateNotesCount();
        RefreshChecks();
    }

    private void ReleaseOption_Click(object sender, RoutedEventArgs e) => RefreshChecks();

    // ---- gh: finden, herunterladen, anmelden

    private async Task RefreshGhAsync()
    {
        _ghPath = GhLocator.Find(_settings);
        _gh = _ghPath is null ? null : new GhClient(_runner, _ghPath);
        _ghLoggedIn = false;
        if (_gh is not null)
        {
            GhStatusText.Text = PublishTexts.GhChecking;
            _ghLoggedIn = await _gh.IsLoggedInAsync(CancellationToken.None);
        }

        GhStatusText.Text = _ghPath is null
            ? PublishTexts.GhMissing
            : _ghLoggedIn ? PublishTexts.GhLoggedIn(_ghPath) : PublishTexts.GhNotLoggedIn(_ghPath);
        GhDownloadButton.Visibility = _ghPath is null ? Visibility.Visible : Visibility.Collapsed;
        GhLoginButton.Visibility = _ghPath is not null && !_ghLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        RefreshChecks();
        await OnGhStatusChangedAsync();
    }

    /// <summary>Wird nach jeder Aktualisierung des <c>gh</c>-Status aufgerufen; spätere Tasks hängen hier die Prüfung der Repositories an.</summary>
    private async Task OnGhStatusChangedAsync() => await CheckReposAsync();

    private async void GhRefresh_Click(object sender, RoutedEventArgs e)
    {
        _lastRefresh = DateTime.Now;
        await RefreshGitStateAsync();
        await RefreshGhAsync();
    }

    private async void GhDownload_Click(object sender, RoutedEventArgs e)
    {
        var folder = GhInstaller.DefaultFolder;
        var answer = MessageBox.Show(
            this,
            PublishTexts.GhDownloadConfirm(folder),
            PublishTexts.GhDownload,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        GhDownloadButton.IsEnabled = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ParadoxModTool");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var download = GhInstaller.SelectDownload(await http.GetStringAsync(GhInstaller.LatestReleaseApi));
            var expected = GhInstaller.ExpectedSha256(await http.GetStringAsync(download.ChecksumsUrl), download.ZipName);
            var bytes = await http.GetByteArrayAsync(download.ZipUrl);
            _settings.GhPath = GhInstaller.Install(bytes, expected, folder);
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Report(PublishTexts.DownloadFailed(ex.Message));
        }
        finally
        {
            GhDownloadButton.IsEnabled = true;
        }

        await RefreshGhAsync();
    }

    private void GhLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_ghPath is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_ghPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(_ghPath)!,
                Arguments = "auth login --web --hostname github.com --git-protocol https",
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Report(GitTexts.GhStartFailed(ex.Message));
            return;
        }

        Report(PublishTexts.GhLoginStarted);
    }

    // ---- Git-Stand

    private async Task RefreshGitStateAsync()
    {
        _gitState = ModFolder is { } folder ? await _git.InspectAsync(folder, CancellationToken.None) : null;
        OnGitStateChanged();
    }

    /// <summary>Wird aufgerufen, wenn sich <see cref="_gitState"/> geändert hat; spätere Tasks ergänzen hier den Git-Reiter.</summary>
    private void OnGitStateChanged()
    {
        RefreshRepos();
        RefreshChecks();
        UpdateGitTab();
    }

    // ---- Repositories

    private sealed class RepoRow
    {
        public RepoRow(GitHubRepoEntry entry, string tag)
        {
            Entry = entry;
            Enabled = entry.Enabled;
            Text = $"{entry.Repo}    Tag: {tag}";
        }

        public GitHubRepoEntry Entry { get; }

        public bool Enabled { get; set; }

        public string Text { get; }
    }

    private IEnumerable<RepoRow> RepoRows => RepoList.ItemsSource as IEnumerable<RepoRow> ?? Enumerable.Empty<RepoRow>();

    private RepoRow? SelectedRepoRow => RepoList.SelectedItem as RepoRow;

    private List<GitHubRepoEntry> CurrentRepos() =>
        RepoRows.Select(r => new GitHubRepoEntry { Repo = r.Entry.Repo, TagFormat = r.Entry.TagFormat, Enabled = r.Enabled }).ToList();

    private IReadOnlyList<GitHubRepoEntry> EnabledRepos() => CurrentRepos().Where(r => r.Enabled).ToList();

    private void RefreshRepos()
    {
        var keep = SelectedRepoRow?.Entry.Repo;
        var rows = _mod.GitHubRepos.Select(r => new RepoRow(r, TagFor(r))).ToList();
        RepoList.ItemsSource = rows;
        RepoList.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Entry.Repo, keep, StringComparison.OrdinalIgnoreCase));
        var origin = _gitState?.OriginRepo;
        RepoOriginButton.Visibility = origin is not null && !_mod.GitHubRepos.Any(r => r.Repo.Equals(origin, StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible
            : Visibility.Collapsed;
        RepoEditButton.IsEnabled = RepoRemoveButton.IsEnabled = rows.Count > 0;
        RefreshCleanupRepos();
    }

    private bool SaveRepos(IEnumerable<GitHubRepoEntry> repos)
    {
        try
        {
            _registry.SetGitHubRepos(_mod.Id, repos);
        }
        catch (ArgumentException ex)
        {
            Report(ex.Message);
            RefreshRepos();
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report(PublishTexts.ReposSaveFailed(ex.Message));
            RefreshRepos();
            return false;
        }

        RefreshRepos();
        RefreshZipProblems();
        _ = OnGhStatusChangedAsync();
        return true;
    }

    private void RepoCheck_Click(object sender, RoutedEventArgs e) => SaveRepos(CurrentRepos());

    private void RepoAdd_Click(object sender, RoutedEventArgs e)
    {
        if (GitHubRepoDialog.Ask(this, null, _gitState?.OriginRepo) is { } entry)
        {
            SaveRepos(CurrentRepos().Append(entry));
        }
    }

    private void RepoEdit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRepoRow is not { } row || GitHubRepoDialog.Ask(this, new GitHubRepoEntry { Repo = row.Entry.Repo, TagFormat = row.Entry.TagFormat, Enabled = row.Enabled }) is not { } changed)
        {
            return;
        }

        SaveRepos(CurrentRepos().Select(r => r.Repo.Equals(row.Entry.Repo, StringComparison.OrdinalIgnoreCase) ? changed : r));
    }

    private void RepoRemove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRepoRow is not { } row
            || MessageBox.Show(this, PublishTexts.RepoRemoveConfirm(row.Entry.Repo), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SaveRepos(CurrentRepos().Where(r => !r.Repo.Equals(row.Entry.Repo, StringComparison.OrdinalIgnoreCase)));
    }

    private void RepoOrigin_Click(object sender, RoutedEventArgs e)
    {
        if (_gitState?.OriginRepo is { } origin)
        {
            SaveRepos(CurrentRepos().Append(new GitHubRepoEntry { Repo = origin }));
        }
    }

    // ---- Reiter "Git"

    private void UpdateGitTab()
    {
        var state = _gitState;
        if (state is null)
        {
            GitInfoText.Text = PublishTexts.NotGitFolder;
            GitChangeList.ItemsSource = null;
            GitCommitButton.IsEnabled = GitPushButton.IsEnabled = false;
            return;
        }

        GitInfoText.Text = PublishTexts.GitInfo(
            state.TopLevel,
            state.Origin ?? PublishTexts.GitNone,
            state.Branch ?? PublishTexts.GitDetached,
            state.Upstream ?? PublishTexts.GitNone,
            state.ShortHead,
            state.HeadSubject,
            state.Ahead,
            state.Behind,
            state.Changes.Count,
            state.HeadPushed ? PublishTexts.GitYes : PublishTexts.GitNo);
        var lines = state.Changes.Take(200).Select(c => $"{c.Status,-3} {c.Path}").ToList();
        if (state.Changes.Count > 200)
        {
            lines.Add(PublishTexts.AndMore(state.Changes.Count - 200));
        }

        GitChangeList.ItemsSource = lines;
        GitCommitButton.IsEnabled = !state.IsClean && !_gitBusy;
        GitPushButton.IsEnabled = !_gitBusy && state.Branch is not null && state.Behind == 0 && (!state.HeadPushed || state.Upstream is null || state.Ahead > 0);
    }

    private async void GitRefresh_Click(object sender, RoutedEventArgs e) => await RefreshGitStateAsync();

    private async void GitCommit_Click(object sender, RoutedEventArgs e)
    {
        if (_gitState is not { } state || ModFolder is not { } folder || _gitBusy)
        {
            return;
        }

        var message = CommitMessageBox.Text.Trim();
        if (message.Length == 0)
        {
            Report(GitTexts.CommitMessageMissing);
            return;
        }

        var shown = string.Join("\n", state.Changes.Take(15).Select(c => $"{c.Status,-3} {c.Path}"));
        var more = state.Changes.Count > 15 ? "\n" + PublishTexts.AndMore(state.Changes.Count - 15) : string.Empty;
        if (MessageBox.Show(
                this,
                PublishTexts.CommitConfirm(state.Changes.Count, shown, more, message),
                Caption,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await RunGitAsync(() => _git.CommitAllAsync(folder, message, CancellationToken.None));
    }

    private async void GitPush_Click(object sender, RoutedEventArgs e)
    {
        if (_gitState is not { } state || ModFolder is not { } folder || _gitBusy)
        {
            return;
        }

        var target = state.Upstream ?? $"origin/{state.Branch}";
        if (MessageBox.Show(this, PublishTexts.PushConfirm(state.Branch, target), Caption, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await RunGitAsync(() => _git.PushAsync(folder, state, CancellationToken.None));
    }

    private async Task RunGitAsync(Func<Task<GitOperationResult>> operation)
    {
        _gitBusy = true;
        GitCommitButton.IsEnabled = GitPushButton.IsEnabled = false;
        try
        {
            var result = await operation();
            GitOutputBox.Text = string.Join(Environment.NewLine, result.Output);
            if (!result.Success)
            {
                Report(result.Message);
            }
            else
            {
                ResultText.Text = result.Message;
                ResultText.Foreground = Muted;
            }
        }
        finally
        {
            _gitBusy = false;
            await RefreshGitStateAsync();
        }
    }

    // ---- ZIP-Varianten

    private sealed class VariantRow
    {
        public VariantRow(GitHubZipVariant variant)
        {
            Variant = variant;
            Enabled = variant.Enabled;
            var details = new List<string> { variant.FileNameFormat };
            if (variant.OnlyFolders.Count > 0)
            {
                details.Add(PublishTexts.VariantOnly(string.Join(", ", variant.OnlyFolders)));
            }

            if (variant.RootFolder is not null)
            {
                details.Add(PublishTexts.VariantRoot(variant.RootFolder));
            }

            Text = $"{variant.Name}    ({string.Join("; ", details)})";
        }

        public GitHubZipVariant Variant { get; }

        public bool Enabled { get; set; }

        public string Text { get; }
    }

    private IEnumerable<VariantRow> VariantRows => VariantList.ItemsSource as IEnumerable<VariantRow> ?? Enumerable.Empty<VariantRow>();

    private VariantRow? SelectedVariantRow => VariantList.SelectedItem as VariantRow;

    /// <summary>Die gespeicherten Varianten oder, wenn es keine gibt, die nicht gespeicherte Standardvariante.</summary>
    private IReadOnlyList<GitHubZipVariant> StoredOrDefaultVariants() =>
        _mod.GitHubZipVariants.Count > 0 ? _mod.GitHubZipVariants : new List<GitHubZipVariant> { new() { Name = "Standard" } };

    private IReadOnlyList<GitHubZipVariant> AllVariants() =>
        VariantRows.Select(r => Copy(r.Variant, r.Enabled)).ToList();

    private IReadOnlyList<GitHubZipVariant> EnabledVariants() => AllVariants().Where(v => v.Enabled).ToList();

    private static GitHubZipVariant Copy(GitHubZipVariant v, bool enabled) => new()
    {
        Name = v.Name,
        FileNameFormat = v.FileNameFormat,
        ExcludedFolders = v.ExcludedFolders.ToList(),
        ExcludedFilePatterns = v.ExcludedFilePatterns.ToList(),
        OnlyFolders = v.OnlyFolders.ToList(),
        RootFolder = v.RootFolder,
        Enabled = enabled,
    };

    private void RefreshVariants()
    {
        var keep = SelectedVariantRow?.Variant.Name;
        var rows = StoredOrDefaultVariants().Select(v => new VariantRow(v)).ToList();
        VariantList.ItemsSource = rows;
        VariantList.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Variant.Name, keep, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault();
        VariantEditButton.IsEnabled = VariantRemoveButton.IsEnabled = VariantListButton.IsEnabled = rows.Count > 0;
        RefreshZipProblems();
        UpdateVariantInfo();
    }

    private void RefreshZipProblems()
    {
        _zipProblems = ModFolder is { } folder && VersionValid
            ? ZipPackager.Validate(folder, _mod.Name, Version!, FirstTag(), EnabledVariants())
            : Array.Empty<string>();
        RefreshChecks();
    }

    /// <summary>Der Tag des ersten angehakten Repositories (für den Platzhalter {tag} im ZIP-Dateinamen); ohne Repo der Standard-Tag.</summary>
    private string FirstTag() =>
        EnabledRepos().FirstOrDefault() is { } first ? TagFor(first) : GitHubNames.ExpandTag(null, Version ?? string.Empty, _mod.Name);

    private void UpdateVariantInfo()
    {
        if (SelectedVariantRow is not { } row || ModFolder is not { } folder)
        {
            VariantInfoText.Text = string.Empty;
            return;
        }

        try
        {
            var result = ZipPackager.Measure(folder, row.Variant);
            VariantInfoText.Text = PublishTexts.VariantInfo(result.FileCount, FormatBytes(result.TotalBytes), result.Excluded.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            VariantInfoText.Text = GitTexts.ModFolderUnreadable(ex.Message);
        }
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:N1} MB" : $"{Math.Max(1, bytes / 1024):N0} KB";

    private bool SaveVariants(IEnumerable<GitHubZipVariant> variants)
    {
        try
        {
            _registry.SetGitHubZipVariants(_mod.Id, variants);
        }
        catch (ArgumentException ex)
        {
            Report(ex.Message);
            RefreshVariants();
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report(PublishTexts.VariantsSaveFailed(ex.Message));
            RefreshVariants();
            return false;
        }

        RefreshVariants();
        return true;
    }

    private void VariantList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateVariantInfo();

    private void VariantCheck_Click(object sender, RoutedEventArgs e) => SaveVariants(AllVariants());

    private void VariantAdd_Click(object sender, RoutedEventArgs e)
    {
        if (ZipVariantDialog.Ask(this) is { } variant)
        {
            SaveVariants(AllVariants().Append(variant));
        }
    }

    private void VariantEdit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVariantRow is not { } row || ZipVariantDialog.Ask(this, Copy(row.Variant, row.Enabled)) is not { } changed)
        {
            return;
        }

        SaveVariants(AllVariants().Select(v => v.Name.Equals(row.Variant.Name, StringComparison.OrdinalIgnoreCase) ? changed : v));
    }

    private void VariantRemove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVariantRow is not { } row
            || MessageBox.Show(this, PublishTexts.VariantRemoveConfirm(row.Variant.Name), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SaveVariants(AllVariants().Where(v => !v.Name.Equals(row.Variant.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private void VariantExcluded_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVariantRow is not { } row || ModFolder is not { } folder)
        {
            return;
        }

        try
        {
            var excluded = ZipPackager.Measure(folder, row.Variant).Excluded;
            var lines = excluded.Take(60).ToList();
            var more = excluded.Count > lines.Count ? "\n" + PublishTexts.AndMore(excluded.Count - lines.Count) : string.Empty;
            Report(lines.Count == 0 ? PublishTexts.VariantNothingExcluded : PublishTexts.VariantExcludedList(string.Join("\n", lines) + more));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report(GitTexts.ModFolderUnreadable(ex.Message));
        }
    }

    private async void VariantBuild_Click(object sender, RoutedEventArgs e)
    {
        if (ModFolder is not { } folder || !VersionValid)
        {
            Report(PublishTexts.ZipNeedsVersionFor(Classic));
            return;
        }

        var variants = EnabledVariants();
        if (variants.Count == 0)
        {
            Report(PublishTexts.NoVariantChecked);
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = PublishTexts.ZipFolderPick, UseDescriptionForTitle = true };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("publish.zipFolder");
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var target = dialog.SelectedPath;
        VariantBuildButton.IsEnabled = false;
        try
        {
            var tag = FirstTag();
            var zips = await Task.Run(() => ZipPackager.Build(folder, _mod.Name, Version!, tag, variants, target));
            Report(PublishTexts.ZipsCreated(zips.Count, target));
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report(GitTexts.ZipsFailed(ex.Message));
        }
        finally
        {
            VariantBuildButton.IsEnabled = true;
        }
    }

    // ---- Prüfungen

    private static string Key(string repo, string tag) => repo + "|" + tag;

    private sealed record HintRow(string Text, Brush Brush);

    private sealed record ResultRow(string Text, string? Url);

    /// <summary>Prüft für jedes angehakte Repository Erreichbarkeit, Schreibrecht und ob der Tag schon ein Release hat (Netzaufrufe über gh).</summary>
    private async Task CheckReposAsync()
    {
        var generation = ++_checkGeneration;
        _checks.Clear();
        RefreshChecks();
        if (_gh is null || !_ghLoggedIn || !VersionValid)
        {
            return;
        }

        foreach (var repo in EnabledRepos())
        {
            var tag = TagFor(repo);
            GitHubRepoCheck check;
            try
            {
                var info = await _gh.GetRepoAsync(repo.Repo, CancellationToken.None);
                if (info.IsEmpty)
                {
                    check = new GitHubRepoCheck(repo.Repo, tag, GhRepoInfo.EmptyMessage, info.CanWrite, null);
                }
                else
                {
                    var existing = info.CanWrite ? await _gh.FindReleaseAsync(repo.Repo, tag, CancellationToken.None) : null;
                    check = new GitHubRepoCheck(repo.Repo, tag, null, info.CanWrite, existing is not null);
                }
            }
            catch (GhException ex)
            {
                check = new GitHubRepoCheck(repo.Repo, tag, ex.Message, null, null);
            }

            if (generation != _checkGeneration)
            {
                return;
            }

            _checks[Key(repo.Repo, tag)] = check;
            RefreshChecks();
        }
    }

    private void RefreshChecks()
    {
        if (!_initialized)
        {
            return;
        }

        var repos = EnabledRepos();
        var checks = repos
            .Select(r => (Repo: r.Repo, Tag: TagFor(r)))
            .Select(t => _checks.TryGetValue(Key(t.Repo, t.Tag), out var known) ? known : new GitHubRepoCheck(t.Repo, t.Tag, null, null, null))
            .ToList();
        _readiness = GitHubReadiness.Evaluate(new GitHubReadinessInput(
            GhFound: _ghPath is not null,
            LoggedIn: _ghLoggedIn,
            VersionValid: VersionValid,
            ChangelogExists: _entries is not null,
            ChangelogHasVersion: CurrentSection() is not null,
            Title: TitleBox.Text,
            Notes: NotesBox.Text,
            SelectedRepoCount: repos.Count,
            Checks: checks,
            UpdateExisting: UpdateCheck.IsChecked == true,
            ZipProblems: _zipProblems,
            GitWarnings: GitWarnings.From(_gitState),
            ClassicMetadata: Classic));
        HintList.ItemsSource = _readiness.Hints.Select(h => new HintRow(h.Text, h.Blocking ? Danger : Muted)).ToList();
        PublishButton.IsEnabled = _readiness.CanPublish && !_publishing;
    }

    // ---- Veröffentlichen

    private void SetPublishing(bool publishing)
    {
        _publishing = publishing;
        CancelButton.Visibility = publishing ? Visibility.Visible : Visibility.Collapsed;
        PublishProgress.Visibility = publishing ? Visibility.Visible : Visibility.Collapsed;
        PublishButton.IsEnabled = !publishing && _readiness?.CanPublish == true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (_publishing)
        {
            return;
        }

        RefreshChecks();
        if (_readiness is not { CanPublish: true } || _gh is null || ModFolder is not { } folder || !VersionValid)
        {
            Report(PublishTexts.GitHubNotReady);
            return;
        }

        var tagHead = true;
        var warnings = GitWarnings.From(_gitState);
        if (warnings.Count > 0)
        {
            var answer = MessageBox.Show(
                this,
                PublishTexts.GitNotClean(string.Join("\n", warnings)),
                Caption,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel)
            {
                return;
            }

            if (answer == MessageBoxResult.No)
            {
                Tabs.SelectedIndex = 1;
                return;
            }

            tagHead = false;
        }

        var repos = EnabledRepos();
        var variants = EnabledVariants();
        var summary = PublishTexts.GitHubConfirm(Version, _mod.Name) + "\n\n"
            + string.Join("\n", repos.Select(r => $"• {r.Repo}  (Tag {TagFor(r)})"))
            + "\n\n" + PublishTexts.ConfirmZips(variants.Count == 0 ? PublishTexts.ConfirmNoZips : string.Join(", ", variants.Select(v => v.Name)))
            + (DraftCheck.IsChecked == true ? "\n" + PublishTexts.ConfirmDraft : string.Empty)
            + (PreReleaseCheck.IsChecked == true ? "\n" + PublishTexts.ConfirmPreRelease : string.Empty);
        if (MessageBox.Show(this, summary, Caption, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        var request = new GitHubPublishRequest(
            _mod.Name,
            Version!,
            folder,
            repos,
            TitleBox.Text.Trim(),
            NotesBox.Text,
            DraftCheck.IsChecked == true,
            PreReleaseCheck.IsChecked == true,
            UpdateCheck.IsChecked == true,
            variants,
            _gitState,
            tagHead);

        SetPublishing(true);
        OutputBox.Clear();
        OutputExpander.IsExpanded = true;
        ResultList.ItemsSource = null;
        ResultText.Text = string.Empty;
        _cts = new CancellationTokenSource();
        var lines = new Progress<string>(line =>
        {
            OutputBox.AppendText(line + Environment.NewLine);
            OutputBox.ScrollToEnd();
        });

        GitHubPublishResult result;
        try
        {
            result = await new GitHubPublisher(_gh).PublishAsync(request, lines, _cts.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result = new GitHubPublishResult(Array.Empty<GitHubRepoResult>(), PublishTexts.GitHubPublishFailed(ex.Message));
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetPublishing(false);
        }

        HandleResult(result);
    }

    private void HandleResult(GitHubPublishResult result)
    {
        if (result.Problem is not null)
        {
            ResultText.Text = result.Problem;
            ResultText.Foreground = Danger;
            return;
        }

        var problems = new List<string>();
        var successes = result.Repos.Where(r => r.Succeeded).ToList();
        if (successes.Count > 0 && VersionValid)
        {
            try
            {
                if (_log.Find(Version!) is null)
                {
                    _log.Add(Version!, DateTime.Now.ToString("yyyy-MM-dd"));
                }

                _log.SetGitHub(
                    Version!,
                    successes.Select(r => new GitHubPublication { Repo = r.Repo, Tag = r.Tag, Url = r.Url ?? string.Empty, PublishedAt = DateTime.Now.ToString("s") }));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                problems.Add(PublishTexts.ReleaseMarkFailed(ex.Message));
            }
        }

        ResultList.ItemsSource = result.Repos.Select(r => new ResultRow($"{r.Repo}  {r.Tag}: {r.Message}", r.Url)).ToList();
        ResultText.Text = string.Join("\n", new[] { PublishTexts.ResultSummary(successes.Count, result.Repos.Count) }.Concat(problems));
        ResultText.Foreground = result.AllSucceeded && problems.Count == 0 ? Muted : Danger;
        _ = CheckReposAsync();
    }

    private void ResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        OpenReleaseButton.IsEnabled = (ResultList.SelectedItem as ResultRow)?.Url is { Length: > 0 };

    private void ResultList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenRelease();

    private void OpenRelease_Click(object sender, RoutedEventArgs e) => OpenRelease();

    private void OpenRelease()
    {
        if ((ResultList.SelectedItem as ResultRow)?.Url is { } url && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
