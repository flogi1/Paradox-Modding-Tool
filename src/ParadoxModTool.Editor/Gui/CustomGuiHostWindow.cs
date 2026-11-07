using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "In Vanilla-Fenster einbauen…" (EU IV, Plan 3 Task 7): wählt das Gastfenster aus den aufgelösten unterstützten Fenstern (je mit Datei, ROOT und FROM)
/// und fragt, ob das oberste Mod-Fenster danach aus seiner Datei verschwinden soll (Vorgabe: nein). Das Fenster schreibt nichts; geplant und geschrieben
/// wird im Hauptfenster (ein Rückgängig-Schritt).
/// </summary>
public sealed class CustomGuiHostWindow : Window
{
    private readonly IReadOnlyList<SupportedWindow> _supported;
    private readonly ListBox _list = new() { Width = 560, Height = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly CheckBox _remove = new() { IsChecked = false, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 560, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _build = new() { Content = GuiTexts.CustomGuiHostDo, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };

    /// <param name="windowName">Das oberste Mod-Fenster, das eingebaut wird.</param>
    /// <param name="supported">Die aufgelösten unterstützten Fenster (<see cref="CustomGuiIndex.Supported"/>), in dieser Reihenfolge angezeigt.</param>
    public CustomGuiHostWindow(string windowName, IReadOnlyList<SupportedWindow> supported)
    {
        _supported = supported;
        Title = GuiTexts.CustomGuiHostTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        ShownWindows = supported.Select(s => GuiTexts.CustomGuiHostItem(s.WindowName, s.File, s.Root, s.From)).ToList();
        _list.ItemsSource = ShownWindows;
        _list.SelectedIndex = supported.Count > 0 ? 0 : -1;
        _list.SelectionChanged += (_, _) => _build.IsEnabled = SelectedHost is not null;
        _list.MouseDoubleClick += (_, _) =>
        {
            if (SelectedHost is not null && IsVisible)
            {
                DialogResult = true;
            }
        };
        _remove.Content = GuiTexts.CustomGuiHostRemove(windowName);
        _build.IsEnabled = SelectedHost is not null;
        _build.Click += (_, _) =>
        {
            if (SelectedHost is not null && IsVisible)
            {
                DialogResult = true;
            }
        };
        _remove.Checked += (_, _) => UpdateHint(windowName);
        _remove.Unchecked += (_, _) => UpdateHint(windowName);
        UpdateHint(windowName);
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");

        var cancel = new Button { Content = GuiTexts.ScriptedGuiCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_build);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.CustomGuiHostLabel(windowName), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(_list);
        panel.Children.Add(_remove);
        panel.Children.Add(_hint);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _list.Focus();
    }

    /// <summary>Das gewählte Gastfenster; <see langword="null"/> ohne Auswahl.</summary>
    public SupportedWindow? SelectedHost => _list.SelectedIndex >= 0 && _list.SelectedIndex < _supported.Count ? _supported[_list.SelectedIndex] : null;

    /// <summary>Soll das oberste Mod-Fenster aus seiner Datei verschwinden? Nur, wenn der Nutzer es ankreuzt.</summary>
    public bool RemoveOriginal
    {
        get => _remove.IsChecked == true;
        set => _remove.IsChecked = value;
    }

    public int SelectedIndex
    {
        get => _list.SelectedIndex;
        set => _list.SelectedIndex = value;
    }

    /// <summary>Die Zeilen der Liste, wie sie im Fenster stehen.</summary>
    internal IReadOnlyList<string> ShownWindows { get; }

    /// <summary>Der Hinweis, wie er im Fenster steht.</summary>
    internal string HintText => _hint.Text;

    internal Button BuildButton => _build;

    /// <summary>Hinweis zur Probe; ohne Haken bei "entfernen" dazu, dass die oberste Kopie bleibt und die Prüfung ihre Elemente meldet.</summary>
    private void UpdateHint(string windowName)
    {
        _hint.Text = _supported.Count == 0 ? GuiTexts.CustomGuiHostNone
            : RemoveOriginal ? GuiTexts.CustomGuiHostHint
            : GuiTexts.CustomGuiHostHint + "\n\n" + GuiTexts.CustomGuiHostKeepsCopy(windowName);
    }
}
