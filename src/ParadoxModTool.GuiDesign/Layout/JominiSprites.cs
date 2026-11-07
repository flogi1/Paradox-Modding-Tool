using System.Globalization;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Liest aus einem aufgelösten Element das Texturbild (<see cref="JominiSpriteVisual"/>): Bildausschnitt (<c>framesize</c>, <c>frame_grid</c>,
/// <c>frame</c>, <c>upframe</c>), Art (<c>spriteType</c>), Rand, Einpassung, Spiegelung, Dichte, Farbe und Deckkraft sowie die Platzhalter. Es wird
/// nie dekodiert; die Größe kommt aus dem Kopf (<see cref="TextureLocator.HeaderOf"/>, je Lauf einmal). Die Annahmen über das Spielverhalten stehen
/// in <see cref="JominiSpriteRules"/>; solange sie nicht gemessen sind, nennt jedes Sprite sie in den Hinweisen.
/// </summary>
internal static class JominiSprites
{
    /// <summary>Teile des normalisierten Widget-Namens, die die Vorschau nicht zeichnen kann (Platzhalter).</summary>
    private static readonly string[] NotDrawnKinds =
    {
        "portrait", "coat_of_arms", "coatofarms", "video", "flag3d", "entity", "piechart", "progresspie", "treemap",
    };

    /// <summary>
    /// Das Sprite des Elements; <see langword="null"/> ohne (oder mit leerer) <c>texture</c> bei einer zeichenbaren Art. <paramref name="notes"/> bekommt
    /// die Hinweise ohne Doppelte; ein Aufrufer, der schon Hinweise hat, prüft selbst gegen seine Liste. <paramref name="background"/>: das Element ist ein
    /// <c>background</c> (nur dort wirkt <c>fittype</c>, siehe <see cref="ReadFit"/>).
    /// </summary>
    internal static JominiSpriteVisual? Build(ResolvedElement element, TextureLocator textures, List<string> notes, bool background = false)
    {
        var kind = GuiWidgetKinds.Normalize(element.BaseWidget);
        var texture = Unquote(element.Property("texture")?.Value);

        if (NotDrawnKinds.Any(part => kind.Contains(part, StringComparison.OrdinalIgnoreCase)))
        {
            AddOnce(notes, GuiTexts.LayoutWidgetNotDrawn(kind));
            return Placeholder(texture, JominiSpritePlaceholder.NotDrawn, kind);
        }

        if (string.IsNullOrEmpty(texture))
        {
            return null;
        }

        if (texture.Contains('['))
        {
            AddOnce(notes, GuiTexts.LayoutTextureDynamic(texture));
            return Placeholder(texture, JominiSpritePlaceholder.Dynamic, texture);
        }

        if (textures.Locate(texture) is not { } fullPath)
        {
            AddOnce(notes, JominiNoteKinds.Tag(GuiTexts.LayoutTextureMissing(texture), GuiFindingKind.TextureMissing));
            return Placeholder(texture, JominiSpritePlaceholder.Missing, texture);
        }

        // Kopf nicht lesbar (kein erkennbares Bildformat) zählt wie ein nicht unterstütztes Format.
        var header = textures.HeaderOf(texture) ?? new TextureHeader(0, 0, GuiTexts.TextureFormatNone);
        if (header.UnsupportedFormat is { } format)
        {
            AddOnce(notes, JominiNoteKinds.Tag(GuiTexts.LayoutTextureFormat(texture, format), GuiFindingKind.ImageUnreadable));
            return Placeholder(texture, JominiSpritePlaceholder.UnsupportedFormat, format) with
            {
                TextureFullPath = fullPath,
                TextureWidth = header.Width,
                TextureHeight = header.Height,
                Source = new LayoutRect(0, 0, header.Width, header.Height),
            };
        }

        var source = FrameSource(element, kind, header.Width, header.Height, notes);
        var density = Number(element.Property("texture_density")) is { } d && d > 0 ? d : 1;

        var border = ReadBorder(element, notes);
        var spriteKind = ReadKind(element, border, notes);
        var fit = ReadFit(element, kind, spriteKind, background, notes);
        var (mirrorHorizontal, mirrorVertical) = ReadMirror(element);
        var (tint, opacity) = ReadColor(element, notes);

        if (!JominiSpriteRules.Measured)
        {
            // Nur wenn die Regeln (wieder) als ungemessen gelten: Rand und Kacheln nennen. fittype und Farbe über 1 melden sich selbst (ReadFit, Parse).
            var cornered = spriteKind is JominiSpriteKind.CorneredStretched or JominiSpriteKind.CorneredTiled;
            if (cornered && (border.Left > 0 || border.Top > 0 || border.Right > 0 || border.Bottom > 0))
            {
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("spriteborder"));
            }

            if (spriteKind is JominiSpriteKind.CorneredTiled or JominiSpriteKind.Tiled)
            {
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("tiling"));
            }
        }

        if (spriteKind == JominiSpriteKind.CorneredTiled)
        {
            // Kanten und Mitte von corneredtiled mit Rand: Die Kachelperiode (Quelle ÷ Dichte) ist von 13d (ohne Rand) übertragen; 13c zeigt sie
            // nicht (die Mitte der Probe ist einfarbig). Daher bleibt der Hinweis auch nach der Messung.
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("tiling"));
        }

        return new JominiSpriteVisual(
            texture, fullPath, header.Width, header.Height, source, density, spriteKind, border, fit,
            mirrorHorizontal, mirrorVertical, tint, opacity, JominiSpritePlaceholder.None, null);
    }

    private static JominiSpriteVisual Placeholder(string texture, JominiSpritePlaceholder placeholder, string label) =>
        new(texture, null, 0, 0, new LayoutRect(0, 0, 0, 0), 1, JominiSpriteKind.Stretched, default, JominiFit.Fill, false, false,
            new JominiColor(1, 1, 1, 1), 1, placeholder, label);

    /// <summary>
    /// Der Bildausschnitt in Texturpixeln. Bilder zählen zeilenweise von 1 (13y: <c>frame = 3</c> bei 2 × 2 Bildern ist links unten): Spalten =
    /// max(1, ⌊W/w⌋), Zeilen = max(1, ⌊H/h⌋). Ein Bild ist ganze Pixel groß (<see cref="JominiSpriteRules.WholeFramePixels"/>, 14d). 0 und fehlend heißen 1;
    /// <c>upframe</c> siehe <see cref="ReadFrame"/>.
    /// </summary>
    private static LayoutRect FrameSource(ResolvedElement element, string kind, int width, int height, List<string> notes)
    {
        if (JominiFrameGrid.For(width, height, element.Property("framesize"), element.Property("frame_grid"), notes) is not { } layout)
        {
            // Bindung oder Prozent: nicht auswertbar, die ganze Textur.
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("framesize"));
            return new LayoutRect(0, 0, width, height);
        }

        var count = layout.Count;
        var frame = ReadFrame(element, kind, notes);
        if (frame > count)
        {
            AddOnce(notes, JominiNoteKinds.Tag(GuiTexts.LayoutFrameOutOfRange((int)Math.Min(frame, int.MaxValue), (int)Math.Min(count, int.MaxValue)), GuiFindingKind.FrameOutOfRange));
            if (!JominiSpriteRules.FrameBeyondCountIsLast)
            {
                // Annahme "kein Bild": leerer Ausschnitt, der Zeichner zeichnet nichts.
                return new LayoutRect(0, 0, 0, 0);
            }

            frame = count;
        }

        return layout.SourceOf(frame);
    }

    /// <summary>
    /// Die Bildnummer ab 1: <c>frame</c> (auch in Anführungszeichen). Eine Bindung gibt 1 mit Hinweis. <c>upframe</c> zeigt ein Button ohne <c>frame</c> nicht
    /// (13z, alle drei Spiele: Bild 1; 14a: <c>frame</c> gilt). Gemessen ist das an Buttons ohne <c>gfxtype</c> bzw. mit <c>buttongfx</c> (der CK-III-Typ
    /// <c>button</c>); bei einem anderen <c>gfxtype</c> (z. B. <c>framedbuttongfx</c>) bleibt <c>upframe</c> die Annahme für den Grundzustand, mit Hinweis.
    /// </summary>
    private static long ReadFrame(ResolvedElement element, string kind, List<string> notes)
    {
        var property = element.Property("frame");
        if (property is null && kind.StartsWith("button", StringComparison.Ordinal) && element.Property("upframe") is { } upframe
            && Unquote(element.Property("gfxtype")?.Value) is { Length: > 0 } gfxType
            && !string.Equals(gfxType, "buttongfx", StringComparison.OrdinalIgnoreCase))
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("upframe"));
            property = upframe;
        }

        if (property is null)
        {
            return 1;
        }

        if (JominiFrameGrid.FrameNumber(property) is not { } number)
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("frame"));
            return 1;
        }

        return number;
    }

    /// <summary><c>spriteborder = { x y }</c>, dann die Einzelwerte <c>spriteborder_left/right/top/bottom</c>. Negativ und unlesbar zählt als 0 (mit Hinweis).</summary>
    /// <remarks>
    /// <paramref name="liveName"/>/<paramref name="liveValues"/>: eine Zeile, deren Werte statt der Eigenschaft gelten (im Eigenschaftenfeld getippt, noch nicht
    /// übernommen); die Vorrangregel (Einzelwerte vor dem Paar) bleibt.
    /// </remarks>
    internal static JominiBorder ReadBorder(ResolvedElement element, List<string> notes, string? liveName = null, IReadOnlyList<string>? liveValues = null)
    {
        IReadOnlyList<string>? ValuesOf(string name) => name == liveName
            ? liveValues ?? Array.Empty<string>()
            : element.Property(name) is { } property ? (name == "spriteborder" ? property.Items : new[] { property.Value }) : null;

        double left = 0, top = 0, right = 0, bottom = 0;
        if (ValuesOf("spriteborder") is { } both)
        {
            if (both.Count >= 2)
            {
                left = right = Side(both[0], notes);
                top = bottom = Side(both[1], notes);
            }
            else
            {
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("spriteborder"));
            }
        }

        left = Side(ValuesOf("spriteborder_left"), left, notes);
        right = Side(ValuesOf("spriteborder_right"), right, notes);
        top = Side(ValuesOf("spriteborder_top"), top, notes);
        bottom = Side(ValuesOf("spriteborder_bottom"), bottom, notes);
        return new JominiBorder(left, top, right, bottom);
    }

    private static double Side(IReadOnlyList<string>? values, double fallback, List<string> notes) =>
        values is null ? fallback : Side(values.Count > 0 ? values[0] : string.Empty, notes);

    private static double Side(string text, List<string> notes)
    {
        if (Number(text) is { } value && value >= 0)
        {
            return value;
        }

        AddOnce(notes, GuiTexts.LayoutTextureApproximated("spriteborder"));
        return 0;
    }

    /// <summary>
    /// <c>spriteType</c> ohne Anführungszeichen und Groß-/Kleinschreibung. Ohne Angabe gilt bei einem Rand <see cref="JominiSpriteRules.BorderWithoutTypeSlices"/>.
    /// <c>corneredtiled</c> ohne Rand kachelt die ganze Fläche. Unbekannte Werte strecken (mit Hinweis).
    /// </summary>
    private static JominiSpriteKind ReadKind(ResolvedElement element, JominiBorder border, List<string> notes)
    {
        var hasBorder = border.Left > 0 || border.Top > 0 || border.Right > 0 || border.Bottom > 0;
        var type = Unquote(element.Property("spriteType")?.Value ?? element.Property("spritetype")?.Value);
        if (string.IsNullOrEmpty(type))
        {
            return hasBorder && JominiSpriteRules.BorderWithoutTypeSlices ? JominiSpriteKind.CorneredStretched : JominiSpriteKind.Stretched;
        }

        switch (type.ToLowerInvariant())
        {
            case "corneredstretched":
                return JominiSpriteKind.CorneredStretched;
            case "corneredtiled":
                return hasBorder ? JominiSpriteKind.CorneredTiled : JominiSpriteKind.Tiled;
            case "stretched":
                return JominiSpriteKind.Stretched;
            default:
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("spritetype"));
                return JominiSpriteKind.Stretched;
        }
    }

    /// <summary>
    /// <c>fittype</c>: An einem <c>icon</c> wirkt es nicht, das Bild füllt den Kasten (13l–13r, alle drei Spiele, jeder Wert gleich <c>fill</c>). Vanilla setzt
    /// es fast nur an <c>background</c>; dort ist es nicht gemessen und bleibt die Annahme mit Hinweis (nur bei gestreckten Sprites). An anderen Arten wird es
    /// ignoriert, mit Hinweis (nicht gemessen); ebenso unbekannte Werte.
    /// </summary>
    private static JominiFit ReadFit(ResolvedElement element, string widgetKind, JominiSpriteKind kind, bool background, List<string> notes)
    {
        if (element.Property("fittype") is not { } property)
        {
            return JominiFit.Fill;
        }

        if (!background)
        {
            if (widgetKind != "icon")
            {
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("fittype"));
            }

            return JominiFit.Fill;
        }

        // background: angenommene Einpassung, nicht gemessen.
        AddOnce(notes, GuiTexts.LayoutTextureApproximated("fittype"));
        if (kind != JominiSpriteKind.Stretched)
        {
            return JominiFit.Fill;
        }

        switch (Unquote(property.Value).ToLowerInvariant())
        {
            case "fill":
                return JominiFit.Fill;
            case "centercrop":
                return JominiFit.CenterCrop;
            case "center":
                return JominiFit.Center;
            case "start":
                return JominiFit.Start;
            case "end":
                return JominiFit.End;
            default:
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("fittype"));
                return JominiFit.Fill;
        }
    }

    private static (bool Horizontal, bool Vertical) ReadMirror(ResolvedElement element)
    {
        var parts = Unquote(element.Property("mirror")?.Value).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (parts.Contains("horizontal", StringComparer.OrdinalIgnoreCase), parts.Contains("vertical", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Farbe = <c>color</c> × <c>tintcolor</c> je Komponente (fehlend = weiß; 13v: <c>tintcolor</c> färbt wie <c>color</c>); Alpha steckt in der Deckkraft:
    /// <c>alpha</c> × Alpha von <c>color</c> × Alpha von <c>tintcolor</c>, auf 0–1 begrenzt (13u, 13w). Ein <c>alpha</c>, das keine Zahl ist, gilt als 1 (mit
    /// Hinweis). Die Vererbung von <c>alpha</c> (<see cref="JominiSpriteRules.AlphaInherited"/>, 13x) liegt nicht im Element; die Engine multipliziert die
    /// Vorfahren nach dem Legen hinein.
    /// </summary>
    private static (JominiColor Tint, double Opacity) ReadColor(ResolvedElement element, List<string> notes)
    {
        var color = JominiTextureColor.Parse(element.Property("color"), notes) ?? new JominiColor(1, 1, 1, 1);
        var tint = JominiTextureColor.Parse(element.Property("tintcolor"), notes) ?? new JominiColor(1, 1, 1, 1);
        var alpha = 1.0;
        if (OwnAlpha(element) is { } value)
        {
            alpha = value;
        }
        else
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("alpha"));
        }

        var opacity = Math.Clamp(alpha * color.A * tint.A, 0, 1);
        return (new JominiColor(color.R * tint.R, color.G * tint.G, color.B * tint.B, 1), opacity);
    }

    /// <summary>
    /// Das eigene <c>alpha</c> des Elements, ungekappt: fehlend = 1, keine Zahl (Bindung, Prozent, Name) = <see langword="null"/> (der Aufrufer nimmt 1 und
    /// nennt es). Dieselbe Lesart für die Deckkraft des Sprites und die Vererbung an die Nachkommen.
    /// </summary>
    internal static double? OwnAlpha(ResolvedElement element) => JominiShowState.Property(element, "alpha") is { } property ? Number(property) : 1;

    private static string Unquote(string? text) => text?.Trim().Trim('"').Trim() ?? string.Empty;

    /// <summary>
    /// Bildgröße in ganzen Pixeln (abgerundet, mindestens 1), wenn <see cref="JominiSpriteRules.WholeFramePixels"/> gilt. Ab einem Bruchteil von 0,5
    /// unterscheiden sich Abrunden und Runden; gemessen ist nur 33,33 (14d), daher dort der Hinweis "frame size".
    /// </summary>
    internal static double WholePixels(double value, List<string> notes) => JominiFrameGrid.WholePixels(value, notes);

    private static double? Number(ResolvedProperty? property) => property is null ? null : Number(property.Value);

    /// <summary>Eine endliche Zahl; Bindungen (<c>[…]</c>), Prozente und Namen sind keine.</summary>
    private static double? Number(string? text) =>
        double.TryParse(Unquote(text), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private static void AddOnce(List<string> notes, string note)
    {
        if (!notes.Contains(note))
        {
            notes.Add(note);
        }
    }
}
