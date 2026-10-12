using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Ai;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Befehle von pmt-generator.exe (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 3).</summary>
public sealed class GeneratorCliTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Package() => AiGuideTests.CopyTo(_temp, AiGuideTests.ExamplePackage, "stadtwappen");

    private string Mod() => AiGuideTests.CopyTo(_temp, AiGuideTests.ExampleMod, "mod");

    private static void SetReadKeys(string package, string keys)
    {
        var path = Path.Combine(package, "package.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["read"]!["keys"] = keys;
        File.WriteAllText(path, json.ToJsonString());
    }

    [Fact]
    public void Check_example_is_ok()
    {
        var result = GeneratorCli.Check(Package());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("OK", result.Lines[^1]);
    }

    [Fact]
    public void Check_reports_issue_lines()
    {
        var package = Package();
        SetReadKeys(package, "x");

        var result = GeneratorCli.Check(package);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("read.keys: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_without_package_json_is_one_line()
    {
        var result = GeneratorCli.Check(_temp.PathOf("empty"));

        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Lines);
    }

    [Fact]
    public void Probe_lists_keys()
    {
        var result = GeneratorCli.Probe(Package(), Mod(), null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "HAM" }, result.Lines);
    }

    [Fact]
    public void Probe_with_key_reports_patterns()
    {
        var result = GeneratorCli.Probe(Package(), Mod(), "HAM");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("read.keys: 1 match(es)", result.Lines);
        Assert.Contains("--- state ---", result.Lines);
        Assert.Contains(result.Lines, l => l.Contains("name = Hanseatic pride", StringComparison.Ordinal));
    }

    [Fact]
    public void Roundtrip_example_is_same()
    {
        var result = GeneratorCli.Roundtrip(Package(), Mod(), "HAM", null, null);

        Assert.True(result.ExitCode == 0, string.Join("\n", result.Lines));
        Assert.DoesNotContain(result.Lines, l => l.StartsWith("differs", StringComparison.Ordinal) || l.StartsWith("new", StringComparison.Ordinal));
        Assert.Contains(result.Lines, l => l == "same in_game/common/static_modifiers/sw_HAM.txt");
    }

    [Fact]
    public void Roundtrip_iv2_tst_is_same()
    {
        using var fixture = new Iv2.Iv2Fixture();
        SlicePackage.Write(fixture.Plan(fixture.State(), fixture.FreshMod));
        var package = AiGuideTests.CopyTo(_temp, Path.Combine(Iv2.Iv2Fixture.RepoRoot, "generator-packages", "iv2"), "iv2");

        var result = GeneratorCli.Roundtrip(package, fixture.FreshMod, "TST", null, null);

        Assert.True(result.ExitCode == 0, string.Join("\n", result.Lines));
    }

    [Fact]
    public void Roundtrip_changed_template_differs()
    {
        var package = Package();
        var template = Path.Combine(package, "templates", "setup.sbn");
        File.WriteAllText(template, "has_tag = {{ tag }}\n");

        var result = GeneratorCli.Roundtrip(package, Mod(), "HAM", null, null);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("differs in_game/common/scripted_triggers/sw_setup.txt", result.Lines);
        Assert.Contains(result.Lines, l => l.StartsWith("  + ", StringComparison.Ordinal) && l.Contains("has_tag = HAM", StringComparison.Ordinal));
    }

    [Fact]
    public void Roundtrip_without_read_is_error()
    {
        var package = Package();
        var path = Path.Combine(package, "package.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json.Remove("read");
        File.WriteAllText(path, json.ToJsonString());

        var result = GeneratorCli.Roundtrip(package, Mod(), "HAM", null, null);

        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Lines);
    }

    [Fact]
    public void Roundtrip_keep_writes_files_and_leaves_mod_alone()
    {
        var mod = Mod();
        var before = File.ReadAllText(Path.Combine(mod, "in_game/common/scripted_triggers/sw_setup.txt"));
        var keep = _temp.PathOf("keep");

        GeneratorCli.Run(new[] { "roundtrip", Package(), mod, "HAM", "--keep", keep, "--languages", "english" });

        Assert.True(File.Exists(Path.Combine(keep, "in_game/common/static_modifiers/sw_HAM.txt")));
        Assert.Equal(before, File.ReadAllText(Path.Combine(mod, "in_game/common/scripted_triggers/sw_setup.txt")));
    }

    [Fact]
    public void ModLanguages_come_from_localization_folders()
    {
        Assert.Equal(new[] { "english", "german" }, GeneratorCli.ModLanguages(Mod()));
    }

    [Fact]
    public void Check_mod_and_take_over_work_with_countable_top_level()
    {
        var package = GeneratorPackageLoader.Load(Package());
        var mod = Mod();
        var languages = new[] { "english", "german" };

        var row = Assert.Single(GeneratorCheck.Run(package, mod, languages));
        var takeOver = GeneratorStateReader.TakeOver(package, mod, new[] { "HAM" }, _temp.PathOf("target"), languages);

        Assert.Equal("HAM", row.Key);
        Assert.Equal((0, 0), (row.Missing, row.Changed));
        Assert.Equal(2, Assert.Single(takeOver.States).Head.Children["ideas"].Count);
        Assert.NotNull(GeneratorCheck.WithoutUnreferenced(package, row));
    }

    [Fact]
    public void Run_unknown_command_is_usage()
    {
        Assert.Equal(2, GeneratorCli.Run(new[] { "frobnicate" }).ExitCode);
        Assert.Equal(2, GeneratorCli.Run(Array.Empty<string>()).ExitCode);
        Assert.Equal(0, GeneratorCli.Run(new[] { "check", Package() }).ExitCode);
    }
}
