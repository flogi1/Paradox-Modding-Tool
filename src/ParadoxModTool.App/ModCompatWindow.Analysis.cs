using System.IO;
using System.Text;
using System.Windows;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile der Ergebnisliste.</summary>
internal sealed class FindingRow
{
    public FindingRow(CompatFinding finding)
    {
        Finding = finding;
    }

    public CompatFinding Finding { get; }

    public string SeverityText => CompatText.Severity(Finding.Severity);

    public string KindText => CompatText.Kind(Finding.Kind);

    public string Path => Finding.RelativePath;

    public string Key => Finding.Key;

    public string WinnerText => Finding.Winner?.Name ?? string.Empty;

    public string SuggestionText => CompatText.Suggestion(Finding.Suggestion);

    public string UncertainText => Finding.Uncertain ? "unsicher" : string.Empty;
}

/// <summary>Rechte Hälfte des Fensters "Mod-Kompatibilität": Prüfung, Ergebnisse, Vergleich, Bericht.</summary>
public partial class ModCompatWindow
{
    private IReadOnlyList<CompatFinding> _all = Array.Empty<CompatFinding>();
    private string _subtitle = string.Empty;
    private CancellationTokenSource? _cts;
    private bool _busy;

    private string RulesFile => System.IO.Path.Combine(_host.GameDirectory, "compat", "rules.json");

    /// <summary>Die Regeltabelle: ausgelieferte Tabelle des Spielprofils, darüber die Spielebene.</summary>
    private OverrideRules BuildRules()
    {
        var game = OverrideRulesStore.Load(RulesFile);
        return _host.Profile is { } profile ? OverrideRules.FromProfile(profile, game) : new OverrideRules(Array.Empty<OverrideRule>(), game);
    }

    partial void OnClosedCore() => _cts?.Cancel();

    partial void OnMembersChanged()
    {
        var keep = (FocusCombo.SelectedItem as MemberRow)?.Member.Folder;
        var rows = MemberRows;
        FocusCombo.ItemsSource = rows;
        FocusCombo.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Member.Folder, keep, StringComparison.OrdinalIgnoreCase))
            ?? rows.FirstOrDefault(r => r.Member.Origin == CompatOrigin.Register && r.Member.Enabled)
            ?? rows.FirstOrDefault();
        UpdateRunButtons();
    }

    private void UpdateRunButtons()
    {
        RunButton.IsEnabled = !_busy && FocusCombo.SelectedItem is not null;
        CancelButton.IsEnabled = _busy;
    }

    private void FocusCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateRunButtons();

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private List<CompatParticipant> BuildParticipants()
    {
        var list = new List<CompatParticipant>();
        if (_host.VanillaRoot is { } root && Directory.Exists(root))
        {
            list.Add(new CompatParticipant("vanilla", "Vanilla", root, 0, true));
        }

        var rank = 1;
        foreach (var member in _set.Members)
        {
            list.Add(new CompatParticipant(CompatSet.NormalizeFolder(member.Folder), member.Name, member.Folder, rank++, false, member.ModId));
        }

        return list;
    }

    private void CreateCompatch_Click(object sender, RoutedEventArgs e)
    {
        var participants = (ResultList.SelectedItem as FindingRow)?.Finding.Contributions
            .Select(c => c.Participant)
            .Where(p => !p.IsVanilla)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Rank)
            .ToList();
        if (participants is null || participants.Count < 2)
        {
            StatusText.Text = ParadoxModTool.Compatch.CompatchTexts.NeedTwoMods;
            return;
        }

        _host.OpenCompatch(participants[0].Folder, participants[1].Folder);
    }

    private bool IsCompatchFor(CompatParticipant compatch, CompatParticipant other) =>
        compatch.ModId is { } id && _host.Registry.Get(id) is { IsCompatch: true } mod
        && CompatchTargets.Covers(_host.Registry, mod, other.ModId, other.Folder);

    private ModRelationKind Relation(CompatParticipant a, CompatParticipant b)
    {
        if (IsCompatchFor(a, b) || IsCompatchFor(b, a))
        {
            return ModRelationKind.Addon;
        }

        if (a.ModId is null || b.ModId is null || a.ModId == b.ModId)
        {
            return ModRelationKind.Unrelated;
        }

        return _host.Registry.Get(a.ModId) is { } modA && _host.Registry.Get(b.ModId) is { } modB
            ? ModProjects.Relation(_host.Registry, modA, modB)
            : ModRelationKind.Unrelated;
    }

    private async void Run_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (FocusCombo.SelectedItem is not MemberRow focusRow)
        {
            StatusText.Text = "Wähle Mod 1.";
            return;
        }

        var participants = BuildParticipants();
        if (participants.Count(p => !p.IsVanilla) < 2)
        {
            StatusText.Text = "Das Set braucht mindestens zwei Mods.";
            return;
        }

        var focusId = CompatSet.NormalizeFolder(focusRow.Member.Folder);
        IReadOnlyCollection<string>? counterparts = null;
        var counterText = "alle im Set";
        if (CounterCombo.SelectedIndex == 1)
        {
            var picked = MemberRows.Where(r => r.Member.Enabled).Select(r => CompatSet.NormalizeFolder(r.Member.Folder)).Where(id => id != focusId).ToList();
            if (picked.Count == 0)
            {
                StatusText.Text = "Setze bei mindestens einer anderen Mod das Häkchen \"Vergleich\" für die Gegenseite.";
                return;
            }

            counterparts = picked;
            counterText = $"{picked.Count} markierte";
        }

        var request = new CompatRequest
        {
            Participants = participants,
            Rules = BuildRules(),
            FocusId = focusId,
            CounterpartIds = counterparts,
            Cache = _cache,
            Relation = Relation,
            IsCompatchFor = IsCompatchFor,
        };
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _busy = true;
        UpdateRunButtons();
        StatusText.Text = "Prüfe…";
        var progressWindow = new ModCompatProgressWindow(() => _cts?.Cancel()) { Owner = this };
        var progress = new Progress<CompatProgress>(p =>
        {
            StatusText.Text = p.Total > 0 ? $"{p.Text} ({p.Done}/{p.Total})" : p.Text + "…";
            progressWindow.Report(p);
        });
        progressWindow.Show();
        try
        {
            var result = await Task.Run(() => CompatAnalyzer.Analyze(request, progress, token), token);
            _all = result.Findings;
            _subtitle = $"Set: {_set.Name} · Mod 1: {focusRow.Member.Name} · Gegenseite: {counterText} · {DateTime.Now:yyyy-MM-dd HH:mm}";
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Abgebrochen.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = string.Empty;
            MessageBox.Show(this, "Die Prüfung ist fehlgeschlagen:\n" + ex.Message, "Mod-Kompatibilität", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
            UpdateRunButtons();
            progressWindow.CloseAfterFinished();
        }
    }

    private static bool IsSense(CompatKind kind) => kind is CompatKind.RedundantOverride or CompatKind.TooCoarse or CompatKind.AdditiveOnly or CompatKind.ReplaceNeeded;

    private List<CompatFinding> Filtered()
    {
        var text = FilterBox.Text.Trim();
        return _all
            .Where(f => (f.Severity == CompatSeverity.Conflict && ConflictCheck.IsChecked == true)
                || (f.Severity == CompatSeverity.Hint && HintCheck.IsChecked == true)
                || (f.Severity == CompatSeverity.Info && InfoCheck.IsChecked == true))
            .Where(f => SenseCheck.IsChecked != true || IsSense(f.Kind))
            .Where(f => HideUncertainCheck.IsChecked != true || !f.Uncertain)
            .Where(f => text.Length == 0
                || f.RelativePath.Contains(text, StringComparison.OrdinalIgnoreCase)
                || f.Key.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void ApplyFilter()
    {
        var shown = Filtered();
        ResultList.ItemsSource = shown.Select(f => new FindingRow(f)).ToList();
        StatusText.Text = _all.Count == 0
            ? "Keine Funde."
            : $"{shown.Count} von {_all.Count} Funden angezeigt.";
        ShowDetail();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void Filter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private CompatFinding? SelectedFinding => (ResultList.SelectedItem as FindingRow)?.Finding;

    private void ResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ShowDetail();

    private void ResultList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Compare_Click(sender, e);

    private CompatSetMember? MemberOf(CompatParticipant participant) =>
        _set.Members.FirstOrDefault(m => string.Equals(CompatSet.NormalizeFolder(m.Folder), participant.Id, StringComparison.OrdinalIgnoreCase));

    private static (CompatContribution Left, CompatContribution Right)? ComparePair(CompatFinding finding)
    {
        var c = finding.Contributions;
        for (var i = c.Count - 1; i > 0; i--)
        {
            if (!string.Equals(c[0].FullPath, c[i].FullPath, StringComparison.OrdinalIgnoreCase) && File.Exists(c[0].FullPath) && File.Exists(c[i].FullPath))
            {
                return (c[0], c[i]);
            }
        }

        return null;
    }

    /// <summary>Der Beitrag einer eigenen Mod (Register), dessen Datei im Editor geöffnet werden darf; der Fokus-Mod zuerst.</summary>
    private CompatContribution? EditableContribution(CompatFinding finding) =>
        finding.Contributions
            .Where(c => MemberOf(c.Participant)?.Origin == CompatOrigin.Register && File.Exists(c.FullPath))
            .OrderByDescending(c => c.Participant.Rank)
            .FirstOrDefault();

    private void ShowDetail()
    {
        var finding = SelectedFinding;
        if (finding is null)
        {
            DetailBox.Text = string.Empty;
            CompareButton.IsEnabled = false;
            OpenButton.IsEnabled = false;
            return;
        }

        var text = new StringBuilder();
        text.AppendLine($"{CompatText.Severity(finding.Severity)} · {CompatText.Kind(finding.Kind)}{(finding.Uncertain ? " · unsicher" : string.Empty)}");
        text.AppendLine(finding.Message);
        if (finding.Winner is not null)
        {
            text.AppendLine("Gewinner: " + finding.Winner.Name);
        }

        if (CompatText.Suggestion(finding.Suggestion) is { Length: > 0 } suggestion)
        {
            text.AppendLine("Empfehlung: " + suggestion);
        }

        if (finding.Note is not null)
        {
            text.AppendLine("Hinweis: " + finding.Note);
        }

        text.AppendLine();
        foreach (var c in finding.Contributions)
        {
            text.AppendLine($"{c.Participant.Name}: {CompatText.Operator(c.Operator)}  {c.FullPath}{(c.Line > 0 ? ":" + c.Line : string.Empty)}");
            if (!string.IsNullOrWhiteSpace(c.Text))
            {
                text.AppendLine("    " + string.Join("\n    ", c.Text.Split('\n').Take(12).Select(l => l.TrimEnd('\r'))));
            }
        }

        DetailBox.Text = text.ToString();
        CompareButton.IsEnabled = ComparePair(finding) is not null;
        OpenButton.IsEnabled = EditableContribution(finding) is not null;
    }

    private static string LabelOf(CompatContribution c) => c.Participant.IsVanilla ? "Vanilla" : "Mod: " + c.Participant.Name;

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFinding is { } finding && ComparePair(finding) is { } pair)
        {
            _host.CompareFiles(pair.Left.FullPath, pair.Right.FullPath, LabelOf(pair.Left), LabelOf(pair.Right));
        }
    }

    private void OpenInEditor_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFinding is { } finding && EditableContribution(finding) is { } contribution)
        {
            _host.OpenInEditor(contribution.FullPath);
        }
    }

    private void Rules_Click(object sender, RoutedEventArgs e)
    {
        var shipped = _host.Profile is { } profile ? OverrideRules.FromProfile(profile).Rules : Array.Empty<OverrideRule>();
        new OverrideRulesWindow(shipped, RulesFile) { Owner = this }.ShowDialog();
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var shown = Filtered();
        if (_all.Count == 0 || shown.Count == 0)
        {
            StatusText.Text = "Es gibt keine angezeigten Funde zum Speichern.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Bericht speichern",
            Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt",
            FileName = $"Kompatibilitaet {StoreNames.Safe(_set.Name, "Set")}.md",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, CompatReport.Build(shown, new CompatReportOptions(Subtitle: _subtitle)), new UTF8Encoding(true));
            StatusText.Text = "Bericht gespeichert: " + dialog.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Der Bericht konnte nicht gespeichert werden:\n" + ex.Message, "Mod-Kompatibilität", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
