using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.ErrorLog.Tests;

public class SymbolIndexTests
{
    private readonly ITestOutputHelper _output;

    public SymbolIndexTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FindsTopLevelDefinitionsSettersReferencesAndLocalization()
    {
        using var tree = new TempTree(
            ("own/in_game/common/scripted_effects/a.txt",
                "my_effect = {\n\tset_variable = { name = my_var value = 1 }\n\tif = { limit = { has_variable = my_var } }\n}\n# my_comment_only = { }\nother_effect =\n{\n}\n"),
            ("own/in_game/localization/english/a_l_english.yml", "l_english:\n my_var_desc:0 \"Uses my_var\"\n"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, IndexReferences: true) });

        var effect = Assert.Single(index.Find("my_effect"));
        Assert.Equal(SymbolRole.Definition, effect.Role);
        Assert.Equal(1, effect.Line);
        Assert.Same(own, effect.Mod);
        Assert.Equal(SymbolRole.Definition, Assert.Single(index.Find("other_effect")).Role);

        var variable = index.Find("my_var");
        Assert.Contains(variable, o => o.Role == SymbolRole.Set && o.Line == 2);
        Assert.Contains(variable, o => o.Role == SymbolRole.Reference && o.Line == 3);
        Assert.Contains(variable, o => o.Role == SymbolRole.Localization && o.FilePath.EndsWith("a_l_english.yml"));
        Assert.Empty(index.Find("my_comment_only"));
    }

    [Fact]
    public void NestedKeysAreNotDefinitions()
    {
        using var tree = new TempTree(("own/in_game/common/x/a.txt", "outer = {\n\tinner = { }\n}\n"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, IndexReferences: true) });

        Assert.Equal(SymbolRole.Definition, Assert.Single(index.Find("outer")).Role);
        Assert.Equal(SymbolRole.Reference, Assert.Single(index.Find("inner")).Role);
    }

    [Fact]
    public void BracesInsideStringsDoNotChangeDepth()
    {
        using var tree = new TempTree(("own/in_game/common/x/a.txt", "first = {\n\tdesc = \"{ a }\"\n\tinner = { }\n}\nsecond = { }\n"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, IndexReferences: true) });

        Assert.Equal(SymbolRole.Reference, Assert.Single(index.Find("inner")).Role);
        Assert.Equal(SymbolRole.Definition, Assert.Single(index.Find("second")).Role);
    }

    [Fact]
    public void GuiFilesAreIndexedWithTheirOwnRole()
    {
        using var tree = new TempTree(
            ("own/in_game/gui/x.gui", "text = \"[GetVariable('iv2_x')]\"\n# iv2_comment\n"),
            ("game/in_game/gui/v.gui", "text = \"[GetVariable('v_x')]\"\n"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, true), new SymbolRoot(tree.PathOf("game"), null, false) });

        var gui = Assert.Single(index.Find("iv2_x"));
        Assert.Equal((SymbolRole.Gui, 1), (gui.Role, gui.Line));
        Assert.Empty(index.Find("iv2_comment"));
        Assert.Empty(index.Find("v_x"));
    }

    [Fact]
    public void RootsWithoutReferenceIndexingStoreOnlyDefinitionsFromCommon()
    {
        using var tree = new TempTree(
            ("game/in_game/common/scripted_effects/v.txt", "vanilla_effect = {\n\tset_variable = { name = v_var }\n}\n"),
            ("game/in_game/events/e.txt", "namespace = e\ne.1 = { }\n"));

        var index = SymbolIndex.Build(new[] { new SymbolRoot(tree.PathOf("game"), null, IndexReferences: false) });

        var definition = Assert.Single(index.Find("vanilla_effect"));
        Assert.Null(definition.Mod);
        Assert.Empty(index.Find("v_var"));
        Assert.Empty(index.Find("namespace"));
    }

    [Fact]
    public void SkipsSnapshotsAndDotFolders()
    {
        using var tree = new TempTree(
            ("own/snapshots/in_game/common/a.txt", "ghost = { }"),
            ("own/.git/in_game/common/a.txt", "ghost2 = { }"),
            ("own/in_game/common/a.txt", "real = { }"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, IndexReferences: true) });

        Assert.Empty(index.Find("ghost"));
        Assert.Empty(index.Find("ghost2"));
        Assert.Single(index.Find("real"));
    }

    [Fact]
    public void SimilarNamesUseEditDistanceAndFilter()
    {
        using var tree = new TempTree(("fw/in_game/common/scripted_effects/f.txt", "cmf_suppress = { }\ncmf_show_alert = { }\n"));
        var framework = new ModSource("Framework", tree.PathOf("fw"), IsOwn: false, LoadOrder: -1);

        var index = SymbolIndex.Build(new[] { new SymbolRoot(framework.Directory, framework, IndexReferences: false) });

        Assert.Equal(new[] { "cmf_suppress" }, index.SimilarNames("cmf_supress", o => o.Role == SymbolRole.Definition));
        Assert.Empty(index.SimilarNames("cmf_supress", o => o.Role == SymbolRole.Set));
        Assert.Equal(1, SymbolIndex.Distance("abc", "abd", 2));
        Assert.True(SymbolIndex.Distance("abc", "xyzw", 2) > 2);
    }

    [Fact]
    public void BuildsDefinitionIndexOverVanillaInReasonableTime()
    {
        var game = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game";
        if (!Directory.Exists(game))
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var index = SymbolIndex.Build(new[] { new SymbolRoot(game, null, IndexReferences: false) });
        _output.WriteLine($"Vanilla-Definitionen in {watch.ElapsedMilliseconds} ms");

        Assert.Contains(index.Find("event_illustration_estate_effect"), o => o.Role == SymbolRole.Definition && o.Mod is null);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60));
    }
}
