using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Layout;

public sealed class LayoutOptions
{
    public double ScreenWidth { get; init; } = 1920;

    public double ScreenHeight { get; init; } = 1080;

    /// <summary>Elemente mit <c>visible = "[…]"</c> (nicht auswertbar) anzeigen.</summary>
    public bool ShowConditional { get; init; } = true;

    /// <summary>Elemente mit <c>visible = no</c> anzeigen.</summary>
    public bool ShowHidden { get; init; }

    /// <summary>Anzahl simulierter Einträge in Rastern und Listen.</summary>
    public int ListSampleCount { get; init; } = 3;

    /// <summary>UI-Skalierung: gelegt wird auf einem virtuellen Bildschirm von <c>ScreenWidth / UiScale</c> × <c>ScreenHeight / UiScale</c>.</summary>
    public double UiScale { get; init; } = 1;

    /// <summary>Angesehene Datei; erster Teil der Schlüssel bedingter Elemente (<see cref="GuiConditionalKeys.FilePrefix"/>).</summary>
    public string? ViewedFile { get; init; }

    /// <summary>Syntaxknoten der Vorschau-Wurzel; zweiter Teil der Schlüssel bedingter Elemente (<see cref="GuiConditionalKeys.RootPart"/>).</summary>
    public GuiNode? PreviewRootNode { get; init; }

    /// <summary>Schlüssel ausgeblendeter bedingter Elemente (<see cref="GuiConditionalKeys"/>); sie werden wie unsichtbare Kinder behandelt.</summary>
    public IReadOnlySet<string> HiddenConditionals { get; init; } = new HashSet<string>();

    /// <summary>Sprache der Jomini-Texte in kanonischer Form (<c>l_english</c>, <c>l_german</c>); fehlende Texte kommen englisch.</summary>
    public string Language { get; init; } = "l_english";

    /// <summary>Kopie mit der angegebenen angesehenen Datei und Vorschau-Wurzel; alle übrigen Werte bleiben (neue Eigenschaften hier ergänzen).</summary>
    public LayoutOptions With(string? viewedFile, GuiNode? previewRootNode) => new()
    {
        ScreenWidth = ScreenWidth,
        ScreenHeight = ScreenHeight,
        ShowConditional = ShowConditional,
        ShowHidden = ShowHidden,
        ListSampleCount = ListSampleCount,
        UiScale = UiScale,
        ViewedFile = viewedFile,
        PreviewRootNode = previewRootNode,
        HiddenConditionals = HiddenConditionals,
        Language = Language,
    };
}

/// <summary>Rechteck in Bildschirmkoordinaten.</summary>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height);

/// <summary>Berechneter Kasten eines Elements in Bildschirmkoordinaten. <c>Notes</c> nennen Annäherungen (Plan-Ruling 7).</summary>
public sealed class LayoutBox
{
    public required ResolvedElement Element { get; init; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>
    /// Jomini <c>scale</c>: Produkt der eigenen und aller Vorfahren-Faktoren (1 ohne <c>scale</c>). Der Kasten ist schon skaliert (Lauf 6, 15a/15b); Zeichner
    /// nehmen Ränder, Kacheln und Text (in unskalierten Einheiten gesetzt) mit diesem Faktor mal.
    /// </summary>
    public double RenderScale { get; set; } = 1;

    /// <summary>-1 oder die Nummer des simulierten Listeneintrags.</summary>
    public int RepeatIndex { get; init; } = -1;

    public List<LayoutBox> Children { get; } = new();

    public List<string> Notes { get; } = new();

    public bool Uncertain => Notes.Count > 0;

    /// <summary>Was in diesem Kasten gezeichnet wird (Sprite oder Text); <see langword="null"/> bei reinen Containern und Kästen ohne Darstellung.</summary>
    public GuiVisual? Visual { get; set; }

    /// <summary>Nur Jomini: das Texturbild des Kastens (<c>texture</c>), gezeichnet unter <see cref="Visual"/>; so behält ein Button Textur und Beschriftung. <see langword="null"/> ohne Textur.</summary>
    public JominiSpriteVisual? Sprite { get; set; }

    /// <summary>Der Kasten ragt um mehr als 1 Pixel über den Kasten seines Elternelements hinaus.</summary>
    public bool OutsideParent { get; set; }

    /// <summary>Der Kasten ragt um mehr als 1 Pixel über den (virtuellen) Bildschirm hinaus.</summary>
    public bool OutsideScreen { get; set; }

    /// <summary>Der Kasten liegt ganz neben dem (virtuellen) Bildschirm (klassisch versteckt Vanilla Elemente bei x = 99512); "Einpassen" lässt ihn weg.</summary>
    public bool OffScreen { get; set; }

    /// <summary>
    /// Beschnitt durch umgebende <c>scrollarea</c>- bzw. <c>scissor = yes</c>-Kästen (Schnitt aller), in Bildschirmkoordinaten;
    /// <see langword="null"/>, wenn kein Vorfahr beschneidet.
    /// </summary>
    public LayoutRect? Clip { get; set; }

    /// <summary>Kasten eines <c>background</c>: füllt das Elternelement (um <c>margin</c> verändert), belegt kein Fach, wird zuerst gezeichnet.</summary>
    public bool IsBackground { get; set; }

    /// <summary>Bedingt sichtbares Element (<c>visible = "[…]"</c>), das gelegt wurde.</summary>
    public bool Conditional { get; init; }

    /// <summary>Schlüssel zum Ausblenden (<see cref="GuiConditionalKeys"/>), von der Engine beim Absteigen berechnet; null, wenn nicht bedingt.</summary>
    public string? ConditionalKey { get; init; }

    /// <summary>
    /// Nur am Wurzelkasten: Schlüssel aller bedingten Elemente des aufgelösten Baums, auch der ausgeblendeten (die keinen Kasten haben). So findet die
    /// Oberfläche den Schlüssel für "Wieder einblenden", ohne ihn selbst zu berechnen. An anderen Kästen leer.
    /// </summary>
    public IReadOnlyDictionary<ResolvedElement, string> ConditionalKeys { get; set; } = EmptyKeys;

    private static readonly IReadOnlyDictionary<ResolvedElement, string> EmptyKeys = new Dictionary<ResolvedElement, string>();

    public IEnumerable<LayoutBox> Descendants()
    {
        var stack = new Stack<LayoutBox>(Children.AsEnumerable().Reverse());
        while (stack.Count > 0)
        {
            var box = stack.Pop();
            yield return box;
            for (var i = box.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(box.Children[i]);
            }
        }
    }
}
