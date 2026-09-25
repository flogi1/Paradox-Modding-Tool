using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class MergeEngineTests
{
    private static ScriptStatement Find(ScriptBlock block, string key) =>
        block.Entries.OfType<ScriptStatement>().First(s => s.Key == key);

    private static ScriptStatement? FindOrNull(ScriptBlock block, string key) =>
        block.Entries.OfType<ScriptStatement>().FirstOrDefault(s => s.Key == key);

    private static string ValueText(ScriptStatement stmt) => Assert.IsType<ScriptScalar>(stmt.Value).Text;

    [Fact]
    public void Merge_NurModHatGeaendert_UebernimmtModWert()
    {
        var old = ScriptDocument.Parse("cost = 100\nother = 1\n");
        var newVanilla = ScriptDocument.Parse("cost = 100\nother = 1\n");
        var mod = ScriptDocument.Parse("cost = 150\nother = 1\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        Assert.Equal("150", ValueText(Find(result.MergedRoot, "cost")));
        Assert.Equal("1", ValueText(Find(result.MergedRoot, "other")));
    }

    [Fact]
    public void Merge_NurSpielHatGeaendert_ModBleibtUnberuehrt()
    {
        var old = ScriptDocument.Parse("cost = 100\n");
        var newVanilla = ScriptDocument.Parse("cost = 200\n");
        var mod = ScriptDocument.Parse("cost = 100\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        Assert.Equal("200", ValueText(Find(result.MergedRoot, "cost")));
    }

    [Fact]
    public void Merge_BeideUnterschiedlichGeaendert_MeldetKonfliktUndBehaeltSpielVersion()
    {
        var old = ScriptDocument.Parse("cost = 100\n");
        var newVanilla = ScriptDocument.Parse("cost = 200\n");
        var mod = ScriptDocument.Parse("cost = 150\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.True(result.HasConflicts);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("cost", conflict.Path);
        Assert.Equal("200", ValueText(Find(result.MergedRoot, "cost")));
    }

    [Fact]
    public void Merge_BeideAufDenselbenNeuenWertGeaendert_KeinKonflikt()
    {
        var old = ScriptDocument.Parse("cost = 100\n");
        var newVanilla = ScriptDocument.Parse("cost = 200\n");
        var mod = ScriptDocument.Parse("cost = 200\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        Assert.Equal("200", ValueText(Find(result.MergedRoot, "cost")));
    }

    [Fact]
    public void Merge_ModFuegtNeuenSchluesselHinzu_WirdUebernommen()
    {
        var old = ScriptDocument.Parse("cost = 100\n");
        var newVanilla = ScriptDocument.Parse("cost = 100\n");
        var mod = ScriptDocument.Parse("cost = 100\nbonus = 5\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        Assert.Equal("5", ValueText(Find(result.MergedRoot, "bonus")));
    }

    [Fact]
    public void Merge_VerschachtelterBlockMitUnterschiedlichenUnterschluesseln_WirdRekursivZusammengefuehrt()
    {
        var old = ScriptDocument.Parse("idea = {\n a = 1\n b = 2\n}\n");
        var newVanilla = ScriptDocument.Parse("idea = {\n a = 10\n b = 2\n}\n");
        var mod = ScriptDocument.Parse("idea = {\n a = 1\n b = 20\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        var ideaBlock = Assert.IsType<ScriptBlock>(Find(result.MergedRoot, "idea").Value);
        Assert.Equal("10", ValueText(Find(ideaBlock, "a")));
        Assert.Equal("20", ValueText(Find(ideaBlock, "b")));
    }

    [Fact]
    public void Merge_SpielHatVonModGeaendertenSchluesselEntfernt_MeldetKonflikt()
    {
        var old = ScriptDocument.Parse("cost = 100\nlegacy = 1\n");
        var newVanilla = ScriptDocument.Parse("cost = 100\n");
        var mod = ScriptDocument.Parse("cost = 100\nlegacy = 2\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.True(result.HasConflicts);
        Assert.Contains(result.Conflicts, c => c.Path == "legacy");
        Assert.Null(FindOrNull(result.MergedRoot, "legacy"));
    }

    [Fact]
    public void Merge_WiederholteSchluessel_EinfuegenVorneVerschiebtSpaetereAenderungNicht()
    {
        var old = ScriptDocument.Parse("e = {\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 2 }\n}\n");
        var newVanilla = ScriptDocument.Parse("e = {\n if = { limit = { x = 0 } add = 0 }\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 2 }\n}\n");
        var mod = ScriptDocument.Parse("e = {\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 20 }\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        var ifs = ((ScriptBlock)Find(result.MergedRoot, "e").Value).Entries.OfType<ScriptStatement>().ToList();
        Assert.Equal(new[] { "0", "1", "20" }, ifs.Select(s => ValueText(Find((ScriptBlock)s.Value, "add"))));
    }

    [Fact]
    public void Merge_WiederholteSchluessel_EntferntesErstesElementVerliertAenderungAmZweitenNicht()
    {
        var old = ScriptDocument.Parse("t = {\n modifier = { a = 1 }\n modifier = { b = 1 }\n}\n");
        var newVanilla = ScriptDocument.Parse("t = {\n modifier = { b = 1 }\n}\n");
        var mod = ScriptDocument.Parse("t = {\n modifier = { a = 1 }\n modifier = { b = 5 }\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.False(result.HasConflicts);
        var modifier = Assert.Single(((ScriptBlock)Find(result.MergedRoot, "t").Value).Entries.OfType<ScriptStatement>());
        Assert.Equal("5", ValueText(Find((ScriptBlock)modifier.Value, "b")));
    }

    [Fact]
    public void Merge_WiederholteSchluessel_BeideFuegenVerschiedeneBloeckeAnDerselbenStelleEin_Konflikt()
    {
        var old = ScriptDocument.Parse("e = {\n if = { limit = { b = yes } x = 0 }\n}\n");
        var newVanilla = ScriptDocument.Parse("e = {\n if = { limit = { b = yes } x = 0 }\n if = { limit = { a = yes } x = 2 }\n}\n");
        var mod = ScriptDocument.Parse("e = {\n if = { limit = { b = yes } x = 0 }\n if = { limit = { a = yes } x = 1 }\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        Assert.True(result.HasConflicts);
        Assert.Equal(2, ((ScriptBlock)Find(result.MergedRoot, "e").Value).Entries.OfType<ScriptStatement>().Count());
    }
}
