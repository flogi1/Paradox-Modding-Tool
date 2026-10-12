using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>Teil 4: Das IV2-Paket liest seine Sets zurück, aus eigener Ausgabe (Rundreise) und aus der Python-Ausgabe.</summary>
public sealed class Iv2ReadTests : IDisposable
{
    private static readonly string[] Languages = { "english", "french", "german", "spanish" };

    private readonly Iv2Fixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static List<FormNode> Ideas(GeneratorState state, int group) => state.Head.Children["categories"][group].Children["ideas"];

    [Fact]
    public void Round_trip_reads_back_the_written_set()
    {
        var written = _fixture.State();
        SlicePackage.Write(_fixture.Plan(written, _fixture.FreshMod));

        var read = GeneratorStateReader.Read(_fixture.Package, _fixture.FreshMod, "TST", Languages)!.State;

        Assert.Equal(new[] { "english", "french", "german" }, read.Languages);
        Assert.Equal("coa/TST.dds", read.Head.Values["coa"]);
        for (var group = 0; group < 4; group++)
        {
            var want = Ideas(written, group);
            var have = Ideas(read, group);
            Assert.Equal(want.Count, have.Count);
            for (var i = 0; i < want.Count; i++)
            {
                Assert.Equal(want[i].Values["name"], have[i].Values["name"]);
                Assert.Equal(want[i].Values["desc"], have[i].Values["desc"]);
                Assert.Equal(Translations(want[i]), Translations(have[i]));
                Assert.Equal(want[i].Children["levels"].Select(l => (l.Values["modifiers"].Trim(), l.Values["expensive"])), have[i].Children["levels"].Select(l => (l.Values["modifiers"], l.Values["expensive"])));
            }
        }
    }

    [Fact]
    public void Plan_of_read_state_is_unchanged()
    {
        SlicePackage.Write(_fixture.Plan(_fixture.State(), _fixture.FreshMod));

        var read = GeneratorStateReader.Read(_fixture.Package, _fixture.FreshMod, "TST", Languages)!.State;
        var plan = _fixture.Plan(read, _fixture.FreshMod);

        Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, f.RelativePath));
    }

    [Fact]
    public void Reads_python_output()
    {
        var mod = _fixture.Temp.PathOf("python");
        Bundle.Unpack(Path.Combine(_fixture.Fixtures, "Expected-existing.bundle"), mod);

        var read = GeneratorStateReader.Read(_fixture.Package, mod, "TST", Languages)!.State;

        var adm = Ideas(read, 0);
        Assert.Equal("Iron 'Will'", adm[0].Values["name"]);
        Assert.Equal("First line second line", adm[0].Values["desc"]);
        Assert.Equal(new[] { 1, 2, 4 }, adm.Select(i => i.Children["levels"].Count));
        Assert.Equal("hand_written = 1", adm[1].Children["levels"][1].Values["modifiers"]);
        Assert.Equal(new[] { "no", "yes" }, adm[1].Children["levels"].Select(l => l.Values["expensive"]));
        Assert.Empty(Ideas(read, 2));
    }

    private static string Translations(FormNode node) =>
        string.Join("|", node.Translations.OrderBy(t => t.Key).SelectMany(t => t.Value.Where(v => v.Value.Length > 0).OrderBy(v => v.Key).Select(v => $"{t.Key}.{v.Key}={v.Value}")));
}
