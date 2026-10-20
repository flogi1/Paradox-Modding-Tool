using System.Globalization;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>
/// Texte der Jomini-Vorschau für die Layout-Engine: Inhalt aus <c>raw_text</c> bzw. lokalisiertem <c>text</c>, Auszeichnung, Spielschriften, Formate und
/// Texticons. Schriften, Lokalisierung und Formate laden träge beim ersten Gebrauch (oder vorab mit <see cref="Warm"/>); ein Fehler beim Laden bleibt nicht
/// hängen, der nächste Zugriff versucht es neu. Von jedem Thread benutzbar.
/// </summary>
public sealed class JominiTextService
{
    /// <summary>Schrift, die ein Textfeld ohne <c>font</c> bekommt (wenn das Spiel sie definiert).</summary>
    private const string StandardFont = "StandardGameFont";

    /// <summary>Annahme ohne <c>fontsize</c> (mit Hinweis); die Vanilla-Typen setzen sie immer.</summary>
    private const double DefaultFontSize = 15;

    private readonly OnDemand<JominiFontLibrary> _fonts;
    private readonly JominiLocalisation _localisation;
    private readonly OnDemand<JominiTextFormats> _formats;
    private readonly OnDemand<IReadOnlyList<string>> _typeFonts;
    private readonly Func<string, string?> _locateTexture;
    private readonly Func<string, (int Width, int Height)?> _textureSize;
    private readonly GuiRules _rules;

    private JominiTextService(
        OnDemand<JominiFontLibrary> fonts,
        JominiLocalisation localisation,
        GuiIndex index,
        GuiRules rules,
        Func<string, string?> locateTexture,
        Func<string, (int Width, int Height)?> textureSize)
        : this(
            fonts,
            localisation,
            new OnDemand<JominiTextFormats>(() => JominiTextFormats.From(index)),
            new OnDemand<IReadOnlyList<string>>(() => TypeFonts(index, rules)),
            rules,
            locateTexture,
            textureSize)
    {
    }

    private JominiTextService(
        OnDemand<JominiFontLibrary> fonts,
        JominiLocalisation localisation,
        OnDemand<JominiTextFormats> formats,
        OnDemand<IReadOnlyList<string>> typeFonts,
        GuiRules rules,
        Func<string, string?> locateTexture,
        Func<string, (int Width, int Height)?> textureSize)
    {
        _fonts = fonts;
        _localisation = localisation;
        _rules = rules;
        _formats = formats;
        _typeFonts = typeFonts;
        _locateTexture = locateTexture;
        _textureSize = textureSize;
    }

    /// <summary>
    /// Schriften aus den Suchordnern der Texturen (<see cref="GuiTextureLocator.Roots"/>), Icon-Texturen über <see cref="GuiTextureLocator.Locate"/>,
    /// Lokalisierung aus Installation, Abhängigkeiten und Mod, Formate aus <paramref name="index"/>. Größen von Icon-Texturen ohne <c>size</c> aus dem
    /// Dateikopf (<see cref="GuiTextureLocator.SizeOf"/>: DDS, PNG, TGA, JPG). Liest hier noch nichts.
    /// </summary>
    public static JominiTextService Build(GuiWorkspaceOptions options, GuiIndex index, GuiTextureLocator textures)
    {
        var roots = textures.Roots;
        return new JominiTextService(
            new OnDemand<JominiFontLibrary>(() => JominiFontLibrary.Load(roots)),
            JominiLocalisation.Load(options.InstallDirectory, options.DependencyRoots, options.ModRoot, options.Rules.Layout.Contexts),
            index,
            options.Rules,
            textures.Locate,
            textures.SizeOf);
    }

    /// <summary>Neue Formate aus <paramref name="index"/> (z.B. nach dem Speichern einer .gui-Datei); Schriften und Lokalisierung bleiben.</summary>
    public JominiTextService WithIndex(GuiIndex index) => new(_fonts, _localisation, index, _rules, _locateTexture, _textureSize);

    /// <summary>
    /// Neue, träge Lokalisierung mit denselben Wurzeln (z.B. nach dem Speichern einer .yml-Datei); Schriften und Formate bleiben. Die nächste Vorschau
    /// liest die Texte neu.
    /// </summary>
    public JominiTextService WithFreshLocalisation() =>
        new(_fonts, _localisation.Fresh(), _formats, _typeFonts, _rules, _locateTexture, _textureSize);

    /// <summary>Sprachen der Installation in kanonischer Form, <c>l_english</c> zuerst.</summary>
    public IReadOnlyList<string> Languages => _localisation.Languages;

    /// <summary>
    /// Schriften, die <see cref="Warm"/> vorlädt: die der Vanilla-Typen <c>textbox</c> und <c>text_single</c> (aufgelöst samt Vorlagen; Victoria 3:
    /// EBGaramond über regular_font) und <c>StandardGameFont</c>, soweit definiert; findet sich keine, alle definierten Schriften.
    /// </summary>
    internal IReadOnlyList<string> WarmFonts
    {
        get
        {
            var fonts = _fonts.Value;
            var names = _typeFonts.Value.Append(StandardFont).Where(fonts.Defines).Distinct(StringComparer.Ordinal).ToList();
            return names.Count > 0 ? names : fonts.FontNames.ToList();
        }
    }

    /// <summary>
    /// Lädt vorab Schriften (die erste Datei des regulären Schnitts je <see cref="WarmFonts"/>; Rückfalldateien bleiben träge), Formate,
    /// <paramref name="language"/> und Englisch, damit die erste Vorschau nicht wartet. Wirft nie: scheitert etwas, lädt der erste Gebrauch erneut.
    /// </summary>
    public void Warm(string language)
    {
        try
        {
            var fonts = _fonts.Value;
            foreach (var font in WarmFonts)
            {
                fonts.Face(font, JominiFontStyle.Regular, language);
            }

            _ = _formats.Value;
            _localisation.Text(string.Empty, language);
            _localisation.Text(string.Empty, JominiLocalisation.English);
        }
        catch (Exception)
        {
            // Nur das Vorladen scheitert; die Vorschau lädt beim ersten Text erneut.
        }
    }

    /// <summary>
    /// Größe des gesetzten Texts (breiteste Zeile × Texthöhe). <paramref name="boxWidth"/> ist die Breite des Kastens, falls schon bekannt (Kürzen, Umbruch
    /// ohne <c>max_width</c>); bei autoresize <see langword="null"/>. Eine unbekannte Schrift ergibt 0×0. <paramref name="scale"/>: eigenes <c>scale</c> des
    /// Textfelds (siehe <see cref="Layout"/>); die Größe ist in unskalierten Einheiten.
    /// </summary>
    internal (double W, double H) Measure(ResolvedElement text, double? boxWidth, string language, List<string> notes, double scale = 1)
    {
        var visual = Layout(text, boxWidth, language, notes, scale: scale);
        return (visual.Width, visual.Height);
    }

    /// <summary>
    /// Der gesetzte Text für den Kasten der Breite <paramref name="boxWidth"/> (≤ 0: kein Kasten, der Text läuft frei hinaus). <paramref name="labelOnly"/>:
    /// Beschriftung eines Elements, dessen Größe nicht am Text hängt (Button); Annahmen zu Schriftgröße, Schrift und fontcolor bleiben dann ohne Hinweis,
    /// Platzhalter, fehlende Schlüssel und unbekannte Schriften nicht. <paramref name="scale"/>: eigenes <c>scale</c> des Elements (siehe <see cref="Layout"/>).
    /// </summary>
    internal JominiTextVisual Visual(ResolvedElement text, double boxWidth, string language, List<string> notes, bool labelOnly = false, double scale = 1) =>
        Layout(text, boxWidth > 0 ? boxWidth : null, language, notes, labelOnly, scale);

    /// <summary>
    /// Setzt den Text. Mit eigenem <c>scale</c> s ≠ 1 (Lauf 6, 15c, alle drei Spiele): gesetzt wird in der ganzzahligen Schriftgröße floor(fontsize × s) mit
    /// Kasten- und Umbruchbreiten × s; das Ergebnis wird durch s geteilt, so dass der Text in unskalierten Einheiten vorliegt (Größe des Textfelds und Fach in
    /// einer hbox) und der Zeichner ihn mit <see cref="ParadoxModTool.GuiDesign.Layout.LayoutBox.RenderScale"/> wieder in der gesetzten Größe zeichnet. Gemessen nur bei 15 × 0,5 = 7.
    /// </summary>
    private JominiTextVisual Layout(ResolvedElement element, double? boxWidth, string language, List<string> notes, bool labelOnly = false, double scale = 1)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var fonts = _fonts.Value;
        var (alignH, alignV) = Align(element.Property("align")?.Value);

        // Schrift: der zuletzt gesetzte Wert gilt (kein Rückgriff auf ältere Werte aus Typ oder Vorlage). Unbekannt (Lauf 4, Victoria 3): das Spiel zeigt
        // nichts. Ohne font die Standardschrift des Spiels; fehlt auch sie, setzt der Textsatz Segoe UI mit Hinweis ein.
        var font = element.Property("font")?.Value;
        if (string.IsNullOrWhiteSpace(font))
        {
            font = fonts.Defines(StandardFont) ? StandardFont : null;
        }
        else if (!fonts.Defines(font) && !fonts.IsEmpty)
        {
            notes.Add(GuiTexts.LayoutTextFontUnknown(font));
            return new JominiTextVisual(Array.Empty<JominiTextLine>(), 0, 0, alignH, alignV, false, false, Array.Empty<string>(), Array.Empty<string>());
        }

        var fontSize = Number(element, "fontsize");
        if (fontSize is not > 0)
        {
            fontSize = DefaultFontSize;
            if (!labelOnly)
            {
                notes.Add(GuiTexts.LayoutTextApproximated("fontsize"));
            }
        }

        var formats = _formats.Value;
        var defaultFormat = element.Property("default_format")?.Value;
        var fontColor = element.Property("fontcolor");
        var color = Color(fontColor);
        // Unter eigenem scale wird alles in der gesetzten Größe gesetzt und danach durch s geteilt: die Grundgröße und feste Formatgrößen (size:N, auch aus
        // default_format) als floor(N × s), Texticons fester Größe (ohne iconsize.fontsize) und alle Icon-Versätze × s (JominiTextLayout). Gemessen ist nur
        // die Grundgröße (15c); der Rest ist angenommen (Hinweis "text" an der Engine).
        var style = new JominiTextStyle(font, JominiFontStyle.Regular, Scaled(fontSize.Value, s), color ?? JominiColor.White, false, false, false);
        if (fontColor is not null && color is null && !labelOnly && !FormatSetsColor(formats, style, defaultFormat))
        {
            // fontcolor als Datenbindung (EU V: "[GetDebugFontColor]"): weiß angenommen; schlägt default_format die Farbe ohnehin, ohne Hinweis.
            notes.Add(GuiTexts.LayoutTextApproximated("fontcolor"));
        }

        var tokens = JominiTextMarkup.Parse(Content(element, language, notes), key => _localisation.TextOrEnglish(key, language));

        // Umbruch nur mit multiline: an max_width; ohne max_width an der Breite eines festen Kastens (nicht gemessen, mit Hinweis).
        var multiline = element.Property("multiline")?.Value == "yes";
        var maxWidth = multiline && Number(element, "max_width") is { } max && max > 0 ? max : (double?)null;
        if (multiline && maxWidth is null && boxWidth is { } box && element.Property("autoresize")?.Value != "yes")
        {
            maxWidth = box;
            notes.Add(GuiTexts.LayoutTextApproximated("multiline"));
        }

        var request = new JominiTextRequest(
            tokens,
            style,
            defaultFormat,
            multiline,
            ScaledWidth(maxWidth, s),
            ScaledWidth(boxWidth, s),
            element.Property("elide")?.Value == "right",
            Number(element, "fontsize_min") is { } minimum ? Scaled(minimum, s) : null,
            language,
            alignH,
            alignV,
            NoteMissingFonts: !labelOnly,
            Scale: s);
        var visual = JominiTextLayout.Layout(request, fonts, formats, _locateTexture, notes, _textureSize);
        if (visual.HasPlaceholder)
        {
            notes.Add(GuiTexts.LayoutTextPlaceholder);
        }

        return s == 1 ? visual : Unscaled(visual, s);
    }

    /// <summary>Schriftgröße unter eigenem <c>scale</c> (<see cref="JominiTextFormats.ScaledSize"/>).</summary>
    private static double Scaled(double fontSize, double scale) => JominiTextFormats.ScaledSize(fontSize, scale);

    /// <summary>
    /// Kasten- oder Umbruchbreite unter eigenem <c>scale</c>. Die Breite eines autoresize-Felds ist gesetzte Breite / s; mal s kann sie eine Rundungsstelle unter der
    /// gesetzten Breite landen und das Kürzen (elide) fälschlich auslösen. Ein Spielraum weit unter einem Pixel verhindert das (Vorschübe sind ganze Pixel).
    /// </summary>
    private static double? ScaledWidth(double? width, double scale) => scale == 1 ? width : width * scale + 1e-6;

    /// <summary>Der in der skalierten Größe gesetzte Text in unskalierten Einheiten: alle Längen und Schriftgrößen durch <paramref name="scale"/>.</summary>
    private static JominiTextVisual Unscaled(JominiTextVisual visual, double scale) => visual with
    {
        Lines = visual.Lines.Select(line => new JominiTextLine(
            line.Runs.Select(run => run with
            {
                FontSize = run.FontSize / scale,
                Advances = run.Advances.Select(advance => advance / scale).ToArray(),
                Width = run.Width / scale,
                IconHeight = run.IconHeight / scale,
                IconOffsetY = run.IconOffsetY / scale,
            }).ToArray(),
            line.Width / scale,
            line.Height / scale,
            line.Baseline / scale)).ToArray(),
        Width = visual.Width / scale,
        Height = visual.Height / scale,
    };

    /// <summary>
    /// Inhalt: <c>raw_text</c> wie geschrieben, sonst <c>text</c> über die Lokalisierung (Sprache, dann Englisch). Ein nicht gefundener Wert wird selbst
    /// gezeigt; sieht er wie ein Schlüssel aus (ein Wort ohne Klammern), mit Hinweis.
    /// </summary>
    private string Content(ResolvedElement element, string language, List<string> notes)
    {
        if (element.Property("raw_text") is { } raw)
        {
            return raw.Value;
        }

        if (element.Property("text")?.Value is not { Length: > 0 } key)
        {
            return string.Empty;
        }

        if (_localisation.TextOrEnglish(key, language) is { } localised)
        {
            return localised;
        }

        // Mit Leerraum, Klammern oder $ ist es Text oder Auszeichnung, kein Schlüssel.
        if (!key.Any(c => char.IsWhiteSpace(c) || c is '[' or ']' or '(' or ')' or '$'))
        {
            notes.Add(GuiTexts.LayoutTextKeyMissing(key));
        }

        return key;
    }

    /// <summary>
    /// <c>align</c>-Flags: left/right/hcenter/center waagerecht, top/bottom/vcenter/center senkrecht, <c>nobaseline</c> ohne Wirkung. Fehlend: links und
    /// senkrecht mittig (Lauf 4, alle drei Spiele).
    /// </summary>
    private static (JominiTextAlignH H, JominiTextAlignV V) Align(string? value)
    {
        var horizontal = JominiTextAlignH.Left;
        var vertical = JominiTextAlignV.Center;
        foreach (var flag in (value ?? string.Empty).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (flag.ToLowerInvariant())
            {
                case "left":
                    horizontal = JominiTextAlignH.Left;
                    break;
                case "right":
                    horizontal = JominiTextAlignH.Right;
                    break;
                case "hcenter":
                    horizontal = JominiTextAlignH.Center;
                    break;
                case "top":
                    vertical = JominiTextAlignV.Top;
                    break;
                case "bottom":
                    vertical = JominiTextAlignV.Bottom;
                    break;
                case "vcenter":
                    vertical = JominiTextAlignV.Center;
                    break;
                case "center":
                    horizontal = JominiTextAlignH.Center;
                    vertical = JominiTextAlignV.Center;
                    break;
            }
        }

        return (horizontal, vertical);
    }

    /// <summary>
    /// <c>fontcolor = { r g b a }</c>; ist eine Komponente über 1, gilt das ganze Tupel als 0–255. Nicht auswertbar (z.B. eine Datenbindung) = null.
    /// </summary>
    private static JominiColor? Color(ResolvedProperty? property)
    {
        if (property is null || property.Items.Count < 3)
        {
            return null;
        }

        var numbers = new double[property.Items.Count];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (!double.TryParse(property.Items[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        var scale = numbers.Any(n => n > 1) ? 255.0 : 1.0;
        return new JominiColor(numbers[0] / scale, numbers[1] / scale, numbers[2] / scale, numbers.Length > 3 ? numbers[3] / scale : 1);
    }

    /// <summary>Setzt <c>default_format</c> eine eigene Farbe (dann ist <c>fontcolor</c> ohne Wirkung)?</summary>
    private static bool FormatSetsColor(JominiTextFormats formats, JominiTextStyle style, string? defaultFormat)
    {
        var spec = defaultFormat?.Trim().TrimStart('#');
        if (string.IsNullOrEmpty(spec))
        {
            return false;
        }

        // Zwei verschiedene Ausgangsfarben: bleibt die Farbe nicht gleich der Ausgangsfarbe, setzt das Format sie.
        var black = style with { Color = new JominiColor(0, 0, 0, 1) };
        var white = style with { Color = JominiColor.White };
        return formats.Apply(black, spec).Color == formats.Apply(white, spec).Color;
    }

    /// <summary>Schriften der Vanilla-Typen <c>textbox</c> und <c>text_single</c>, aufgelöst wie ein Element ohne eigene Angaben.</summary>
    private static IReadOnlyList<string> TypeFonts(GuiIndex index, GuiRules rules)
    {
        var document = GuiParser.Parse("textbox = { } text_single = { }");
        var resolver = new GuiResolver(index, rules, document);
        var fonts = new List<string>();
        foreach (var node in document.Root.Children)
        {
            if (resolver.Resolve(node).Property("font")?.Value is { Length: > 0 } font)
            {
                fonts.Add(font);
            }
        }

        return fonts;
    }

    private static double? Number(ResolvedElement element, string name) =>
        element.Property(name) is { } property && double.TryParse(property.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : null;

    /// <summary>Wert, der beim ersten Zugriff erzeugt wird; anders als <see cref="Lazy{T}"/> merkt er sich keine Ausnahme, der nächste Zugriff versucht es neu.</summary>
    private sealed class OnDemand<T>
        where T : class
    {
        private readonly Func<T> _create;
        private readonly object _gate = new();
        private T? _value;

        public OnDemand(Func<T> create) => _create = create;

        public T Value
        {
            get
            {
                if (Volatile.Read(ref _value) is { } value)
                {
                    return value;
                }

                lock (_gate)
                {
                    if (_value is null)
                    {
                        Volatile.Write(ref _value, _create());
                    }

                    return _value!;
                }
            }
        }
    }
}
