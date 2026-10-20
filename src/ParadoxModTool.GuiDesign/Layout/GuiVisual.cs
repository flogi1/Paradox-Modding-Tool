using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Was die Vorschau in einem Kasten zeichnet: klassische Sprites und Bitmap-Texte sowie Jomini-Texte (<see cref="JominiTextVisual"/>) und -Texturbilder (<see cref="JominiSpriteVisual"/>).</summary>
public abstract record GuiVisual;

/// <summary>
/// Ein Sprite. <paramref name="TextureFullPath"/> ist <see langword="null"/>, wenn Sprite oder Textur fehlen. <paramref name="Frame"/> zählt ab 1 und liegt in
/// 1…<paramref name="Frames"/>. <paramref name="Background"/>: der Kasten ist ein <c>background</c>-Kind, auf den Elternkasten gestreckt.
/// </summary>
public sealed record GuiSpriteVisual(
    string SpriteName,
    string? TextureFullPath,
    ClassicSpriteKind Kind,
    int Frame,
    int Frames,
    int BorderX,
    int BorderY,
    bool TilingCenter,
    bool Background) : GuiVisual;

/// <summary>Ein Text in einer Bitmap-Schrift. <paramref name="Format"/>: <c>left</c>, <c>centre</c>/<c>center</c> oder <c>right</c>, klein geschrieben.</summary>
public sealed record GuiTextVisual(string Text, string? FontName, string Format, int MaxWidth, int MaxHeight) : GuiVisual;

/// <summary>Art eines Abschnitts im Jomini-Text.</summary>
public enum JominiTextRunKind
{
    Text,
    Placeholder,
    Icon,
}

/// <summary>Waagerechte Ausrichtung eines Jomini-Texts im Kasten.</summary>
public enum JominiTextAlignH
{
    Left,
    Center,
    Right,
}

/// <summary>Senkrechte Ausrichtung eines Jomini-Texts im Kasten.</summary>
public enum JominiTextAlignV
{
    Top,
    Center,
    Bottom,
}

/// <summary>
/// Ein Abschnitt einer Zeile mit einer Darstellung und einer Schriftdatei.
/// <list type="bullet">
/// <item><see cref="JominiTextRunKind.Text"/> und <see cref="JominiTextRunKind.Placeholder"/>: <paramref name="Text"/> ist der gezeigte Text,
/// <paramref name="Advances"/> hat je Unicode-Zeichen (Rune, nicht UTF-16-Einheit) den gerundeten Vorschub; <paramref name="Width"/> ist ihre Summe.
/// <paramref name="FontFile"/> ist null nur, wenn nicht einmal die Ersatzschrift lesbar ist.</item>
/// <item><see cref="JominiTextRunKind.Icon"/>: <paramref name="Text"/> ist der Icon-Name, <paramref name="Advances"/> hat genau einen Eintrag (die
/// Breite), <paramref name="IconTexture"/> ist der volle Texturpfad oder null (fehlend: schraffiertes Quadrat), <paramref name="IconOffsetY"/> der
/// unskalierte <c>offset</c> y des <c>iconsize</c>-Blocks.</item>
/// </list>
/// </summary>
public sealed record JominiTextRun(
    JominiTextRunKind Kind,
    string Text,
    string? FontFile,
    double FontSize,
    bool SimulateBold,
    bool SimulateItalic,
    JominiColor Color,
    bool Underline,
    bool Glow,
    bool Shadow,
    IReadOnlyList<double> Advances,
    double Width,
    string? IconTexture,
    double IconHeight,
    double IconOffsetY);

/// <summary>
/// Eine Zeile. Zeilen werden mit <paramref name="Height"/> untereinander gestapelt: jede Zeile außer der letzten hat den Zeilenabstand
/// (<see cref="JominiTextMetrics.LinePitch"/>), die letzte die Einzelzeilenhöhe (<see cref="JominiTextMetrics.LineHeight"/>), so dass die Summe die
/// Texthöhe ergibt. <paramref name="Baseline"/> ist der Abstand der Grundlinie von der Oberkante dieser Zeile. <paramref name="Width"/> zählt
/// Leerzeichen am Zeilenende nicht mit, wenn die Zeile durch den Umbruch endet.
/// </summary>
public sealed record JominiTextLine(IReadOnlyList<JominiTextRun> Runs, double Width, double Height, double Baseline);

/// <summary>
/// Ein gesetzter Jomini-Text: <paramref name="Width"/> ist die breiteste Zeile, <paramref name="Height"/> die Summe der Zeilenhöhen. Die Ausrichtung im
/// Kasten macht der Zeichner. <paramref name="Elided"/>: rechts mit " ..." gekürzt. <paramref name="MissingFonts"/>/<paramref name="MissingIcons"/>
/// nennen jede fehlende Schrift und jedes fehlende Icon einmal.
/// </summary>
public sealed record JominiTextVisual(
    IReadOnlyList<JominiTextLine> Lines,
    double Width,
    double Height,
    JominiTextAlignH AlignH,
    JominiTextAlignV AlignV,
    bool Elided,
    bool HasPlaceholder,
    IReadOnlyList<string> MissingFonts,
    IReadOnlyList<string> MissingIcons) : GuiVisual;

/// <summary>Art eines Jomini-Sprites (<c>spriteType</c>): ganze Fläche gestreckt, 9 Teile mit gestreckter Mitte, 9 Teile mit gekachelten Kanten und Mitte, oder ganz gekachelt.</summary>
public enum JominiSpriteKind
{
    Stretched,
    CorneredStretched,
    CorneredTiled,
    Tiled,
}

/// <summary>Einpassung (<c>fittype</c>) eines gestreckten Sprites in seinen Kasten.</summary>
public enum JominiFit
{
    Fill,
    CenterCrop,
    Center,
    Start,
    End,
}

/// <summary>Warum ein Sprite nicht als Bild gezeichnet wird (der Zeichner zeigt dann einen Platzhalter).</summary>
public enum JominiSpritePlaceholder
{
    None,
    Missing,
    Dynamic,
    UnsupportedFormat,
    NotDrawn,
}

/// <summary>Sprite-Rand wie in <c>spriteborder</c> (links, oben, rechts, unten): GUI-Einheiten, in der Textur × <c>texture_density</c> Pixel (<see cref="JominiSpriteRules.BorderInGuiUnits"/>).</summary>
public readonly record struct JominiBorder(double Left, double Top, double Right, double Bottom);

/// <summary>Texturbild eines Jomini-Kastens; Source in Texturpixeln, Border wie <c>spriteborder</c>, Tint/Opacity schon zusammengefasst.</summary>
public sealed record JominiSpriteVisual(
    string Texture,
    string? TextureFullPath,
    int TextureWidth,
    int TextureHeight,
    LayoutRect Source,
    double Density,
    JominiSpriteKind Kind,
    JominiBorder Border,
    JominiFit Fit,
    bool MirrorHorizontal,
    bool MirrorVertical,
    JominiColor Tint,
    double Opacity,
    JominiSpritePlaceholder Placeholder,
    string? PlaceholderLabel) : GuiVisual;
