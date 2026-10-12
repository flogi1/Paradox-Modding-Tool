using System.Globalization;

namespace ParadoxModTool.Generators;

/// <summary>Ergebnis von <see cref="AutoNumber.Resolve"/>: die gewählte Nummer, ihr Plan und die übersprungenen Nummern mit der Kollision, an der sie scheiterten.</summary>
public sealed record RenumberResult(int Number, GeneratorPlan Plan, IReadOnlyList<(int Number, HashCollision Collision)> Skipped);

/// <summary>
/// Schlüsselfeld mit <c>autoNumber</c> (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, Abschnitt 2): die nächste freie Nummer eines neuen
/// Arbeitsstands, und das Weiterzählen, wenn die Lokalisierungsschlüssel einer Nummer denselben Hash hätten wie schon vorhandene.
/// </summary>
public static class AutoNumber
{
    /// <summary>Das Feld mit <c>autoNumber</c> (der Lader erlaubt es nur am letzten Schlüsselfeld vom Typ Zahl); <see langword="null"/> ohne.</summary>
    public static FieldDef? FieldOf(GeneratorPackage package) => package.Head.FirstOrDefault(f => f.AutoNumber);

    /// <summary>
    /// Höchste Nummer unter den Schlüsseln, deren übrige Schlüsselteile gleich <paramref name="otherParts"/> sind, plus 1 (Lücken bleiben frei, 1 ohne Treffer).
    /// Schlüssel, deren letzter Teil keine ganze Zahl ist, zählen nicht.
    /// </summary>
    public static int Next(GeneratorPackage package, IEnumerable<string> takenKeys, IReadOnlyDictionary<string, string> otherParts)
    {
        var numberField = package.KeyFields[^1];
        var others = package.KeyFields.Take(package.KeyFields.Count - 1).ToList();
        var max = 0;
        foreach (var key in takenKeys)
        {
            var parts = PackageKey.Split(package, key);
            if (others.All(f => string.Equals(parts.GetValueOrDefault(f), otherParts.GetValueOrDefault(f), StringComparison.Ordinal))
                && int.TryParse(parts.GetValueOrDefault(numberField), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                max = Math.Max(max, number);
            }
        }

        return max + 1;
    }

    /// <summary>
    /// Ob <paramref name="currentKey"/> ein neuer Stand ist, der umnummeriert werden darf: nur bei einem Paket mit <c>read</c> (ohne Einleser lässt sich
    /// "neu" nicht von "schon in der Mod" unterscheiden) und wenn kein Schlüssel aus <paramref name="knownKeys"/> (Mod, Ausgabeordner) so heißt.
    /// </summary>
    public static bool IsNew(GeneratorPackage package, string currentKey, IEnumerable<string> knownKeys) =>
        package.Read is not null && !knownKeys.Contains(currentKey, StringComparer.OrdinalIgnoreCase);

    /// <summary>Der Schlüssel <paramref name="currentKey"/> mit der Nummer <paramref name="number"/> im Feld mit <c>autoNumber</c>.</summary>
    public static string KeyFor(GeneratorPackage package, string currentKey, int number)
    {
        var field = FieldOf(package) ?? throw new InvalidOperationException("Das Paket hat kein Feld mit autoNumber.");
        var parts = new Dictionary<string, string>(PackageKey.Split(package, currentKey), StringComparer.Ordinal)
        {
            [field.Id] = number.ToString(CultureInfo.InvariantCulture),
        };
        return PackageKey.Compose(package, parts)!;
    }

    /// <summary>
    /// Benennt den Stand um: speichert eine Kopie unter <paramref name="newKey"/>, nimmt den Schlüssel in <paramref name="state"/> auf und löscht die alte Datei
    /// nur, wenn sie zu diesem Stand gehört (<c>state.Key</c> ist der Schlüssel des Formulars, <paramref name="currentKey"/>). Nach einer ungespeicherten
    /// Änderung der Schlüsselfelder gehört die Datei unter <c>state.Key</c> einem anderen Stand und bleibt.
    /// </summary>
    public static void Rekey(GeneratorPackage package, GeneratorState state, string currentKey, string newKey)
    {
        var storedKey = state.Key;
        var rekeyed = GeneratorStateStore.Clone(state);
        PackageKey.Apply(package, rekeyed, newKey);
        GeneratorStateStore.Save(package.DataFolder, rekeyed);
        if (string.Equals(storedKey, currentKey, StringComparison.OrdinalIgnoreCase) && !string.Equals(storedKey, newKey, StringComparison.OrdinalIgnoreCase))
        {
            GeneratorStateStore.Delete(package.DataFolder, storedKey);
        }

        PackageKey.Apply(package, state, newKey);
    }

    /// <summary>
    /// Die erste Nummer ab <paramref name="start"/>, die nicht vergeben ist und deren Plan keine Hash-Kollision hat. Vergebene Nummern und Nummern mit
    /// Kollision zählen als Versuch; nach <paramref name="maxTries"/> Versuchen bricht es ab.
    /// </summary>
    public static RenumberResult Resolve(int start, Func<int, bool> taken, Func<int, GeneratorPlan> planFor, LocalizationKeyIndex index, int maxTries = 50)
    {
        var skipped = new List<(int, HashCollision)>();
        for (var i = 0; i < maxTries; i++)
        {
            var number = start + i;
            if (taken(number))
            {
                continue;
            }

            var plan = planFor(number);
            var collisions = HashCollisions.Find(plan, index);
            if (collisions.Count == 0)
            {
                return new RenumberResult(number, plan, skipped);
            }

            skipped.AddRange(collisions.Select(c => (number, c)));
        }

        throw new GeneratorOutputException(GeneratorsTexts.NoFreeNumber(start, maxTries));
    }
}
