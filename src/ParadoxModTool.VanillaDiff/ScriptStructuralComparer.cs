namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Struktureller Gleichheitsvergleich für <see cref="ScriptEntry"/>/<see cref="ScriptValue"/>,
/// der bewusst Kommentare, Groß-/Kleinschreibung von Strings (Groß-/Kleinschreibung bei
/// Bezeichnern IST relevant, siehe unten) und jegliche Original-Formatierung/Whitespace
/// ignoriert. Das ist die gemeinsame Grundlage für <see cref="ScriptDiffer"/> und
/// <see cref="MergeEngine"/>: "semantisch gleichwertige, aber anders formatierte Blöcke"
/// (Abschnitt 7) gelten als gleich.
/// </summary>
public static class ScriptStructuralComparer
{
    public static bool EntryEquals(ScriptEntry? a, ScriptEntry? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a is ScriptStatement sa && b is ScriptStatement sb)
        {
            return string.Equals(sa.Key, sb.Key, StringComparison.Ordinal)
                && sa.Operator == sb.Operator
                && ValueEquals(sa.Value, sb.Value);
        }

        if (a is ScriptListItem la && b is ScriptListItem lb)
        {
            return ValueEquals(la.Value, lb.Value);
        }

        return false;
    }

    public static bool ValueEquals(ScriptValue? a, ScriptValue? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a is ScriptScalar scalarA && b is ScriptScalar scalarB)
        {
            return scalarA.IsQuoted == scalarB.IsQuoted && string.Equals(scalarA.Text, scalarB.Text, StringComparison.Ordinal);
        }

        if (a is ScriptBlock blockA && b is ScriptBlock blockB)
        {
            if (blockA.Entries.Count != blockB.Entries.Count)
            {
                return false;
            }

            for (var i = 0; i < blockA.Entries.Count; i++)
            {
                if (!EntryEquals(blockA.Entries[i], blockB.Entries[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Extrahiert, sofern vorhanden, den Schlüssel eines Eintrags (nur <see cref="ScriptStatement"/> hat einen).</summary>
    public static string? KeyOf(ScriptEntry entry) => entry is ScriptStatement stmt ? stmt.Key : null;
}
