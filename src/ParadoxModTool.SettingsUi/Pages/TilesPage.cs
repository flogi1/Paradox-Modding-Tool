using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Settings;
using ParadoxModTool.Shell;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie "Kachelleiste": Leiste ein/aus, Standardgröße, Beschriftung und je Kachel Reihenfolge, Sichtbarkeit und "Nur Symbol".
/// Die Liste wird bei jeder Änderung neu gebaut, damit sie dem Modell folgt (auch bei Änderungen von außen).
/// </summary>
public sealed class TilesPage : SettingsPage
{
    private const double VisibleColumnWidth = 62;
    private const double IconOnlyColumnWidth = 86;

    public TilesPage(ISettingsHost host)
        : base(host)
    {
        // Im klassischen Layout gibt es keine Kachelleiste; die Einstellungen bleiben bearbeitbar und gelten beim Umschalten.
        var classicHint = AddHint(SettingsTexts.TilesClassicHint);
        OnRefresh(() => classicHint.Visibility = Host.Settings.WindowLayout == "Classic" ? Visibility.Visible : Visibility.Collapsed);

        AddCheckBox(SettingsTexts.TileBarVisible, null, () => Host.Settings.TileBarVisible, value => Host.Settings.TileBarVisible = value);

        AddHeading(SettingsTexts.TileSizeHeading);
        AddSizeRadio("Large", SettingsTexts.TileSizeLarge);
        AddSizeRadio("Medium", SettingsTexts.TileSizeMedium);
        AddSizeRadio("Compact", SettingsTexts.TileSizeCompact);
        AddCheckBox(SettingsTexts.TileLabelsVisible, null, () => Host.Settings.TileLabelsVisible, value => Host.Settings.TileLabelsVisible = value);

        AddHeading(SettingsTexts.TileOrderHeading);
        AddHint(SettingsTexts.TileOrderHint);
        Root.Children.Add(BuildColumnHeader());

        var box = new ReorderListBox(() => Host.Settings.TileOrder, list => Host.Settings.TileOrder = list, BuildRow);
        Root.Children.Add(box);
        OnRefresh(box.Rebuild);

        AddButton(SettingsTexts.TileResetOrder, null, () =>
        {
            Host.Settings.TileOrder = TileCatalog.DefaultOrder;
            foreach (var id in TileCatalog.DefaultOrder)
            {
                Host.Settings.SetTileIconOnly(id, false);
            }
        });

        RefreshOnSettings();
        // Der GUI-Schalter dämpft die GUI-Kacheln; die Liste liest ihn beim Neuaufbau.
        RefreshOn(handler => GuiSettings.Changed += handler, handler => GuiSettings.Changed -= handler);
    }

    /// <summary>Beschriftungen aus anderen Projekten: Error-Log und Save Explorer teilen den Kategorienamen, die Desync-Analyse hat einen eigenen Text.</summary>
    private static string? ExternalLabel(string tileId) => tileId switch
    {
        TileIds.ErrorLog => SettingsTexts.CategoryLabel(SettingsCategory.ErrorLog),
        TileIds.SaveExplorer => SettingsTexts.CategoryLabel(SettingsCategory.SaveExplorer),
        TileIds.Desync => SettingsTexts.TileDesync,
        _ => null,
    };

    private void AddSizeRadio(string size, string label) =>
        AddRadio("tileSize", label, () => Host.Settings.TileSize == size, () => Host.Settings.TileSize = size);

    /// <summary>Spaltenköpfe über den Häkchen; die Breiten entsprechen denen in <see cref="BuildRow"/>.</summary>
    private static StackPanel BuildColumnHeader()
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 6, 2) };
        header.Children.Add(HeaderText(SettingsTexts.TileVisible, VisibleColumnWidth));
        header.Children.Add(HeaderText(SettingsTexts.TileIconOnly, IconOnlyColumnWidth));
        return header;
    }

    private static TextBlock HeaderText(string text, double width)
    {
        var block = new TextBlock { Text = text, Width = width, TextAlignment = TextAlignment.Right };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private ReorderRow BuildRow(string id)
    {
        var guiOff = TileCatalog.Find(id)?.RequiresGuiTools == true && !GuiSettings.ToolsEnabled;
        var muted = !Host.Settings.IsTileVisible(id) || guiOff;

        // Das Steuerelement übernimmt "Trailing" und hängt es um; deshalb in jedem Aufruf neu bauen.
        var trailing = new StackPanel { Orientation = Orientation.Horizontal };
        trailing.Children.Add(BuildCheck(SettingsTexts.TileVisible, VisibleColumnWidth, Host.Settings.IsTileVisible(id), value => Host.Settings.SetTileVisible(id, value)));
        trailing.Children.Add(BuildCheck(SettingsTexts.TileIconOnly, IconOnlyColumnWidth, Host.Settings.IsTileIconOnly(id), value => Host.Settings.SetTileIconOnly(id, value)));

        return new ReorderRow(
            id,
            MenuTexts.TileLabel(id, ExternalLabel),
            Muted: muted,
            MutedHint: guiOff ? SettingsTexts.TileNeedsGuiTools : null,
            Icon: ToolIcons.Create(id, 16, "Theme.Text"),
            Trailing: trailing);
    }

    /// <summary>Häkchen ohne Beschriftung in einer Spalte fester Breite; der Spaltenname steht im ToolTip.</summary>
    private static Border BuildCheck(string column, double width, bool isChecked, Action<bool> set)
    {
        var check = new CheckBox { IsChecked = isChecked, ToolTip = column, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => set(check.IsChecked == true);
        return new Border { Width = width, Child = check };
    }
}
