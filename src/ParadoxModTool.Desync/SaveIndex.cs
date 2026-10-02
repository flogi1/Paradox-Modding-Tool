using System.Globalization;

namespace ParadoxModTool.Desync;

public enum SaveReadState
{
    Complete,

    /// <summary>Die Datei endet, während noch Blöcke offen sind (abgeschnitten oder noch im Schreiben).</summary>
    Incomplete,

    /// <summary>Binärer Spielstand; er wird nicht gelesen.</summary>
    Binary,
}

public enum NodeKind
{
    Root,

    /// <summary>Block auf oberster Ebene.</summary>
    Section,

    /// <summary>Block bis zur Tiefe <see cref="DesyncProfile.NodeDepth"/>, der kein Objekt ist.</summary>
    Block,

    /// <summary>Objekt: Block mit Zahlenschlüssel in einem Objektbehälter.</summary>
    Unit,

    /// <summary>Objekt als <c>&lt;id&gt;=&lt;Wert&gt;</c> in einem Objektbehälter (z.B. <c>3003121664=none</c>).</summary>
    ScalarUnit,
}

/// <summary>
/// Knoten des Index. <see cref="Name"/> bezeichnet den Knoten unter seinen Geschwistern: Schlüssel, <c>key#2</c> bei Wiederholung,
/// <c>[i]</c> für namenlose Blöcke oder <c>sub_unit=123</c> (Feld aus <see cref="DesyncProfile.AnonymousIds"/>); bei Objekten der Slot.
/// <see cref="Start"/> und <see cref="End"/> sind Byte-Positionen im Stream (Ende exklusiv), ab dem Schlüssel des Knotens.
/// <see cref="FullHash"/> umfasst den ganzen Text, <see cref="OwnHash"/> nur die Bytes außerhalb der Kindknoten, ohne Leerraum
/// (Leerzeichen, Tab, CR, LF) außerhalb von Zeichenketten: Zeilenumbrüche und Einrückung zwischen den Kindern gehören dem Elternknoten,
/// ändern sich aber, wenn ein Kind auf einer Seite fehlt, und sollen keine Eigenänderung ergeben.
/// Zwischen zwei Wortbytes bleibt eine Grenze erhalten (<c>1 2</c> und <c>12</c> unterscheiden sich).
/// </summary>
public sealed record SaveNode(int Id, int Parent, NodeKind Kind, string Name, long Start, long End, ulong FullHash, ulong OwnHash, int? Slot, int? Generation);

/// <summary>Ergebnis des ersten Durchgangs über einen Spielstand: Knotenbaum mit Hashes, Kopfwerte und Namenstabellen.</summary>
public sealed class SaveIndex
{
    private readonly IReadOnlyList<ulong> _ids;
    private readonly int[] _childStart;
    private readonly int[] _children;

    /// <param name="ids">Volle Objekt-Id je Knoten (nur für Objekte belegt).</param>
    internal SaveIndex(
        IReadOnlyList<SaveNode> nodes,
        IReadOnlyList<ulong> ids,
        SaveReadState state,
        long length,
        IReadOnlyDictionary<string, string> scalars,
        IReadOnlyList<string> enabledDlcs,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> nameTables)
    {
        Nodes = nodes;
        _ids = ids;
        State = state;
        Length = length;
        Scalars = scalars;
        EnabledDlcs = enabledDlcs;
        NameTables = nameTables;

        // Kinder als ein flaches Feld mit Anfangspositionen je Knoten; Ids steigen in Dateireihenfolge.
        _childStart = new int[nodes.Count + 1];
        for (var id = 1; id < nodes.Count; id++)
        {
            _childStart[nodes[id].Parent + 1]++;
        }

        for (var id = 0; id < nodes.Count; id++)
        {
            _childStart[id + 1] += _childStart[id];
        }

        _children = new int[Math.Max(0, nodes.Count - 1)];
        var next = (int[])_childStart.Clone();
        for (var id = 1; id < nodes.Count; id++)
        {
            _children[next[nodes[id].Parent]++] = id;
        }
    }

    /// <summary>Alle Knoten, Index gleich <see cref="SaveNode.Id"/>; Index 0 ist die Wurzel.</summary>
    public IReadOnlyList<SaveNode> Nodes { get; }

    public SaveReadState State { get; }

    /// <summary>Gelesene Bytes.</summary>
    public long Length { get; }

    /// <summary>Skalare der obersten Ebene (erstes Vorkommen) und <c>metadata/&lt;key&gt;</c> für die direkten Skalare von <c>metadata</c>.</summary>
    public IReadOnlyDictionary<string, string> Scalars { get; }

    public IReadOnlyList<string> EnabledDlcs { get; }

    /// <summary>Je Muster aus <see cref="DesyncProfile.Names"/>: Objekt-Id → Name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> NameTables { get; }

    /// <summary>Volle Objekt-Id des Knotens (Slot und Generation); nur für Objekte (<see cref="NodeKind.Unit"/>, <see cref="NodeKind.ScalarUnit"/>) belegt.</summary>
    internal ulong FullIdOf(int id) => _ids[id];

    public IReadOnlyList<int> ChildrenOf(int id) =>
        new ArraySegment<int>(_children, _childStart[id], _childStart[id + 1] - _childStart[id]);

    /// <summary>Namen von der obersten Ebene bis zum Knoten, mit <c>/</c> verbunden; Objekte mit ihrer vollen Id.</summary>
    public string PathOf(int id)
    {
        var segments = new List<string>();
        for (var current = id; current > 0; current = Nodes[current].Parent)
        {
            var node = Nodes[current];
            segments.Add(node.Kind is NodeKind.Unit or NodeKind.ScalarUnit
                ? _ids[current].ToString(CultureInfo.InvariantCulture)
                : node.Name);
        }

        segments.Reverse();
        return string.Join('/', segments);
    }
}
