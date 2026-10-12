using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>
/// Optionaler Abgleich mit einem Tag einer echten IV2-Mod (Spec Teil 2, lokaler BYZ-Lauf): läuft nur mit <c>PMT_IV2_BYZ_DIR</c> (Ausgabe von
/// <c>byz_from_mod.py</c>), sonst endet er sofort. Daten der Mod kommen nicht ins Repo.
/// </summary>
public sealed class Iv2LocalModTests
{
    private readonly ITestOutputHelper _output;

    public Iv2LocalModTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Mod_tag_matches_python()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_IV2_BYZ_DIR");
        if (string.IsNullOrEmpty(folder) || !File.Exists(Path.Combine(folder, "state.json")))
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2"), temp.PathOf("data/iv2"), shipped: true, uiLanguage: "en");
        var state = System.Text.Json.JsonSerializer.Deserialize<GeneratorState>(
            File.ReadAllText(Path.Combine(folder, "state.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        Bundle.Unpack(Path.Combine(folder, "Base.bundle"), temp.PathOf("mod"));

        var plan = GeneratorPlanner.Plan(package, state, new GeneratorTarget(temp.PathOf("mod"), null));

        var expected = Bundle.Read(Path.Combine(folder, "Expected-existing.bundle"));
        var failures = new List<string>();
        foreach (var (file, text) in expected)
        {
            try
            {
                Iv2PackageTests.Compare(file, text, plan);
            }
            catch (Exception ex) when (ex is Xunit.Sdk.XunitException)
            {
                failures.Add(ex.Message);
            }
        }

        _output.WriteLine($"{expected.Count} Dateien verglichen, {failures.Count} abweichend.");
        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    private static readonly string[] Eu5Languages = { "english", "french", "german", "spanish", "polish", "russian", "braz_por", "japanese", "korean", "simp_chinese", "turkish" };

    /// <summary>Teil 4: PMT-Einleser und Python-Einleser liefern für den Tag dieselben Ideen, Stufen, Namen, Beschreibungen und Modifikatoren.</summary>
    [Fact]
    public void Pmt_reader_matches_python_reader()
    {
        if (LocalRun() is not { } run)
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2"), temp.PathOf("data/iv2"), shipped: true, uiLanguage: "en");
        var pmt = GeneratorStateReader.Read(package, run.Mod, run.Python.Key, Eu5Languages)!.State;

        for (var group = 0; group < 4; group++)
        {
            var want = run.Python.Head.Children["categories"][group].Children["ideas"];
            var have = pmt.Head.Children["categories"][group].Children["ideas"];
            Assert.Equal(want.Count, have.Count);
            for (var i = 0; i < want.Count; i++)
            {
                Assert.Equal(want[i].Values["name"], have[i].Values["name"]);
                Assert.Equal(want[i].Values["desc"], have[i].Values["desc"]);
                Assert.Equal(want[i].Children["levels"].Count, have[i].Children["levels"].Count);
                Assert.Equal(
                    want[i].Children["levels"].Select(l => Lines(l.Values["modifiers"])),
                    have[i].Children["levels"].Select(l => Lines(l.Values["modifiers"])));
            }
        }

        _output.WriteLine($"{run.Python.Key}: {pmt.Head.Children["categories"].Sum(c => c.Children["ideas"].Count)} Ideen gleich gelesen.");
    }

    /// <summary>Teil 4: "Mod prüfen" auf der Kopie der echten Mod; der Bericht geht in die Testausgabe.</summary>
    [Fact]
    public void Check_report_for_mod()
    {
        if (LocalRun() is not { } run)
        {
            return;
        }

        using var temp = new TempFolder();
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2"), temp.PathOf("data/iv2"), shipped: true, uiLanguage: "en");
        var rows = GeneratorCheck.Run(package, run.Mod, Eu5Languages);

        foreach (var row in rows)
        {
            _output.WriteLine($"{row.Key}: fehlt {row.Missing}, weicht ab {row.Changed}, gleich {row.Equal}, nur Format {row.FormatOnly}, Alt-Einträge {row.LegacyRemoved}, " +
                $"ungenutzt [{string.Join(", ", row.Unreferenced.Select(b => b.BlockKey + (b.Removable ? string.Empty : " (Mitte)")))}], Fehler {row.Error ?? "-"}");
        }

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Null(r.Error));
    }

    /// <summary>Ausgabe von byz_from_mod.py: Python-Stand und Pfad der Mod-Kopie; ohne Umgebungsvariable <see langword="null"/>.</summary>
    private static (GeneratorState Python, string Mod)? LocalRun()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_IV2_BYZ_DIR");
        if (string.IsNullOrEmpty(folder) || !File.Exists(Path.Combine(folder, "state.json")) || !File.Exists(Path.Combine(folder, "mod.txt")))
        {
            return null;
        }

        var state = System.Text.Json.JsonSerializer.Deserialize<GeneratorState>(
            File.ReadAllText(Path.Combine(folder, "state.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        return (state, File.ReadAllText(Path.Combine(folder, "mod.txt")).Trim());
    }

    private static string Lines(string text) =>
        string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(l => System.Text.RegularExpressions.Regex.Replace(l.Trim(), @"\s+", " ")).Where(l => l.Length > 0));
}
