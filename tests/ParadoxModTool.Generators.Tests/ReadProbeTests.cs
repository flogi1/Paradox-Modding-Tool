using System.Security.Cryptography;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>"Probelauf" der Einlesen-Maske (Spec Teil 5b, Abschnitt 1) mit dem IV2-Paket und dem geschriebenen Kunstset TST.</summary>
public sealed class ReadProbeTests : IDisposable
{
    private static readonly string[] Languages = { "english", "german" };

    private readonly Iv2Fixture _fixture = new();
    private readonly PackageDocument _document;
    private readonly PackageFiles _files;

    public ReadProbeTests()
    {
        var folder = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2");
        _document = PackageDocument.Load(folder);
        _files = new PackageFiles(folder);
        SlicePackage.Write(_fixture.Plan(_fixture.State(), _fixture.FreshMod));
    }

    public void Dispose() => _fixture.Dispose();

    private string DataFolder => _fixture.Temp.PathOf("data/iv2");

    private ReadProbeResult Run(string key = "TST") => ReadProbe.Run(_document, _files, null, _fixture.FreshMod, key, Languages, DataFolder);

    [Fact]
    public void Keys_lists_written_key()
    {
        var (keys, error) = ReadProbe.Keys(_document, _files, null, _fixture.FreshMod, DataFolder);

        Assert.Null(error);
        Assert.Equal(new[] { "TST" }, keys);
    }

    [Fact]
    public void Probe_counts_matches_per_pattern()
    {
        var result = Run();

        Assert.Null(result.Error);
        var keys = result.Patterns.Single(p => p.Where == "read.keys");
        Assert.Equal(1, keys.Count);
        Assert.Equal("key=TST", keys.Examples[0].Captures);
        var text = File.ReadAllText(Path.Combine(_fixture.FreshMod, "main_menu/common/static_modifiers/02_IV2_TST.txt"));
        var blocks = System.Text.RegularExpressions.Regex.Matches(text, @"^national_idea_modifier_TST_\d+(_\d)?\s*=", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        var elements = result.Patterns.Single(p => p.Where == "read.elements.block");
        Assert.Equal(blocks, elements.Count);
        Assert.Contains("number=", elements.Examples[0].Captures);
        Assert.True(elements.Examples.Count <= 10);
        Assert.True(result.Patterns.Single(p => p.Where == "read.localization[1].key").Count > 0);
        Assert.Equal(1, result.Patterns.Single(p => p.Where == "read.assets[1].file").Count);
    }

    [Fact]
    public void Probe_reports_pattern_without_matches()
    {
        _document.Read!.Flags.Items[0].Set("block", @"nothing_{{ tag }}_(?<number>\d+)");

        var flag = Run().Patterns.Single(p => p.Where == "read.flags[1].block");

        Assert.Equal(0, flag.Count);
        Assert.Empty(flag.Examples);
    }

    [Fact]
    public void Probe_state_text_lists_idea_names()
    {
        var first = _fixture.State().Head.Children["categories"][0].Children["ideas"][0].Values["name"];

        var result = Run();

        Assert.Contains("name = " + first, result.StateText);
        Assert.Contains("tag = TST", result.StateText);
    }

    [Fact]
    public void Probe_missing_elements_file_is_error()
    {
        var result = Run("XYZ");

        Assert.NotNull(result.Error);
        Assert.Contains("02_IV2_XYZ.txt", result.Error);
    }

    [Fact]
    public void Probe_load_error_is_error()
    {
        _document.KeyField = "nope";

        var result = Run();

        Assert.NotNull(result.Error);
        Assert.Empty(result.Patterns);
    }

    [Fact]
    public void Probe_unknown_name_in_pattern_is_reported_not_thrown()
    {
        _document.Read!.ElementsBlock = @"x_{{ tagg }}_(?<number>\d+)";

        var result = Run();

        Assert.True(result.Error is not null || result.Patterns.Single(p => p.Where == "read.elements.block").Problem is not null);
    }

    [Fact]
    public void Probe_writes_nothing()
    {
        var before = Hashes(_fixture.FreshMod);

        Run();

        Assert.Equal(before, Hashes(_fixture.FreshMod));
    }

    [Fact]
    public void Describe_indents_levels_and_translations()
    {
        var package = _fixture.Package;
        var state = _fixture.State();

        var text = ReadProbe.Describe(package, state);

        Assert.Contains("\ncategories 1\n", "\n" + text);
        Assert.Contains("\n  ideas 1\n", text);
        Assert.Contains("\n    levels 1\n", text);
    }

    private static Dictionary<string, string> Hashes(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
}
