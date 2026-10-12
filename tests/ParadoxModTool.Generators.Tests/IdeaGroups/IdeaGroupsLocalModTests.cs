using System.Text;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests.IdeaGroups;

/// <summary>
/// Optionaler Abgleich mit allen Ideengruppen einer echten IV2-Mod (Spec 2026-10-06-generator-packages-idea-groups, Tests): läuft nur mit
/// <c>PMT_IV2_IG_MOD</c> (Ordner der Mod), sonst endet er sofort. Jede Gruppe wird eingelesen und in einen Temp-Ausgabeordner neu geplant; jede
/// geplante Datei wird mit der Datei der Mod verglichen: gleich, nur Format (Leerraum, Kommentare) oder verschieden. In die Mod wird nie geschrieben.
/// Mit <c>PMT_IV2_IG_REPORT</c> schreibt der Test den ganzen Bericht in diese Datei. Hart geprüft wird nur, dass jede Gruppe ohne Fehler eingelesen und
/// geplant wird; inhaltliche Abweichungen stehen im Bericht (die Mod ist uneinheitlich und ändert sich laufend, eine feste Erlaubt-Liste veraltete sofort).
/// </summary>
public sealed class IdeaGroupsLocalModTests
{
    [Fact]
    public void All_groups_of_the_local_mod_roundtrip()
    {
        var mod = Environment.GetEnvironmentVariable("PMT_IV2_IG_MOD");
        if (string.IsNullOrEmpty(mod) || !Directory.Exists(mod))
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-idea-groups"), temp.PathOf("data"), shipped: true, uiLanguage: "en");
        var languages = Directory.GetDirectories(Path.Combine(mod, "main_menu", "localization")).Select(d => Path.GetFileName(d)!).ToList();
        var report = new StringBuilder();
        var errors = new List<string>();
        var counts = new Dictionary<string, int> { ["same"] = 0, ["format"] = 0, ["differs"] = 0, ["new"] = 0 };
        foreach (var key in GeneratorStateReader.Keys(package, mod))
        {
            GeneratorPlan plan;
            try
            {
                var state = GeneratorStateReader.Read(package, mod, key, languages)!.State;
                plan = GeneratorPlanner.Plan(package, state, new GeneratorTarget(mod, temp.PathOf("out/" + key), languages));
            }
            catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException)
            {
                errors.Add(key + ": " + ex.Message);
                continue;
            }

            foreach (var file in plan.Files.Where(f => f.NewText is not null))
            {
                var original = file.ModBaseText ?? file.OriginalText;
                var verdict = original is null ? "new"
                    : original == file.NewText ? "same"
                    : GeneratorCheck.SameContent(file.RelativePath, original, file.NewText!) ? "format"
                    : "differs";
                counts[verdict]++;
                if (verdict is "same")
                {
                    continue;
                }

                report.Append(key).Append(' ').Append(verdict).Append(' ').Append(file.RelativePath).Append('\n');
                if (verdict is "differs" or "new")
                {
                    report.Append(FirstDifference(original ?? string.Empty, file.NewText!)).Append('\n');
                }
            }
        }

        report.Insert(0, $"same {counts["same"]}, format only {counts["format"]}, differs {counts["differs"]}, new {counts["new"]}\n"
            + string.Concat(errors.Select(e => "error " + e + "\n")));
        if (Environment.GetEnvironmentVariable("PMT_IV2_IG_REPORT") is { Length: > 0 } reportPath)
        {
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        }

        Assert.Empty(errors);
    }

    /// <summary>Erste inhaltlich abweichende Zeile (Leerzeilen und Einrückung zählen nicht), Tabs sichtbar.</summary>
    private static string FirstDifference(string expected, string actual)
    {
        var a = expected.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
        var b = actual.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end>";
            var right = i < b.Length ? b[i] : "<end>";
            if (left != right)
            {
                return $"    line {i + 1}\n    mod: {Show(left)}\n    pmt: {Show(right)}";
            }
        }

        return "    (no line difference)";
    }

    private static string Show(string line) => line.Replace("\t", "→").Length > 200 ? line.Replace("\t", "→")[..200] : line.Replace("\t", "→");
}
