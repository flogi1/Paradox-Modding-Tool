namespace ParadoxModTool.Performance;

/// <summary>Woher die Datei einer Dump-Zeile stammt.</summary>
public enum RowOrigin
{
    /// <summary>Nur in der aktiven Mod.</summary>
    Mod,

    /// <summary>In der aktiven Mod und in Vanilla (die Mod überschreibt oder ergänzt die Datei).</summary>
    Both,

    /// <summary>Nicht in der aktiven Mod, aber in einer anderen Mod des Playsets.</summary>
    OtherMod,

    /// <summary>Nur in Vanilla.</summary>
    Vanilla,

    /// <summary>In keinem der bekannten Ordner.</summary>
    Unknown,
}
