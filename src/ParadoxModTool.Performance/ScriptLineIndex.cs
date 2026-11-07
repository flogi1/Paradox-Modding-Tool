namespace ParadoxModTool.Performance;

/// <summary>
/// Rechnet zwischen Zeichen-Offsets und Zeilen eines Skripttexts um. Zeilen sind 1-basiert wie <see cref="ProfilingRow.Line"/>; ein Zeilenumbruch
/// ist <c>\n</c>, ein davor stehendes <c>\r</c> gehört nicht mehr zur Zeile.
/// </summary>
public sealed class ScriptLineIndex
{
    private readonly int[] _starts;
    private readonly int _length;
    private readonly string _text;

    public ScriptLineIndex(string text)
    {
        _text = text;
        _length = text.Length;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _starts = starts.ToArray();
    }

    /// <summary>Anzahl der Zeilen; ein leerer Text hat eine (leere) Zeile, ein Text mit abschließendem Umbruch eine leere letzte Zeile.</summary>
    public int LineCount => _starts.Length;

    /// <summary>Zeile (ab 1) des Offsets; Offsets außerhalb des Textes werden auf die erste bzw. letzte Zeile gesetzt.</summary>
    public int LineOf(int offset)
    {
        if (offset <= 0)
        {
            return 1;
        }

        var index = Array.BinarySearch(_starts, Math.Min(offset, _length));
        return index >= 0 ? index + 1 : ~index;
    }

    /// <summary>Offset des ersten Zeichens der Zeile; Zeilen außerhalb von 1..<see cref="LineCount"/> werden begrenzt.</summary>
    public int StartOf(int line) => _starts[Math.Clamp(line, 1, LineCount) - 1];

    /// <summary>Offset hinter dem letzten Zeichen der Zeile, vor <c>\r\n</c> bzw. <c>\n</c>.</summary>
    public int EndOf(int line)
    {
        line = Math.Clamp(line, 1, LineCount);
        var end = line < LineCount ? _starts[line] - 1 : _length;
        if (line < LineCount && end > _starts[line - 1] && _text[end - 1] == '\r')
        {
            end--;
        }

        return end;
    }
}
