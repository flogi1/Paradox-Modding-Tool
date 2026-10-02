namespace ParadoxModTool.VanillaDiff;

/// <summary>Markierte Zeilen einer Seite (Ziehen im Zeilennummern-Rand): Startzeile und aktuelle Zeile, in beliebiger Richtung.</summary>
public readonly record struct LineMarks(int Anchor, int Current)
{
    public int First => Math.Min(Anchor, Current);

    public int Last => Math.Max(Anchor, Current);

    public int Count => Last - First + 1;

    public bool Contains(int line) => line >= First && line <= Last;
}
