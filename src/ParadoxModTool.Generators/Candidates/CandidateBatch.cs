using System.Globalization;
using ParadoxModTool.Generators.Editing;

namespace ParadoxModTool.Generators.Candidates;

/// <summary>
/// Ein ausgewählter Kandidat. <paramref name="Names"/>: Sprache (<c>english</c>, <c>german</c>, …) → offizieller Name; ein fehlender oder leerer Eintrag
/// heißt "kein Name in dieser Sprache". <paramref name="Boolean"/>: ja/nein-Modifier (eine Stufe mit <c>yes</c>).
/// </summary>
public sealed record CandidatePick(string Modifier, bool Boolean, IReadOnlyDictionary<string, string> Names);

/// <summary>
/// Erzeugt aus gewählten Kandidaten (<see cref="CandidatesDef"/>) mit einem Schritt neue Arbeitsstände: Schlüssel- und Unter-Reiter-Angaben einmal gefragt,
/// die Nummer fortlaufend, der Name der offizielle Name des Modifiers und der Modifier auf allen Stufen mit leerem Wert.
/// </summary>
public static class CandidateBatch
{
    private const string English = "english";

    /// <summary>
    /// Die Fehler, die schon die Angaben auslösen: Paket-Fehler (Pfad <see langword="null"/>) und Fehler der gefragten Felder, geprüft an einem Probe-Stand,
    /// den <see cref="Create"/> genauso baut wie den ersten echten. Leer heißt: die Angaben passen. Die leeren Modifier-Werte zählen hier nicht, die füllt erst der Nutzer.
    /// </summary>
    public static IReadOnlyList<string> AskIssues(GeneratorPackage package, GeneratorRenderer renderer, IReadOnlyDictionary<string, string> asked, IReadOnlyList<string> languages)
    {
        var def = Def(package);
        var probe = Build(package, def, new CandidatePick("x", false, new Dictionary<string, string> { [English] = "x" }), asked, Array.Empty<string>(), languages, null);
        return FormValidator.Check(package, probe, renderer)
            .Where(i => i.Severity == IssueSeverity.Error && (i.Path is null || def.Create.Ask.Contains(i.Path, StringComparer.Ordinal)))
            .Select(i => i.Message)
            .ToList();
    }

    /// <summary>
    /// Ein Stand je Auswahl in Auswahlreihenfolge. Jeder erzeugte Schlüssel kommt in die Menge der vergebenen, bevor die nächste Nummer berechnet wird; so zählen
    /// Mod und schon vorhandene Stände (<paramref name="takenKeys"/>) und die Stände dieses Stapels gleichermaßen.
    /// </summary>
    public static IReadOnlyList<GeneratorState> Create(GeneratorPackage package, IReadOnlyList<CandidatePick> picks, IReadOnlyDictionary<string, string> asked,
        IEnumerable<string> takenKeys, IReadOnlyList<string> languages, string? modRoot)
    {
        var def = Def(package);
        var taken = new List<string>(takenKeys);
        var states = new List<GeneratorState>();
        foreach (var pick in picks)
        {
            var state = Build(package, def, pick, asked, taken, languages, modRoot);
            taken.Add(state.Key);
            states.Add(state);
        }

        return states;
    }

    private static CandidatesDef Def(GeneratorPackage package) =>
        package.Candidates ?? throw new InvalidOperationException("Das Paket hat keinen Abschnitt candidates.");

    private static GeneratorState Build(GeneratorPackage package, CandidatesDef def, CandidatePick pick, IReadOnlyDictionary<string, string> asked,
        IReadOnlyCollection<string> taken, IReadOnlyList<string> languages, string? modRoot)
    {
        var numberField = AutoNumber.FieldOf(package) ?? throw new InvalidOperationException("Das Paket hat kein Feld mit autoNumber.");
        var parts = package.KeyFields.Where(f => f != numberField.Id).ToDictionary(f => f, f => asked.GetValueOrDefault(f) ?? string.Empty, StringComparer.Ordinal);
        parts[numberField.Id] = AutoNumber.Next(package, taken, parts).ToString(CultureInfo.InvariantCulture);
        var key = PackageKey.Compose(package, parts) ?? throw new ArgumentException("Ein Schlüsselfeld ist nicht gefragt oder leer.", nameof(asked));

        var state = GeneratorState.CreateEmpty(package, languages);
        state.ModRoot = modRoot;
        PackageKey.Apply(package, state, key);
        foreach (var field in def.Create.Ask)
        {
            state.Head.Values[field] = asked.GetValueOrDefault(field) ?? string.Empty;
        }

        var english = pick.Names.GetValueOrDefault(English);
        state.Head.Values[def.Create.Name] = string.IsNullOrWhiteSpace(english) ? pick.Modifier : english;
        foreach (var language in state.Languages.Where(l => !string.Equals(l, English, StringComparison.Ordinal)))
        {
            if (pick.Names.GetValueOrDefault(language) is { Length: > 0 } name && name.Trim().Length > 0)
            {
                if (!state.Head.Translations.TryGetValue(def.Create.Name, out var translations))
                {
                    state.Head.Translations[def.Create.Name] = translations = new Dictionary<string, string>(StringComparer.Ordinal);
                }

                translations[language] = name;
            }
        }

        var path = def.Create.Modifiers.Split('.');
        var levels = state.Head.Children[path[0]];
        if (pick.Boolean && levels.Count > 1)
        {
            levels.RemoveRange(1, levels.Count - 1);
        }

        foreach (var level in levels)
        {
            level.Values[path[1]] = ModifierLines.Insert(string.Empty, pick.Modifier, pick.Boolean ? "yes" : string.Empty);
        }

        return state;
    }
}
