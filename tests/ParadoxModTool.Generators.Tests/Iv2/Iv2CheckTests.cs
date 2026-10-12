using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>Teil 4: "Mod prüfen" mit dem echten IV2-Paket, auf der Python-Ausgabe und nach einer von Hand entfernten Stufe.</summary>
public sealed class Iv2CheckTests : IDisposable
{
    private static readonly string[] Languages = { "english", "french", "german" };

    private readonly Iv2Fixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Check_on_python_output()
    {
        var mod = _fixture.Temp.PathOf("python");
        Bundle.Unpack(Path.Combine(_fixture.Fixtures, "Expected-existing.bundle"), mod);

        var rows = GeneratorCheck.Run(_fixture.Package, mod, Languages);

        var row = Assert.Single(rows, r => r.Key == "TST");
        Assert.Null(row.Error);
        Assert.Empty(row.Unreferenced);
        Assert.Equal(0, row.LegacyRemoved);
        Assert.Contains(rows, r => r.Key == "TST");
    }

    [Fact]
    public void Unreferenced_after_hand_removed_level()
    {
        SlicePackage.Write(_fixture.Plan(_fixture.State(), _fixture.FreshMod));
        RemoveLevelFour(_fixture.FreshMod);

        var row = GeneratorCheck.Check(_fixture.Package, _fixture.FreshMod, "TST", Languages);

        Assert.Null(row.Error);
        var block = Assert.Single(row.Unreferenced);
        Assert.Equal(("national_idea_modifier_TST_3_3", true), (block.BlockKey, block.Removable));
        var cleaned = GeneratorCheck.WithoutUnreferenced(_fixture.Package, row);
        Assert.Equal(3, cleaned.Head.Children["categories"][0].Children["ideas"][2].Children["levels"].Count);
    }

    /// <summary>Entfernt alle Verweise auf Stufe 4 von Idee 3 (TST_3_3) aus allen Dateien außer den Static Modifiers, wie eine Handänderung.</summary>
    private static void RemoveLevelFour(string mod)
    {
        var word = new Regex(@"(?<![A-Za-z0-9_])\w*TST_3_3\w*", RegexOptions.CultureInvariant);
        foreach (var path in Directory.EnumerateFiles(mod, "*.*", SearchOption.AllDirectories).Where(p => !p.EndsWith("02_IV2_TST.txt", StringComparison.Ordinal) && !p.EndsWith(".dds", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(path);
            if (word.IsMatch(text))
            {
                File.WriteAllText(path, word.Replace(text, "removed_by_hand"));
            }
        }
    }
}
