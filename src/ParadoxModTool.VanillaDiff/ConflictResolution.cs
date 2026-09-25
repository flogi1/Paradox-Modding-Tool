namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Eine vom Nutzer getroffene Entscheidung: bei diesem <see cref="MergeConflict"/> soll die
/// Mod-Fassung gelten (die Spiel-Update-Fassung steht nach <see cref="MergeEngine.Merge"/> ohnehin
/// schon im Baum - für sie ist keine Entscheidung nötig).
/// </summary>
public sealed record ConflictResolutionChoice(MergeConflict Conflict);

/// <summary>
/// Setzt vom Nutzer aufgelöste Konflikte (Abschnitt 7, Merge-Funktion) in den zusammengeführten
/// Baum ein. <see cref="MergeEngine.Merge"/> lässt bei einem Konflikt bewusst die
/// Spiel-Update-Fassung stehen; hier wird an genau den vom Nutzer bestätigten Stellen stattdessen
/// der komplette Mod-Eintrag eingesetzt (nicht nur der nackte Wert), damit dessen eigener
/// Quell-Span für <see cref="ScriptWriter"/>s "Mod-Formatierung beibehalten"-Modus erhalten
/// bleibt. Je nach <see cref="MergeConflict.Kind"/> wird die Stelle ersetzt, entfernt oder
/// wieder eingefügt.
/// </summary>
public static class ConflictResolution
{
    public static ScriptBlock ApplyResolutions(ScriptBlock root, IReadOnlyList<ConflictResolutionChoice> resolutions)
    {
        var patchedRoot = root;

        foreach (var resolution in resolutions)
        {
            if (resolution.Conflict.PathSegments.Count == 0)
            {
                continue;
            }

            patchedRoot = ApplyOne(patchedRoot, resolution.Conflict, 0);
        }

        return patchedRoot;
    }

    private static ScriptBlock ApplyOne(ScriptBlock block, MergeConflict conflict, int depth)
    {
        var key = conflict.PathSegments[depth];
        var isLast = depth == conflict.PathSegments.Count - 1;
        var entries = new List<ScriptEntry>(block.Entries);
        var units = EntryUnits.Build(entries);
        var unit = units.FirstOrDefault(u => u.Key == key);

        if (!isLast)
        {
            if (unit?.Value is not ScriptBlock nested)
            {
                return block;
            }

            var patched = ApplyOne(nested, conflict, depth + 1);
            entries[entries.IndexOf(unit.Entry)] = EntryUnits.WithBlockValue(unit.Entry, patched);
            return new ScriptBlock { Entries = entries };
        }

        switch (conflict.Kind)
        {
            case MergeConflictKind.ReplaceWithMod when unit is not null && conflict.ModEntry is not null:
                entries[entries.IndexOf(unit.Entry)] = conflict.ModEntry;
                break;

            case MergeConflictKind.RemoveForMod when unit is not null:
                entries.Remove(unit.Entry);
                foreach (var header in unit.Header)
                {
                    entries.Remove(header);
                }

                break;

            case MergeConflictKind.InsertForMod when unit is null && conflict.ModEntry is not null:
                var anchor = conflict.AnchorKey is null ? null : units.FirstOrDefault(u => u.Key == conflict.AnchorKey);
                var insertAt = anchor is null ? 0 : entries.IndexOf(anchor.Entry) + 1;
                entries.InsertRange(insertAt, conflict.ModHeader.Append(conflict.ModEntry));
                break;
        }

        return new ScriptBlock { Entries = entries };
    }
}
