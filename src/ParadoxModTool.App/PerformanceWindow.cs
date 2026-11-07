using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Performance;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Performance-Checker" (Spec, Ablauf im Fenster): drei Reiter Messung, Auswertung, Vergleich und eine Statuszeile darunter. Die
/// Reiter füllen eigene Teildateien (<c>PerformanceWindow.Measure.cs</c> usw.). Ohne Besitzer ist es ein eigenständiges Fenster mit eigenem
/// Eintrag in der Taskleiste (Einstellung des Fehlerprotokolls, wie die Desync-Analyse).
/// </summary>
internal sealed partial class PerformanceWindow : Window
{
    private readonly PerformanceWindowHost _host;
    private readonly AppSettings _settings;
    private readonly PerformanceService _service;
    private readonly Controls.TabControl _tabs = new();
    private readonly Controls.TabItem _measureTab = new() { Header = PerformanceTexts.TabMeasure };
    private readonly Controls.TabItem _analyzeTab = new() { Header = PerformanceTexts.TabAnalyze };
    private readonly Controls.TabItem _compareTab = new() { Header = PerformanceTexts.TabCompare };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    /// <summary>Das KI-Fenster "Performance mit KI analysieren"; höchstens eines, es schließt mit diesem Fenster.</summary>
    private AiTaskWindow? _aiWindow;

    /// <param name="owner">null: eigenständiges Fenster mit eigenem Eintrag in der Taskleiste.</param>
    public PerformanceWindow(PerformanceWindowHost host, AppSettings settings, Window? owner)
    {
        _host = host;
        _settings = settings;
        Title = PerformanceTexts.WindowTitle;
        Width = 1150;
        Height = 780;
        MinWidth = 720;
        MinHeight = 420;
        if (owner is not null)
        {
            Owner = owner;
        }

        ShowInTaskbar = owner is null;
        WindowBoundsPersistence.Apply(this, _settings.PerformanceWindowBounds, null);

        // Kann nach der Spiel-Exe fragen (Vanilla-Wurzeln); das Öffnen des Fensters ist die Nutzeraktion dazu.
        _service = new PerformanceService(host, Dispatcher);
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _measureTab.Content = BuildMeasureTab();
        _analyzeTab.Content = BuildAnalyzeTab();
        _compareTab.Content = BuildCompareTab();
        _tabs.Items.Add(_measureTab);
        _tabs.Items.Add(_analyzeTab);
        _tabs.Items.Add(_compareTab);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        Controls.DockPanel.SetDock(_status, Controls.Dock.Bottom);
        root.Children.Add(_status);
        root.Children.Add(_tabs);
        Content = root;

        Closing += (_, _) => SaveBounds();
        Activated += (_, _) => RefreshMeasureTab();
        PreviewKeyDown += Guide_PreviewKeyDown;
    }

    /// <summary>F1 öffnet die Anleitung beim Kapitel des aktuellen Reiters, auch während ein Eingabefeld den Fokus hat.</summary>
    private void Guide_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            var section = _tabs.SelectedItem == _analyzeTab
                ? PerformanceHelpTexts.SectionOrigin
                : _tabs.SelectedItem == _compareTab ? PerformanceHelpTexts.SectionCompare : PerformanceHelpTexts.SectionFlow;
            PerformanceHelpWindow.Show(this, section);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Sanduhr während <paramref name="action"/>: Auswahl und Vergleich lesen beim ersten Mal den Dump und prüfen die Herkunft jeder Zeile
    /// (einige Sekunden im UI-Thread). Verschachtelt sicher: danach gilt wieder der Zeiger von vorher.
    /// </summary>
    internal static void WithWaitCursor(Action action)
    {
        var previous = Mouse.OverrideCursor;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            action();
        }
        finally
        {
            Mouse.OverrideCursor = previous;
        }
    }

    /// <summary>Mit dem Fenster enden der Dateiwächter (Spec: kein Warten ohne sichtbares Fenster) und das KI-Fenster.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _aiWindow?.Close();
        _aiWindow = null;
        _service.Dispose();
        base.OnClosed(e);
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.PerformanceWindowBounds = bounds;
        }
    }
}
