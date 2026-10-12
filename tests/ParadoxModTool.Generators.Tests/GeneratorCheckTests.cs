using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// "Mod prüfen" (Spec Teil 4): Stand frisch aus der Mod lesen, planen ohne zu schreiben, ungenutzte Blöcke finden. Kleines Paket: Blöcke in
/// <c>m_&lt;key&gt;.txt</c> (geprüft), Verweise in <c>ga_&lt;key&gt;.txt</c> und in einer Basisdatei <c>base.txt</c>.
/// </summary>
public sealed class GeneratorCheckTests : IDisposable
{
    private static readonly string[] Languages = { "english" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private GeneratorPackage Package()
    {
        _temp.Write("package/templates/m.sbn",
            "{{ for c in categories }}{{ for i in c.ideas }}{{ for l in i.levels }}m_{{ tag }}_{{ i.number }}{{ l.suffix }} = {\n\tgame_data = { category = country }\n}\n{{ end }}{{ end }}{{ end }}");
        _temp.Write("package/templates/ga.sbn",
            "{{ for c in categories }}{{ for i in c.ideas }}{{ for l in i.levels }}ga_{{ tag }}_{{ i.number }}{{ l.suffix }} = { modifier = m_{{ tag }}_{{ i.number }}{{ l.suffix }} }\n{{ end }}{{ end }}{{ end }}");
        _temp.Write("package/templates/base_skeleton.sbn", "list = {\n}\n");
        var outputs = new[]
        {
            new OutputDef("m", OutputKind.SyncBlocks, "m_{{ tag }}.txt", Template: "m.sbn", KeyPattern: "m_{{ tag }}_*", Keep: new[] { "game_data" }, CheckUnreferenced: true),
            new OutputDef("ga", OutputKind.OwnFile, "ga_{{ tag }}.txt", Template: "ga.sbn"),
            new OutputDef("base", OutputKind.EnsureLine, "base.txt", Block: "list", Line: "tag = {{ tag }}", Skeleton: "base_skeleton.sbn"),
        };
        var read = new ReadDef(
            @"^m_(?<key>[A-Z]{3})\.txt$",
            new ReadElementsDef("m_{{ tag }}.txt", @"m_{{ tag }}_(?<number>\d+)(?<suffix>_\d)?", "levels", new Dictionary<string, ReadFieldDef>()),
            Array.Empty<ReadLocalizationDef>(), Array.Empty<ReadFlagDef>(), Array.Empty<ReadAssetDef>());
        return SamplePackages.Ideas(folder: _temp.PathOf("package"), outputs: outputs, read: read);
    }

    private string Mod => _temp.PathOf("mod");

    private void ModFile(string relative, string text) => _temp.Write("mod/" + relative, text);

    private static string Blocks(params string[] keys) => string.Concat(keys.Select(k => $"{k} = {{\n\tgame_data = {{ category = country }}\n}}\n"));

    private static string Ga(params string[] keys) => string.Concat(keys.Select(k => $"ga_{k[2..]} = {{ modifier = {k} }}\n"));

    [Fact]
    public void Unreferenced_trailing_level_is_found_and_removed()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1", "m_AAA_1_1", "m_AAA_1_2"));
        ModFile("ga_AAA.txt", Ga("m_AAA_1", "m_AAA_1_1"));

        var row = Assert.Single(GeneratorCheck.Run(Package(), Mod, Languages));

        Assert.Equal("AAA", row.Key);
        Assert.Null(row.Error);
        var block = Assert.Single(row.Unreferenced);
        Assert.Equal(("m_AAA_1_2", true), (block.BlockKey, block.Removable));
        var cleaned = GeneratorCheck.WithoutUnreferenced(Package(), row);
        Assert.Equal(2, cleaned.Head.Children["categories"][0].Children["ideas"][0].Children["levels"].Count);
        Assert.Equal(3, row.State!.Head.Children["categories"][0].Children["ideas"][0].Children["levels"].Count);
    }

    [Fact]
    public void Block_referenced_only_by_base_file_counts_as_used()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1", "m_AAA_1_1"));
        ModFile("ga_AAA.txt", Ga("m_AAA_1"));
        ModFile("base.txt", "list = {\n\tuses = m_AAA_1_1\n}\n");

        Assert.Empty(GeneratorCheck.Check(Package(), Mod, "AAA", Languages).Unreferenced);
    }

    [Fact]
    public void Middle_level_is_reported_not_removed()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1", "m_AAA_1_1", "m_AAA_1_2"));
        ModFile("ga_AAA.txt", Ga("m_AAA_1", "m_AAA_1_2"));

        var row = GeneratorCheck.Check(Package(), Mod, "AAA", Languages);

        Assert.Equal(("m_AAA_1_1", false), (Assert.Single(row.Unreferenced).BlockKey, row.Unreferenced[0].Removable));
        Assert.Equal(3, GeneratorCheck.WithoutUnreferenced(Package(), row).Head.Children["categories"][0].Children["ideas"][0].Children["levels"].Count);
    }

    [Fact]
    public void Whole_idea_at_the_end_is_removed()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1", "m_AAA_2", "m_AAA_2_1"));
        ModFile("ga_AAA.txt", Ga("m_AAA_1"));

        var row = GeneratorCheck.Check(Package(), Mod, "AAA", Languages);

        Assert.All(row.Unreferenced, b => Assert.True(b.Removable));
        Assert.Single(GeneratorCheck.WithoutUnreferenced(Package(), row).Head.Children["categories"][0].Children["ideas"]);
    }

    [Fact]
    public void Whole_word_matching()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_5", "m_AAA_5_1"));
        ModFile("ga_AAA.txt", Ga("m_AAA_5_1"));

        var row = GeneratorCheck.Check(Package(), Mod, "AAA", Languages);

        Assert.Equal(new[] { "m_AAA_5" }, row.Unreferenced.Select(b => b.BlockKey));
    }

    [Fact]
    public void Broken_key_is_listed_with_error()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { a = \n");
        ModFile("m_BBB.txt", Blocks("m_BBB_1"));
        ModFile("ga_BBB.txt", Ga("m_BBB_1"));

        var rows = GeneratorCheck.Run(Package(), Mod, Languages);

        Assert.Equal(new[] { "AAA", "BBB" }, rows.Select(r => r.Key));
        Assert.Contains("m_AAA.txt", rows[0].Error);
        Assert.Null(rows[1].Error);
    }

    [Fact]
    public void Counts_missing_changed_equal()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1"));
        ModFile("ga_AAA.txt", "ga_AAA_1 = { modifier = m_AAA_1 }\nold = 1\n");

        var row = GeneratorCheck.Check(Package(), Mod, "AAA", Languages);

        Assert.Equal((1, 1, 1, 0), (row.Missing, row.Changed, row.Equal, row.FormatOnly));
    }

    [Fact]
    public void Whitespace_and_comment_differences_count_as_format_only()
    {
        ModFile("m_AAA.txt", Blocks("m_AAA_1"));
        ModFile("ga_AAA.txt", "# Kopf\nga_AAA_1 = {\n    modifier = m_AAA_1\n}\n");
        ModFile("base.txt", "list = {\n\ttag = AAA\n}\n");

        var row = GeneratorCheck.Check(Package(), Mod, "AAA", Languages);

        Assert.Equal((0, 0, 2, 1), (row.Missing, row.Changed, row.Equal, row.FormatOnly));
    }

    [Fact]
    public void Content_compare_handles_localization_as_key_values()
    {
        Assert.True(GeneratorCheck.SameContent("a.yml", "l_english:\n k: \"v\"\n", "l_english:\n  k:0 \"v\" # x\n"));
        Assert.False(GeneratorCheck.SameContent("a.yml", "l_english:\n k: \"v\"\n", "l_english:\n k: \"w\"\n"));
        Assert.True(GeneratorCheck.SameContent("a.txt", "a={b=\"x y\"}", "a = {\n\tb = \"x y\" # c\n}\n"));
        Assert.False(GeneratorCheck.SameContent("a.txt", "a = { b = 1 }", "a = { b = 2 }"));
    }
}
