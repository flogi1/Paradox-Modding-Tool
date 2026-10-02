using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class SuppressionSetTests
{
    private static readonly ModSource Own = new("Own", @"C:\mods\own", true, 0);

    private static (ErrorEntry Entry, ErrorAttribution Attribution) Sample(string name = "cmf_suppress")
    {
        var entry = Catalogs.Classify($"[01:00:00][a.cpp:1]: Unknown effect {name} at common/a.txt:1")[0];
        return (entry, new ErrorAttribution(ErrorBucket.OwnMod, Own, new ResolvedSource(@"C:\mods\own\in_game\common\a.txt", Own, false)));
    }

    [Fact]
    public void SuppressesByCodeCodeAndNameModAndFile()
    {
        var (entry, attribution) = Sample();
        var (otherName, otherAttribution) = Sample("real_typo");

        var byName = new SuppressionSet();
        byName.Add(new Suppression(SuppressionScope.CodeAndName, "EFF-001", "cmf_suppress"));
        Assert.True(byName.IsSuppressed(entry, attribution));
        Assert.False(byName.IsSuppressed(otherName, otherAttribution));

        foreach (var suppression in new[]
        {
            new Suppression(SuppressionScope.Code, "EFF-001"),
            new Suppression(SuppressionScope.Mod, @"C:\MODS\own\"),
            new Suppression(SuppressionScope.File, @"c:\mods\own\in_game\common\a.txt"),
        })
        {
            var set = new SuppressionSet();
            set.Add(suppression);
            Assert.True(set.IsSuppressed(entry, attribution), suppression.Scope.ToString());
        }
    }

    [Fact]
    public void DefaultSuppressedCatalogEntriesAreSuppressed()
    {
        var entry = Catalogs.Classify("[01:00:00][d.cpp:1]: Optimization warning: Database static size differs")[0];

        Assert.True(new SuppressionSet().IsSuppressed(entry, new ErrorAttribution(ErrorBucket.Engine, null, null)));
    }

    [Fact]
    public void AddIgnoresDuplicatesAndRemoveWorks()
    {
        var set = new SuppressionSet();
        var suppression = new Suppression(SuppressionScope.Code, "EFF-001");

        Assert.True(set.Add(suppression));
        Assert.False(set.Add(suppression with { }));
        Assert.True(set.Remove(suppression));
        Assert.Empty(set.Items);
    }

    [Fact]
    public void SavesAndLoadsAndToleratesMissingOrBrokenFiles()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("errorlog/suppressions.json");
        var set = new SuppressionSet();
        set.Add(new Suppression(SuppressionScope.CodeAndName, "VAR-001", "iv2_x"));

        set.Save(path);
        var loaded = SuppressionSet.Load(path);

        Assert.Equal(set.Items, loaded.Items);
        Assert.Empty(SuppressionSet.Load(tree.PathOf("missing.json")).Items);
        File.WriteAllText(path, "{ kaputt");
        Assert.Empty(SuppressionSet.Load(path).Items);
    }
}
