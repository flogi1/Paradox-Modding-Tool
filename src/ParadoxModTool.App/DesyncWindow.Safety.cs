using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Desync;
using ParadoxModTool.ErrorLog;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Tab "MP-Prüfung" (Spec Runde 3, Abschnitt "UI: tab MP-Prüfung"): ohne gewähltes Paar (oder mit "Vorabprüfung statt Dump-Mods") prüft er
/// die eigenen Mods oder das ganze Playset (Vorabprüfung), mit Paar die Mods aus dem Dump des OOS und reiht zusätzlich deren Dateien nach
/// Verdacht. Die Prüfung startet nur über "Prüfen" und läuft im Hintergrund; nur der jüngste Lauf schreibt in die Anzeige. Urteile des Nutzers hält <see cref="DesyncService.Verdicts"/>.
/// In Mod- oder Spielordner wird nichts geschrieben.
/// </summary>
public sealed partial class DesyncWindow
{
    /// <summary>Reihenfolge der Filter-Kästchen: strengste zuerst.</summary>
    private static readonly SafetySeverity[] SafetySeverities = { SafetySeverity.Risk, SafetySeverity.Check, SafetySeverity.Allowed };

    private readonly Controls.Button _safetyRun = SmallButton(DesyncTexts.SafetyRun);
    private readonly Controls.Button _safetyCancelButton = SmallButton(DesyncTexts.SafetyCancel);
    private readonly Controls.CheckBox _safetyPrecheck = new() { Content = DesyncTexts.SafetyPrecheck, Margin = new Thickness(6, 0, 6, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _safetyPlayset = new() { Content = DesyncTexts.SafetyWholePlayset, Margin = new Thickness(6, 0, 6, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.ProgressBar _safetyProgress = new() { Width = 160, Height = 14, Minimum = 0, Maximum = 100, Margin = new Thickness(6, 0, 6, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _safetyStatus = new() { Margin = new Thickness(6, 0, 0, 4), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly Controls.Border _safetyNotice = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _safetyNoticeText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.TextBlock _safetyMode = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _safetySuspectsHeader = new() { Text = DesyncTexts.SafetySuspectsHeader, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ListView _safetySuspects = new() { Margin = new Thickness(0, 4, 0, 0), SelectionMode = Controls.SelectionMode.Single, Visibility = Visibility.Collapsed };
    private readonly Controls.ListView _safetyFindings = new() { Margin = new Thickness(0, 4, 0, 0), SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.RowDefinition _safetySuspectsRow = new() { Height = new GridLength(0) };
    private readonly Dictionary<SafetySeverity, Controls.CheckBox> _safetyFilters = new();
    private readonly Dictionary<string, SafetyRule> _safetyRuleById = new(StringComparer.Ordinal);
    private readonly List<string> _safetyScanNotices = new();

    /// <summary>OOS-Id, zu der der Stand des Tabs gehört; <c>null</c> für die Vorabprüfung ohne Paar.</summary>
    private string? _safetyPairId;
    private int _safetyRunId;
    private CancellationTokenSource? _safetyCancel;
    private SafetyScanResult? _safetyScan;
    private SafetyOos? _safetyOos;
    private IReadOnlyList<SuspectFile> _safetySuspectFiles = Array.Empty<SuspectFile>();
    private bool _safetyNeedsDiff;
    private string? _safetySaveError;
    private CancellationTokenSource? _safetyTermsCancel;
    private int _safetyTermsBuild;

    /// <summary>Das Vergleichsergebnis, dessen Begriffssuche nach der Prüfung gerade läuft; kommt es noch einmal an, startet sie nicht erneut.</summary>
    private DesyncDiffResult? _safetyTermsResult;
    private int _safetyLeadsBuild;

    /// <summary>Besitzer der Fortschrittsanzeige: nur Meldungen des jüngsten Abschnitts (Prüfung oder Begriffssuche) bewegen sie.</summary>
    private int _safetyProgressOwner;

    private UIElement BuildSafetyTab()
    {
        // Ohne Abschnitt "safety" im Profil gibt es nur den Hinweis, keinen Knopf.
        if (_service.Profile.Safety is not { } rules)
        {
            var none = new Controls.StackPanel { Margin = new Thickness(6) };
            none.Children.Add(Notice(DesyncTexts.SafetyNoRules));
            return none;
        }

        foreach (var rule in rules.Rules)
        {
            _safetyRuleById.TryAdd(rule.Id, rule);
        }

        _safetyNotice.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        // Viele unlesbare Dateien sollen die Listen nicht aus dem Tab schieben.
        _safetyNotice.Child = new Controls.ScrollViewer
        {
            Content = _safetyNoticeText,
            MaxHeight = 120,
            VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled,
        };
        _safetyStatus.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _safetyStatus.Text = DesyncTexts.SafetyNotStarted;

        var toolbar = new Controls.WrapPanel();
        toolbar.Children.Add(_safetyRun);
        toolbar.Children.Add(_safetyCancelButton);
        toolbar.Children.Add(_safetyPrecheck);
        toolbar.Children.Add(_safetyPlayset);
        toolbar.Children.Add(_safetyProgress);
        toolbar.Children.Add(_safetyStatus);
        _safetyRun.Click += (_, _) => StartSafety();
        _safetyPrecheck.Click += (_, _) => RenderSafetyControls();
        // Bricht die Prüfung ab, oder die Begriffssuche, die ein später fertiger Zustandsvergleich gestartet hat.
        _safetyCancelButton.Click += (_, _) =>
        {
            _safetyCancel?.Cancel();
            _safetyTermsCancel?.Cancel();
        };

        var suspects = new Controls.GridView();
        suspects.Columns.Add(Column(DesyncTexts.SafetyColumnScore, nameof(SafetySuspectRow.Score), 60));
        suspects.Columns.Add(Column(DesyncTexts.SafetyColumnMod, nameof(SafetySuspectRow.Mod), 160));
        suspects.Columns.Add(Column(DesyncTexts.SafetyColumnFile, nameof(SafetySuspectRow.File), 300));
        suspects.Columns.Add(Column(DesyncTexts.SafetyColumnReasons, nameof(SafetySuspectRow.Reasons), 560));
        _safetySuspects.View = suspects;
        _safetySuspects.ItemContainerStyle = ToolTipRowStyle(nameof(SafetySuspectRow.Reasons));
        _safetySuspects.MouseDoubleClick += (_, e) => JumpToSafetyRow(_safetySuspects, e);

        var findings = new Controls.GridView();
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnSeverity, nameof(SafetyFindingRow.Severity), 120));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnMod, nameof(SafetyFindingRow.Mod), 150));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnFile, nameof(SafetyFindingRow.File), 260));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnLine, nameof(SafetyFindingRow.Line), 55));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnRule, nameof(SafetyFindingRow.Rule), 140));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnToken, nameof(SafetyFindingRow.Token), 140));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnContext, nameof(SafetyFindingRow.Context), 90));
        findings.Columns.Add(Column(DesyncTexts.SafetyColumnExplain, nameof(SafetyFindingRow.Explain), 480));
        _safetyFindings.View = findings;
        _safetyFindings.ItemContainerStyle = ToolTipRowStyle(nameof(SafetyFindingRow.Explain));
        _safetyFindings.MouseDoubleClick += (_, e) => JumpToSafetyRow(_safetyFindings, e);
        BuildSafetyMenu();

        var findingsBar = new Controls.WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        findingsBar.Children.Add(new Controls.TextBlock
        {
            Text = DesyncTexts.SafetyFindingsHeader,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 4),
        });
        foreach (var severity in SafetySeverities)
        {
            // "erlaubt" ist zunächst aus: die Liste zeigt, was zu prüfen oder riskant ist.
            var filter = new Controls.CheckBox
            {
                Content = DesyncTexts.SafetySeverityName(severity),
                IsChecked = severity != SafetySeverity.Allowed,
                Margin = new Thickness(0, 0, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            filter.Click += (_, _) => RenderSafetyFindings();
            _safetyFilters[severity] = filter;
            findingsBar.Children.Add(filter);
        }

        // Zwei Listen, die sich die Höhe teilen; ohne Verdachtsliste (Vorabprüfung) hat ihre Zeile die Höhe 0.
        var lists = new Controls.Grid();
        lists.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
        lists.RowDefinitions.Add(_safetySuspectsRow);
        lists.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
        lists.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(1.5, GridUnitType.Star) });
        var row = 0;
        foreach (var element in new UIElement[] { _safetySuspectsHeader, _safetySuspects, findingsBar, _safetyFindings })
        {
            Controls.Grid.SetRow(element, row++);
            lists.Children.Add(element);
        }

        var tab = new Controls.DockPanel { Margin = new Thickness(6) };
        foreach (var top in new UIElement[] { toolbar, _safetyNotice, _safetyMode })
        {
            Controls.DockPanel.SetDock(top, Controls.Dock.Top);
            tab.Children.Add(top);
        }

        tab.Children.Add(lists);

        Closed += (_, _) =>
        {
            _safetyCancel?.Cancel();
            _safetyTermsCancel?.Cancel();
        };

        RenderSafetyControls();
        RenderSafety();
        return tab;
    }

    /// <summary>Kontextmenü der Treffer: Urteil setzen oder zurücksetzen, für alle gewählten Zeilen.</summary>
    private void BuildSafetyMenu()
    {
        var harmless = new Controls.MenuItem { Header = DesyncTexts.SafetyMarkHarmless };
        var risky = new Controls.MenuItem { Header = DesyncTexts.SafetyMarkRisky };
        var reset = new Controls.MenuItem { Header = DesyncTexts.SafetyResetVerdict };
        harmless.Click += (_, _) => SetSafetyVerdict(SafetyVerdict.Harmless);
        risky.Click += (_, _) => SetSafetyVerdict(SafetyVerdict.Risky);
        reset.Click += (_, _) => SetSafetyVerdict(null);
        var menu = new Controls.ContextMenu();
        menu.Items.Add(harmless);
        menu.Items.Add(risky);
        menu.Items.Add(reset);
        _safetyFindings.ContextMenu = menu;
        _safetyFindings.ContextMenuOpening += (_, e) =>
        {
            var selected = _safetyFindings.SelectedItems.OfType<SafetyFindingRow>().ToList();
            if (selected.Count == 0)
            {
                e.Handled = true;
                return;
            }

            var verdicts = selected.Select(r => _service.Verdicts.Get(r.Finding.RuleId, r.Finding.Token)).ToList();
            harmless.IsEnabled = verdicts.Any(v => v != SafetyVerdict.Harmless);
            risky.IsEnabled = verdicts.Any(v => v != SafetyVerdict.Risky);
            reset.IsEnabled = verdicts.Any(v => v is not null);
        };
    }

    // ---------------------------------------------------------------- Ablauf

    /// <summary>Das gewählte Paar hat gewechselt (eine andere OOS-Id oder kein Paar): ein laufender Lauf endet, Ergebnisse gelten nicht mehr.</summary>
    private void OnSafetyPairChanged()
    {
        if (_service.Profile.Safety is null)
        {
            return;
        }

        // Dasselbe Paar nach dem Neulesen des Katalogs (neues Objekt, gleiche Id) behält seinen Stand.
        if (_pair?.OosId16 != _safetyPairId)
        {
            _safetyRunId++;
            _safetyCancel?.Cancel();
            _safetyCancel = null;
            CancelSafetyTerms();
            _safetyPairId = _pair?.OosId16;
            ClearSafetyResults();
            _safetySaveError = null;
            _safetyMode.Text = string.Empty;
            _safetyStatus.Text = DesyncTexts.SafetyNotStarted;
            RenderSafety();
        }

        RenderSafetyControls();
    }

    private async void StartSafety()
    {
        if (_service.Profile.Safety is not { } rules || _safetyCancel is not null)
        {
            return;
        }

        // Mit "Vorabprüfung statt Dump-Mods" läuft Modus A, obwohl ein Paar gewählt ist; der Tab gehört weiter zu diesem Paar.
        var pair = _safetyPrecheck.IsChecked == true ? null : _pair;
        var run = ++_safetyRunId;
        CancelSafetyTerms();
        var cancel = new CancellationTokenSource();
        _safetyCancel = cancel;
        _safetyPairId = _pair?.OosId16;
        ClearSafetyResults();
        _safetySaveError = null;
        _safetyMode.Text = string.Empty;
        _safetyStatus.Text = string.Empty;
        var progress = SafetyProgress();
        RenderSafetyControls();
        RenderSafety();

        try
        {
            IReadOnlyList<SafetyMod> mods;
            IReadOnlyList<string> dumpModNames = Array.Empty<string>();
            if (pair is null)
            {
                mods = OwnOrPlaysetMods(_safetyPlayset.IsChecked == true);
                _safetyMode.Text = DesyncTexts.SafetyModeA(mods.Count);
            }
            else
            {
                var summary = await Task.Run(() => _service.SummaryOf(pair), cancel.Token);
                if (run != _safetyRunId)
                {
                    return;
                }

                // Reihenfolge der Referenz-Maschine (Ladereihenfolge im Dump); sie bestimmt auch die Zuordnung der Log-Hinweise.
                dumpModNames = DumpLeadAttribution.ModOrder(summary, pair.Reference?.MachineId);
                mods = DumpMods(dumpModNames);
                _safetyMode.Text = DesyncTexts.SafetyModeB(pair.OosId16, dumpModNames.Count);
            }

            if (mods.Count == 0)
            {
                _safetyStatus.Text = DesyncTexts.SafetyNoMods;
                return;
            }

            var verdicts = _service.Verdicts;
            var scan = await Task.Run(() => SafetyScanner.Scan(mods, rules, verdicts, progress, cancel.Token), cancel.Token);
            if (run != _safetyRunId)
            {
                return;
            }

            _safetyScan = scan;
            _safetyScanNotices.AddRange(scan.Notices.Select(NoticeText));
            if (pair is not null)
            {
                await RankSafetyOos(pair, rules, scan, dumpModNames, run, cancel.Token);
                if (run != _safetyRunId)
                {
                    return;
                }
            }

            // Aus _safetyScan: ein Urteil während der Rangliste hat die Stufen schon geändert.
            RenderSafetyStatus();
        }
        catch (OperationCanceledException)
        {
            if (run == _safetyRunId)
            {
                ClearSafetyResults();
                _safetyStatus.Text = DesyncTexts.SafetyCancelled;
            }
        }
        catch (Exception ex) when (pair is not null && (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException))
        {
            if (run == _safetyRunId)
            {
                _safetyScanNotices.Add(DesyncTexts.DumpError(pair.OosId16, ex.Message));
                _safetyStatus.Text = string.Empty;
            }
        }
        finally
        {
            if (ReferenceEquals(_safetyCancel, cancel))
            {
                _safetyCancel = null;
            }

            cancel.Dispose();
            if (IsLoaded && run == _safetyRunId)
            {
                RenderSafetyControls();
                RenderSafety();
            }
        }
    }

    /// <summary>
    /// Modus B nach der Prüfung: Log-Hinweise (wie im Bericht, <see cref="ReportDays"/> Spieltage, aber den Mods aus dem Dump zugeordnet, siehe
    /// <see cref="DumpLeadAttribution"/>), Begriffe aus dem Zustandsvergleich, falls es ihn gibt, ihre Treffer in den geprüften Dateien (im
    /// Hintergrund, mit Fortschritt, für dieses Ergebnis gemerkt) und die Rangliste.
    /// </summary>
    private async Task RankSafetyOos(
        OosPair pair,
        SafetyRules rules,
        SafetyScanResult scan,
        IReadOnlyList<string> dumpModNames,
        int run,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TimelineEntry> entries;
        try
        {
            entries = await Task.Run(() => (IReadOnlyList<TimelineEntry>)_service.TimelineOf(pair, ReportDays).Where(e => e.Lead != LeadKind.None).ToList(), cancellationToken);
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (run != _safetyRunId)
            {
                return;
            }

            // Ohne Logs zählen nur Funde und Begriffe.
            _safetyScanNotices.Add(DesyncTexts.DumpError(pair.OosId16, ex.Message));
            entries = Array.Empty<TimelineEntry>();
        }

        if (run != _safetyRunId)
        {
            return;
        }

        // Mod-Liste und Ergebnis im UI-Thread gelesen, erst nach der Prüfung, damit ein inzwischen fertiger Vergleich schon zählt.
        var installed = _errorLog.Session.Mods;
        var result = _service.ResultOf(pair);
        var progress = SafetyProgress();
        var (leads, terms, hits) = await Task.Run(
            () =>
            {
                var dumpLeads = DumpLeadsOf(entries, dumpModNames, installed);
                var (found, foundHits) = TermsAndHits(result, rules, scan, progress, cancellationToken);
                return (dumpLeads, found, foundHits);
            },
            cancellationToken);
        if (run != _safetyRunId)
        {
            return;
        }

        _safetyOos = new SafetyOos(pair.OosId16, entries, dumpModNames, installed, leads, result, terms, hits);
        _safetyNeedsDiff = result is null;
        RankSafety();

        // Die Mod-Liste änderte sich während der Begriffssuche: Diese Änderung kam an RefreshSafetyLeads vorbei (es gab noch keine Rangliste).
        if (!ReferenceEquals(installed, _errorLog.Session.Mods))
        {
            RefreshSafetyLeads();
        }

        // Ein Vergleich, der während der Begriffssuche fertig wurde, kam an OnSafetyDiffChanged vorbei (es gab noch keine Rangliste).
        if (_service.ResultOf(pair) is { } newer && !ReferenceEquals(newer, result))
        {
            OnSafetyDiffChanged(pair, newer);
        }
    }

    /// <summary>
    /// Ein Ergebnis des Zustandsvergleichs liegt vor. Gehört eine Prüfung zu diesem Paar, werden nur die Begriffe und ihre Treffer neu bestimmt
    /// und die Liste neu gereiht; sonst geschieht nichts (die Prüfung startet nie von selbst). Die Suche zeigt ihren Fortschritt und lässt sich
    /// mit "Abbrechen" beenden; dann bleibt die bisherige Rangliste.
    /// </summary>
    private async void OnSafetyDiffChanged(OosPair pair, DesyncDiffResult result)
    {
        // Dasselbe Ergebnis noch einmal (Paar nach dem Neulesen des Katalogs wieder gewählt) ändert die Begriffe nicht; läuft seine Suche
        // schon, startet sie nicht ein zweites Mal.
        if (_service.Profile.Safety is not { } rules || _safetyScan is not { } scan || _safetyOos is not { } oos || oos.OosId16 != pair.OosId16
            || ReferenceEquals(oos.Result, result) || ReferenceEquals(_safetyTermsResult, result))
        {
            return;
        }

        CancelSafetyTerms();
        var build = _safetyTermsBuild;
        var run = _safetyRunId;
        var cancel = new CancellationTokenSource();
        _safetyTermsCancel = cancel;
        _safetyTermsResult = result;
        var progress = SafetyProgress();
        RenderSafetyControls();
        try
        {
            var (terms, hits) = await Task.Run(() => TermsAndHits(result, rules, scan, progress, cancel.Token), cancel.Token);
            if (build != _safetyTermsBuild || run != _safetyRunId || !IsLoaded || _safetyOos is not { } current)
            {
                return;
            }

            _safetyOos = current with { Result = result, Terms = terms, TermHits = hits };
            _safetyNeedsDiff = false;
            RankSafety();
            RenderSafety();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_safetyTermsCancel, cancel))
            {
                _safetyTermsCancel = null;
                _safetyTermsResult = null;
            }

            cancel.Dispose();
            if (IsLoaded)
            {
                RenderSafetyControls();
            }
        }
    }

    /// <summary>Begriffe des Ergebnisses (ohne Ergebnis keine) und in welchen geprüften Dateien sie vorkommen; im Hintergrund aufrufen.</summary>
    private (IReadOnlyDictionary<string, double> Terms, IReadOnlyDictionary<string, IReadOnlySet<string>> Hits) TermsAndHits(
        DesyncDiffResult? result,
        SafetyRules rules,
        SafetyScanResult scan,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var terms = result is null ? new Dictionary<string, double>() : OosSuspects.TermsOf(RankedOf(result), rules);
        return (terms, OosSuspects.TermHits(scan.Files, terms.Keys.ToList(), SafetyScanner.ReadText, cancellationToken, progress));
    }

    /// <summary>Beendet eine laufende Begriffssuche nach einem neuen Vergleichsergebnis; ihr Ergebnis gilt nicht mehr.</summary>
    private void CancelSafetyTerms()
    {
        _safetyTermsBuild++;
        _safetyTermsCancel?.Cancel();
        _safetyTermsCancel = null;
        _safetyTermsResult = null;
    }

    /// <summary>Setzt die Fortschrittsanzeige auf 0 und gibt den Empfänger für den neuen Abschnitt; ältere Empfänger bewegen sie nicht mehr.</summary>
    private IProgress<double> SafetyProgress()
    {
        var owner = ++_safetyProgressOwner;
        _safetyProgress.Value = 0;
        return new Progress<double>(p =>
        {
            if (owner == _safetyProgressOwner)
            {
                _safetyProgress.Value = Math.Clamp(p, 0, 1) * 100;
            }
        });
    }

    /// <summary>Log-Hinweise des Modus B, den Mods aus dem Dump zugeordnet; liest nur, ob Dateien existieren (im Hintergrund aufrufbar).</summary>
    private static IReadOnlyList<ReportLead> DumpLeadsOf(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyList<string> dumpModNames,
        IReadOnlyList<ModSource> installed) =>
        ReportLeads.From(entries, DumpLeadAttribution.For(dumpModNames, installed));

    /// <summary>
    /// Die Mod-Liste des Fehlerprotokolls hat sich vielleicht geändert (gleicher Anlass wie <see cref="RefreshReportLeads"/>): Gibt es eine
    /// Rangliste des Modus B, werden ihre Log-Hinweise neu zugeordnet und die Liste mit den gemerkten Begriffstreffern neu gereiht, wenn sich
    /// dabei ein Hinweis ändert.
    /// </summary>
    private async void RefreshSafetyLeads()
    {
        // Dieselbe Mod-Liste wie bei den aktuellen Hinweisen (die Sitzung meldet auch andere Änderungen): nichts neu zuzuordnen.
        var installed = _errorLog.Session.Mods;
        if (_safetyOos is not { } oos || ReferenceEquals(oos.LeadMods, installed))
        {
            return;
        }

        var build = ++_safetyLeadsBuild;
        var run = _safetyRunId;
        var leads = await Task.Run(() => DumpLeadsOf(oos.Entries, oos.DumpModNames, installed));
        if (build != _safetyLeadsBuild || run != _safetyRunId || !IsLoaded || _safetyOos is not { } current || current.OosId16 != oos.OosId16)
        {
            return;
        }

        if (leads.SequenceEqual(current.Leads))
        {
            _safetyOos = current with { LeadMods = installed };
            return;
        }

        _safetyOos = current with { LeadMods = installed, Leads = leads };
        RankSafety();
        RenderSafety();
    }

    /// <summary>Reiht neu aus der aktuellen Prüfung und den gemerkten Begriffstreffern; liest keine Dateien.</summary>
    private void RankSafety() =>
        _safetySuspectFiles = _safetyOos is { } oos && _safetyScan is { } scan
            ? OosSuspects.Rank(oos.Leads, scan, oos.Terms, oos.TermHits)
            : Array.Empty<SuspectFile>();

    private void ClearSafetyResults()
    {
        _safetyScan = null;
        _safetyOos = null;
        _safetySuspectFiles = Array.Empty<SuspectFile>();
        _safetyScanNotices.Clear();
        _safetyNeedsDiff = false;
    }

    /// <summary>Vorabprüfung: die eigenen Mods, mit "ganzes Playset" alle Mods des Playsets.</summary>
    private List<SafetyMod> OwnOrPlaysetMods(bool wholePlayset) =>
        _errorLog.Session.Mods
            .Where(m => wholePlayset ? m.InPlayset : m.IsOwn)
            .Select(m => new SafetyMod(m.Name, m.Directory, m.IsOwn))
            .ToList();

    /// <summary>
    /// Die Mods aus dem Dump, über den Namen den installierten Mods zugeordnet (<see cref="DumpLeadAttribution.Installed"/>, wie bei den
    /// Log-Hinweisen: Schreibung egal, ein Mod des Playsets geht vor). Namen ohne
    /// installierten Mod werden zu Hinweisen. Ist die Mod-Liste noch leer, gibt es keine Hinweise (dann meldet der Tab "nicht geladen").
    /// </summary>
    private List<SafetyMod> DumpMods(IReadOnlyList<string> names)
    {
        var installed = _errorLog.Session.Mods;
        var mods = new List<SafetyMod>();
        if (installed.Count == 0)
        {
            return mods;
        }

        foreach (var name in names)
        {
            var match = DumpLeadAttribution.Installed(name, installed);
            if (match is null)
            {
                _safetyScanNotices.Add(DesyncTexts.SafetyNoticeNotInstalled(name));
            }
            else
            {
                mods.Add(new SafetyMod(match.Name, match.Directory, match.IsOwn));
            }
        }

        return mods;
    }

    // ---------------------------------------------------------------- Urteile

    /// <summary>Setzt das Urteil für die gewählten Treffer, speichert sofort und zeigt beide Listen neu, ohne neu zu prüfen.</summary>
    private void SetSafetyVerdict(SafetyVerdict? verdict)
    {
        var rows = _safetyFindings.SelectedItems.OfType<SafetyFindingRow>().ToList();
        if (rows.Count == 0 || _safetyScan is not { } scan)
        {
            return;
        }

        // Ließ sich die Datei beim Öffnen nicht lesen, ist der Stand leer: erst neu lesen, sonst überschriebe das Speichern alle Urteile.
        // Gibt es schon ungespeicherte Änderungen, bleibt es dabei (Load leert den Stand zuerst).
        if (_service.VerdictsLoadError is not null && !_service.VerdictsUnsaved)
        {
            _service.LoadVerdicts();
        }

        foreach (var row in rows)
        {
            _service.Verdicts.Set(row.Finding.RuleId, row.Finding.Token, verdict);
        }

        if (_service.VerdictsLoadError is { } loadError)
        {
            // Die Änderung gilt in dieser Sitzung, die Datei bleibt unberührt.
            _service.VerdictsUnsaved = true;
            _safetySaveError = DesyncTexts.SafetyVerdictSaveFailed(loadError);
        }
        else
        {
            try
            {
                _service.Verdicts.Save();
                _safetySaveError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _safetySaveError = DesyncTexts.SafetyVerdictSaveFailed(ex.Message);
            }
        }

        _safetyScan = scan.WithVerdicts(_service.Verdicts);
        RankSafety();
        // Während der Prüfung schreibt deren Ende den Status (aus _safetyScan, also mit diesem Urteil).
        if (_safetyCancel is null)
        {
            RenderSafetyStatus();
        }

        var keys = rows.Select(r => FindingKey(r.Finding)).ToHashSet();
        RenderSafety();
        foreach (var item in _safetyFindings.Items.OfType<SafetyFindingRow>().Where(r => keys.Contains(FindingKey(r.Finding))))
        {
            _safetyFindings.SelectedItems.Add(item);
        }
    }

    private static (string Path, int Line, string Rule, string Token) FindingKey(SafetyFinding finding) =>
        (finding.FullPath, finding.Line, finding.RuleId, finding.Token);

    // ---------------------------------------------------------------- Anzeige

    /// <summary>Status nach der Prüfung: untersuchte Dateien und Zahl der Treffer, die zu prüfen oder riskant sind ("erlaubt" ist anfangs ausgeblendet).</summary>
    private void RenderSafetyStatus()
    {
        if (_safetyScan is { } scan)
        {
            _safetyStatus.Text = DesyncTexts.SafetyDone(scan.Files.Count, scan.Findings.Count(f => f.Severity != SafetySeverity.Allowed));
        }
    }

    /// <summary>Knöpfe und Fortschritt: "Prüfen" ist während der Prüfung aus; "Abbrechen" und Fortschritt zeigen auch eine laufende Begriffssuche.</summary>
    private void RenderSafetyControls()
    {
        var running = _safetyCancel is not null || _safetyTermsCancel is not null;
        _safetyRun.IsEnabled = _safetyCancel is null;
        _safetyCancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        _safetyProgress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        _safetyPrecheck.Visibility = _pair is not null ? Visibility.Visible : Visibility.Collapsed;
        _safetyPlayset.Visibility = _pair is null || _safetyPrecheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderSafety()
    {
        RenderSafetyNotice();
        _safetyMode.Visibility = _safetyMode.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var showSuspects = _safetyOos is not null;
        _safetySuspectsHeader.Visibility = showSuspects ? Visibility.Visible : Visibility.Collapsed;
        _safetySuspects.Visibility = showSuspects ? Visibility.Visible : Visibility.Collapsed;
        _safetySuspectsRow.Height = showSuspects ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _safetySuspects.ItemsSource = showSuspects ? _safetySuspectFiles.Select(s => new SafetySuspectRow(s)).ToList() : null;
        RenderSafetyFindings();
    }

    private void RenderSafetyNotice()
    {
        var lines = new List<string>();
        if (_service.VerdictsBroken)
        {
            lines.Add(DesyncTexts.SafetyVerdictsBroken(_service.VerdictsPath + ".bak"));
        }

        if (_safetySaveError is { } saveError)
        {
            lines.Add(saveError);
        }
        else if (_service.VerdictsLoadError is { } loadError)
        {
            lines.Add(_service.VerdictsPath + ": " + loadError);
        }

        if (_safetyNeedsDiff)
        {
            lines.Add(DesyncTexts.SafetyNeedsDiff);
        }

        lines.AddRange(_safetyScanNotices);
        _safetyNoticeText.Text = string.Join("\n", lines);
        _safetyNotice.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Die Treffer der Prüfung, gefiltert nach den angehakten Stufen.</summary>
    private void RenderSafetyFindings()
    {
        if (_safetyScan is not { } scan)
        {
            _safetyFindings.ItemsSource = null;
            return;
        }

        var shown = _safetyFilters.Where(f => f.Value.IsChecked == true).Select(f => f.Key).ToHashSet();
        _safetyFindings.ItemsSource = scan.Findings
            .Where(f => shown.Contains(f.Severity))
            .Select(f => new SafetyFindingRow(
                f,
                _safetyRuleById.TryGetValue(f.RuleId, out var rule) ? InLanguage(rule.Explain) ?? string.Empty : string.Empty,
                hasVerdict: _service.Verdicts.Get(f.RuleId, f.Token) is not null))
            .ToList();
    }

    private static string NoticeText(SafetyNotice notice) => notice.Kind == SafetyNoticeKind.MissingFolder
        ? DesyncTexts.SafetyNoticeMissing(notice.ModName, notice.Path)
        : DesyncTexts.SafetyNoticeUnreadable(notice.ModName, notice.Path, notice.Message ?? string.Empty);

    /// <summary>Doppelklick auf eine Zeile (nicht auf Kopf oder Leiste) springt zu Datei und Zeile.</summary>
    private void JumpToSafetyRow(Controls.ListView list, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || Controls.ItemsControl.ContainerFromElement(list, source) is not Controls.ListViewItem item)
        {
            return;
        }

        switch (item.Content)
        {
            case SafetyFindingRow finding:
                _errorLog.Host.JumpTo(finding.Finding.FullPath, finding.Finding.Line);
                break;
            case SafetySuspectRow suspect:
                _errorLog.Host.JumpTo(suspect.Suspect.FullPath, suspect.Suspect.Line ?? 1);
                break;
        }
    }

    /// <summary>Zeilenstil des Themas mit dem vollen Text einer langen Spalte als Tooltip.</summary>
    private Style ToolTipRowStyle(string property)
    {
        var style = new Style(typeof(Controls.ListViewItem), TryFindResource(typeof(Controls.ListViewItem)) as Style);
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(property)));
        return style;
    }

    /// <summary>Text eines Grundes der Verdachtsliste.</summary>
    private static string SafetyReasonText(SuspectReason reason)
    {
        switch (reason.Kind)
        {
            case SuspectReasonKind.LogLead:
                return DesyncTexts.SafetyReasonLead(reason.Subject, reason.Line, string.Join("+", reason.Machines.Select(m => m.ToString(CultureInfo.CurrentCulture))));
            case SuspectReasonKind.Rule:
                return DesyncTexts.SafetyReasonRule(reason.RuleId ?? reason.Subject, reason.Token ?? string.Empty, reason.Line ?? 0);

            default:
                return DesyncTexts.SafetyReasonTerm(reason.Subject, reason.FileCount);
        }
    }

    /// <summary>
    /// Stand des Modus B: zu welchem OOS, die Einträge der Zeitleiste mit Hinweis und die Mod-Namen des Dumps in Ladereihenfolge (daraus werden
    /// die Log-Hinweise neu zugeordnet, wenn sich die Mod-Liste ändert), die Mod-Liste des Fehlerprotokolls, mit der die Hinweise zugeordnet
    /// wurden, die Log-Hinweise, das Ergebnis des Zustandsvergleichs, aus dem die Begriffe stammen (<c>null</c>: keines), die Begriffe und
    /// deren Treffer je Datei (voller Pfad).
    /// </summary>
    private sealed record SafetyOos(
        string OosId16,
        IReadOnlyList<TimelineEntry> Entries,
        IReadOnlyList<string> DumpModNames,
        IReadOnlyList<ModSource> LeadMods,
        IReadOnlyList<ReportLead> Leads,
        DesyncDiffResult? Result,
        IReadOnlyDictionary<string, double> Terms,
        IReadOnlyDictionary<string, IReadOnlySet<string>> TermHits);

    /// <summary>Zeile der Trefferliste; die Eigenschaften sind die Spalten.</summary>
    private sealed class SafetyFindingRow
    {
        /// <param name="hasVerdict">Es gibt ein Urteil des Nutzers; die Stufe zeigt es auch dann, wenn sie der des Profils gleicht.</param>
        public SafetyFindingRow(SafetyFinding finding, string explain, bool hasVerdict)
        {
            Finding = finding;
            var name = DesyncTexts.SafetySeverityName(finding.Severity);
            Severity = hasVerdict ? DesyncTexts.SafetyByVerdict(name) : name;
            Context = DesyncTexts.SafetyContextName(finding.Context);
            Explain = finding.Parsed
                ? explain
                : (explain.Length > 0 ? explain + "; " : string.Empty) + DesyncTexts.SafetyUnparsed(finding.ParseErrorLine ?? finding.Line);
        }

        public SafetyFinding Finding { get; }

        public string Severity { get; }

        public string Mod => Finding.ModName;

        public string File => Finding.RelativePath;

        public int Line => Finding.Line;

        public string Rule => Finding.RuleId;

        public string Token => Finding.Token;

        public string Context { get; }

        public string Explain { get; }
    }

    /// <summary>Zeile der Verdachtsliste; die Eigenschaften sind die Spalten.</summary>
    private sealed class SafetySuspectRow
    {
        public SafetySuspectRow(SuspectFile suspect)
        {
            Suspect = suspect;
            Score = suspect.Score.ToString("F0", CultureInfo.CurrentCulture);
            Reasons = string.Join("; ", suspect.Reasons.Select(SafetyReasonText));
        }

        public SuspectFile Suspect { get; }

        public string Score { get; }

        public string Mod => Suspect.ModName;

        public string File => Suspect.RelativePath;

        public string Reasons { get; }
    }
}
