namespace ParadoxModTool.Desync;

public enum UnitChange
{
    /// <summary>Objekt oder Blatt-Block mit anderem Text; der ganze Knotenbereich wird im zweiten Durchgang verglichen.</summary>
    Changed,

    /// <summary>Block mit Kindknoten, dessen eigener Text (ohne die Kindknoten) abweicht.</summary>
    OwnScalarsChanged,

    /// <summary>Gleicher Slot, andere Generation: ein anderes Objekt, es wird nicht feldweise verglichen.</summary>
    SlotReused,

    OnlyReference,

    OnlyDesynced,

    /// <summary>Blatt-Block über <see cref="DesyncProfile.SizeCapBytes"/>; er wird im zweiten Durchgang nicht gelesen.</summary>
    TooLarge,
}

/// <param name="Path">Pfad des Referenzknotens, bei fehlendem Referenzknoten der des abweichenden Knotens.</param>
public sealed record UnitDiff(string Path, UnitChange Change, SaveNode? Reference, SaveNode? Desynced);

/// <summary>Vergleicht zwei Indizes (Host = Referenz, Client = abweichend) und liefert die kleinsten abweichenden Knoten.</summary>
public static class SaveIndexComparer
{
    public static IReadOnlyList<UnitDiff> Compare(SaveIndex reference, SaveIndex desynced, DesyncProfile profile)
    {
        var diffs = new List<UnitDiff>();
        var comparison = new Comparison(reference, desynced, profile.SizeCapBytes, diffs);
        comparison.Pair(reference.Nodes[0], desynced.Nodes[0]);
        return diffs;
    }

    private sealed class Comparison(SaveIndex reference, SaveIndex desynced, long sizeCap, List<UnitDiff> diffs)
    {
        /// <summary>Vergleicht zwei einander zugeordnete Knoten; gleiche Hashes enden sofort.</summary>
        public void Pair(SaveNode referenceNode, SaveNode desyncedNode)
        {
            if (referenceNode.FullHash == desyncedNode.FullHash)
            {
                return;
            }

            if (referenceNode.Kind is NodeKind.Unit or NodeKind.ScalarUnit || desyncedNode.Kind is NodeKind.Unit or NodeKind.ScalarUnit)
            {
                // Objekte werden nie entlang ihrer Felder zerlegt; eine andere Generation ist ein anderes Objekt.
                var change = referenceNode.Generation != desyncedNode.Generation ? UnitChange.SlotReused : UnitChange.Changed;
                Add(change, referenceNode, desyncedNode);
                return;
            }

            var referenceChildren = reference.ChildrenOf(referenceNode.Id);
            var desyncedChildren = desynced.ChildrenOf(desyncedNode.Id);
            if (referenceChildren.Count == 0 && desyncedChildren.Count == 0)
            {
                var tooLarge = referenceNode.End - referenceNode.Start > sizeCap || desyncedNode.End - desyncedNode.Start > sizeCap;
                Add(tooLarge ? UnitChange.TooLarge : UnitChange.Changed, referenceNode, desyncedNode);
                return;
            }

            PairChildren(referenceChildren, desyncedChildren);
            if (referenceNode.OwnHash != desyncedNode.OwnHash)
            {
                Add(UnitChange.OwnScalarsChanged, referenceNode, desyncedNode);
            }
        }

        /// <summary>
        /// Ordnet Kinder über den Namen zu (bei Objekten ist das der Slot); Wiederholungen tragen ihr <c>#n</c> bereits im Namen. Ist ein Slot
        /// im Behälter auf einer Seite nicht eindeutig, stimmt die Aufteilung der Ids in Slot und Generation dort nicht (Spec, Abschnitt 3):
        /// dann werden die Objekte dieses Behälters über ihre volle Id zugeordnet, die übrigen Kinder weiter über den Namen.
        /// </summary>
        private void PairChildren(IReadOnlyList<int> referenceChildren, IReadOnlyList<int> desyncedChildren)
        {
            var byFullId = HasDuplicateSlot(reference, referenceChildren) || HasDuplicateSlot(desynced, desyncedChildren);
            var desyncedByName = new Dictionary<string, int>(desyncedChildren.Count, StringComparer.Ordinal);
            for (var i = 0; i < desyncedChildren.Count; i++)
            {
                desyncedByName.TryAdd(KeyOf(desynced, desyncedChildren[i], byFullId), i);
            }

            var paired = new bool[desyncedChildren.Count];
            foreach (var referenceId in referenceChildren)
            {
                var referenceChild = reference.Nodes[referenceId];
                if (desyncedByName.Remove(KeyOf(reference, referenceId, byFullId), out var index))
                {
                    paired[index] = true;
                    Pair(referenceChild, desynced.Nodes[desyncedChildren[index]]);
                }
                else
                {
                    Add(UnitChange.OnlyReference, referenceChild, null);
                }
            }

            for (var i = 0; i < desyncedChildren.Count; i++)
            {
                if (!paired[i])
                {
                    Add(UnitChange.OnlyDesynced, null, desynced.Nodes[desyncedChildren[i]]);
                }
            }
        }

        private static bool HasDuplicateSlot(SaveIndex index, IReadOnlyList<int> children)
        {
            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in children)
            {
                var node = index.Nodes[child];
                if (node.Kind is NodeKind.Unit or NodeKind.ScalarUnit && !slots.Add(node.Name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Schlüssel der Zuordnung: der Name, bei Zuordnung über die volle Id für Objekte die Id (mit einem Präfix, das kein Name trägt).</summary>
        private static string KeyOf(SaveIndex index, int id, bool byFullId)
        {
            var node = index.Nodes[id];
            return byFullId && node.Kind is NodeKind.Unit or NodeKind.ScalarUnit
                ? "\u0001" + index.FullIdOf(id).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : node.Name;
        }

        private void Add(UnitChange change, SaveNode? referenceNode, SaveNode? desyncedNode)
        {
            var path = referenceNode is not null ? reference.PathOf(referenceNode.Id) : desynced.PathOf(desyncedNode!.Id);
            diffs.Add(new UnitDiff(path, change, referenceNode, desyncedNode));
        }
    }
}
