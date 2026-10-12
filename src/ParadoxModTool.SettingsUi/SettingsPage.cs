using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi;

/// <summary>
/// Grundlage einer Seite des Einstellungsfensters: eine senkrechte Liste in einem Rollbereich. Jeder Eintrag liest seinen Wert über eine
/// Sync-Aktion (<see cref="Refresh"/>), damit Werkzeugleisten-Schalter, Dropdowns und Seite synchron bleiben. Ereignis-Abos laufen über
/// <see cref="RefreshOn"/>/<see cref="RefreshOnSettings"/> und enden mit <see cref="Dispose"/>. Beschriftungen sind <see cref="TextBlock"/>,
/// damit Unterstriche in Namen nicht als Zugriffstaste verschwinden.
/// </summary>
public abstract class SettingsPage : IDisposable
{
    private readonly List<Action> _syncs = new();
    private readonly List<Action> _unsubscribe = new();

    protected SettingsPage(ISettingsHost host)
    {
        Host = host;
        Root = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        View = new ScrollViewer
        {
            Content = Root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    public FrameworkElement View { get; }

    public bool IsDisposed { get; private set; }

    protected ISettingsHost Host { get; }

    protected StackPanel Root { get; }

    /// <summary>Alle Einträge lesen ihren Wert neu.</summary>
    public void Refresh()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var sync in _syncs.ToList())
        {
            sync();
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
    }

    /// <summary>Merkt sich eine Sync-Aktion und führt sie gleich aus.</summary>
    protected void OnRefresh(Action sync)
    {
        _syncs.Add(sync);
        sync();
    }

    /// <summary>Abo auf ein fremdes Ereignis (z. B. <c>ScopeSettings.Changed</c>): bei jeder Meldung <see cref="Refresh"/> auf dem UI-Thread.</summary>
    protected void RefreshOn(Action<EventHandler> add, Action<EventHandler> remove)
    {
        EventHandler handler = (_, _) => RefreshOnUiThread();
        add(handler);
        _unsubscribe.Add(() => remove(handler));
    }

    /// <summary>Abo auf <see cref="AppSettings.Changed"/> und <see cref="AppSettings.Reloaded"/>.</summary>
    protected void RefreshOnSettings()
    {
        EventHandler<string> changed = (_, _) => RefreshOnUiThread();
        EventHandler reloaded = (_, _) => RefreshOnUiThread();
        Host.Settings.Changed += changed;
        Host.Settings.Reloaded += reloaded;
        _unsubscribe.Add(() =>
        {
            Host.Settings.Changed -= changed;
            Host.Settings.Reloaded -= reloaded;
        });
    }

    protected TextBlock AddHeading(string text, Panel? parent = null)
    {
        var heading = new TextBlock { Text = text, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 4) };
        (parent ?? Root).Children.Add(heading);
        return heading;
    }

    protected TextBlock AddHint(string text, Panel? parent = null)
    {
        var hint = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        (parent ?? Root).Children.Add(hint);
        return hint;
    }

    protected CheckBox AddCheckBox(string label, string? tip, Func<bool> get, Action<bool> set, Panel? parent = null)
    {
        var box = new CheckBox { Content = Label(label), ToolTip = tip, Margin = new Thickness(0, 3, 0, 3) };
        box.Click += (_, _) => set(box.IsChecked == true);
        OnRefresh(() => box.IsChecked = get());
        (parent ?? Root).Children.Add(box);
        return box;
    }

    protected RadioButton AddRadio(string group, string label, Func<bool> isSelected, Action select, Panel? parent = null)
    {
        var radio = new RadioButton { GroupName = group, Content = Label(label), Margin = new Thickness(0, 3, 0, 3) };
        radio.Click += (_, _) => select();
        OnRefresh(() => radio.IsChecked = isSelected());
        (parent ?? Root).Children.Add(radio);
        return radio;
    }

    protected Button AddButton(string label, string? tip, Action onClick, Panel? parent = null)
    {
        var button = new Button
        {
            Content = Label(label),
            ToolTip = tip,
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => onClick();
        (parent ?? Root).Children.Add(button);
        return button;
    }

    /// <summary>Knopf für eine länger laufende Aufgabe: gesperrt, solange sie läuft (das Fenster ist nicht modal, ein zweiter Klick startet sie sonst doppelt).</summary>
    protected Button AddTaskButton(string label, string? tip, Func<Task> run, Panel? parent = null)
    {
        Button? button = null;
        button = AddButton(label, tip, () => _ = RunAsync(), parent);
        return button;

        async Task RunAsync()
        {
            button!.IsEnabled = false;
            try
            {
                await run();
            }
            finally
            {
                button.IsEnabled = true;
                Refresh();
            }
        }
    }

    /// <summary>Häkchen für einen Eintrag der allgemeinen Werkzeugleiste.</summary>
    protected CheckBox AddToolbarCheckBox(string id, Panel? parent = null) => AddCheckBox(
        SettingsTexts.ToolbarItemLabel(id),
        null,
        () => Host.Settings.IsToolbarItemVisibleInClassic(id),
        visible => Host.Settings.SetToolbarItemVisible(id, visible),
        parent);

    protected static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private void RefreshOnUiThread()
    {
        if (View.Dispatcher.CheckAccess())
        {
            Refresh();
        }
        else
        {
            View.Dispatcher.BeginInvoke(Refresh);
        }
    }
}
