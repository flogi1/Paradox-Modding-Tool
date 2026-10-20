namespace ParadoxModTool.Performance;

/// <summary>Eine Hotspot-Zeile mit Platz (ab 1) und Herkunft der Datei.</summary>
public sealed record RankedRow(int Rank, ProfilingRow Row, RowOrigin Origin);

/// <summary>Das Maß, nach dem eine Rangliste absteigend sortiert wird.</summary>
public enum RankBy
{
    Total,
    Self,
    Calls,
}
