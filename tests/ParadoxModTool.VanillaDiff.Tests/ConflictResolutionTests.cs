using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class ConflictResolutionTests
{
    private static ScriptStatement Find(ScriptBlock block, string key) =>
        block.Entries.OfType<ScriptStatement>().First(s => s.Key == key);

    [Fact]
    public void ApplyResolutions_TopLevelKonflikt_ErsetztDurchModStatement()
    {
        var old = ScriptDocument.Parse("cost = 100\n");
        var newVanilla = ScriptDocument.Parse("cost = 200\n");
        var mod = ScriptDocument.Parse("cost = 150\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);
        var conflict = Assert.Single(result.Conflicts);
        Assert.NotNull(conflict.ModStatement);
        Assert.Equal("150", Assert.IsType<ScriptScalar>(conflict.ModStatement!.Value).Text);

        var patched = ConflictResolution.ApplyResolutions(
            result.MergedRoot,
            new[] { new ConflictResolutionChoice(conflict) });

        Assert.Equal("150", Assert.IsType<ScriptScalar>(Find(patched, "cost").Value).Text);
    }

    [Fact]
    public void ApplyResolutions_VerschachtelterKonflikt_ErsetztNurDenBetroffenenSchluessel()
    {
        var old = ScriptDocument.Parse("idea = {\n cost = 100\n other = 1\n}\n");
        var newVanilla = ScriptDocument.Parse("idea = {\n cost = 200\n other = 1\n}\n");
        var mod = ScriptDocument.Parse("idea = {\n cost = 150\n other = 1\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("idea.cost", conflict.Path);

        var patched = ConflictResolution.ApplyResolutions(
            result.MergedRoot,
            new[] { new ConflictResolutionChoice(conflict) });

        var ideaBlock = Assert.IsType<ScriptBlock>(Find(patched, "idea").Value);
        Assert.Equal("150", Assert.IsType<ScriptScalar>(Find(ideaBlock, "cost").Value).Text);
        Assert.Equal("1", Assert.IsType<ScriptScalar>(Find(ideaBlock, "other").Value).Text);
    }

    [Fact]
    public void ApplyResolutions_WiederholteSchluessel_TrifftNachEinfuegungDasRichtigeElement()
    {
        var old = ScriptDocument.Parse("e = {\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 2 }\n}\n");
        var newVanilla = ScriptDocument.Parse("e = {\n if = { limit = { x = 0 } add = 0 }\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 3 }\n}\n");
        var mod = ScriptDocument.Parse("e = {\n if = { limit = { x = 1 } add = 1 }\n if = { limit = { x = 2 } add = 20 }\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);
        var conflict = Assert.Single(result.Conflicts);
        var patched = ConflictResolution.ApplyResolutions(result.MergedRoot, new[] { new ConflictResolutionChoice(conflict) });

        var adds = ((ScriptBlock)Find(patched, "e").Value).Entries.OfType<ScriptStatement>()
            .Select(s => Assert.IsType<ScriptScalar>(Find((ScriptBlock)s.Value, "add").Value).Text);
        Assert.Equal(new[] { "0", "1", "20" }, adds);
    }
}
