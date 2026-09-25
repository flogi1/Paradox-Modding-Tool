using ParadoxModTool.GuiDesign.Resolve;

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
}

/// <summary>Berechneter Kasten eines Elements in Bildschirmkoordinaten. <c>Notes</c> nennen Annäherungen (Plan-Ruling 7).</summary>
public sealed class LayoutBox
{
    public required ResolvedElement Element { get; init; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>-1 oder die Nummer des simulierten Listeneintrags.</summary>
    public int RepeatIndex { get; init; } = -1;

    public List<LayoutBox> Children { get; } = new();

    public List<string> Notes { get; } = new();

    public bool Uncertain => Notes.Count > 0;

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
