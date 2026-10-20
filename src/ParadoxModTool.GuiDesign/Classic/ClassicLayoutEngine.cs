using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Layout klassischer <c>.gui</c>-Fenster (HOI IV, EU IV) nach Spec "Fakten → Lage" und den Messungen vom 2026-10-17: <c>show_position</c> vor
/// <c>position</c>, <c>orientation</c> (nicht vererbt) und <c>origo</c>, <c>N%</c>, <c>N%%</c> und negative Größen, <c>frame</c> ab 1, 9-Slice mindestens
/// 2 × <c>borderSize</c>, <c>if_resolution</c> (EU IV). Gelegt wird auf einem virtuellen Bildschirm "Auflösung ÷ UI-Skalierung". Jeder Kasten trägt,
/// was gezeichnet wird (<see cref="LayoutBox.Visual"/>); Annäherungen stehen als Hinweis am Kasten.
/// </summary>
public sealed class ClassicLayoutEngine : IGuiLayoutEngine
{
    private enum Kind
    {
        Container,
        Icon,
        Button,
        Text,
        Grid,
        Position,
        Approximated,
        Unknown,
    }

    private static readonly Dictionary<string, Kind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["containerWindowType"] = Kind.Container,
        ["windowType"] = Kind.Container,
        ["eu3dialogtype"] = Kind.Container,
        ["iconType"] = Kind.Icon,
        ["buttonType"] = Kind.Button,
        ["guiButtonType"] = Kind.Button,
        ["checkboxType"] = Kind.Button,
        ["instantTextBoxType"] = Kind.Text,
        ["textboxType"] = Kind.Text,
        ["editBoxType"] = Kind.Text,
        ["gridBoxType"] = Kind.Grid,
        ["listboxType"] = Kind.Grid,
        ["smoothListboxType"] = Kind.Grid,
        ["OverlappingElementsBoxType"] = Kind.Grid,
        ["positionType"] = Kind.Position,
        ["scrollbarType"] = Kind.Approximated,
        ["extendedScrollbarType"] = Kind.Approximated,
        ["dropDownBoxType"] = Kind.Approximated,
        ["shieldtype"] = Kind.Approximated,
        ["textSpriteType"] = Kind.Approximated,
        ["maskedShieldType"] = Kind.Approximated,
        ["LineChartType"] = Kind.Approximated,
        ["PieChartType"] = Kind.Approximated,
    };

    private readonly ClassicAssets _assets;
    private readonly Dictionary<LayoutBox, Rect> _requested = new();

    /// <summary>Fläche, an der die Kinder eines <c>fullscreen</c>-Fensters ausgerichtet sind (samt eigenem Kasten), für den Überstand.</summary>
    private readonly Dictionary<LayoutBox, Rect> _childArea = new();
    private LayoutOptions _options = new();
    private Rect _screen;

    public ClassicLayoutEngine(ClassicAssets assets) => _assets = assets;

    public LayoutBox Layout(ResolvedElement root, LayoutOptions? options = null)
    {
        _options = options ?? new LayoutOptions();
        _requested.Clear();
        _childArea.Clear();

        // Jede Layout-Runde ist ein neuer Vorschaulauf: neu angelegte oder geänderte Texturen und Schriften erscheinen (siehe ClassicTextureLocator).
        _assets.Textures.NewRun();
        var scale = _options.UiScale > 0 && double.IsFinite(_options.UiScale) ? _options.UiScale : 1;
        _screen = new Rect(0, 0, _options.ScreenWidth / scale, _options.ScreenHeight / scale);
        var box = Place(root, _screen) ?? new LayoutBox { Element = root, Notes = { GuiTexts.LayoutHidden } };

        // UI-Skalierung ist nicht gemessen (Spec "Layout"): jede Skalierung ≠ 1 gilt als angenähert.
        if (scale != 1)
        {
            box.Notes.Add(GuiTexts.ClassicUiScale(scale));
        }

        MarkOverflow(box, null);
        return box;
    }

    private LayoutBox? Place(ResolvedElement element, Rect content)
    {
        var props = new ClassicElementProps(element, _screen.W, _screen.H);
        if (!IsVisible(props))
        {
            return null;
        }

        var notes = new List<string>();
        var kind = KindOf(element);
        if (kind == Kind.Approximated)
        {
            notes.Add(GuiTexts.ClassicApproximated(element.Key));
        }
        else if (kind == Kind.Unknown)
        {
            notes.Add(GuiTexts.ClassicUnknownElement(element.Key));
        }

        var shape = Compute(element, props, kind, content, notes);
        if (IsFullscreen(props, kind))
        {
            notes.Add(GuiTexts.ClassicApproximated("fullscreen"));
        }

        var (x, y) = Anchor(props, content, shape.Position, shape.Width, shape.Height, notes);
        var rect = new Rect(x, y, shape.Width, shape.Height);
        var drawn = Drawn(shape.Sprite, rect, notes);
        var box = new LayoutBox
        {
            Element = element,
            X = drawn.X,
            Y = drawn.Y,
            Width = drawn.W,
            Height = drawn.H,
            Visual = shape.Sprite is { } sprite
                ? SpriteVisual(sprite, props, background: false, notes)
                : kind == Kind.Text && shape.Text is { } text ? TextVisual(text, shape.Width, shape.Height) : null,
        };
        Remember(box, rect, drawn);
        box.Notes.AddRange(notes);
        Arrange(element, props, kind, shape, box, rect);
        box.Notes.AddRange(props.Notes);
        return box;
    }

    /// <summary>Position (vor dem Anker) und Größe eines Elements; legt dabei Sprite und Text fest.</summary>
    private Shape Compute(ResolvedElement element, ClassicElementProps props, Kind kind, Rect content, List<string> notes)
    {
        var position = Position(props, notes);
        var sprite = kind is Kind.Icon or Kind.Button or Kind.Approximated ? FindSprite(props, notes, required: kind != Kind.Approximated) : null;
        var text = kind is Kind.Text or Kind.Button ? FindText(props, kind, notes) : null;

        var width = double.NaN;
        var height = double.NaN;
        var size = props.Block("size");
        if (size is not null)
        {
            var (w, h) = ClassicProps.Pair(size, "width", "x", "height", "y");
            width = Dimension(w, content.W, position.X, notes);
            height = Dimension(h, content.H, position.Y, notes);
        }

        // fullscreen ohne size (oder ohne eine Seite): der eigene Kasten ist der Bildschirm, nicht aus den Kindern geschätzt.
        if (IsFullscreen(props, kind))
        {
            width = double.IsNaN(width) ? _screen.W : width;
            height = double.IsNaN(height) ? _screen.H : height;
        }

        if (double.IsNaN(width) || double.IsNaN(height))
        {
            var (naturalWidth, naturalHeight) = NaturalSize(element, props, kind, sprite, text, notes);
            width = double.IsNaN(width) ? naturalWidth : width;
            height = double.IsNaN(height) ? naturalHeight : height;
        }

        if (size is not null)
        {
            (width, height) = Clamp(size, width, height, content, position);
        }

        return new Shape(position, Finite(width), Finite(height), sprite, text);
    }

    private (double W, double H) NaturalSize(ResolvedElement element, ClassicElementProps props, Kind kind, SpriteRef? sprite, TextRef? text, List<string> notes)
    {
        switch (kind)
        {
            case Kind.Position:
                return (0, 0);

            case Kind.Text:
                return text!.Size;

            case Kind.Grid:
                return GridCells(props, double.PositiveInfinity) is { Count: > 0 } cells
                    ? (cells.Max(c => c.X + c.W), cells.Max(c => c.Y + c.H))
                    : (0, 0);
        }

        if (sprite?.TextureSize is { } texture)
        {
            var scale = ClassicProps.Number(props.Scalar("scale")) is { } s && s > 0 ? s : 1;
            return ((double)texture.Width / Math.Max(1, sprite.Sprite!.Frames) * scale, texture.Height * scale);
        }

        if (kind == Kind.Button && text is not null)
        {
            return text.Size;
        }

        if (kind is Kind.Icon or Kind.Button)
        {
            // Fehlendes Sprite oder fehlende Textur ist schon vermerkt.
            if (sprite is null)
            {
                notes.Add(GuiTexts.LayoutSizeUnknown);
            }

            return (0, 0);
        }

        return ContainerSize(element, props, notes);
    }

    /// <summary>Container ohne <c>size</c>: <c>maxWidth</c>/<c>maxHeight</c>, sonst die Hülle der Kinder (ohne Anker und Prozentwerte, daher angenähert).</summary>
    private (double W, double H) ContainerSize(ResolvedElement element, ClassicElementProps props, List<string> notes)
    {
        var maxWidth = ClassicProps.Number(props.Scalar("maxWidth"));
        var maxHeight = ClassicProps.Number(props.Scalar("maxHeight"));
        if (maxWidth is { } mw && maxHeight is { } mh)
        {
            return (mw, mh);
        }

        double right = 0;
        double bottom = 0;
        var any = false;
        foreach (var child in element.Children.Where(c => !IsBackground(c) && !IsIgnoredBlock(c)))
        {
            var childProps = new ClassicElementProps(child, _screen.W, _screen.H);
            if (!IsVisible(childProps))
            {
                continue;
            }

            var shape = Compute(child, childProps, KindOf(child), default, new List<string>());
            right = Math.Max(right, shape.Position.X + shape.Width);
            bottom = Math.Max(bottom, shape.Position.Y + shape.Height);
            any = true;
        }

        notes.Add(any ? GuiTexts.ClassicSizeFromChildren : GuiTexts.LayoutSizeUnknown);
        return (maxWidth ?? right, maxHeight ?? bottom);
    }

    /// <param name="rect">Die angegebene Fläche des Elements (bei vergrößertem 9-Slice nicht der gezeichnete Kasten).</param>
    private void Arrange(ResolvedElement element, ClassicElementProps props, Kind kind, Shape shape, LayoutBox box, Rect rect)
    {
        var content = Content(props, rect);

        // fullscreen = yes (Controller-Ruling nach dem EU-IV-Probe-Bild): Ursprung der Kinder bleibt die linke obere Ecke des Fensters, ihre Anker
        // (orientation) und %/%%-Größen rechnen aber mit dem ganzen virtuellen Bildschirm. Belegt: mainmenu_panel_bottom (CENTER_DOWN in mainmenu,
        // 1024 × 1024 bei 0,0) liegt CENTER_DOWN des Bildschirms; join_direct in multiplayer_menu bleibt relativ zum Fenster.
        var childArea = content;
        if (IsFullscreen(props, kind))
        {
            childArea = new Rect(content.X, content.Y, _screen.W, _screen.H);
            _childArea[box] = Union(rect, childArea);
        }


        // Knopftext: ein Kind mit demselben Element über dem Sprite, weil ein Kasten nur ein Bild trägt.
        if (kind == Kind.Button && shape.Text is { Display.Length: > 0 } buttonText)
        {
            // multiline = yes (HOI IV show_subscription): der Knopftext bricht an der Knopfbreite um.
            var label = ClassicProps.IsYes(props.Scalar("multiline")) && rect.W > 0 ? Fit(buttonText, rect.W, box.Notes) : buttonText;
            box.Children.Add(new LayoutBox
            {
                Element = element,
                X = rect.X,
                Y = rect.Y,
                Width = rect.W,
                Height = rect.H,
                Visual = TextVisual(label, rect.W, rect.H),
            });
        }

        if (kind == Kind.Grid)
        {
            box.Notes.Add(GuiTexts.ClassicGridContent);
            var cells = GridCells(props, content.W);
            for (var i = 0; i < cells.Count; i++)
            {
                box.Children.Add(new LayoutBox
                {
                    Element = element,
                    RepeatIndex = i,
                    X = content.X + cells[i].X,
                    Y = content.Y + cells[i].Y,
                    Width = cells[i].W,
                    Height = cells[i].H,
                });
            }
        }

        foreach (var child in element.Children)
        {
            // Unbekannter Block ohne name: kein Element, nur ein Hinweis am Elternkasten.
            if (IsIgnoredBlock(child))
            {
                box.Notes.Add(GuiTexts.ClassicBlockIgnored(child.Key));
                continue;
            }

            var placed = IsBackground(child) ? PlaceBackground(child, rect) : Place(child, childArea);
            if (placed is not null)
            {
                box.Children.Add(placed);
            }
        }
    }

    /// <summary>
    /// <c>background</c>-Kind: das Sprite auf den ganzen Elternkasten gestreckt (nicht auf die Fläche innerhalb von <c>margin</c>). Bei einem
    /// <c>fullscreen</c>-Fenster ebenso auf dessen eigenen Kasten, nicht auf den Bildschirm (nicht gemessen).
    /// </summary>
    private LayoutBox? PlaceBackground(ResolvedElement element, Rect parent)
    {
        var props = new ClassicElementProps(element, _screen.W, _screen.H);
        if (!IsVisible(props))
        {
            return null;
        }

        var notes = new List<string>();
        var sprite = FindSprite(props, notes, required: true);
        var drawn = Drawn(sprite, parent, notes);
        var box = new LayoutBox
        {
            Element = element,
            X = drawn.X,
            Y = drawn.Y,
            Width = drawn.W,
            Height = drawn.H,
            Visual = sprite is null ? null : SpriteVisual(sprite, props, background: true, notes),
        };
        Remember(box, parent, drawn);
        box.Notes.AddRange(notes);
        box.Notes.AddRange(props.Notes);
        return box;
    }

    /// <summary>Plätze für <see cref="LayoutOptions.ListSampleCount"/> Beispieleinträge der Größe <c>slotsize</c>, relativ zur Inhaltsfläche.</summary>
    private List<Rect> GridCells(ClassicElementProps props, double contentWidth)
    {
        var cells = new List<Rect>();
        var (w, h) = props.Block("slotsize") is { } slot ? ClassicProps.Pair(slot, "width", "x", "height", "y") : (null, null);
        var slotWidth = ClassicProps.Number(w) ?? 0;
        var slotHeight = ClassicProps.Number(h) ?? 0;
        var count = Math.Max(0, _options.ListSampleCount);
        if (slotWidth <= 0 || slotHeight <= 0 || count == 0)
        {
            return cells;
        }

        var horizontal = ClassicProps.Number(props.Scalar("max_slots_horizontal"));
        var vertical = ClassicProps.Number(props.Scalar("max_slots_vertical"));
        for (var i = 0; i < count; i++)
        {
            int column;
            int row;
            if (horizontal is null && vertical is { } rows && rows >= 1)
            {
                // Nur senkrecht begrenzt: Spalte für Spalte füllen.
                column = i / (int)rows;
                row = i % (int)rows;
            }
            else
            {
                var columns = horizontal is { } fixedColumns && fixedColumns >= 1
                    ? (int)fixedColumns
                    : double.IsFinite(contentWidth) ? Math.Max(1, (int)(contentWidth / slotWidth)) : 1;
                column = i % columns;
                row = i / columns;
            }

            cells.Add(new Rect(column * slotWidth, row * slotHeight, slotWidth, slotHeight));
        }

        return cells;
    }

    private static (double X, double Y) Position(ClassicElementProps props, List<string> notes)
    {
        var block = props.Block("show_position") ?? props.Block("position");
        if (block is null)
        {
            return (0, 0);
        }

        var (x, y) = ClassicProps.Pair(block, "x", "x", "y", "y");
        return (Coordinate(x, notes), Coordinate(y, notes));
    }

    private static double Coordinate(string? value, List<string> notes)
    {
        if (value is null)
        {
            return 0;
        }

        if (ClassicProps.Number(value) is { } number)
        {
            return number;
        }

        notes.Add(GuiTexts.LayoutPositionUnresolved(value));
        return 0;
    }

    private static (double X, double Y) Anchor(ClassicElementProps props, Rect content, (double X, double Y) position, double width, double height, List<string> notes)
    {
        var (ox, oy) = Factors(props.Scalar("orientation"), notes);
        var (gx, gy) = Factors(props.Scalar("origo"), notes);
        var x = content.X + position.X + content.W * ox - width * gx;
        var y = content.Y + position.Y + content.H * oy - height * gy;
        if (ClassicProps.IsYes(props.Scalar("centerPosition")))
        {
            x -= width / 2;
            y -= height / 2;
        }

        return (x, y);
    }

    private static (double X, double Y) Factors(string? value, List<string> notes)
    {
        if (value is null)
        {
            return (0, 0);
        }

        if (!ClassicAnchors.TryParse(value, out var fx, out var fy, out var alias))
        {
            notes.Add(GuiTexts.ClassicAnchorUnknown(ClassicAnchors.Clean(value)));
            return (0, 0);
        }

        if (alias)
        {
            notes.Add(GuiTexts.ClassicAnchorAlias(ClassicAnchors.Clean(value)));
        }

        return (fx, fy);
    }

    /// <summary>Eine Seitenlänge aus <c>size</c>: <c>N%%</c> = N % von (Elterngröße − eigene Position), <c>N%</c> = N % der Elterngröße, negativ = Elterngröße + N.</summary>
    private static double Dimension(string? value, double parent, double position, List<string> notes)
    {
        if (value is null)
        {
            return double.NaN;
        }

        if (Length(value, parent, position) is { } length)
        {
            return length;
        }

        notes.Add(GuiTexts.LayoutSizeUnresolved(value));
        return double.NaN;
    }

    private static double? Length(string value, double parent, double position)
    {
        var text = value.Trim();
        if (text.EndsWith("%%", StringComparison.Ordinal))
        {
            return ClassicProps.Number(text[..^2]) is { } percent ? percent / 100 * (parent - position) : null;
        }

        if (text.EndsWith('%'))
        {
            return ClassicProps.Number(text[..^1]) is { } percent ? percent / 100 * parent : null;
        }

        return ClassicProps.Number(text) is { } number ? number < 0 ? parent + number : number : null;
    }

    /// <summary><c>min</c>/<c>max</c> im <c>size</c>-Block begrenzen die Größe.</summary>
    private static (double W, double H) Clamp(GuiNode size, double width, double height, Rect content, (double X, double Y) position)
    {
        if (ClassicProps.Block(size, "min") is { } min)
        {
            var (w, h) = ClassicProps.Pair(min, "width", "x", "height", "y");
            width = w is not null && Length(w, content.W, position.X) is { } minWidth ? Math.Max(width, minWidth) : width;
            height = h is not null && Length(h, content.H, position.Y) is { } minHeight ? Math.Max(height, minHeight) : height;
        }

        if (ClassicProps.Block(size, "max") is { } max)
        {
            var (w, h) = ClassicProps.Pair(max, "width", "x", "height", "y");
            width = w is not null && Length(w, content.W, position.X) is { } maxWidth ? Math.Min(width, maxWidth) : width;
            height = h is not null && Length(h, content.H, position.Y) is { } maxHeight ? Math.Min(height, maxHeight) : height;
        }

        return (width, height);
    }

    /// <summary>
    /// Gezeichneter Kasten: ein 9-Slice unter 2 × <c>borderSize</c> zeichnet das Spiel größer (gemessen rund 110 bei Höhe 20 und Rand 64, um die Mitte der
    /// angegebenen Fläche gewachsen, Probe-Bild 2026-10-17). PMT nimmt 2 × <c>borderSize</c> um die Mitte und vermerkt es; Anker, Kinder und Überstand
    /// rechnen weiter mit der angegebenen Fläche (Spec "Sprites → 9-Slice", Ruling Review Task 8).
    /// </summary>
    private static Rect Drawn(SpriteRef? sprite, Rect requested, List<string> notes)
    {
        if (sprite?.Sprite is not { Kind: ClassicSpriteKind.CorneredTile } cornered)
        {
            return requested;
        }

        var width = Math.Max(requested.W, 2.0 * cornered.BorderX);
        var height = Math.Max(requested.H, 2.0 * cornered.BorderY);
        if (width == requested.W && height == requested.H)
        {
            return requested;
        }

        notes.Add(GuiTexts.ClassicCorneredMinimum(cornered.BorderX, cornered.BorderY));
        return new Rect(requested.X - (width - requested.W) / 2, requested.Y - (height - requested.H) / 2, width, height);
    }

    /// <summary>Merkt die angegebene Fläche, wenn der gezeichnete Kasten davon abweicht (für den Überstand).</summary>
    private void Remember(LayoutBox box, Rect requested, Rect drawn)
    {
        if (requested != drawn)
        {
            _requested[box] = requested;
        }
    }

    private static Rect Content(ClassicElementProps props, Rect rect)
    {
        double left = 0;
        double top = 0;
        double right = 0;
        double bottom = 0;
        if (props.Block("margin") is { } margin)
        {
            left = ClassicProps.Number(ClassicProps.Scalar(margin, "left")) ?? 0;
            top = ClassicProps.Number(ClassicProps.Scalar(margin, "top")) ?? 0;
            right = ClassicProps.Number(ClassicProps.Scalar(margin, "right")) ?? 0;
            bottom = ClassicProps.Number(ClassicProps.Scalar(margin, "bottom")) ?? 0;
        }

        return new Rect(rect.X + left, rect.Y + top, Math.Max(0, rect.W - left - right), Math.Max(0, rect.H - top - bottom));
    }

    private SpriteRef? FindSprite(ClassicElementProps props, List<string> notes, bool required)
    {
        var name = props.Scalar("spriteType") ?? props.Scalar("quadTextureSprite");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var (sprite, caseMismatch) = _assets.Sprites.Find(name);
        if (sprite is null)
        {
            notes.Add(GuiTexts.ClassicSpriteMissing(name));
            return new SpriteRef(name, null, null, null);
        }

        if (caseMismatch)
        {
            notes.Add(GuiTexts.ClassicSpriteCaseMismatch(name));
        }

        if (sprite.Kind is not (ClassicSpriteKind.Sprite or ClassicSpriteKind.CorneredTile or ClassicSpriteKind.FrameAnimated))
        {
            notes.Add(GuiTexts.ClassicSpriteKindApproximated(sprite.Kind.ToString()));
        }

        var path = string.IsNullOrEmpty(sprite.TexturePath) ? null : _assets.Textures.Locate(sprite.TexturePath);
        if (path is null)
        {
            notes.Add(GuiTexts.ClassicTextureMissing(sprite.TexturePath ?? string.Empty));
            return new SpriteRef(name, sprite, null, null);
        }

        // Die Datei gibt es, ihr Kopf ist aber unlesbar (abgeschnitten, unbekanntes Format): Größe 0, nie still.
        var size = _assets.Textures.SizeOf(sprite.TexturePath!);
        if (size is null)
        {
            notes.Add(GuiTexts.ClassicTextureUnreadable(sprite.TexturePath!));
        }

        return new SpriteRef(name, sprite, path, size);
    }

    private static GuiSpriteVisual SpriteVisual(SpriteRef sprite, ClassicElementProps props, bool background, List<string> notes)
    {
        if (sprite.Sprite is not { } found)
        {
            return new GuiSpriteVisual(sprite.Name, null, ClassicSpriteKind.Other, 1, 1, 0, 0, false, background);
        }

        // frame zählt ab 1; 0 und fehlend zeigen Frame 1 (gemessen 2026-10-17).
        var frames = Math.Max(1, found.Frames);
        var frame = (int)Math.Round(ClassicProps.Number(props.Scalar("frame")) ?? 1);
        if (frame < 1)
        {
            frame = 1;
        }
        else if (frame > frames)
        {
            notes.Add(GuiTexts.ClassicFrameClamped(frame, frames));
            frame = frames;
        }

        return new GuiSpriteVisual(sprite.Name, sprite.FullPath, found.Kind, frame, frames, found.BorderX, found.BorderY, found.TilingCenter, background);
    }

    /// <summary>
    /// Text des Elements: lokalisiert (Englisch), sonst der Schlüssel selbst wie im Spiel; <c>\n</c> trennt Zeilen. Knöpfe lesen <c>buttonText</c>/<c>buttonFont</c>,
    /// sonst <c>text</c>/<c>font</c> (HOI IV <c>single_player_button</c>). Textfelder brechen an Leerzeichen innerhalb von <c>maxWidth</c> um. Gemessen mit
    /// der Bitmap-Schrift, ohne Farbcodes.
    /// </summary>
    private TextRef? FindText(ClassicElementProps props, Kind kind, List<string> notes)
    {
        var button = kind == Kind.Button;
        var key = (button ? props.Scalar("buttonText") ?? props.Scalar("text") : props.Scalar("text")) ?? string.Empty;
        if (button && key.Length == 0)
        {
            return null;
        }

        var fontName = button ? props.Scalar("buttonFont") ?? props.Scalar("font") : props.Scalar("font");
        var lines = (_assets.Localisation.Text(key) ?? key).Replace("\\n", "\n").Replace("\r\n", "\n").Split('\n');
        Func<string, double> measure;
        double lineHeight;
        if (fontName is not null && _assets.Fonts.Get(fontName) is { } font)
        {
            measure = line => font.Font.MeasureWidth(ClassicText.StripColorCodes(line));
            lineHeight = font.Font.LineHeight;
            if (lines.Any(line => font.Font.HasMissingGlyphs(ClassicText.StripColorCodes(line))))
            {
                notes.Add(GuiTexts.ClassicGlyphsMissing);
            }
        }
        else
        {
            notes.Add(GuiTexts.ClassicFontMissing(fontName ?? string.Empty));
            measure = line => ClassicText.StripColorCodes(line).Length * 7;
            lineHeight = 20;
        }

        var maxWidth = ClassicProps.Number(props.Scalar("maxWidth"));
        var maxHeight = ClassicProps.Number(props.Scalar("maxHeight"));
        var rawFormat = ClassicAnchors.Clean(props.Scalar("format") ?? (button ? "center" : "left")).ToLowerInvariant();
        // upper_left (HOI IV, 496-mal) und upper_right sind so gezeichnet wie geschrieben (Text von oben); up/down/lower_* nur waagerecht, mit Hinweis.
        var format = ClassicText.HorizontalFormat(rawFormat, out var verticalNote, out var unknownFormat);
        if (verticalNote)
        {
            notes.Add(GuiTexts.ClassicFormatVertical(rawFormat));
        }
        else if (unknownFormat)
        {
            notes.Add(GuiTexts.ClassicFormatUnknown(rawFormat));
        }

        var text = new TextRef(lines, fontName, format, maxWidth is > 0 ? maxWidth : null, maxHeight is > 0 ? maxHeight : null, lineHeight, measure);
        return Fit(text, button ? null : text.MaxWidth, notes);
    }

    /// <summary>
    /// Bricht die Zeilen an Leerzeichen innerhalb von <paramref name="wrapWidth"/> um (ohne Breite: kein Umbruch) und schneidet ab, was über <c>maxHeight</c>
    /// geht. Beides mit Hinweis, wenn der Text nicht passt. Größe: Breite <c>maxWidth</c> (sonst die breiteste Zeile), Höhe Zeilen × Zeilenhöhe bis <c>maxHeight</c>.
    /// </summary>
    private static TextRef Fit(TextRef text, double? wrapWidth, List<string> notes)
    {
        var lines = wrapWidth is { } wrap ? text.Lines.SelectMany(line => ClassicText.Wrap(line, wrap, text.Measure)).ToList() : text.Lines.ToList();
        var limit = wrapWidth ?? text.MaxWidth;
        if (limit is { } widest && lines.Any(line => text.Measure(line) > widest))
        {
            notes.Add(GuiTexts.ClassicTextTooWide(widest));
        }

        var total = lines.Count;
        var height = lines.All(line => line.Length == 0) ? 0 : total * text.LineHeight;
        if (text.MaxHeight is { } maxHeight && height > maxHeight)
        {
            var shown = Math.Clamp((int)Math.Floor(maxHeight / text.LineHeight), 1, total);
            lines = lines.Take(shown).ToList();
            notes.Add(GuiTexts.ClassicTextCut(shown, total));
            height = maxHeight;
        }

        var width = text.MaxWidth ?? lines.Max(text.Measure);
        return text with { Lines = lines, Size = (width, height) };
    }

    private static GuiTextVisual TextVisual(TextRef text, double width, double height) =>
        new(text.Display, text.FontName, text.Format, (int)Math.Round(text.MaxWidth ?? width), (int)Math.Round(text.MaxHeight ?? height));

    /// <summary><c>hide = yes</c> und <c>visible = no</c> folgen <see cref="LayoutOptions.ShowHidden"/>.</summary>
    private bool IsVisible(ClassicElementProps props)
    {
        var hidden = ClassicProps.IsYes(props.Scalar("hide")) || props.Scalar("visible") is { } visible && ClassicProps.IsNo(visible);
        return !hidden || _options.ShowHidden;
    }

    /// <summary>
    /// Kästen, die über den Elternkasten oder den Bildschirm hinausragen (mehr als 1 Pixel). Nichts wird dafür weggelassen. Gerechnet wird mit der
    /// angegebenen Fläche: ein größer gezeichneter 9-Slice zählt nicht als Überstand.
    /// </summary>
    private void MarkOverflow(LayoutBox box, LayoutBox? parent)
    {
        var own = Requested(box);
        if (parent is not null && Exceeds(own, _childArea.TryGetValue(parent, out var area) ? area : Requested(parent)))
        {
            box.OutsideParent = true;
        }

        if (Exceeds(own, _screen))
        {
            box.OutsideScreen = true;
            box.OffScreen = !Intersects(own, _screen);
        }

        foreach (var child in box.Children)
        {
            MarkOverflow(child, box);
        }
    }

    private Rect Requested(LayoutBox box) =>
        _requested.TryGetValue(box, out var requested) ? requested : new Rect(box.X, box.Y, box.Width, box.Height);

    private static bool Exceeds(Rect box, Rect outer) =>
        box.X < outer.X - 1 || box.Y < outer.Y - 1 || box.X + box.W > outer.X + outer.W + 1 || box.Y + box.H > outer.Y + outer.H + 1;

    private static Rect Union(Rect a, Rect b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new Rect(left, top, Math.Max(a.X + a.W, b.X + b.W) - left, Math.Max(a.Y + a.H, b.Y + b.H) - top);
    }

    /// <summary><c>fullscreen = yes</c> (auch <c>fullScreen</c>) an einem Fenster.</summary>
    private static bool IsFullscreen(ClassicElementProps props, Kind kind) => kind == Kind.Container && ClassicProps.IsYes(props.Scalar("fullscreen"));

    private static bool Intersects(Rect box, Rect outer) =>
        box.X < outer.X + outer.W && box.X + box.W > outer.X && box.Y < outer.Y + outer.H && box.Y + box.H > outer.Y;

    /// <summary>Elementschlüssel, die die klassische Vorschau kennt (ohne Rücksicht auf Groß-/Kleinschreibung); der Katalog lernt sie auch ohne <c>name</c>.</summary>
    public static bool IsKnownElement(string key) => Kinds.ContainsKey(key);

    private static Kind KindOf(ResolvedElement element) => Kinds.TryGetValue(element.Key, out var kind) ? kind : Kind.Unknown;

    /// <summary>Unbekannter Schlüssel ohne <c>name</c>: kein Element (nur solche mit Namen gelten als Container, Brief Task 8).</summary>
    private static bool IsIgnoredBlock(ResolvedElement element) =>
        !IsBackground(element) && KindOf(element) == Kind.Unknown && string.IsNullOrEmpty(ClassicProps.Scalar(element, "name"));

    private static bool IsBackground(ResolvedElement element) =>
        element.Role != GuiRole.WidgetHolder && string.Equals(element.Key, "background", StringComparison.OrdinalIgnoreCase);

    private static double Finite(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

    private readonly record struct Rect(double X, double Y, double W, double H);

    private sealed record SpriteRef(string Name, ClassicSprite? Sprite, string? FullPath, (int Width, int Height)? TextureSize);

    /// <summary>Text mit Zeilen (nach Umbruch und Abschneiden), Schrift, Ausrichtung, Grenzen (nur Werte über 0) und Messfunktion.</summary>
    private sealed record TextRef(
        IReadOnlyList<string> Lines, string? FontName, string Format, double? MaxWidth, double? MaxHeight, double LineHeight, Func<string, double> Measure)
    {
        public string Display => string.Join("\n", Lines);

        public (double W, double H) Size { get; init; }
    }

    private sealed record Shape((double X, double Y) Position, double Width, double Height, SpriteRef? Sprite, TextRef? Text);
}
