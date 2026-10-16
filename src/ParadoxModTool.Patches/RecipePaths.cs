namespace ParadoxModTool.Patches;

/// <summary>Sammelt die unsicheren Pfade eines Rezepts (Spec Teil 3): sie dürfen weder gelesen noch geschrieben werden.</summary>
public static class RecipePaths
{
    /// <summary>
    /// Die Pfade und Muster der Regel (<c>Files</c>, <c>SourceFiles</c>, <c>Target</c>, wie eingetragen, ohne Platzhalter zu füllen), die
    /// <see cref="PathSafety.IsSafeGlob"/> nicht bestehen; leer, wenn alles sicher ist.
    /// </summary>
    public static IReadOnlyList<string> UnsafePaths(PatchRule rule) =>
        RuleFields.Of(rule).Where(f => f.IsPath && IsUnsafe(f.Value)).Select(f => f.Value ?? string.Empty).ToList();

    /// <summary>Das Dateimuster, aus dem die Regel liest (<c>files</c> bzw. <c>sourceFiles</c>); null bei Regeln, die nur schreiben.</summary>
    public static string? ReadPatternOf(PatchRule rule) => RuleFields.Of(rule).FirstOrDefault(f => f.Kind == RuleFieldKind.Glob)?.Value;

    /// <summary>Abgeleitete Spalten, deren Suchdateien (<see cref="DerivedLookup.Files"/>) unsicher sind: Spaltenname und Muster.</summary>
    public static IReadOnlyList<(string Column, string Files)> UnsafeLookups(PatchRecipe recipe) =>
        (recipe.Columns ?? new List<PatchColumn>())
            .Where(column => column.Lookup is not null && IsUnsafe(column.Lookup.Files))
            .Select(column => (column.Name, column.Lookup!.Files ?? string.Empty))
            .ToList();

    /// <summary>
    /// Ein leerer Pfad ist unsicher im Sinne von <see cref="PathSafety"/>, liest aber nichts (z. B. die Iterator-Regel der Vorlage, bis der Ordner eingetragen ist);
    /// "Feld fehlt" meldet die Rezept-Prüfung. Hier zählt nur, was eingetragen ist und aus Mod oder Spiel hinausführt; ein Muster nur aus leeren
    /// Teilen (<c>" ; "</c>) fehlt ebenso.
    /// </summary>
    public static bool IsUnsafe(string? path) => PathSafety.GlobParts(path).Count > 0 && !PathSafety.IsSafeGlob(path);

    /// <summary>Das Muster hat keinen nicht leeren Teil (leer, nur Leerzeichen oder nur <c>;</c>).</summary>
    public static bool IsMissing(string? path) => PathSafety.GlobParts(path).Count == 0;

    /// <summary>Zeichen, die in einem Ziel nichts verloren haben: Muster (<c>; * ?</c>) und unter Windows ungültige Dateinamenzeichen.</summary>
    private static readonly char[] NotInTarget = { ';', '*', '?', '"', '<', '>', '|' };

    /// <summary>Das Ziel der Regel (<c>Target</c> von blockCopy, template und listEntry): eine einzelne Mod-Datei; null bei Regeln ohne Ziel.</summary>
    public static string? TargetOf(PatchRule rule) => RuleFields.Of(rule).FirstOrDefault(f => f.Kind == RuleFieldKind.Target)?.Value;

    /// <summary>
    /// Das Ziel ist eingetragen, aber kein einzelner Dateipfad: ein Muster (<c>;</c>, <c>*</c>, <c>?</c>), ungültige Zeichen, ein leerer Teil
    /// (<c>a//b</c>, <c>/</c> am Ende) oder ein Teil mit Leerzeichen am Rand bzw. Punkt am Ende (Windows schneidet beides still ab). Leer zählt
    /// nicht ("Feld fehlt" meldet die Prüfung), Pfade aus Mod oder Spiel hinaus meldet <see cref="IsUnsafe"/>.
    /// </summary>
    public static bool IsNotSingleFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.IndexOfAny(NotInTarget) >= 0 || path.Any(char.IsControl))
        {
            return true;
        }

        return path.Split('/', '\\').Any(part => part.Length == 0 || part.Trim().Length != part.Length || part.EndsWith('.'));
    }
}
