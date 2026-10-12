using System.Text.RegularExpressions;
using ParadoxModTool.Generators.Editing;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators.Candidates;

/// <summary>
/// Eine Zeile der Kandidatenliste. <see cref="Key"/> ist der Modifier-Schlüssel, <see cref="Name"/> sein Anzeigename. <see cref="Group"/>: Kennung der ersten
/// passenden Gruppe in Paketreihenfolge, leer = "Allgemein". <see cref="Hidden"/>: die Gruppe ist ausgeblendet oder die eigene Regex trifft;
/// <see cref="NotAsIdea"/> steht daneben für sich. <see cref="StateKey"/>: der Arbeitsstand, in dem der Modifier schon steht.
/// </summary>
public sealed record CandidateRow(string Key, string Name, string Group, bool IsNew, bool Boolean, bool Hidden, bool NotAsIdea, string? StateKey);

/// <summary>Die Kandidatenliste eines Pakets (Abschnitt <c>candidates</c>): Modifier, die keine dynamische Idee benutzt, in Gruppen sortiert.</summary>
public static class CandidateList
{
    /// <summary>
    /// Die schon benutzten Modifier der Mod: jeder direkte Schlüssel der obersten Blöcke, die zu <see cref="CandidateUsedDef.Block"/> passen, in den Dateien,
    /// die zu <see cref="CandidateUsedDef.File"/> passen (relativer Pfad mit "/"), ohne <see cref="CandidateUsedDef.Except"/>. Auskommentierte Zeilen
    /// zählen nicht. Ohne Abschnitt oder Mod-Ordner ist die Menge leer; eine unlesbare Datei wirft <see cref="GeneratorOutputException"/>.
    /// </summary>
    public static IReadOnlySet<string> Used(GeneratorPackage package, string modRoot)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (package.Candidates is not { } candidates || !Directory.Exists(modRoot))
        {
            return used;
        }

        var rules = candidates.Used
            .Select(u => (File: new Regex(u.File, RegexOptions.CultureInvariant), Block: GeneratorStateReader.Whole(u.Block), Except: new HashSet<string>(u.Except, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        foreach (var path in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories)
                     .Select(f => Path.GetRelativePath(modRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var matching = rules.Where(r => r.File.IsMatch(path)).ToList();
            if (matching.Count == 0)
            {
                continue;
            }

            foreach (var statement in GeneratorStateReader.Parse(modRoot, path).Statements)
            {
                if (statement.Value is not ScriptBlock block)
                {
                    continue;
                }

                foreach (var rule in matching.Where(r => r.Block.IsMatch(statement.Key)))
                {
                    foreach (var child in block.Entries.OfType<ScriptStatement>().Where(c => !rule.Except.Contains(c.Key)))
                    {
                        used.Add(child.Key);
                    }
                }
            }
        }

        return used;
    }

    /// <summary>
    /// Modifier, die schon in einem Arbeitsstand des Pakets stehen → Schlüssel des ersten Stands (Schlüsselreihenfolge ordinal). Gelesen wird das Feld
    /// <see cref="CandidateCreateDef.Modifiers"/> (<c>Ebene.Feld</c>) jedes Elements dieser obersten Ebene in jedem gespeicherten Stand.
    /// </summary>
    public static IReadOnlyDictionary<string, string> InWorkStates(GeneratorPackage package)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (package.Candidates?.Create.Modifiers.Split('.') is not [var level, var field])
        {
            return result;
        }

        foreach (var key in GeneratorStateStore.Keys(package.DataFolder).OrderBy(k => k, StringComparer.Ordinal))
        {
            // Ein unlesbarer Stand (gesperrt, kein Zugriff) wird übersprungen; die übrigen Stände markieren weiter.
            GeneratorState? state;
            try
            {
                state = GeneratorStateStore.Load(package.DataFolder, key);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (state is null || !state.Head.Children.TryGetValue(level, out var elements))
            {
                continue;
            }

            foreach (var element in elements)
            {
                if (element.Values.TryGetValue(field, out var text))
                {
                    foreach (var modifier in ModifierLines.Keys(text))
                    {
                        result.TryAdd(modifier, key);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Jede Quelle, die nicht in <paramref name="used"/> steht. Reihenfolge: "Allgemein" zuerst, dann die Gruppen in Paketreihenfolge; in einer Gruppe
    /// zuerst die neuen, dann nach Anzeigename (ohne Groß-/Kleinschreibung), dann nach Schlüssel. Eine ungültige eigene Regex wird übergangen.
    /// </summary>
    public static IReadOnlyList<CandidateRow> Build(CandidatesDef def, IReadOnlyList<CandidateSource> sources, IReadOnlySet<string> used,
        IReadOnlyDictionary<string, string> inStates, CandidateSettings settings, Func<string, string?> displayName)
    {
        var groups = def.Groups.Select(g => (Def: g, Pattern: Compile(g.Pattern))).ToList();
        var custom = Compile(settings.CustomHide);
        var notAsIdea = new HashSet<string>(settings.NotAsIdea, StringComparer.OrdinalIgnoreCase);
        var rows = new List<(CandidateRow Row, int Order)>();
        foreach (var source in sources.Where(s => !used.Contains(s.Name)))
        {
            var index = groups.FindIndex(g => Matches(g.Pattern, source.Name));
            var groupHidden = index >= 0 && (settings.HiddenGroups.TryGetValue(groups[index].Def.Id, out var hidden) ? hidden : groups[index].Def.Hidden);
            var row = new CandidateRow(
                source.Name,
                displayName(source.Name) is { Length: > 0 } name ? name : source.Name,
                index >= 0 ? groups[index].Def.Id : string.Empty,
                source.IsNew,
                source.Boolean,
                groupHidden || Matches(custom, source.Name),
                notAsIdea.Contains(source.Name),
                inStates.TryGetValue(source.Name, out var stateKey) ? stateKey : null);
            rows.Add((row, index));
        }

        return rows
            .OrderBy(r => r.Order)
            .ThenByDescending(r => r.Row.IsNew)
            .ThenBy(r => r.Row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.Row.Key, StringComparer.Ordinal)
            .Select(r => r.Row)
            .ToList();
    }

    /// <summary>Zeitlimit für Gruppen- und eigene Regex: eine Paket- oder Nutzer-Regex mit Backtracking-Explosion darf das Fenster nicht einfrieren.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Trifft die Regex? Ohne Regex oder bei Zeitüberschreitung gilt: kein Treffer.</summary>
    public static bool Matches(Regex? regex, string text)
    {
        try
        {
            return regex?.IsMatch(text) == true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Die Regex (Suche, nicht ganzer Treffer); leer oder ungültig = <see langword="null"/>.</summary>
    private static Regex? Compile(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
