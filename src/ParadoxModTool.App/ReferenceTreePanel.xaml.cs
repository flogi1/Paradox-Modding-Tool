using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Verweise": zeigt für einen Namen, wo er gesetzt, gelesen, benutzt wird und was er selbst benutzt. Die Daten (Flag- und
/// Prüfungs-Index) holt <see cref="LoadSources"/> vom Hauptfenster; der Baum bleibt stehen, bis der Nutzer neu lädt oder einen anderen Namen öffnet.
/// </summary>
public partial class ReferenceTreePanel : System.Windows.Controls.UserControl
{
    private readonly ReferenceHistory _history = new();
    private readonly ObservableCollection<ReferenceTreeItem> _roots = new();

    /// <summary>Modell über die zuletzt geladenen Quellen; null, solange nichts geladen ist (oder nach <see cref="Reset"/>).</summary>
    private ReferenceTreeModel? _model;

    /// <summary>Jede Anzeige zählt hoch; eine ältere, noch ladende Anzeige verwirft danach ihr Ergebnis.</summary>
    private int _run;

    /// <summary>Die Statuszeile zeigt nur Fortschritt (verschwindet nach dem Laden) bzw. "nicht gefunden" (verschwindet beim nächsten Treffer).</summary>
    private bool _statusIsProgress;
    private bool _statusIsNotFound;

    /// <summary>Über das Kürzel ohne Namen geöffnet: der Reiter zählt als Inhalt, damit die Leiste sichtbar bleibt.</summary>
    private bool _opened;

    /// <summary>Laufende Aufrufe von <see cref="LoadSources"/>; Fortschritt zählt nur währenddessen.</summary>
    private int _loading;

    public ReferenceTreePanel()
    {
        InitializeComponent();
        Tree.ItemsSource = _roots;
        Tree.ContextMenu = new ContextMenu();
        BackButton.ToolTip = ReferenceTreeTexts.Back;
        RefreshButton.ToolTip = ReferenceTreeTexts.Refresh;
        InputBox.ToolTip = ReferenceTreeTexts.InputTip;
        UpdateButtons();
    }

    /// <summary>Doppelklick auf eine Stelle: das Hauptfenster öffnet die Datei an der Zeile.</summary>
    public event EventHandler<SiteLocation>? OpenRequested;

    /// <summary>Nach jeder möglichen Änderung von <see cref="ShowsContent"/>.</summary>
    public event EventHandler? ContentChanged;

    /// <summary>
    /// Liefert die Quellen (Argument true: ⟳, die Daten neu aufbauen; false: vorhandene nehmen) oder null; den Grund für null schreibt das
    /// Hauptfenster über <see cref="SetStatus"/>. Fortschritt über <see cref="ShowProgress"/>.
    /// </summary>
    public Func<bool, Task<ReferenceSources?>>? LoadSources { get; set; }

    /// <summary>Der Reiter zeigt etwas: eine Wurzel, einen geöffneten Namen (auch während des Ladens oder "nicht gefunden") oder ein leeres Eingabefeld nach dem Kürzel.</summary>
    public bool ShowsContent => _roots.Count > 0 || _history.Current is not null || _opened;

    /// <summary>Öffnet den Namen (Verlauf), lädt bei Bedarf die Daten und zeigt die Wurzeln.</summary>
    public void ShowName(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        // Vor dem Öffnen festhalten: der Name im Verlauf macht den Reiter schon zu Inhalt.
        var before = ShowsContent;
        _history.Open(name);
        _ = ShowCurrentAsync(reload: false, before);
    }

    /// <summary>Leert Baum und Verlauf und zeigt den Hinweis (z. B. nach einem Wechsel von Mod oder Playset).</summary>
    public void Reset(string hint)
    {
        var before = ShowsContent;
        _run++;
        _model = null;
        _roots.Clear();
        _history.Clear();
        // Ein Hinweis (z. B. "neu laden") hält einen benutzten Reiter offen, damit die Leiste ihn zeigt; leer blendet sie aus.
        _opened = ReferenceTreeRules.KeepsContentAfterReset(hint, before);
        InputBox.Text = string.Empty;
        SetStatus(hint);
        UpdateButtons();
        RaiseContentChanged(before);
    }

    /// <summary><see cref="ContentChanged"/> nur, wenn sich <see cref="ShowsContent"/> seit <paramref name="before"/> geändert hat.</summary>
    private void RaiseContentChanged(bool before)
    {
        if (ReferenceTreeRules.ContentChanged(before, ShowsContent))
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Die Daten unter dem Baum haben sich erneuert: Baum bleibt stehen, Hinweis auf ⟳.</summary>
    public void MarkDataUpdated()
    {
        if (_model is not null)
        {
            SetStatus(ReferenceTreeTexts.DataUpdated);
        }
    }

    /// <summary>Bleibender Hinweis in der Statuszeile; leer blendet sie aus.</summary>
    public void SetStatus(string text)
    {
        _statusIsProgress = false;
        _statusIsNotFound = false;
        StatusLine.Text = text;
        StatusLine.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Fortschritt beim Laden; verschwindet, sobald die Daten da sind.</summary>
    public void ShowProgress(string text)
    {
        if (_loading == 0)
        {
            return; // verspätete Meldung nach dem Laden
        }

        SetStatus(text);
        _statusIsProgress = true;
    }

    /// <summary>Kürzel ohne Namen: Fokus ins Eingabefeld (nachdem die Leiste eingeblendet ist).</summary>
    public void FocusInput()
    {
        var before = ShowsContent;
        _opened = true;
        RaiseContentChanged(before);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    /// <summary>Zeigt den aktuellen Namen des Verlaufs; lädt die Daten, wenn noch keine da sind oder <paramref name="reload"/> gilt.</summary>
    /// <param name="before"><see cref="ShowsContent"/> vor der auslösenden Änderung (z. B. vor dem Öffnen des Namens im Verlauf).</param>
    private async Task ShowCurrentAsync(bool reload, bool before)
    {
        var run = ++_run;
        var name = _history.Current;
        // Gemeldet wird nur eine echte Änderung von ShowsContent gegenüber dem Stand vor dem Aufruf (sonst flackert die Leiste bei ⟳).
        var reported = before;
        void NotifyContent()
        {
            if (ReferenceTreeRules.ContentChanged(reported, ShowsContent))
            {
                reported = ShowsContent;
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        InputBox.Text = name ?? InputBox.Text;
        UpdateButtons();
        try
        {
            if (_model is null || reload)
            {
                _model = null;
                _roots.Clear();
                ReferenceSources? sources;
                _loading++;
                try
                {
                    ShowProgress(ReferenceTreeTexts.Building);
                    NotifyContent();
                    sources = LoadSources is null ? null : await LoadSources(reload);
                }
                finally
                {
                    _loading--;
                }

                if (run != _run)
                {
                    return;
                }

                if (_statusIsProgress)
                {
                    SetStatus(string.Empty);
                }

                if (sources is null)
                {
                    NotifyContent();
                    return;
                }

                _model = new ReferenceTreeModel(sources);
            }

            ShowRoots(name);
            NotifyContent();
        }
        catch (Exception ex)
        {
            // Ein Fehler beim Laden oder im Modell darf den Reiter nicht kaputt machen: Meldung, ⟳ versucht es erneut.
            if (run != _run)
            {
                return;
            }

            _model = null;
            _roots.Clear();
            SetStatus(ReferenceTreeTexts.BuildFailed(ex.Message));
            NotifyContent();
        }
    }

    /// <summary>Füllt die Wurzeln; <see cref="ContentChanged"/> meldet der Aufrufer (<see cref="ShowCurrentAsync"/>) einmal nach dem Vergleich.</summary>
    private void ShowRoots(string? name)
    {
        _roots.Clear();
        if (name is not null && _model is not null)
        {
            var roots = _model.Roots(name);
            foreach (var root in roots)
            {
                _roots.Add(ReferenceTreeItem.Create(_model, root));
            }

            if (roots.Count == 0)
            {
                SetStatus(ReferenceTreeTexts.NotFound);
                _statusIsNotFound = true;
            }
            else if (_statusIsNotFound)
            {
                SetStatus(string.Empty);
            }
        }
    }

    private void UpdateButtons() => BackButton.IsEnabled = _history.CanGoBack;

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        var before = ShowsContent;
        if (_history.Back() is not null)
        {
            _ = ShowCurrentAsync(reload: false, before);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = ShowCurrentAsync(reload: true, ShowsContent);

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ShowName(InputBox.Text);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Doppelklick auf den Text einer Stelle öffnet sie. In der Tunnel-Phase abgefangen: so klappt der Doppelklick die Stelle nicht zugleich
    /// auf oder zu. Auf dem Aufklapp-Dreieck (z. B. einer Aufwärts-Stelle) bleibt er unberührt und klappt wie bei anderen Einträgen.
    /// </summary>
    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ItemAt(e.OriginalSource)?.Node is { Location: { } location } node
            && ReferenceTreeRules.OpensOnDoubleClick(node, OnExpander(e.OriginalSource)))
        {
            e.Handled = true;
            OpenRequested?.Invoke(this, location);
        }
    }

    /// <summary>Der Klick traf das Aufklapp-Dreieck (ToggleButton der TreeViewItem-Vorlage) des nächsten Eintrags.</summary>
    private static bool OnExpander(object source)
    {
        for (var element = source as DependencyObject; element is not null and not TreeViewItem; element = ParentOf(element))
        {
            if (element is System.Windows.Controls.Primitives.ToggleButton)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Rechtsklick auf eine Stelle oder Wurzel mit Namen: "Als Wurzel öffnen".</summary>
    private void Tree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = Tree.ContextMenu;
        menu.Items.Clear();
        if (ItemAt(e.OriginalSource)?.Node is { NodeKind: RefNodeKind.Site or RefNodeKind.Root, Name.Length: > 0 } node)
        {
            var item = new MenuItem { Header = new TextBlock { Text = ReferenceTreeTexts.OpenAsRoot } };
            item.Click += (_, _) => ShowName(node.Name);
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
        {
            e.Handled = true;
        }
    }

    /// <summary>Eintrag des nächsten <see cref="TreeViewItem"/> über dem angeklickten Element (nicht der äußerste Vorfahr).</summary>
    private static ReferenceTreeItem? ItemAt(object source)
    {
        for (var element = source as DependencyObject; element is not null; element = ParentOf(element))
        {
            if (element is TreeViewItem container)
            {
                return container.DataContext as ReferenceTreeItem;
            }
        }

        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
