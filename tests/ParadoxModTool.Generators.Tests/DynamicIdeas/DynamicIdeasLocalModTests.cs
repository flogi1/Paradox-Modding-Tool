using ParadoxModTool.Generators.Candidates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.Generators.Tests.Iv2;
using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Generators.Tests.DynamicIdeas;

/// <summary>
/// Optionaler Abgleich mit allen dynamischen Nationalideen einer echten IV2-Mod (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, Cross-checks
/// "Roundtrip"): läuft nur mit <c>PMT_IV2_DYN_MOD</c> (Ordner der Mod), sonst endet er sofort. Jede Idee wird eingelesen und in einen Temp-Ausgabeordner
/// neu geplant; jede geplante Datei wird mit der Datei der Mod verglichen: gleich, nur Format (Leerraum, Kommentare), verschieden oder neu. In die Mod
/// wird nie geschrieben. Mit <c>PMT_IV2_DYN_REPORT</c> schreibt der Test den ganzen Bericht in diese Datei: oben die Zählung, dann je Datei der Mod die
/// Zählung nach Urteil und die Ideen mit "differs" oder "new" (je Datei der Mod gruppiert, also nach Ort),
/// dann je Idee die abweichenden Dateien mit der ersten abweichenden Zeile. Mit <c>PMT_IV2_DYN_TAGS</c> (die KI-Tag-Datei des Tag-Editors) liest und plant der Test
/// auch die KI-Tags (Ort 21) gegen eine Kopie dieser Datei; ein unveränderter Eintrag zählt als "same" der Datei <c>extern/iv2_ai_tags.json</c>.
/// Hart geprüft wird nur, dass jede Idee ohne Fehler eingelesen und geplant wird; inhaltliche Abweichungen stehen im Bericht (die bekannten
/// Unregelmäßigkeiten der Mod listet die Spec).
/// </summary>
public sealed class DynamicIdeasLocalModTests
{
    private static readonly string[] Verdicts = { "same", "format", "differs", "new" };

    [Fact]
    public void All_ideas_of_the_local_mod_roundtrip()
    {
        var mod = Environment.GetEnvironmentVariable("PMT_IV2_DYN_MOD");
        if (string.IsNullOrEmpty(mod) || !Directory.Exists(mod))
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), temp.PathOf("data"), shipped: true, uiLanguage: "en");
        var languages = Directory.GetDirectories(Path.Combine(mod, "main_menu", "localization")).Select(d => Path.GetFileName(d)!).ToList();
        var details = new StringBuilder();
        var errors = new List<string>();
        var counts = Verdicts.ToDictionary(v => v, _ => 0);
        // Je Datei der Mod: Zählung nach Urteil
        var perFile = new SortedDictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        // Je Datei: Zahl der Urteile "Nur Format", bei denen sich Kommentarzeilen unterscheiden
        var commentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        // Je Datei: die Ideen mit "differs" oder "new", in Lesereihenfolge
        var deviating = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        // KI-Tag-Datei (Ort 21): mit PMT_IV2_DYN_TAGS eine Kopie im Temp-Ordner, die das Paket wie im Fenster über externals.json findet; die echte Datei
        // wird nie beschrieben. Ohne die Variable ist nur der Hinweis "keine Datei gewählt" von read.json erwartet.
        string? expectedNote = null;
        var tagSource = Environment.GetEnvironmentVariable("PMT_IV2_DYN_TAGS");
        if (!string.IsNullOrEmpty(tagSource) && File.Exists(tagSource))
        {
            var copy = temp.PathOf("tags/iv2_ai_tags.json");
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(tagSource, copy);
            ExternalPathStore.Set(temp.PathOf("data"), "aiTags", copy);
        }
        else
        {
            expectedNote = GeneratorsTexts.ReadJsonNoFile(package.Externals["aiTags"].Label);
        }

        var externals = ExternalPathStore.Load(temp.PathOf("data"));
        var keys = GeneratorStateReader.Keys(package, mod);
        var renderer = new GeneratorRenderer(package);
        var withoutValueCount = 0;
        foreach (var key in keys)
        {
            GeneratorPlan plan;
            try
            {
                // Ohne Ergebnis oder mit Hinweisen des Einlesers (übersprungene Nummern, fehlende Dateien) ist die Idee nicht sauber eingelesen
                if (GeneratorStateReader.Read(package, mod, key, languages, externals) is not { } result)
                {
                    errors.Add(key + ": no read result");
                    continue;
                }

                errors.AddRange(result.Notes.Where(n => n != expectedNote).Select(n => key + ": read note: " + n));
                // Die Regel "Modifier ohne Wert" der Formularprüfung darf keine eingelesene Idee treffen (einzige Fehlerart an einem Modifikatorfeld)
                var withoutValue = FormValidator.Check(package, result.State, renderer)
                    .Where(i => i.Severity == IssueSeverity.Error && i.Path is not null && i.Path.EndsWith(".modifiers", StringComparison.Ordinal)).ToList();
                withoutValueCount += withoutValue.Count;
                errors.AddRange(withoutValue.Select(i => key + ": " + i.Path + ": " + i.Message));
                plan = GeneratorPlanner.Plan(package, result.State, new GeneratorTarget(mod, temp.PathOf("out/" + key), languages, externals));
            }
            catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException)
            {
                errors.Add(key + ": " + ex.Message);
                continue;
            }

            foreach (var file in plan.Files.Where(f => f.NewText is not null))
            {
                var original = file.ModBaseText ?? file.OriginalText;
                // Die KI-Tag-Datei ist JSON: nur gleich oder verschieden (ein unveränderter Eintrag lässt die ganze Datei bytegleich)
                var verdict = original is null ? "new"
                    : original == file.NewText ? "same"
                    : file.ExternalPath is null && GeneratorCheck.SameContent(file.RelativePath, original, file.NewText!) ? "format"
                    : "differs";
                counts[verdict]++;
                if (!perFile.TryGetValue(file.RelativePath, out var fileCounts))
                {
                    perFile[file.RelativePath] = fileCounts = Verdicts.ToDictionary(v => v, _ => 0);
                }

                fileCounts[verdict]++;
                if (verdict is "differs" or "new")
                {
                    if (!deviating.TryGetValue(file.RelativePath, out var ids))
                    {
                        deviating[file.RelativePath] = ids = new List<string>();
                    }

                    ids.Add($"{key} ({verdict})");
                    details.Append(key).Append(' ').Append(verdict).Append(' ').Append(file.RelativePath).Append('\n');
                    details.Append(FirstDifference(original ?? string.Empty, file.NewText!, withComments: false)).Append('\n');
                }
                else if (verdict is "format" && FirstDifference(original!, file.NewText!, withComments: true) is var comments && comments != NoDifference)
                {
                    // Nur Format, aber mit anderen Kommentarzeilen: eigens gelistet, damit kein Kommentar der Mod unbemerkt verloren geht
                    commentCounts[file.RelativePath] = commentCounts.GetValueOrDefault(file.RelativePath) + 1;
                    details.Append(key).Append(" comments ").Append(file.RelativePath).Append('\n').Append(comments).Append('\n');
                }
            }
        }

        var report = new StringBuilder();
        report.Append($"keys {keys.Count}: same {counts["same"]}, format only {counts["format"]}, differs {counts["differs"]}, new {counts["new"]}\n");
        report.Append($"modifier lines without value: {withoutValueCount}\n");
        report.Append(string.Concat(errors.Select(e => "error " + e + "\n")));
        report.Append('\n');
        foreach (var (path, fileCounts) in perFile)
        {
            report.Append(path).Append(": ").Append(string.Join(", ", Verdicts.Where(v => fileCounts[v] > 0).Select(v => $"{v} {fileCounts[v]}")))
                .Append(commentCounts.TryGetValue(path, out var withComments) ? $" (format with other comments {withComments})" : string.Empty).Append('\n');
            if (deviating.TryGetValue(path, out var ids))
            {
                report.Append("    ").Append(string.Join(", ", ids)).Append('\n');
            }
        }

        report.Append('\n').Append(details);
        if (Environment.GetEnvironmentVariable("PMT_IV2_DYN_REPORT") is { Length: > 0 } reportPath)
        {
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        }

        Assert.Empty(errors);
    }

    /// <summary>
    /// Die benutzten Modifier der echten Mod (Kandidatenliste): die Zahl liegt nahe an der Erhebung (483), jeder Modifier des Blocks
    /// <c>national_idea_modifier_army_1</c> ist darin, <c>game_data</c> nicht. Mit <c>PMT_IV2_DYN_REPORT</c> kommt die Zeilenzahl je Gruppe in die Datei <c>&lt;PMT_IV2_DYN_REPORT&gt;.candidates.txt</c>,
    /// gebaut aus den Modifiern der Skript-Datenbank (<c>PMT_IV2_DYN_SCRIPTDOCS</c> = <c>entries.json</c>, Art "Modifier", Scope Country); ohne die
    /// Variable entfällt dieser Teil. Die Zahlen werden nicht geprüft. In die Mod wird nie geschrieben.
    /// </summary>
    [Fact]
    public void Candidates_used_set_matches_live_mod()
    {
        var mod = Environment.GetEnvironmentVariable("PMT_IV2_DYN_MOD");
        if (string.IsNullOrEmpty(mod) || !Directory.Exists(mod))
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), temp.PathOf("data"), shipped: true, uiLanguage: "en");

        var used = CandidateList.Used(package, mod);

        Assert.InRange(used.Count, 450, 520);
        Assert.DoesNotContain("game_data", used);
        var army = ScriptDocument.Parse(File.ReadAllText(Path.Combine(mod, "main_menu", "common", "static_modifiers", "01_IV2_dynamic_national_ideas_army.txt"))).Entries.OfType<ScriptStatement>()
            .Single(s => s.Key == "national_idea_modifier_army_1");
        var keys = ((ScriptBlock)army.Value).Entries.OfType<ScriptStatement>().Select(s => s.Key).Where(k => k != "game_data").ToList();
        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.Contains(k, used));

        var report = new StringBuilder($"candidates: used {used.Count}\n");
        if (Environment.GetEnvironmentVariable("PMT_IV2_DYN_SCRIPTDOCS") is { Length: > 0 } docsPath && File.Exists(docsPath))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(docsPath));
            var entries = json.RootElement.EnumerateArray().Where(e => e.GetProperty("Kind").GetString() == "Modifier").ToList();
            var latest = entries.Count == 0 ? 0 : entries.Max(e => e.GetProperty("FirstSeenImport").GetInt32());
            var docs = entries.Select(e => (
                e.GetProperty("Name").GetString()!,
                (IReadOnlyList<string>)e.GetProperty("Scopes").EnumerateArray().Select(s => s.GetString()!).ToList(),
                e.GetProperty("FirstSeenImport").GetInt32() == latest && latest > 1));
            var sources = CandidateSources.Merge(package.Candidates!.Category, Array.Empty<(string, string, bool)>(), docs);
            var rows = CandidateList.Build(package.Candidates, sources, used, new Dictionary<string, string>(), new CandidateSettings(), _ => null);
            report.Append($"candidates: sources {sources.Count}, rows {rows.Count}\n");
            foreach (var group in rows.GroupBy(r => r.Group).OrderBy(g => g.Key.Length == 0 ? -1 : package.Candidates.Groups.Select(x => x.Id).ToList().IndexOf(g.Key)))
            {
                report.Append($"candidates: group {(group.Key.Length == 0 ? "(general)" : group.Key)}: {group.Count()} rows, {group.Count(r => r.Hidden)} hidden\n");
            }
        }

        if (Environment.GetEnvironmentVariable("PMT_IV2_DYN_REPORT") is { Length: > 0 } reportPath)
        {
            // Eigene Datei: der Roundtrip-Test überschreibt die Berichtsdatei selbst
            File.WriteAllText(reportPath + ".candidates.txt", report.ToString(), new UTF8Encoding(false));
        }
    }

    private const string NoDifference = "    (no line difference)";

    /// <summary>
    /// Erste abweichende Zeile, Tabs sichtbar. Leerzeilen, Einrückung und Leerraum innerhalb der Zeile zählen nicht; Kommentarzeilen nur mit
    /// <paramref name="withComments"/>.
    /// </summary>
    private static string FirstDifference(string expected, string actual, bool withComments)
    {
        string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n').Select(l => Regex.Replace(l.Trim(), @"\s+", " "))
            .Where(l => l.Length > 0 && (withComments || !l.StartsWith('#'))).ToArray();
        var a = Lines(expected);
        var b = Lines(actual);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end>";
            var right = i < b.Length ? b[i] : "<end>";
            if (left != right)
            {
                return $"    line {i + 1}\n    mod: {Show(left)}\n    pmt: {Show(right)}";
            }
        }

        return NoDifference;
    }

    private static string Show(string line) => line.Replace("\t", "→").Length > 200 ? line.Replace("\t", "→")[..200] : line.Replace("\t", "→");
}
