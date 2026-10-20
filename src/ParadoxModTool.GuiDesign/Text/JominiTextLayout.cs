using System.Text;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>
/// Was ein Jomini-Textelement zum Setzen braucht. <paramref name="DefaultFormat"/> kommt wie im Spiel mit "#" (<c>"#medium"</c>).
/// <paramref name="MaxWidth"/> zählt nur mit <paramref name="Multiline"/> (Umbruch); <paramref name="BoxWidth"/> nur einzeilig mit
/// <paramref name="ElideRight"/> (Kürzen, vorher <paramref name="FontSizeMin"/>). <paramref name="NoteMissingFonts"/> = false: fehlende Schriften stehen
/// nur in <see cref="JominiTextVisual.MissingFonts"/>, ohne Hinweis (Beschriftungen, deren Größe nicht am Text hängt). <paramref name="Scale"/>: eigenes
/// <c>scale</c> des Elements; feste Formatgrößen werden dann in floor(N × Scale), Texticons fester Größe und Icon-Versätze × Scale gesetzt (die Grundgröße
/// in <paramref name="BaseStyle"/> ist schon gesetzt).
/// </summary>
public sealed record JominiTextRequest(
    IReadOnlyList<JominiTextToken> Tokens,
    JominiTextStyle BaseStyle,
    string? DefaultFormat,
    bool Multiline,
    double? MaxWidth,
    double? BoxWidth,
    bool ElideRight,
    double? FontSizeMin,
    string Language,
    JominiTextAlignH AlignH,
    JominiTextAlignV AlignV,
    bool NoteMissingFonts = true,
    double Scale = 1);

/// <summary>
/// Setzt Jomini-Textbausteine in gemessene Zeilen: Breiten aus gerundeten Glyphenvorschüben (ohne Unterschneidung), Umbruch an Leerzeichen, Kürzen
/// rechts mit " ..." und Zeilenhöhen aus <see cref="JominiTextMetrics"/>. Berührt kein WPF-Objekt und läuft auf jedem Thread.
/// </summary>
public static class JominiTextLayout
{
    /// <summary>Name dynamischer Icons (<c>@[…]!</c>) in <see cref="JominiTextVisual.MissingIcons"/> und in der Notiz.</summary>
    internal const string DynamicIconName = "@[…]";

    /// <summary>Name in <see cref="JominiTextVisual.MissingFonts"/>, wenn die Darstellung gar keine Schrift nennt.</summary>
    internal const string NoFontName = "–";

    private const string ElideSuffix = " ...";

    /// <param name="textureSize">Pixelgröße einer Textur (Pfad wie im <c>iconsize</c>-Block) für Icons ohne <c>size</c>; <see langword="null"/>: unbekannt.</param>
    public static JominiTextVisual Layout(
        JominiTextRequest request,
        JominiFontLibrary fonts,
        JominiTextFormats formats,
        Func<string, string?> locateTexture,
        List<string> notes,
        Func<string, (int Width, int Height)?>? textureSize = null)
    {
        var context = new Context(request, fonts, formats, locateTexture, textureSize ?? (_ => null), notes);
        var atoms = context.Atoms(sizeOverride: null);

        var elided = false;
        if (!request.Multiline && request.ElideRight && request.BoxWidth is { } box && atoms.Count > 0 && Sum(atoms) > box)
        {
            // fontsize_min (gemessen in allen drei Spielen): passt der Text nicht, wird er gleich mit der Mindestgröße gesetzt, ohne Zwischenstufen.
            // Die Schnittstelle danach weicht je Spiel um ±1 Zeichen ab, deshalb die Notiz. Nur endliche, positive Werte (fontsize_min = -5 ist
            // schlechte Mod-Datei, keine Größe).
            if (request.FontSizeMin is { } min && double.IsFinite(min) && min > 0 && min < context.BaseStyle.Size)
            {
                atoms = context.Atoms(sizeOverride: min);
                notes.Add(GuiTexts.LayoutTextApproximated("fontsize_min"));
            }

            if (Sum(atoms) > box)
            {
                atoms = Elide(atoms, box, context);
                elided = true;
            }
        }

        var lineAtoms = Lines(atoms, request);

        // Höhe (Lauf 4): größte Schrift im Text, Metriken der ersten Schrift; H1 + (n − 1) × Zeilenabstand.
        var content = atoms.Where(a => !a.IsBreak).ToList();
        var face = content.Count > 0 ? content[0].Face : context.FaceFor(context.BaseStyle).Face;
        var size = content.Count > 0 ? content.Max(a => a.Style.Size) : context.BaseStyle.Size;
        var singleHeight = JominiTextMetrics.LineHeight(face, size);
        var pitch = JominiTextMetrics.LinePitch(face, size);
        var baseline = JominiTextMetrics.AscentPixels(face, size);

        var lines = new List<JominiTextLine>(lineAtoms.Count);
        for (var i = 0; i < lineAtoms.Count; i++)
        {
            var runs = Runs(lineAtoms[i]);
            lines.Add(new JominiTextLine(runs, runs.Sum(r => r.Width), i < lineAtoms.Count - 1 ? pitch : singleHeight, baseline));
        }

        return new JominiTextVisual(
            lines,
            lines.Count == 0 ? 0 : lines.Max(l => l.Width),
            lines.Sum(l => l.Height),
            request.AlignH,
            request.AlignV,
            elided,
            context.HasPlaceholder,
            context.MissingFonts,
            context.MissingIcons);
    }

    private static double Sum(IReadOnlyList<Atom> atoms) => atoms.Sum(a => a.Advance);

    /// <summary>
    /// Kürzen (Probe 7h): der längste Anfang <c>p</c>, für den <c>Breite(p.TrimEnd() + " ...") ≤ Kastenbreite</c> gilt; das Kürzel bekommt die Darstellung
    /// des letzten behaltenen Zeichens. Passt nicht einmal das Kürzel, bleibt nur es.
    /// </summary>
    private static List<Atom> Elide(List<Atom> atoms, double box, Context context)
    {
        var prefix = new double[atoms.Count + 1];
        for (var i = 0; i < atoms.Count; i++)
        {
            prefix[i + 1] = prefix[i] + atoms[i].Advance;
        }

        var suffixes = new Dictionary<JominiTextStyle, List<Atom>>();
        List<Atom> Suffix(JominiTextStyle style)
        {
            if (!suffixes.TryGetValue(style, out var suffix))
            {
                suffix = new List<Atom>();
                foreach (var rune in ElideSuffix.EnumerateRunes())
                {
                    suffix.Add(context.Char(rune, style, JominiTextRunKind.Text, group: 0));
                }

                suffixes[style] = suffix;
            }

            return suffix;
        }

        for (var length = atoms.Count; length >= 0; length--)
        {
            var kept = length;
            while (kept > 0 && atoms[kept - 1].IsSpace)
            {
                kept--;
            }

            // Ohne behaltenes Zeichen trägt das Kürzel die Darstellung des ersten Zeichens, bei leerem Text die Grunddarstellung.
            var suffix = Suffix(kept > 0 ? atoms[kept - 1].Style : atoms.Count > 0 ? atoms[0].Style : context.BaseStyle);
            if (kept == 0 || prefix[kept] + Sum(suffix) <= box)
            {
                var result = atoms.Take(kept).ToList();
                result.AddRange(suffix);
                return result;
            }
        }

        // Nicht erreichbar: bei length 0 greift kept == 0.
        return atoms;
    }

    /// <summary>Teilt in Zeilen: harte Umbrüche (nur mit multiline vorhanden) und mit max_width der Umbruch an Leerzeichen.</summary>
    private static List<List<Atom>> Lines(List<Atom> atoms, JominiTextRequest request)
    {
        var paragraphs = new List<List<Atom>> { new() };
        foreach (var atom in atoms)
        {
            if (atom.IsBreak)
            {
                paragraphs.Add(new List<Atom>());
            }
            else
            {
                paragraphs[^1].Add(atom);
            }
        }

        if (!request.Multiline || request.MaxWidth is not { } maxWidth)
        {
            return paragraphs;
        }

        var lines = new List<List<Atom>>();
        foreach (var paragraph in paragraphs)
        {
            Wrap(paragraph, maxWidth, lines);
        }

        return lines;
    }

    /// <summary>
    /// Gieriger Umbruch vor dem Wort, das <paramref name="maxWidth"/> überschreiten würde; die Leerzeichen am Umbruch gehören zu keiner Zeile. Ein Wort,
    /// das allein zu breit ist, bricht zwischen den Zeichen. Jede ausgegebene Zeile enthält mindestens ein Nicht-Leerzeichen, so endet die Schleife auch,
    /// wenn schon eine Glyphe breiter als <paramref name="maxWidth"/> ist (dann ein Zeichen je Zeile).
    /// </summary>
    private static void Wrap(List<Atom> paragraph, double maxWidth, List<List<Atom>> lines)
    {
        var line = new List<Atom>();
        double lineWidth = 0;
        var hasContent = false;
        var pending = new List<Atom>();

        void Emit()
        {
            lines.Add(line);
            line = new List<Atom>();
            lineWidth = 0;
            hasContent = false;
        }

        var i = 0;
        while (i < paragraph.Count)
        {
            if (paragraph[i].IsSpace)
            {
                pending.Add(paragraph[i]);
                i++;
                continue;
            }

            var end = i;
            while (end < paragraph.Count && !paragraph[end].IsSpace)
            {
                end++;
            }

            double wordWidth = 0;
            for (var k = i; k < end; k++)
            {
                wordWidth += paragraph[k].Advance;
            }

            var pendingWidth = Sum(pending);
            if (hasContent && lineWidth + pendingWidth + wordWidth > maxWidth)
            {
                Emit();
            }
            else
            {
                // Leerzeichen zwischen Wörtern einer Zeile (und am Absatzanfang) bleiben.
                line.AddRange(pending);
                lineWidth += pendingWidth;
            }

            pending.Clear();

            if (lineWidth + wordWidth <= maxWidth)
            {
                line.AddRange(paragraph.Skip(i).Take(end - i));
                lineWidth += wordWidth;
                hasContent = true;
            }
            else
            {
                // Zu breites Wort: Zeichen für Zeichen, mindestens eins je Zeile.
                for (var k = i; k < end; k++)
                {
                    if (hasContent && lineWidth + paragraph[k].Advance > maxWidth)
                    {
                        Emit();
                    }

                    line.Add(paragraph[k]);
                    lineWidth += paragraph[k].Advance;
                    hasContent = true;
                }
            }

            i = end;
        }

        // Leerzeichen am Absatzende bleiben Teil der letzten Zeile.
        line.AddRange(pending);
        lines.Add(line);
    }

    /// <summary>Fasst aufeinanderfolgende Zeichen gleicher Art, Darstellung und Datei zu Abschnitten zusammen; jedes Icon ist ein eigener Abschnitt.</summary>
    private static List<JominiTextRun> Runs(List<Atom> atoms)
    {
        var runs = new List<JominiTextRun>();
        var start = 0;
        while (start < atoms.Count)
        {
            var first = atoms[start];
            var end = start + 1;
            while (end < atoms.Count && first.Kind != JominiTextRunKind.Icon && SameRun(first, atoms[end]))
            {
                end++;
            }

            var text = new StringBuilder();
            var advances = new List<double>(end - start);
            for (var i = start; i < end; i++)
            {
                text.Append(atoms[i].Text);
                advances.Add(atoms[i].Advance);
            }

            var style = first.Style;
            runs.Add(new JominiTextRun(
                first.Kind,
                text.ToString(),
                first.File,
                style.Size,
                first.SimulateBold,
                first.SimulateItalic,
                style.Color,
                style.Underline,
                style.Glow,
                style.Shadow,
                advances,
                advances.Sum(),
                first.IconTexture,
                first.IconHeight,
                first.IconOffsetY));
            start = end;
        }

        return runs;
    }

    private static bool SameRun(Atom a, Atom b) =>
        a.Kind == b.Kind && a.Group == b.Group && a.Style == b.Style && a.File == b.File && a.SimulateBold == b.SimulateBold && a.SimulateItalic == b.SimulateItalic;

    /// <summary>
    /// Ein Zeichen, Icon oder harter Umbruch mit seiner Darstellung. <c>Group</c> trennt Platzhalter und Icons voneinander (0 = gewöhnlicher Text).
    /// </summary>
    private sealed record Atom(
        JominiTextRunKind Kind,
        string Text,
        JominiTextStyle Style,
        JominiFontFace Face,
        string? File,
        double Advance,
        bool SimulateBold,
        bool SimulateItalic,
        bool IsSpace,
        bool IsBreak,
        int Group,
        string? IconTexture,
        double IconHeight,
        double IconOffsetY);

    /// <summary>Zustand eines Layout-Aufrufs: Schriftschnitte je Darstellung und jede fehlende Schrift bzw. jedes fehlende Icon einmal mit Notiz.</summary>
    private sealed class Context
    {
        private readonly JominiTextRequest _request;
        private readonly JominiFontLibrary _fonts;
        private readonly JominiTextFormats _formats;
        private readonly Func<string, string?> _locateTexture;
        private readonly Func<string, (int Width, int Height)?> _textureSize;
        private readonly List<string> _notes;
        private readonly double _scale;
        private readonly Dictionary<JominiTextStyle, (JominiFontFace Face, bool Bold, bool Italic)> _faces = new();
        private readonly HashSet<string> _missingFonts = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missingIcons = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missingTextures = new(StringComparer.OrdinalIgnoreCase);
        private int _group;
        private bool _lineBreakNoted;
        private bool _textureSizeNoted;

        public Context(
            JominiTextRequest request,
            JominiFontLibrary fonts,
            JominiTextFormats formats,
            Func<string, string?> locateTexture,
            Func<string, (int Width, int Height)?> textureSize,
            List<string> notes)
        {
            _request = request;
            _fonts = fonts;
            _formats = formats;
            _locateTexture = locateTexture;
            _textureSize = textureSize;
            _notes = notes;

            // default_format gilt über der Grunddarstellung und schlägt fontcolor (Probe-Fenster 3, Victoria 3).
            var spec = request.DefaultFormat?.Trim().TrimStart('#');
            _scale = double.IsFinite(request.Scale) && request.Scale > 0 ? request.Scale : 1;
            BaseStyle = string.IsNullOrEmpty(spec) ? request.BaseStyle : formats.Apply(request.BaseStyle, spec, _scale);
        }

        public JominiTextStyle BaseStyle { get; }

        public bool HasPlaceholder { get; private set; }

        public List<string> MissingFonts { get; } = new();

        public List<string> MissingIcons { get; } = new();

        /// <summary>Alle Bausteine als Zeichen; <paramref name="sizeOverride"/> ersetzt die Grundgröße (fontsize_min), Formate mit <c>size</c> gelten weiter.</summary>
        public List<Atom> Atoms(double? sizeOverride)
        {
            var atoms = new List<Atom>();
            var current = sizeOverride is { } size ? BaseStyle with { Size = size } : BaseStyle;
            var stack = new Stack<JominiTextStyle>();
            foreach (var token in _request.Tokens)
            {
                switch (token)
                {
                    case TextToken text:
                        foreach (var rune in text.Text.EnumerateRunes())
                        {
                            atoms.Add(Char(rune, current, JominiTextRunKind.Text, group: 0));
                        }

                        break;
                    case FormatStart format:
                        stack.Push(current);
                        current = _formats.Apply(current, format.Spec, _scale);
                        break;
                    case FormatEnd:
                        if (stack.Count > 0)
                        {
                            current = stack.Pop();
                        }

                        break;
                    case PlaceholderToken placeholder:
                        HasPlaceholder = true;
                        var group = ++_group;
                        var display = placeholder.Concept ? placeholder.Display : "‹" + placeholder.Display + "›";
                        foreach (var rune in display.EnumerateRunes())
                        {
                            atoms.Add(Char(rune, current, JominiTextRunKind.Placeholder, group));
                        }

                        break;
                    case IconToken icon:
                        atoms.Add(Icon(icon.Name, current));
                        break;
                    case LineBreakToken:
                        // Ohne multiline zählt ein Umbruch als Leerzeichen (Annahme, nicht gemessen): einmal je Aufruf eine Notiz.
                        if (!_request.Multiline && !_lineBreakNoted)
                        {
                            _lineBreakNoted = true;
                            _notes.Add(GuiTexts.LayoutTextLineBreakAsSpace);
                        }

                        atoms.Add(_request.Multiline
                            ? new Atom(JominiTextRunKind.Text, "", current, FaceFor(current).Face, null, 0, false, false, IsSpace: false, IsBreak: true, 0, null, 0, 0)
                            : Char(new Rune(' '), current, JominiTextRunKind.Text, group: 0));
                        break;
                }
            }

            return atoms;
        }

        public Atom Char(Rune rune, JominiTextStyle style, JominiTextRunKind kind, int group)
        {
            var (face, bold, italic) = FaceFor(style);
            var advance = face.Advance(rune.Value, style.Size, out var file);
            return new Atom(kind, rune.ToString(), style, face, file.Length == 0 ? null : file, advance, bold, italic,
                IsSpace: rune.Value == ' ', IsBreak: false, group, null, 0, 0);
        }

        /// <summary>
        /// Texticon: Größe aus dem passenden <c>iconsize</c>-Block, mit <c>fontsize</c> auf die Textgröße skaliert und aufgerundet (gemessen: CK III 25 →
        /// 24 bei 15/16). Der y-Versatz folgt der fontsize-Anpassung nicht (Annahme). Unter eigenem <c>scale</c> s werden eine feste Größe (ohne <c>fontsize</c>) und der
        /// y-Versatz × s gesetzt (das spätere Teilen durch s bringt sie zurück). Ein Block ohne <c>size</c> (Victoria 3) nimmt die Pixelgröße seiner Textur, ebenso
        /// skaliert (nicht gemessen, eine Notiz je Aufruf). Fehlende und dynamische Icons, Blöcke ohne auswertbare positive Größe und Blöcke ohne
        /// <c>size</c>, deren Textur fehlt, sind ein Quadrat der Schriftgröße mit Notiz; eine nicht gefundene Textur bekommt die Notiz einmal je Textur.
        /// </summary>
        private Atom Icon(string? name, JominiTextStyle style)
        {
            var face = FaceFor(style).Face;
            var group = ++_group;
            var definition = name is null ? null : _formats.Icon(name, style.Size);
            if (definition is { SizeGiven: false, Texture: { Length: > 0 } sizeTexture })
            {
                if (_textureSize(sizeTexture) is { Width: > 0, Height: > 0 } pixels)
                {
                    definition = definition with { Width = pixels.Width, Height = pixels.Height };
                    if (!_textureSizeNoted)
                    {
                        _textureSizeNoted = true;
                        _notes.Add(GuiTexts.LayoutTextApproximated("texticon"));
                    }
                }
                else if (_locateTexture(sizeTexture) is null && _missingTextures.Add(sizeTexture))
                {
                    _notes.Add(GuiTexts.LayoutTextureMissing(sizeTexture));
                }
            }

            if (definition is null || !(definition.Width > 0) || !(definition.Height > 0))
            {
                var missing = name ?? DynamicIconName;
                if (_missingIcons.Add(missing))
                {
                    MissingIcons.Add(missing);
                    _notes.Add(GuiTexts.LayoutTextIconMissing(missing));
                }

                return new Atom(JominiTextRunKind.Icon, missing, style, face, null, style.Size, false, false, false, false, group, null, style.Size, 0);
            }

            double width = definition.Width * _scale, height = definition.Height * _scale;
            if (definition.FontSize is { } iconFontSize && iconFontSize > 0)
            {
                // Auf die (schon gesetzte) Textgröße skaliert: kein weiteres × s.
                width = JominiTextMetrics.Ceiling(definition.Width * style.Size / iconFontSize);
                height = JominiTextMetrics.Ceiling(definition.Height * style.Size / iconFontSize);
            }

            var texture = string.IsNullOrEmpty(definition.Texture) ? null : _locateTexture(definition.Texture);
            if (texture is null && !string.IsNullOrEmpty(definition.Texture) && _missingTextures.Add(definition.Texture))
            {
                _notes.Add(GuiTexts.LayoutTextureMissing(definition.Texture));
            }

            return new Atom(JominiTextRunKind.Icon, name!, style, face, null, width, false, false, false, false, group, texture, height, definition.OffsetY * _scale);
        }

        /// <summary>
        /// Schriftschnitt einer Darstellung. Unbekannte Schrift (oder gar keine): Ersatzschrift Segoe UI mit Notiz; Fett und Kursiv werden dann simuliert.
        /// </summary>
        public (JominiFontFace Face, bool Bold, bool Italic) FaceFor(JominiTextStyle style)
        {
            if (_faces.TryGetValue(style, out var cached))
            {
                return cached;
            }

            var face = style.Font is null ? null : _fonts.Face(style.Font, style.Style, _request.Language);
            if (face is null)
            {
                var missing = style.Font ?? NoFontName;
                if (_missingFonts.Add(missing))
                {
                    MissingFonts.Add(missing);
                    if (_request.NoteMissingFonts)
                    {
                        _notes.Add(GuiTexts.LayoutTextFontMissing(missing));
                    }
                }

                face = JominiFontLibrary.Substitute;
            }

            var result = (
                face,
                face.SimulateBold || (face.IsSubstitute && style.Style.HasFlag(JominiFontStyle.Bold)),
                face.SimulateItalic || (face.IsSubstitute && style.Style.HasFlag(JominiFontStyle.Italic)));
            _faces[style] = result;
            return result;
        }
    }
}
