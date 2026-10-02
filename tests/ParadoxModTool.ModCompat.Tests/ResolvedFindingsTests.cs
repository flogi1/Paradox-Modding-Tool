using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class ResolvedFindingsTests : CompatTestBase
{
    private const string Types = "in_game/common/building_types/";

    private CompatResult Run(params CompatParticipant[] mods) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = mods, Rules = Rules() });

    private CompatFinding Farm(params CompatParticipant[] mods) => Assert.Single(Run(mods).Findings, f => f.Key == "farm");

    private string StoreFile => Path.Combine(Dir, "compat", "resolved.json");

    [Fact]
    public void A_marked_conflict_stays_resolved_after_saving_and_loading()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        Assert.False(resolved.IsResolved(Farm(a, b)));

        resolved.Mark(Farm(a, b));
        resolved.Save(StoreFile);

        var loaded = ResolvedFindings.Load(StoreFile);
        Assert.Equal(1, loaded.Count);
        Assert.True(loaded.IsResolved(Farm(a, b)));
    }

    [Fact]
    public void Changing_the_contested_key_in_one_mod_shows_the_conflict_again()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(Farm(a, b));

        Put(b, Types + "b.txt", "farm = { cost = 3 }\n");
        Assert.False(resolved.IsResolved(Farm(a, b)));
    }

    [Fact]
    public void Changes_elsewhere_in_the_file_keep_the_mark()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(Farm(a, b));

        Put(b, Types + "b.txt", "mill = { cost = 5 }\n\nfarm = { cost = 2 }\n");
        Assert.True(resolved.IsResolved(Farm(a, b)));
    }

    [Fact]
    public void A_file_level_conflict_shows_again_when_a_file_changes()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "gfx/icon.dds", "eins");
        Put(b, "gfx/icon.dds", "zwei");
        var finding = Assert.Single(Run(a, b).Findings, f => f.RelativePath == "gfx/icon.dds");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(finding);
        Assert.True(resolved.IsResolved(Assert.Single(Run(a, b).Findings, f => f.RelativePath == "gfx/icon.dds")));

        Put(a, "gfx/icon.dds", "drei");
        Assert.False(resolved.IsResolved(Assert.Single(Run(a, b).Findings, f => f.RelativePath == "gfx/icon.dds")));
    }

    [Fact]
    public void The_same_key_between_other_mods_is_not_resolved()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var c = Mod("C", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        Put(c, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(Farm(a, b));

        Assert.False(resolved.IsResolved(Farm(a, c)));
    }

    [Fact]
    public void Mods_with_an_id_are_recognized_in_another_folder()
    {
        var a = Mod("A", 1, "111");
        var b = Mod("B", 2, "222");
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(Farm(a, b));

        var moved = new CompatParticipant("B2", "B", Make("B2"), 2, false, "222");
        Put(moved, Types + "b.txt", "farm = { cost = 2 }\n");
        Assert.True(resolved.IsResolved(Farm(a, moved)));
    }

    [Fact]
    public void Unmarking_shows_the_conflict_again_and_marking_twice_keeps_one_entry()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var resolved = ResolvedFindings.Load(StoreFile);
        resolved.Mark(Farm(a, b));
        resolved.Mark(Farm(a, b));
        Assert.Equal(1, resolved.Count);

        Assert.True(resolved.Unmark(Farm(a, b)));
        Assert.False(resolved.IsResolved(Farm(a, b)));
        Assert.Equal(0, resolved.Count);
    }

    [Fact]
    public void A_missing_or_broken_file_gives_an_empty_list()
    {
        Assert.Equal(0, ResolvedFindings.Load(StoreFile).Count);
        Directory.CreateDirectory(Path.GetDirectoryName(StoreFile)!);
        File.WriteAllText(StoreFile, "{ kaputt");
        Assert.Equal(0, ResolvedFindings.Load(StoreFile).Count);
    }
}
