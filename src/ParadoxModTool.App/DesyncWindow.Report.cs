using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using ParadoxModTool.Desync;
using ParadoxModTool.Notes;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Was das Desync-Fenster vom Hauptfenster für den Bericht "PMT-Bugtracker" braucht.</summary>
/// <param name="ChangeBugs">
/// Ändert die Bug-Daten und speichert sie; <c>false</c>, wenn nicht gespeichert wurde (den Fehler meldet das Hauptfenster selbst).
/// </param>
/// <param name="OpenBugTracker">Öffnet den Bug-Tracker; mit Id ist diese Meldung gewählt.</param>
public sealed record DesyncWindowHost(Func<Action<BugData>, bool> ChangeBugs, Action<string?> OpenBugTracker);

/// <summary>
/// Tab "Bericht" (Spec Runde 2, Abschnitt 2): dieselben Tatsachen des gewählten Paares für drei Empfänger, Paradox-Forum, Mod-Autor und
/// PMT-Bugtracker. Die Berichte baut die Bibliothek; hier werden sie nur angezeigt, kopiert, gespeichert oder als Bug angelegt.
/// PMT füllt nie ein Formular im Browser aus.
/// </summary>
public sealed partial class DesyncWindow
{
    /// <summary>Spieltage vor dem OOS, aus denen der Bericht Hinweise nimmt, unabhängig von der Auswahl im Tab "Logs".</summary>
    private const int ReportDays = 7;

    private static readonly TimeSpan CopiedDuration = TimeSpan.FromSeconds(1.5);

    private readonly DesyncWindowHost _host;
    private readonly Controls.ComboBox _reportRecipient = new() { MinWidth = 160, Margin = new Thickness(6, 0, 0, 4) };
    private readonly Controls.TextBlock _reportDiffNote = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) };
    private readonly Controls.Button _reportCompare = SmallButton(DesyncTexts.StateStart);
    private readonly Controls.WrapPanel _reportDiffRow = new() { Visibility = Visibility.Collapsed };
    private readonly Controls.Border _reportBody = new() { Margin = new Thickness(0, 6, 0, 0) };

    private ReportInput? _reportInput;
    private IReadOnlyList<ReportLead> _reportLeads = Array.Empty<ReportLead>();
    private ReportFacts? _reportFacts;
    private string? _reportError;
    private int _reportBuild;
    private readonly Dictionary<string, BugDraft> _bugDrafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _zipsRunning = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Path, Controls.Button Button)> _zipButtons = new();

    private UIElement BuildReportTab()
    {
        foreach (var recipient in new[] { DesyncTexts.ReportParadox, DesyncTexts.ReportModAuthor, DesyncTexts.ReportBugTracker })
        {
            _reportRecipient.Items.Add(new Controls.ComboBoxItem { Content = recipient });
        }

        // Ohne Formular im Profil (fehlend oder leer) bleibt "Paradox-Forum" sichtbar, ist aber gesperrt; vorgewählt ist dann "Mod-Autor".
        var hasForm = _service.Profile.ParadoxForm is { Count: > 0 };
        if (!hasForm)
        {
            var paradox = (Controls.ComboBoxItem)_reportRecipient.Items[0];
            paradox.IsEnabled = false;
            paradox.ToolTip = DesyncTexts.ReportNoForm;
            Controls.ToolTipService.SetShowOnDisabled(paradox, true);
        }

        _reportRecipient.SelectedIndex = hasForm ? 0 : 1;
        _reportRecipient.SelectionChanged += (_, _) => RenderReport();

        // Derselbe Vergleich wie im Tab "Zustand": der Knopf ist genau dann bedienbar, wenn es "Vergleichen" dort ist. Die Hinweise daneben
        // folgen den Elementen des Tabs "Zustand": "beide Dumps nötig" und der Fortschritt eines laufenden Vergleichs dieses Paares.
        _reportCompare.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = _start });
        _reportCompare.Click += (_, _) => StartCompare();
        _reportDiffNote.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        var needsBoth = new Controls.TextBlock { Text = DesyncTexts.StateNeedsBoth, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) };
        needsBoth.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        needsBoth.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Visibility)) { Source = _needsBoth });
        var running = new Controls.TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) };
        running.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        running.SetBinding(Controls.TextBlock.TextProperty, new Binding(nameof(Controls.TextBlock.Text)) { Source = _progressText });
        running.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Visibility)) { Source = _progressRow });
        _reportDiffRow.Children.Add(_reportDiffNote);
        _reportDiffRow.Children.Add(_reportCompare);
        _reportDiffRow.Children.Add(needsBoth);
        _reportDiffRow.Children.Add(running);

        var recipientRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        recipientRow.Children.Add(Label(DesyncTexts.ReportRecipient));
        recipientRow.Children.Add(_reportRecipient);

        var report = new Controls.DockPanel { Margin = new Thickness(6) };
        foreach (var top in new UIElement[] { recipientRow, _reportDiffRow })
        {
            Controls.DockPanel.SetDock(top, Controls.Dock.Top);
            report.Children.Add(top);
        }

        report.Children.Add(_reportBody);
        return report;
    }

    // ---------------------------------------------------------------- Tatsachen

    private void ClearReport()
    {
        _reportBuild++;
        _reportInput = null;
        _reportLeads = Array.Empty<ReportLead>();
        _reportFacts = null;
        _reportError = null;
        RenderReport();
    }

    /// <summary>Liest Zusammenfassung und Hinweise des Paares im Hintergrund (die Dumps werden dafür neu geöffnet) und baut dann die Tatsachen.</summary>
    private async void LoadReport(OosPair pair, int selection)
    {
        ClearReport();
        OosSummary summary;
        IReadOnlyList<TimelineEntry> leads;
        try
        {
            (summary, leads) = await Task.Run(() =>
                (_service.SummaryOf(pair), (IReadOnlyList<TimelineEntry>)_service.TimelineOf(pair, ReportDays).Where(e => e.Lead != LeadKind.None).ToList()));
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (IsCurrent(selection))
            {
                _reportError = DesyncTexts.DumpError(pair.OosId16, ex.Message);
                RenderReport();
            }

            return;
        }

        if (!IsCurrent(selection))
        {
            return;
        }

        _reportInput = new ReportInput(pair, summary, leads);
        RebuildReport(selection);
    }

    /// <summary>
    /// Baut die Tatsachen aus den gelesenen Eingaben neu: nach dem Lesen, nach einem Vergleichsergebnis und wenn sich die Zuordnung zu Mods
    /// ändert. Zuordnung und Ergebnis werden im UI-Thread gelesen (der Zwischenspeicher der Zuordnung ist nicht threadsicher), Rangliste und
    /// Dump-Größen im Hintergrund. Nur der jüngste Lauf zählt.
    /// </summary>
    private async void RebuildReport(int selection)
    {
        if (_reportInput is not { } input || !IsCurrent(selection))
        {
            return;
        }

        var build = ++_reportBuild;
        var leads = ReportLeadsOf(input.Leads);
        var result = _service.ResultOf(input.Pair);
        var profile = _service.Profile;
        var facts = await Task.Run(() => ReportFacts.Build(input.Summary, leads, result is null ? null : RankedOf(result), input.Pair, profile));
        if (build != _reportBuild || !IsCurrent(selection))
        {
            return;
        }

        _reportLeads = leads;
        _reportFacts = facts;
        RenderReport();
    }

    /// <summary>Index und Mod-Liste des Fehlerprotokolls kamen nach: neu bauen, wenn sich dadurch ein Hinweis ändert (sonst bleiben Eingaben im Tab).</summary>
    private void RefreshReportLeads()
    {
        if (_reportInput is { } input && !ReportLeadsOf(input.Leads).SequenceEqual(_reportLeads))
        {
            RebuildReport(_selection);
        }
    }

    private IReadOnlyList<ReportLead> ReportLeadsOf(IReadOnlyList<TimelineEntry> entries)
    {
        var session = _errorLog.Session;
        return ReportLeads.From(entries, error =>
        {
            var attribution = session.AttributionOf(error);
            return new LeadAttribution(attribution.Mod?.Name, attribution.Mod?.IsOwn == true, attribution.Source?.FullPath);
        });
    }

    // ---------------------------------------------------------------- Anzeige

    private void RenderReport()
    {
        if (_reportFacts is not { } facts)
        {
            _reportDiffRow.Visibility = Visibility.Collapsed;
            _reportBody.Child = _reportError is { } error ? Notice(error) : null;
            return;
        }

        _reportDiffNote.Text = facts.DiffAvailable ? DesyncTexts.ReportDiffIncluded : DesyncTexts.ReportDiffMissing;
        _reportCompare.Visibility = facts.DiffAvailable ? Visibility.Collapsed : Visibility.Visible;
        _reportDiffRow.Visibility = Visibility.Visible;
        _reportBody.Child = _reportRecipient.SelectedIndex switch
        {
            1 => ModAuthorView(facts),
            2 => BugTrackerView(facts),
            _ => ParadoxView(facts),
        };
    }

    private UIElement ParadoxView(ReportFacts facts)
    {
        _zipButtons.Clear();
        var panel = new Controls.StackPanel();
        // Ein leeres Formular im Profil zählt wie ein fehlendes.
        if (_service.Profile.ParadoxForm is not { Count: > 0 } form)
        {
            panel.Children.Add(Notice(DesyncTexts.ReportNoForm));
            return Scrolling(panel);
        }

        panel.Children.Add(Muted(DesyncTexts.ReportFormHint));
        foreach (var entry in ParadoxFormReport.Build(facts, form))
        {
            AddHeading(panel, entry.Label);
            switch (entry.Kind)
            {
                case FormFieldKind.Text:
                case FormFieldKind.Option:
                    panel.Children.Add(CopyRow(ReadOnlyBox(entry.Value, multiline: false), entry.Value));
                    break;
                case FormFieldKind.Multiline:
                    panel.Children.Add(CopyRow(ReadOnlyBox(entry.Value, multiline: true), entry.Value));
                    break;
                case FormFieldKind.Hint:
                    panel.Children.Add(Muted(entry.Value));
                    break;
                case FormFieldKind.Attachments:
                    if (entry.Value.Length > 0)
                    {
                        panel.Children.Add(Muted(entry.Value));
                    }

                    foreach (var dump in entry.Attachments)
                    {
                        panel.Children.Add(DumpRow(dump));
                    }

                    break;
            }
        }

        return Scrolling(panel);
    }

    private UIElement DumpRow(ReportDump dump)
    {
        var row = new Controls.WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        if (dump.Missing || dump.Path is not { } path)
        {
            var missing = new Controls.TextBlock { Text = DesyncTexts.ReportDumpMissing(dump.MachineId), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            missing.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
            row.Children.Add(missing);
            return row;
        }

        var trimmed = path.TrimEnd('\\', '/');
        var machine = DesyncTexts.ReportMachine(dump.MachineId);
        var size = DesyncTexts.ReportDumpSize(dump.SizeBytes / (1024.0 * 1024.0));
        row.Children.Add(new Controls.TextBlock
        {
            Text = machine + ": " + Path.GetFileName(trimmed) + " (" + size + ")",
            ToolTip = trimmed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 4),
        });

        var show = SmallButton(DesyncTexts.ReportShowInExplorer);
        show.Click += (_, _) => ShowInExplorer(trimmed);
        row.Children.Add(show);
        if (dump.IsFolder)
        {
            // Läuft das Speichern dieses Ordners noch (Bericht inzwischen neu gebaut), gibt es keinen zweiten Lauf.
            var zip = SmallButton(DesyncTexts.ReportSaveZip);
            zip.IsEnabled = !_zipsRunning.Contains(trimmed);
            zip.Click += (_, _) => SaveZip(trimmed);
            _zipButtons.Add((trimmed, zip));
            row.Children.Add(zip);
        }

        return row;
    }

    private UIElement ModAuthorView(ReportFacts facts)
    {
        var markdown = ModAuthorReport.Build(facts, _service.Profile.Advice);
        var buttons = new Controls.WrapPanel();
        buttons.Children.Add(CopyButton(markdown));
        var save = SmallButton(DesyncTexts.ReportSaveFile);
        save.Click += (_, _) => SaveMarkdown(markdown, facts.Summary.OosId16);
        buttons.Children.Add(save);

        // Füllt den Tab und scrollt selbst; in einem ScrollViewer würde das Textfeld unbegrenzt wachsen.
        var box = ReadOnlyBox(markdown, multiline: true);
        box.MinLines = 1;
        box.MaxLines = int.MaxValue;
        var view = new Controls.DockPanel();
        Controls.DockPanel.SetDock(buttons, Controls.Dock.Top);
        view.Children.Add(buttons);
        view.Children.Add(box);
        return view;
    }

    private UIElement BugTrackerView(ReportFacts facts)
    {
        var entry = DesyncBugEntry.Build(facts, _service.Profile.Advice);
        // Eingaben und angelegter Bug gelten je Paar, solange das Fenster offen ist: sie überstehen das Neubauen und den Wechsel der Auswahl.
        if (!_bugDrafts.TryGetValue(facts.Summary.OosId16, out var draft))
        {
            draft = new BugDraft();
            _bugDrafts[facts.Summary.OosId16] = draft;
        }

        var panel = new Controls.StackPanel();

        panel.Children.Add(Text(DesyncTexts.ReportBugTitle));
        var title = new Controls.TextBox { Text = draft.Title ?? entry.Title, Margin = new Thickness(0, 0, 0, 6) };
        title.TextChanged += (_, _) => draft.Title = title.Text;
        panel.Children.Add(title);

        // Nur eigene Mods aus dem Register (im UI-Thread gelesen, das Register ist nicht threadsicher).
        panel.Children.Add(Text(DesyncTexts.ReportBugMod));
        var mods = new Controls.ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        var ownMods = _errorLog.Host.Registry()?.Mods.Select(m => new Controls.ComboBoxItem { Content = m.Name, Tag = m.Id }).ToList() ?? new List<Controls.ComboBoxItem>();
        foreach (var item in ownMods)
        {
            mods.Items.Add(item);
        }

        mods.SelectedItem = ownMods.FirstOrDefault(i => (string)i.Tag == draft.ModId)
            ?? ownMods.FirstOrDefault(i => (string)i.Tag == entry.ModId)
            ?? ownMods.FirstOrDefault();
        mods.SelectionChanged += (_, _) => draft.ModId = (mods.SelectedItem as Controls.ComboBoxItem)?.Tag as string;
        panel.Children.Add(mods);

        if (entry.Links.Count > 0)
        {
            panel.Children.Add(Text(DesyncTexts.ReportBugLinks));
            foreach (var link in entry.Links)
            {
                var line = link.LineText;
                panel.Children.Add(Muted("• " + link.DisplayPath + (line.Length > 0 ? " (" + line + ")" : string.Empty)));
            }
        }
        else
        {
            panel.Children.Add(Muted(DesyncTexts.ReportBugNoLinks));
        }

        var create = SmallButton(DesyncTexts.ReportBugCreate);
        create.HorizontalAlignment = HorizontalAlignment.Left;
        create.Margin = new Thickness(0, 8, 0, 4);
        panel.Children.Add(create);
        var created = new Controls.StackPanel();
        panel.Children.Add(created);
        if (ownMods.Count == 0)
        {
            create.IsEnabled = false;
            panel.Children.Add(Notice(DesyncTexts.ReportBugNoOwnMod));
        }

        if (draft.CreatedId is { } createdId)
        {
            ShowBugCreated(create, created, createdId);
        }

        create.Click += (_, _) =>
        {
            var modId = (mods.SelectedItem as Controls.ComboBoxItem)?.Tag as string;
            var edited = string.IsNullOrWhiteSpace(title.Text) ? entry.Title : title.Text.Trim();
            BugReport? report = null;
            // Nicht gespeichert: das Hauptfenster hat den Fehler gemeldet, der Knopf bleibt für einen neuen Versuch.
            if (!_host.ChangeBugs(data => report = (entry with { Title = edited }).AddTo(data, modId)) || report is not { } bug)
            {
                return;
            }

            draft.CreatedId = bug.Id;
            ShowBugCreated(create, created, bug.Id);
        };

        return Scrolling(panel);
    }

    /// <summary>Der Bug ist angelegt: Knopf aus (ein zweiter Klick legte ihn noch einmal an), Hinweis und Link in den Bug-Tracker.</summary>
    private void ShowBugCreated(Controls.Button create, Controls.Panel created, string bugId)
    {
        create.IsEnabled = false;
        created.Children.Clear();
        created.Children.Add(Text(DesyncTexts.ReportBugCreated));
        var open = new Hyperlink(new Run(DesyncTexts.ReportBugOpen));
        open.Click += (_, _) => _host.OpenBugTracker(bugId);
        var openLine = new Controls.TextBlock { TextWrapping = TextWrapping.Wrap };
        openLine.Inlines.Add(open);
        created.Children.Add(openLine);
    }

    // ---------------------------------------------------------------- Aktionen

    private void ShowInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(path)}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, MiscWindowTexts.DiffExplorerFailed(ex.Message), DesyncTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Packt einen Dump-Ordner im Hintergrund als Zip; Ziel wählt der Nutzer (kein Startordner, damit nichts im Spielordner landet).</summary>
    private async void SaveZip(string folder)
    {
        if (_zipsRunning.Contains(folder))
        {
            return;
        }

        var dialog = new SaveFileDialog { Title = DesyncTexts.ReportSaveZip, Filter = DesyncTexts.ReportZipFilter, FileName = Path.GetFileName(folder) + ".zip" };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("desync.zip");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var target = dialog.FileName;
        SetZipRunning(folder, true);
        try
        {
            await Task.Run(() => DumpZipper.SaveAsZip(folder, target));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (IsLoaded)
            {
                MessageBox.Show(this, DesyncTexts.ReportSaveFailed(ex.Message), DesyncTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            SetZipRunning(folder, false);
        }
    }

    /// <summary>Merkt den laufenden Zip-Lauf je Ordner und schaltet die Knöpfe der aktuellen Anzeige dazu.</summary>
    private void SetZipRunning(string folder, bool running)
    {
        if (running)
        {
            _zipsRunning.Add(folder);
        }
        else
        {
            _zipsRunning.Remove(folder);
        }

        foreach (var (path, button) in _zipButtons.Where(z => string.Equals(z.Path, folder, StringComparison.OrdinalIgnoreCase)))
        {
            button.IsEnabled = !running;
        }
    }

    /// <summary>Speichert den Bericht für Mod-Autoren als UTF-8 ohne BOM, über eine temporäre Datei neben dem Ziel.</summary>
    private void SaveMarkdown(string markdown, string oosId16)
    {
        var dialog = new SaveFileDialog { Title = DesyncTexts.ReportSaveFile, Filter = DesyncTexts.ReportTextFilter, FileName = "desync-" + oosId16 + ".txt" };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("desync.report");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var target = dialog.FileName;
        var temporary = target + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllText(temporary, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            MessageBox.Show(this, DesyncTexts.ReportSaveFailed(ex.Message), DesyncTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Knopf "Kopieren"; nach dem Kopieren steht 1,5 s lang "Kopiert" darauf.</summary>
    private static Controls.Button CopyButton(string text)
    {
        var button = SmallButton(DesyncTexts.ReportCopy);
        var label = (Controls.TextBlock)button.Content;
        DispatcherTimer? running = null;
        button.Click += (_, _) =>
        {
            if (!CopyToClipboard(text))
            {
                return;
            }

            running?.Stop();
            var timer = new DispatcherTimer { Interval = CopiedDuration };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                label.Text = DesyncTexts.ReportCopy;
            };
            running = timer;
            label.Text = DesyncTexts.ReportCopied;
            timer.Start();
        };
        return button;
    }

    private static bool CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (COMException)
        {
            // Zwischenablage gerade von einem anderen Programm belegt; der Nutzer kann es erneut versuchen.
            return false;
        }
    }

    // ---------------------------------------------------------------- Bausteine

    /// <summary>Schreibgeschütztes Textfeld; mehrzeilig sechs Zeilen hoch mit Umbruch.</summary>
    private static Controls.TextBox ReadOnlyBox(string text, bool multiline) => new()
    {
        Text = text,
        IsReadOnly = true,
        AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        MinLines = multiline ? 6 : 1,
        MaxLines = multiline ? 6 : 1,
        VerticalScrollBarVisibility = multiline ? Controls.ScrollBarVisibility.Auto : Controls.ScrollBarVisibility.Disabled,
    };

    /// <summary>Textfeld mit dem Knopf "Kopieren" rechts daneben.</summary>
    private static Controls.DockPanel CopyRow(Controls.TextBox box, string text)
    {
        var row = new Controls.DockPanel();
        var copy = CopyButton(text);
        copy.Margin = new Thickness(6, 0, 0, 0);
        copy.VerticalAlignment = VerticalAlignment.Top;
        Controls.DockPanel.SetDock(copy, Controls.Dock.Right);
        row.Children.Add(copy);
        row.Children.Add(box);
        return row;
    }

    private static Controls.ScrollViewer Scrolling(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled,
    };

    /// <summary>Gelesene Eingaben des Berichts: Zusammenfassung und die Zeitleisten-Einträge mit Hinweisart.</summary>
    private sealed record ReportInput(OosPair Pair, OosSummary Summary, IReadOnlyList<TimelineEntry> Leads);

    /// <summary>
    /// Stand des Tabs "PMT-Bugtracker" für ein Paar: bearbeiteter Titel und gewählte Mod (<c>null</c> = unverändert, der Vorschlag gilt) und
    /// die Id des angelegten Bugs.
    /// </summary>
    private sealed class BugDraft
    {
        public string? Title { get; set; }

        public string? ModId { get; set; }

        public string? CreatedId { get; set; }
    }
}
