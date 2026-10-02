using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ParadoxModTool.ModOverview;
using ParadoxModTool.Mods;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Controls = System.Windows.Controls;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ParadoxModTool.App;

/// <summary>Inhalt der Übersicht "Meine Mods": Abschnitte je Spiel, Projektrahmen, Kacheln bzw. Listenzeilen, Vorschaubilder.</summary>
internal sealed partial class MyModsWindow
{
    private const double TileWidth = 250;

    /// <summary>Feste Farben für Platzhalter-Bilder (ohne Vorschaubild); gewählt über eine stabile Summe der Zeichen des Namens.</summary>
    private static readonly Color[] PlaceholderColors =
    {
        Color.FromRgb(0x3B, 0x5B, 0x92), Color.FromRgb(0x6A, 0x3B, 0x92), Color.FromRgb(0x92, 0x63, 0x3B),
        Color.FromRgb(0x3B, 0x7A, 0x55), Color.FromRgb(0x8A, 0x3B, 0x4F), Color.FromRgb(0x3B, 0x7A, 0x86),
    };

    private static readonly GridLength[] ListColumns =
    {
        new(44), new(2, GridUnitType.Star), new(70), new(1.4, GridUnitType.Star), new(120), new(100), new(80), new(150),
    };

    private readonly Dictionary<string, ImageSource?> _thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string GameId, string ModId), Controls.Border> _modElements = new();

    private bool ListView => _host.Settings.MyModsView == "List";

    private void BuildContent(IReadOnlyList<OverviewSection> sections)
    {
        _content.Children.Clear();
        _modElements.Clear();
        if (sections.Count == 0)
        {
            _content.Children.Add(Muted(OverviewTexts.Empty, new Thickness(4)));
            return;
        }

        var allGames = sections.Count == 1 && sections[0].GameId is null;
        if (ListView)
        {
            _content.Children.Add(ListHeader());
        }

        foreach (var section in sections)
        {
            if (section.GameId is not null)
            {
                _content.Children.Add(new Controls.TextBlock { Text = section.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
                _content.Children.Add(new Controls.Separator { Margin = new Thickness(0, 0, 0, 8) });
            }

            Controls.Panel flow = ListView ? new Controls.StackPanel() : new Controls.WrapPanel();
            foreach (var group in section.Groups)
            {
                flow.Children.Add(group.ProjectRoot is { } root ? ProjectFrame(root, group, allGames) : Item(group.Entries[0], allGames, isRoot: false, indent: false));
            }

            _content.Children.Add(flow);
        }
    }

    private FrameworkElement Item(OverviewEntry entry, bool allGames, bool isRoot, bool indent)
    {
        var element = ListView ? ListRow(entry, allGames, isRoot, indent) : Tile(entry, allGames, isRoot, member: indent);
        _modElements[(entry.Mod.GameId, entry.Mod.Mod.Id)] = element;
        element.ContextMenu = BuildContextMenu(entry);
        return element;
    }

    /// <summary>Gestrichelter Rahmen mit Projektname; darin die Kacheln bzw. Zeilen der Mitglieder (Hauptmod zuerst).</summary>
    private FrameworkElement ProjectFrame(OverviewMod root, OverviewGroup group, bool allGames)
    {
        var inner = new Controls.StackPanel { Margin = new Thickness(10, 6, 10, 8) };
        inner.Children.Add(Muted(OverviewTexts.ProjectHeader(root.Mod.Name), new Thickness(0, 0, 0, 6), bold: true));
        Controls.Panel items = ListView ? new Controls.StackPanel() : new Controls.WrapPanel();
        foreach (var entry in group.Entries)
        {
            var isRoot = ReferenceEquals(entry.Mod, root);
            items.Children.Add(Item(entry, allGames, isRoot, indent: !isRoot));
        }

        inner.Children.Add(items);
        var dash = new Rectangle { RadiusX = 6, RadiusY = 6, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 } };
        dash.SetResourceReference(Shape.StrokeProperty, "Theme.ControlBorder");
        var frame = new Controls.Grid { Margin = new Thickness(0, 0, 10, 10) };
        frame.Children.Add(dash);
        frame.Children.Add(inner);
        return frame;
    }

    private Controls.Border Tile(OverviewEntry entry, bool allGames, bool isRoot, bool member)
    {
        var mod = entry.Mod;
        var body = new Controls.StackPanel { Margin = new Thickness(9, 7, 9, 9) };
        body.Children.Add(new Controls.TextBlock
        {
            Text = mod.Mod.Name, FontWeight = FontWeights.Bold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = mod.Mod.Name, Margin = new Thickness(0, 0, 0, 3),
        });
        body.Children.Add(Badges(entry, isRoot));

        body.Children.Add(KeyValue(OverviewTexts.TileVersion, Text(mod.Metadata?.Version)));
        body.Children.Add(KeyValue(OverviewTexts.TileGameVersion, GameVersion(mod)));
        foreach (var (dependency, _) in mod.ResolvedDependencies)
        {
            body.Children.Add(KeyValue(OverviewTexts.TileRequires, Text(dependency.DisplayName)));
        }

        body.Children.Add(Separator());
        body.Children.Add(KeyValue(OverviewTexts.TileSubscribers, Subscribers(mod)));
        body.Children.Add(KeyValue(OverviewTexts.TileFavoritesViews,
            Text(mod.Steam is { } steam && (steam.Favorited is not null || steam.Views is not null) ? $"{Number(steam.Favorited)} · {Number(steam.Views)}" : null)));
        body.Children.Add(KeyValue(OverviewTexts.TileSteamUpdate, Text(mod.Steam?.TimeUpdated?.ToLocalTime().ToString("d"))));
        body.Children.Add(Separator());
        if (!member)
        {
            body.Children.Add(KeyValue(OverviewTexts.TileBugs, Bugs(entry.Bugs)));
        }

        body.Children.Add(KeyValue(OverviewTexts.TileLastRelease, Text(mod.LastRelease is { } release ? $"{release.Version} · {release.Date}" : null)));
        foreach (var dev in entry.DevBranches)
        {
            var line = Muted(OverviewTexts.TileDevBranch(dev.Mod.Name, dev.Metadata?.Version ?? "–"), new Thickness(0, 4, 0, 0));
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(line);
        }

        body.Children.Add(LinkBar(mod.Links));
        body.Children.Add(BuildActions(entry));

        var stack = new Controls.StackPanel();
        stack.Children.Add(Thumbnail(mod, entry.Bugs.New, allGames, 150, TileWidth));
        stack.Children.Add(body);
        var card = new Controls.Border
        {
            Child = stack, Width = TileWidth, Margin = new Thickness(0, 0, 10, 10), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true,
        };
        card.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.PanelBackground");
        card.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.ControlBorder");
        return card;
    }

    private Controls.Border ListRow(OverviewEntry entry, bool allGames, bool isRoot, bool indent)
    {
        var mod = entry.Mod;
        var grid = ListGrid();
        var thumb = Thumbnail(mod, 0, false, 34, 34);
        thumb.Width = 34;
        Place(grid, thumb, 0);
        var name = new Controls.TextBlock
        {
            Text = mod.Mod.Name + (mod.Mod.IsLocked ? "  🔒" : string.Empty), FontWeight = isRoot || !indent ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(indent ? 18 : 0, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = mod.Mod.Name,
        };
        Place(grid, name, 1);
        Place(grid, Cell(mod.GameId.ToUpperInvariant()), 2);
        Place(grid, Cell(RoleText(entry, isRoot)), 3);
        Place(grid, Subscribers(mod).With(e => e.VerticalAlignment = VerticalAlignment.Center), 4);
        Place(grid, !indent ? Bugs(entry.Bugs).With(e => e.VerticalAlignment = VerticalAlignment.Center) : Cell(string.Empty), 5);
        var warnings = Cell(entry.Alerts.Count > 0 ? "⚠ " + entry.Alerts.Count : string.Empty);
        warnings.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
        Place(grid, warnings, 6);
        Place(grid, Cell(mod.LastRelease is { } release ? $"{release.Version} · {release.Date}" : "–"), 7);

        var row = new Controls.Border { Child = grid, Padding = new Thickness(4, 3, 4, 3), BorderThickness = new Thickness(0, 0, 0, 1) };
        row.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Separator");
        return row;
    }

    private FrameworkElement ListHeader()
    {
        var grid = ListGrid();
        var titles = new[]
        {
            string.Empty, OverviewTexts.ColumnName, OverviewTexts.ColumnGame, OverviewTexts.ColumnRole, OverviewTexts.ColumnSubscribers,
            OverviewTexts.ColumnBugs, OverviewTexts.ColumnWarnings, OverviewTexts.ColumnLastRelease,
        };
        for (var i = 0; i < titles.Length; i++)
        {
            Place(grid, new Controls.TextBlock { Text = titles[i], FontWeight = FontWeights.SemiBold }, i);
        }

        var header = new Controls.Border { Child = grid, Padding = new Thickness(4, 4, 4, 4), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 4) };
        header.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.ControlBackground");
        header.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.ControlBorder");
        return header;
    }

    private static Controls.Grid ListGrid()
    {
        var grid = new Controls.Grid();
        foreach (var width in ListColumns)
        {
            grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = width });
        }

        return grid;
    }

    private static void Place(Controls.Grid grid, UIElement element, int column)
    {
        Controls.Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static Controls.TextBlock Cell(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = text.Length > 0 ? text : null };

    private FrameworkElement Badges(OverviewEntry entry, bool isRoot)
    {
        var mod = entry.Mod;
        var panel = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
        panel.Children.Add(Badge(RoleText(entry, isRoot), null));
        if (mod.Mod.Role == ModRole.Submod && mod.Mod.IsCompatch)
        {
            panel.Children.Add(Badge(OverviewTexts.RoleSubmod, null));
        }

        if (mod.Mod.IsLocked)
        {
            panel.Children.Add(Badge("🔒", OverviewTexts.Locked));
        }

        if (!mod.IsPublished)
        {
            panel.Children.Add(Badge(OverviewTexts.TileNotPublished, null));
        }

        return panel;
    }

    /// <summary>Rolle für Badge und Listenspalte: Kompatibilitätspatch mit Zielen, sonst Submod, Hauptmod eines Projekts oder Einzelmod.</summary>
    private static string RoleText(OverviewEntry entry, bool isRoot)
    {
        var mod = entry.Mod.Mod;
        if (mod.IsCompatch)
        {
            return OverviewTexts.RoleCompatch(string.Join(", ", mod.CompatchTargets.Select(t => t.DisplayName).Where(n => n.Length > 0)));
        }

        return mod.Role == ModRole.Submod ? OverviewTexts.RoleSubmod : isRoot ? OverviewTexts.RoleMain : OverviewTexts.RoleSingle;
    }

    private static FrameworkElement Badge(string text, string? tip)
    {
        var badge = new Controls.Border
        {
            Child = new Controls.TextBlock { Text = text, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis }, Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 4, 2), CornerRadius = new CornerRadius(3), MaxWidth = TileWidth - 20, ToolTip = tip ?? text,
        };
        badge.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.ControlBackground");
        return badge;
    }

    private static FrameworkElement KeyValue(string key, FrameworkElement value)
    {
        var row = new Controls.DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        value.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        Controls.DockPanel.SetDock(value, Controls.Dock.Right);
        row.Children.Add(value);
        row.Children.Add(Muted(key, new Thickness(0, 0, 8, 0)));
        return row;
    }

    private static Controls.TextBlock Text(string? value) =>
        new() { Text = string.IsNullOrWhiteSpace(value) ? "–" : value, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = TileWidth * 0.6 };

    private static Controls.TextBlock Muted(string text, Thickness margin, bool bold = false)
    {
        var block = new Controls.TextBlock { Text = text, Margin = margin, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.NoWrap };
        block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private static FrameworkElement Separator()
    {
        var line = new Controls.Border { Height = 1, Margin = new Thickness(0, 4, 0, 4) };
        line.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.Separator");
        return line;
    }

    private static string Number(long? value) => value is { } number ? number.ToString("N0") : "–";

    /// <summary>Unterstützte Spielversion: bei "veraltet" in Warnfarbe mit ⚠, ohne bekannte Spielversion der graue Hinweis.</summary>
    private FrameworkElement GameVersion(OverviewMod mod)
    {
        var supported = mod.Metadata?.SupportedGameVersion;
        var installed = _snapshot?.Games.FirstOrDefault(g => g.Id == mod.GameId)?.InstalledVersion;
        if (mod.VersionSupport == VersionSupport.Outdated)
        {
            var text = Text(supported + " ⚠");
            text.ToolTip = installed;
            text.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
            return text;
        }

        if (installed is null && !string.IsNullOrWhiteSpace(supported))
        {
            var block = Text(supported);
            block.ToolTip = OverviewTexts.GameVersionUnknown;
            block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            return block;
        }

        return Text(supported);
    }

    private static FrameworkElement Subscribers(OverviewMod mod)
    {
        var block = Text(null);
        if (mod.Steam?.Subscriptions is not { } subscribers)
        {
            return block;
        }

        block.Text = subscribers.ToString("N0");
        if (mod.Steam.Trend is { } trend and not 0)
        {
            var run = new System.Windows.Documents.Run((trend > 0 ? "  +" : "  −") + Math.Abs(trend).ToString("N0")) { FontWeight = FontWeights.Normal };
            run.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, trend > 0 ? "Theme.DiffAdded" : "Theme.Danger");
            block.Inlines.Add(run);
        }

        return block;
    }

    private static FrameworkElement Bugs(BugCount count)
    {
        var block = Text($"{count.Open} / ");
        var fresh = new System.Windows.Documents.Run(count.New.ToString());
        if (count.New > 0)
        {
            fresh.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Theme.Danger");
        }

        block.Inlines.Add(fresh);
        return block;
    }

    private FrameworkElement LinkBar(EffectiveLinks links)
    {
        var bar = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var (label, url) in new[]
        {
            (OverviewTexts.LinkWorkshop, links.Workshop), (OverviewTexts.LinkForum, links.Forum), (OverviewTexts.LinkDiscord, links.Discord), (OverviewTexts.LinkGitHub, links.GitHub),
        })
        {
            var button = new Controls.Button { Content = label, FontSize = 11, Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(0, 0, 4, 0), IsEnabled = url is not null, ToolTip = url };
            if (url is not null)
            {
                button.Click += (_, _) => _host.OpenUrl(url);
            }

            bar.Children.Add(button);
        }

        return bar;
    }

    /// <summary>Vorschaubild oder Platzhalter mit Initialen; darüber "N neu" (neue Bugs) und bei "Alle Spiele" das Spiel.</summary>
    private FrameworkElement Thumbnail(OverviewMod mod, int newBugs, bool showGame, double height, double width)
    {
        var grid = new Controls.Grid { Height = height, ClipToBounds = true };
        if (LoadThumbnail(mod.Thumbnail, (int)Math.Ceiling(width * 1.5)) is { } image)
        {
            grid.SetResourceReference(Controls.Panel.BackgroundProperty, "Theme.ControlBackground");
            grid.Children.Add(new Controls.Image { Source = image, Stretch = Stretch.Uniform, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        else
        {
            var color = PlaceholderColors[mod.Mod.Name.Sum(c => c) % PlaceholderColors.Length];
            grid.Background = new SolidColorBrush(color);
            grid.Children.Add(new Controls.TextBlock
            {
                Text = Initials(mod.Mod.Name), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = height > 60 ? 26 : 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });
        }

        if (newBugs > 0)
        {
            var badge = Overlay(OverviewTexts.TileNewBugs(newBugs), System.Windows.HorizontalAlignment.Right);
            badge.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.Danger");
            grid.Children.Add(badge);
        }

        if (showGame)
        {
            var game = Overlay(mod.GameId.ToUpperInvariant(), System.Windows.HorizontalAlignment.Left);
            game.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0));
            grid.Children.Add(game);
        }

        return grid;
    }

    private static Controls.Border Overlay(string text, System.Windows.HorizontalAlignment side) => new()
    {
        Child = new Controls.TextBlock { Text = text, Foreground = Brushes.White, FontSize = 11 },
        Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6), CornerRadius = new CornerRadius(8),
        HorizontalAlignment = side, VerticalAlignment = VerticalAlignment.Top,
    };

    private static string Initials(string name)
    {
        var words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Take(2).Select(w => char.ToUpperInvariant(w[0]));
        var text = new string(letters.ToArray());
        return text.Length > 0 ? text : "?";
    }

    /// <summary>Lädt ein Bild einmal je Pfad in Anzeigegröße; die Datei bleibt nicht gesperrt. Unlesbare Bilder ergeben den Platzhalter.</summary>
    private ImageSource? LoadThumbnail(string? path, int width)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var key = path + "|" + width;
        if (_thumbnails.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = width;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            image = null;
        }

        _thumbnails[key] = image;
        return image;
    }

    /// <summary>Scrollt zur Kachel bzw. Zeile der Mod und hebt sie zwei Sekunden lang hervor.</summary>
    private void ScrollToMod(string gameId, string modId)
    {
        if (!_modElements.TryGetValue((gameId, modId), out var element))
        {
            return;
        }

        element.BringIntoView();
        var thickness = element.BorderThickness;
        element.BorderThickness = new Thickness(2);
        element.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Accent");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            element.BorderThickness = thickness;
            element.SetResourceReference(Controls.Border.BorderBrushProperty, ListView ? "Theme.Separator" : "Theme.ControlBorder");
        };
        timer.Start();
    }
}
