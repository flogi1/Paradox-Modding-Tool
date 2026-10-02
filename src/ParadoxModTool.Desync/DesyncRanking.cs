using System.Globalization;

namespace ParadoxModTool.Desync;

/// <summary>
/// Eine abweichende Einheit samt ihrer Feldabweichungen (Ergebnis des zweiten Durchgangs über beide Spielstände).
/// </summary>
/// <param name="Path">Pfad der Einheit, z.B. <c>countries/database/1384</c>; die oberste Ebene (eigene Werte) hat "".</param>
/// <param name="ReferenceLabel">Beschriftung des Objekts im Referenz-Spielstand (aus <see cref="DesyncProfile.LabelFields"/>), falls vorhanden.</param>
/// <param name="DesyncedLabel">Beschriftung im abweichenden Spielstand.</param>
/// <param name="ReferenceGeneration">Generation der Objekt-Id im Referenz-Spielstand.</param>
/// <param name="DesyncedGeneration">Generation der Objekt-Id im abweichenden Spielstand.</param>
/// <param name="ReferenceSize">Größe des Knotentexts im Referenz-Spielstand in Bytes.</param>
/// <param name="DesyncedSize">Größe des Knotentexts im abweichenden Spielstand in Bytes.</param>
/// <param name="Fields">Feldabweichungen; Pfade relativ zur Einheit.</param>
/// <param name="FieldsTruncated">Es gab mehr Abweichungen, als aufgelistet sind.</param>
/// <param name="LineFallback">Der Text ließ sich nicht parsen, die Abweichungen sind Zeilen.</param>
public sealed record UnitDiffDetail(
    string Path,
    UnitChange Change,
    string? ReferenceLabel,
    string? DesyncedLabel,
    int? ReferenceGeneration,
    int? DesyncedGeneration,
    long ReferenceSize,
    long DesyncedSize,
    IReadOnlyList<FieldDiff> Fields,
    bool FieldsTruncated,
    bool LineFallback);

/// <summary>Gewicht einer Abweichung; die Reihenfolge ist die der Anzeige: Ursachen zuerst.</summary>
public enum DiffWeight
{
    /// <summary>Mögliche Ursache des Desyncs.</summary>
    Cause,

    /// <summary>Folge eines früheren Unterschieds oder bloße Rechenabweichung.</summary>
    Consequence,

    /// <summary>Ohne Bedeutung (Zwischenspeicher, Metadaten).</summary>
    Noise
}

public sealed record RankedField(FieldDiff Field, DiffWeight Weight);

/// <param name="DisplayPath">Pfad der Einheit mit Namen (<see cref="SaveNameResolver.Display"/>).</param>
/// <param name="Manager">Erstes Pfadsegment der Einheit (der Manager im Spielstand); bei der obersten Ebene "".</param>
/// <param name="Weight">Das stärkste Gewicht der Felder bzw. das der Einheit selbst.</param>
public sealed record RankedDiff(UnitDiffDetail Unit, string DisplayPath, string Manager, DiffWeight Weight, IReadOnlyList<RankedField> Fields);

/// <summary>Ordnet Abweichungen nach Ursache, Folge und Rauschen und findet Manager, die in mehreren Paaren Ursachen haben.</summary>
public static class DesyncRanking
{
    /// <summary>
    /// Gewichtet jede Einheit und ihre Felder und sortiert nach Gewicht, Manager, Anzeigepfad. Feldreihenfolge bleibt erhalten.
    /// </summary>
    public static IReadOnlyList<RankedDiff> Rank(IReadOnlyList<UnitDiffDetail> units, DesyncProfile profile, SaveNameResolver names)
    {
        var ranked = new List<RankedDiff>(units.Count);
        foreach (var unit in units)
        {
            var fields = unit.Fields.Select(field => new RankedField(field, WeightOfField(unit.Path, field, profile))).ToList();
            ranked.Add(new RankedDiff(unit, names.Display(unit.Path), ManagerOf(unit.Path), WeightOfUnit(unit, fields, profile), fields));
        }

        return ranked
            .OrderBy(diff => diff.Weight)
            .ThenBy(diff => diff.Manager, StringComparer.Ordinal)
            .ThenBy(diff => diff.DisplayPath, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Manager mit mindestens einer Ursache in mehr als einem Paar, mit der Zahl dieser Paare; häufigste zuerst, dann nach Name.
    /// </summary>
    public static IReadOnlyList<(string Manager, int Count)> Recurring(IReadOnlyList<IReadOnlyList<RankedDiff>> pairs)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            foreach (var manager in pair.Where(diff => diff.Weight == DiffWeight.Cause).Select(diff => diff.Manager).Distinct(StringComparer.Ordinal))
            {
                counts[manager] = counts.GetValueOrDefault(manager) + 1;
            }
        }

        return counts
            .Where(entry => entry.Value > 1)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (entry.Key, entry.Value))
            .ToList();
    }

    private static string ManagerOf(string unitPath)
    {
        var slash = unitPath.IndexOf('/');
        return slash < 0 ? unitPath : unitPath[..slash];
    }

    /// <summary>Rauschen vor Folge vor Rechenabweichung; alles andere ist eine Ursache.</summary>
    private static DiffWeight WeightOfField(string unitPath, FieldDiff field, DesyncProfile profile)
    {
        var path = unitPath.Length == 0 ? field.Path : field.Path.Length == 0 ? unitPath : unitPath + "/" + field.Path;
        return WeightOfPath(path, profile)
            ?? (IsFloatDrift(field.ReferenceValue, field.DesyncedValue, profile.FloatDrift) ? DiffWeight.Consequence : DiffWeight.Cause);
    }

    private static DiffWeight WeightOfUnit(UnitDiffDetail unit, IReadOnlyList<RankedField> fields, DesyncProfile profile)
    {
        if (unit.Change is UnitChange.Changed or UnitChange.OwnScalarsChanged && fields.Count > 0)
        {
            return fields.Min(field => field.Weight);
        }

        // Ausgetauschte, einseitige und zu große Einheiten haben keine Felder: nur ihr Pfad kann sie abstufen.
        return WeightOfPath(unit.Path, profile) ?? DiffWeight.Cause;
    }

    private static DiffWeight? WeightOfPath(string path, DesyncProfile profile)
    {
        if (profile.Noise.Any(pattern => pattern.Matches(path)))
        {
            return DiffWeight.Noise;
        }

        return profile.Consequence.Any(pattern => pattern.Matches(path)) ? DiffWeight.Consequence : null;
    }

    /// <summary>Beide Werte sind Zahlen, mindestens einer mit Dezimalpunkt, und weichen relativ höchstens um <paramref name="drift"/> ab.</summary>
    private static bool IsFloatDrift(string? reference, string? desynced, double drift)
    {
        if (reference is null || desynced is null || !(reference.Contains('.') || desynced.Contains('.')))
        {
            return false;
        }

        if (!TryParse(reference, out var a) || !TryParse(desynced, out var b))
        {
            return false;
        }

        var largest = Math.Max(Math.Abs(a), Math.Abs(b));
        return largest == 0 || Math.Abs(a - b) / largest <= drift;
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
