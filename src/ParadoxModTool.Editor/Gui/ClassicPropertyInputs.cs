using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>Was das Eingabefeld einer klassischen Eigenschaft braucht.</summary>
/// <param name="ShowInCode">Der Wert ist ein mehrzeiliger Block: "Im Code zeigen" springt zur Stelle.</param>
internal sealed record ClassicInputContext(
    string Name,
    ResolvedProperty? Property,
    ResolvedElement Element,
    bool Editable,
    GuiPropertyCatalog Catalog,
    Action ShowInCode);

/// <summary>
/// Eingabe als Code: ein Skalar (<see cref="Second"/> ist null) oder die beiden Werte eines Paars. <see cref="Keys"/>: die angezeigten inneren Schlüssel
/// des Paars in ihrer Reihenfolge; <c>First</c> gehört zu <c>Keys.First</c>, <c>Second</c> zu <c>Keys.Second</c>.
/// </summary>
internal readonly record struct ClassicValue(string First, string? Second = null, ClassicPairKeys? Keys = null);

/// <summary>Das Feld und, wie man die Eingabe als Code liest (null = ungültig); <c>Read</c> ist null, wenn das Feld nichts schreibt (mehrzeiliger Block).</summary>
internal sealed record ClassicInputResult(FrameworkElement Content, Func<ClassicValue?>? Read);

/// <summary>
/// Eingabefelder des Eigenschaftenfelds für klassische Elemente (HOI IV, EU IV): Anker, Ausrichtung, Paar, Sprite, Schrift, Lokalisierungstext und Frame.
/// Welches Feld passt, sagt <see cref="ClassicPropertyInput.InputFor"/>; hier stehen nur die Steuerelemente. Schreiben tut das Eigenschaftenfeld.
/// </summary>
internal static class ClassicPropertyInputs
{
    /// <summary>Höchstzahl der Sprite-Namen in der Liste: HOI IV hat etwa 30 000, und eine ComboBox virtualisiert nicht.</summary>
    internal const int MaxSpriteChoices = 200;

    private static readonly Regex KeyShape = new(@"^[\w.\-']+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Je Sprite-Tabelle einmal: die Namen ohne Doppelte (Sprites nennt auch überdeckte), sortiert.
    private static readonly ConditionalWeakTable<ClassicSpriteIndex, string[]> SpriteNames = new();

    // Die Auswahlliste erkennt Änderungen am Text auch dann, wenn das Feld nicht auf dem Bildschirm ist (Tests) oder die Vorlage fehlt.
    private sealed class ClassicCombo : ComboBox
    {
        static ClassicCombo() => TextProperty.OverrideMetadata(
            typeof(ClassicCombo),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Journal | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public event EventHandler? TextEdited;

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ClassicCombo)d).TextEdited?.Invoke(d, EventArgs.Empty);
    }

    public static ClassicInputResult Create(ClassicInputContext c)
    {
        var widget = c.Element.BaseWidget;
        var input = ClassicPropertyInput.InputFor(c.Name, c.Property, c.Catalog, widget);

        // InputFor kennt für Namen wie size auch den Katalog: Steht dort ein Skalar (size = 5), ist ein Textfeld das brauchbare Feld.
        if (input == ClassicInput.Block && c.Property?.Origin.Node is { Kind: GuiNodeKind.Property })
        {
            input = ClassicInput.Text;
        }

        return input switch
        {
            ClassicInput.Block => Block(c),
            ClassicInput.Pair => Pair(c),
            ClassicInput.Anchor => Anchor(c),
            ClassicInput.Format => Format(c),
            ClassicInput.Sprite => Sprite(c),
            ClassicInput.Font => Font(c),
            ClassicInput.LocalisedText => Localised(c),
            ClassicInput.Frame => Frame(c),
            ClassicInput.Bool => Choice(c, new[] { "yes", "no" }),
            _ => Plain(c, 220),
        };
    }

    /// <summary>Wert einer Zeile wie geschrieben (Konstanten bleiben stehen, Anführungszeichen nicht).</summary>
    internal static string RawValue(ResolvedProperty? property) =>
        property is null ? string.Empty
        : property.Origin.Node is { Kind: GuiNodeKind.Property } node ? node.Value ?? string.Empty
        : property.Value;

    /// <summary>
    /// Die Sprite-Namen für die Liste: erst die, die mit dem Text beginnen, dann die, die ihn enthalten (ohne Rücksicht auf Groß-/Kleinschreibung),
    /// höchstens <paramref name="max"/>. Ohne Text die ersten Namen.
    /// </summary>
    internal static IReadOnlyList<string> SpriteMatches(IReadOnlyList<string> names, string typed, int max = MaxSpriteChoices)
    {
        var text = typed.Trim().Trim('"');
        var result = new List<string>(Math.Min(max, names.Count));
        if (text.Length == 0)
        {
            return names.Take(max).ToList();
        }

        foreach (var name in names)
        {
            if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(name);
                if (result.Count >= max)
                {
                    return result;
                }
            }
        }

        foreach (var name in names)
        {
            if (!name.StartsWith(text, StringComparison.OrdinalIgnoreCase) && name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(name);
                if (result.Count >= max)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Sieht der Wert wie ein Lokalisierungsschlüssel aus (ein Wort aus Buchstaben, Ziffern, <c>_ . - '</c>)? Sonst ist es Text oder ein Ausdruck.</summary>
    internal static bool LooksLikeKey(string value) => KeyShape.IsMatch(value.Trim());

    private static string[] NamesOf(ClassicSpriteIndex index) => SpriteNames.GetValue(
        index,
        i => i.Sprites.Select(s => s.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToArray());

    private static ClassicInputResult Block(ClassicInputContext c)
    {
        var show = new Button { Content = GuiTexts.TreeShowInCode, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0) };
        show.Click += (_, _) => c.ShowInCode();
        return new ClassicInputResult(Row(Muted(GuiTexts.PropertyBlockReadOnly), show), null);
    }

    private static ClassicInputResult Plain(ClassicInputContext c, double width)
    {
        var box = new TextBox { Text = RawValue(c.Property), MinWidth = width, IsEnabled = c.Editable };
        return new ClassicInputResult(box, Scalar(c, () => box.Text));
    }

    private static ClassicInputResult Choice(ClassicInputContext c, IEnumerable<string> options)
    {
        var combo = NewCombo(options, RawValue(c.Property), c.Editable, 140);
        return new ClassicInputResult(combo, Scalar(c, () => combo.Text));
    }

    private static Func<ClassicValue?> Scalar(ClassicInputContext c, Func<string> text) => () =>
        ClassicPropertyInput.ScalarText(c.Name, text(), c.Property?.IsQuoted, c.Catalog, c.Element.BaseWidget) is { } code ? new ClassicValue(code) : null;

    private static ClassicInputResult Anchor(ClassicInputContext c)
    {
        var combo = NewCombo(ClassicPropertyInput.AnchorOptions, RawValue(c.Property), c.Editable, 160);
        return new ClassicInputResult(WithWarning(combo, text =>
        {
            var value = ClassicAnchors.Clean(text);
            if (value.Length == 0 || ClassicPropertyInput.IsValidAnchor(value))
            {
                return null;
            }

            return ClassicAnchors.TryParse(value, out _, out _, out var isAlias) && isAlias ? GuiTexts.ClassicValueAlias(value) : GuiTexts.ClassicValueInvalid(value);
        }), Scalar(c, () => combo.Text));
    }

    private static ClassicInputResult Format(ClassicInputContext c)
    {
        var combo = NewCombo(ClassicPropertyInput.FormatOptions(c.Catalog, c.Element.BaseWidget), RawValue(c.Property), c.Editable, 160);
        return new ClassicInputResult(WithWarning(combo, text =>
        {
            var value = ClassicAnchors.Clean(text);
            return value.Length == 0 || ClassicPropertyInput.IsValidFormat(value) ? null : GuiTexts.ClassicValueInvalid(value);
        }), Scalar(c, () => combo.Text));
    }

    private static ClassicInputResult Font(ClassicInputContext c)
    {
        var fonts = GuiSettings.Workspace?.Classic?.Sprites.Fonts.Select(f => f.Name).Order(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        var combo = NewCombo(fonts, RawValue(c.Property), c.Editable, 200);
        return new ClassicInputResult(combo, Scalar(c, () => combo.Text));
    }

    /// <summary>Eine Auswahlliste zum Tippen. Ohne Textsuche: sie würde <c>center</c> still zu <c>CENTER</c> machen, also Vanilla-Eigenheiten reparieren.</summary>
    private static ClassicCombo NewCombo(IEnumerable<string> options, string text, bool editable, double minWidth) => new()
    {
        IsEditable = true,
        IsTextSearchEnabled = false,
        MinWidth = minWidth,
        ItemsSource = options.ToList(),
        Text = text,
        IsEnabled = editable,
    };

    /// <summary>Das Feld mit einer Warnzeile darunter, die bei jeder Textänderung neu entsteht und bei null verschwindet; ein ungültiger Wert bleibt im Feld stehen.</summary>
    private static FrameworkElement WithWarning(ClassicCombo combo, Func<string, string?> warn)
    {
        var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        line.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
        void Update()
        {
            var message = warn(combo.Text ?? string.Empty);
            line.Text = message ?? string.Empty;
            line.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        }

        combo.TextEdited += (_, _) => Update();
        Update();
        return Stacked(combo, line);
    }

    private static ClassicInputResult Pair(ClassicInputContext c)
    {
        var block = c.Property?.Origin.Node is { Kind: GuiNodeKind.KeyedBlock } node ? node : null;
        var keys = ClassicPropertyInput.PairKeysFor(c.Name, block, c.Catalog, c.Element.BaseWidget);
        var (first, second) = block is null ? (string.Empty, string.Empty) : ClassicPropertyInput.ReadPair(block);
        var a = new TextBox { Text = first, Width = 64, IsEnabled = c.Editable };
        var b = new TextBox { Text = second, Width = 64, IsEnabled = c.Editable };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, box, margin) in new[] { (keys.First, a, 0.0), (keys.Second, b, 10.0) })
        {
            var name = Muted(label);
            name.Margin = new Thickness(margin, 0, 4, 0);
            row.Children.Add(name);
            row.Children.Add(box);
            row.Children.Add(PercentButton(box, c.Editable));
        }

        return new ClassicInputResult(row, () =>
        {
            var x = a.Text.Trim();
            var y = b.Text.Trim();
            return ClassicPropertyInput.IsPairComponent(x) && ClassicPropertyInput.IsPairComponent(y) ? new ClassicValue(x, y, keys) : null;
        });
    }

    /// <summary>Knopf neben einem Paar-Wert: zeigt <c>px</c>, <c>%</c> oder <c>%%</c> und schaltet durch (<see cref="ClassicPropertyInput.CyclePercent"/>).</summary>
    private static Button PercentButton(TextBox box, bool editable)
    {
        var button = new Button { Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(2, 0, 0, 0), MinWidth = 26, ToolTip = GuiTexts.ClassicPercentTip };
        void Update()
        {
            var text = box.Text.Trim();
            button.Content = text.EndsWith("%%", StringComparison.Ordinal) ? "%%" : text.EndsWith('%') ? "%" : "px";

            // Konstanten (@name) und Fremdes lassen sich nicht umschalten.
            button.IsEnabled = editable && ClassicPropertyInput.CyclePercent(text) != text;
        }

        box.TextChanged += (_, _) => Update();
        button.Click += (_, _) => box.Text = ClassicPropertyInput.CyclePercent(box.Text.Trim());
        Update();
        return button;
    }

    private static ClassicInputResult Frame(ClassicInputContext c)
    {
        var box = new TextBox { Text = RawValue(c.Property), Width = 60, IsEnabled = c.Editable };
        var range = Muted(string.Empty);
        range.Margin = new Thickness(6, 0, 0, 0);
        var frames = SpriteOf(c) is { } sprite ? sprite.Frames : 0;
        if (frames > 0)
        {
            range.Text = GuiTexts.ClassicFrameRange(frames);
        }

        var hint = Muted(GuiTexts.ClassicFrameZero);
        hint.Margin = new Thickness(0, 2, 0, 0);
        void Update() => hint.Visibility = box.Text.Trim() == "0" ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => Update();
        Update();
        return new ClassicInputResult(Stacked(Row(box, range), hint), () =>
        {
            var text = box.Text.Trim();
            return text.Length > 0 && (text[0] == '@' || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _)) && ClassicPropertyInput.ScalarText(c.Name, text, false, c.Catalog, c.Element.BaseWidget) is { } code
                ? new ClassicValue(code)
                : null;
        });
    }

    /// <summary>Das Sprite des Elements (<c>spriteType</c>, sonst <c>quadTextureSprite</c>); null, wenn keines gesetzt oder bekannt ist.</summary>
    private static ClassicSprite? SpriteOf(ClassicInputContext c)
    {
        if (GuiSettings.Workspace?.Classic is not { } classic)
        {
            return null;
        }

        var name = c.Element.Properties.LastOrDefault(p => string.Equals(p.Name, "spriteType", StringComparison.OrdinalIgnoreCase))?.Value
            ?? c.Element.Properties.LastOrDefault(p => string.Equals(p.Name, "quadTextureSprite", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(name) ? null : classic.Sprites.Find(name.Trim().Trim('"')).Sprite;
    }

    private static ClassicInputResult Localised(ClassicInputContext c)
    {
        var box = new TextBox { Text = RawValue(c.Property), MinWidth = 260, IsEnabled = c.Editable };
        var preview = Muted(string.Empty);
        preview.TextWrapping = TextWrapping.Wrap;
        preview.Margin = new Thickness(0, 2, 0, 0);
        var version = 0;
        void Update()
        {
            var key = box.Text.Trim().Trim('"');
            var mine = ++version;
            if (!LooksLikeKey(key))
            {
                preview.Text = GuiTexts.ClassicLocNoKey;
                return;
            }

            if (GuiSettings.Workspace?.Classic?.Localisation is not { } localisation)
            {
                preview.Text = GuiTexts.ClassicLocMissing;
                return;
            }

            // Der erste Zugriff liest alle yml-Dateien (etwa 100 MB): nie auf dem UI-Thread.
            preview.Text = GuiTexts.ClassicLocLoading;
            var dispatcher = preview.Dispatcher;
            _ = Task.Run(() => localisation.Text(key)).ContinueWith(
                task => dispatcher.BeginInvoke(() =>
                {
                    if (mine == version)
                    {
                        preview.Text = task.IsCompletedSuccessfully && task.Result is { } text ? text : GuiTexts.ClassicLocMissing;
                    }
                }),
                TaskScheduler.Default);
        }

        box.TextChanged += (_, _) => Update();
        Update();
        return new ClassicInputResult(Stacked(box, preview), Scalar(c, () => box.Text));
    }

    private static ClassicInputResult Sprite(ClassicInputContext c)
    {
        var classic = GuiSettings.Workspace?.Classic;
        var names = classic is null ? Array.Empty<string>() : NamesOf(classic.Sprites);
        var combo = new ClassicCombo { IsEditable = true, IsTextSearchEnabled = false, MinWidth = 240, IsEnabled = c.Editable };
        var current = RawValue(c.Property);
        combo.ItemsSource = SpriteMatches(names, current);
        combo.Text = current;

        var image = new Image { Width = 32, Height = 32, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 6, 0), SnapsToDevicePixels = true };
        var link = new TextBlock { Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        var hyperlink = new Hyperlink(new Run(GuiTexts.ClassicSpriteUnknown));
        link.Inlines.Add(hyperlink);

        var updating = false;
        string? shown = current;
        var version = 0;

        var row = Row(image, combo);
        if (GuiSettings.SpritePicker is { } picker)
        {
            var pick = new Button { Content = GuiTexts.ClassicSpritePick, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = c.Editable };
            pick.Click += (_, _) =>
            {
                if (picker(combo.Text.Trim().Trim('"')) is { Length: > 0 } chosen)
                {
                    combo.Text = chosen;
                }
            };
            row.Children.Add(pick);
        }

        if (GuiSettings.SpriteCreator is { } creator)
        {
            var create = new Button { Content = GuiTexts.ClassicSpriteCreate, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = c.Editable };
            void Create()
            {
                var typed = combo.Text.Trim().Trim('"');
                if (creator(typed.Length == 0 ? null : typed) is { Length: > 0 } made)
                {
                    combo.Text = made;

                    // Dieselbe Eingabe löst kein TextChanged aus (der Name war schon getippt): Hinweis und Vorschau trotzdem nachziehen.
                    Update();
                }
            }

            create.Click += (_, _) => Create();
            hyperlink.Click += (_, _) => Create();
            row.Children.Add(create);
        }

        void Update()
        {
            if (updating)
            {
                return;
            }

            var text = (combo.Text ?? string.Empty).Trim().Trim('"');

            // "Sprite anlegen" ersetzt den Arbeitsbereich (neue Sprite-Tabelle): Das neue Sprite soll sofort gefunden werden, ohne Neuaufbau des Index.
            if (GuiSettings.Workspace?.Classic is { } latest && !ReferenceEquals(latest, classic))
            {
                classic = latest;
                names = NamesOf(latest.Sprites);
                shown = null;
            }

            // Die Liste folgt dem Tippen; nach einer Wahl aus der Liste bleibt sie, sonst würde sie mitten in der Auswahl ausgetauscht.
            if (!(combo.SelectedItem is string chosen && chosen == text) && text != shown)
            {
                updating = true;
                try
                {
                    var typed = combo.Text ?? string.Empty;
                    var box = combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox;
                    var caret = box?.CaretIndex ?? typed.Length;
                    combo.ItemsSource = SpriteMatches(names, text);

                    // Ein neuer Inhalt der Liste löscht in einer ComboBox gern den Text: hinschreiben, was getippt war.
                    if (combo.Text != typed)
                    {
                        combo.Text = typed;
                    }

                    box?.Select(Math.Min(caret, box.Text.Length), 0);
                }
                finally
                {
                    updating = false;
                }
            }

            shown = text;
            var found = classic is null || text.Length == 0 ? null : classic.Sprites.Find(text).Sprite;
            link.Visibility = classic is not null && text.Length > 0 && found is null && GuiSettings.SpriteCreator is not null ? Visibility.Visible : Visibility.Collapsed;
            ShowPreview(image, classic, found, ++version, () => version);
        }

        combo.TextEdited += (_, _) => Update();
        Update();
        return new ClassicInputResult(Stacked(row, link), Scalar(c, () => combo.Text));
    }

    /// <summary>Frame 1 der Textur im Bild; die Textur lädt im Hintergrund über den gemeinsamen <see cref="GuiImageCache"/> der Vorschau.</summary>
    private static void ShowPreview(Image image, ClassicAssets? classic, ClassicSprite? sprite, int mine, Func<int> latest)
    {
        image.Source = null;
        if (classic is null || sprite?.TexturePath is not { Length: > 0 } texture || classic.Textures.Locate(texture) is not { } fullPath)
        {
            return;
        }

        var frames = Math.Max(1, sprite.Frames);
        var dispatcher = image.Dispatcher;
        _ = Task.Run(() => GuiImageCache.Shared.Get(fullPath)).ContinueWith(
            task => dispatcher.BeginInvoke(() =>
            {
                if (mine != latest() || !task.IsCompletedSuccessfully || task.Result is not { } bitmap)
                {
                    return;
                }

                var width = bitmap.PixelWidth / frames;
                if (width <= 0 || bitmap.PixelHeight <= 0)
                {
                    return;
                }

                var frame = new CroppedBitmap(bitmap, new Int32Rect(0, 0, width, bitmap.PixelHeight));
                frame.Freeze();
                image.Source = frame;
            }),
            TaskScheduler.Default);
    }

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    private static StackPanel Stacked(UIElement first, UIElement second)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(first);
        stack.Children.Add(second);
        return stack;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }
}
