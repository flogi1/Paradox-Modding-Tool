namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Adresse eines Knotens im Bedingungsbaum: Indizes von der Wurzel aus (Glieder einer UND/ODER-Gruppe, Index 0 = Inhalt eines NICHT). Leer = Wurzel.
/// </summary>
public sealed record CondPath(IReadOnlyList<int> Indices)
{
    public static CondPath Root { get; } = new(Array.Empty<int>());

    public CondPath? Parent => Indices.Count == 0 ? null : new CondPath(Indices.Take(Indices.Count - 1).ToArray());

    public int Last => Indices.Count == 0 ? -1 : Indices[^1];

    public CondPath Child(int index) => new(Indices.Append(index).ToArray());

    public bool Equals(CondPath? other) => other is not null && Indices.SequenceEqual(other.Indices);

    public override int GetHashCode() => Indices.Aggregate(17, (hash, i) => hash * 31 + i);

    public override string ToString() => "[" + string.Join(",", Indices) + "]";
}
