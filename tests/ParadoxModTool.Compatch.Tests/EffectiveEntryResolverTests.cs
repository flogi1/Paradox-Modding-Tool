using ParadoxModTool.ModCompat;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class EffectiveEntryResolverTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private static EntryAddress Farm => EntryAddress.ForKey("in_game/common/building_types", "farm");

    private ResolvedEntries Resolve(bool euV = true, CompatchFileFilter? filter = null) => EffectiveEntryResolver.Resolve(Input(euV, filter));

    [Fact]
    public void Two_injects_combine_without_patch()
    {
        PutVanilla(Buildings + "00_farm.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");

        var info = Resolve().Addresses[Farm];

        Assert.Contains("cost = 1", Text(info.Vanilla.Entries));
        Assert.DoesNotContain("a = 1", Text(info.Vanilla.Entries));
        Assert.Contains("a = 1", Text(info.A.Entries));
        Assert.DoesNotContain("b = 1", Text(info.A.Entries));
        Assert.Contains("a = 1", Text(info.NoPatch.Entries));
        Assert.Contains("b = 1", Text(info.NoPatch.Entries));
    }

    // Gemessen 2026-10-02 in Europa Universalis V: INJECT:build_gravel_road = { gold = 999 } über Vanilla gold = 10 ergibt 999.
    [Fact]
    public void Inject_overwrites_an_existing_subkey_in_place()
    {
        PutVanilla(Buildings + "00_farm.txt", "farm = { cost = 1 size = 2 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { cost = 5 extra = 3 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { other = 4 }\n");

        var info = Resolve().Addresses[Farm];
        var text = Text(info.NoPatch.Entries);

        Assert.Contains("cost = 5", text);
        Assert.DoesNotContain("cost = 1", text);
        Assert.True(text.IndexOf("cost = 5", StringComparison.Ordinal) < text.IndexOf("size = 2", StringComparison.Ordinal));
        Assert.True(text.IndexOf("size = 2", StringComparison.Ordinal) < text.IndexOf("extra = 3", StringComparison.Ordinal));
        Assert.False(info.NoPatch.Uncertain);
    }

    [Fact]
    public void Later_inject_of_the_same_subkey_wins()
    {
        PutVanilla(Buildings + "00_farm.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { cost = 5 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { cost = 7 }\n");

        var info = Resolve().Addresses[Farm];
        var text = Text(info.NoPatch.Entries);

        Assert.Contains("cost = 7", text);
        Assert.DoesNotContain("cost = 5", text);
        Assert.DoesNotContain("cost = 1", text);
        Assert.False(info.NoPatch.Uncertain);
    }

    [Fact]
    public void Inject_over_a_block_subkey_replaces_it_but_stays_uncertain()
    {
        PutVanilla(Buildings + "00_farm.txt", "farm = { modifier = { a = 1 } cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { modifier = { b = 1 } }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { other = 4 }\n");

        var info = Resolve().Addresses[Farm];
        var text = Text(info.NoPatch.Entries);

        Assert.Contains("b = 1", text);
        Assert.DoesNotContain("a = 1", text);
        Assert.Contains("cost = 1", text);
        Assert.True(info.NoPatch.Uncertain);
    }

    [Fact]
    public void Two_replaces_are_ordered_by_file_name_then_rank()
    {
        PutVanilla(Buildings + "00_farm.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "z.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");

        var info = Resolve().Addresses[Farm];

        Assert.Equal(MergeSide.A, info.NoPatch.Side);
        Assert.Contains("cost = 2", Text(info.NoPatch.Entries));
        Assert.Equal("farm", Assert.IsType<ParadoxModTool.VanillaDiff.ScriptStatement>(info.A.Entries[0]).Key);
    }

    [Fact]
    public void Same_path_replacement_loses_the_lower_mods_other_changes()
    {
        const string Vanilla = "farm = { cost = 1 }\nmine = { cost = 1 }\nwell = { cost = 1 }\n";
        PutVanilla(Buildings + "00_x.txt", Vanilla);
        PutA(Buildings + "00_x.txt", Vanilla.Replace("farm = { cost = 1 }", "farm = { cost = 2 }"));
        PutB(Buildings + "00_x.txt", Vanilla.Replace("mine = { cost = 1 }", "mine = { cost = 3 }"));

        var resolved = Resolve();
        var farm = resolved.Addresses[Farm];

        Assert.Contains("cost = 2", Text(farm.A.Entries));
        Assert.Contains("cost = 1", Text(farm.NoPatch.Entries));
        Assert.Equal(MergeSide.B, farm.NoPatch.Side);
        Assert.Contains(EntryAddress.ForFileRest(Buildings + "00_x.txt"), resolved.Addresses.Keys);
        Assert.Contains(EntryAddress.ForKey("in_game/common/building_types", "well"), resolved.Addresses.Keys);
    }

    [Fact]
    public void Vanilla_outside_relevant_scopes_is_not_parsed()
    {
        PutVanilla("in_game/common/religions/broken.txt", "religion = { x = 1\n");
        PutVanilla(Buildings + "00_farm.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");

        Assert.Empty(Resolve().Unreadable);
    }

    [Fact]
    public void Paths_differing_in_case_map_to_one_address()
    {
        PutA("In_Game/Common/Building_Types/X.txt", "farm = { cost = 2 }\n");
        PutB("in_game/common/building_types/x.txt", "farm = { cost = 3 }\n");

        var resolved = Resolve();

        Assert.Single(resolved.Addresses.Keys, a => a.Kind == AddressKind.FileRest);
        Assert.Single(resolved.Addresses.Keys, a => a.Kind == AddressKind.Key);
    }

    [Fact]
    public void Unparseable_same_path_file_becomes_whole_file_address()
    {
        PutA(Buildings + "x.txt", "farm = { cost = 2\n");
        PutB(Buildings + "x.txt", "farm = { cost = 3 }\n");

        var resolved = Resolve();

        var info = resolved.Addresses[EntryAddress.ForWholeFile(Buildings + "x.txt")];
        Assert.Equal("nicht lesbar", info.Note);
        Assert.NotEqual(info.A.FileHash, info.B.FileHash);
        Assert.Single(resolved.Unreadable);
    }

    [Fact]
    public void Hidden_kinds_are_counted_not_compared()
    {
        PutA("in_game/gfx/x.dds", "a");
        PutB("in_game/gfx/x.dds", "b");

        var resolved = Resolve();

        Assert.Empty(resolved.Addresses);
        Assert.Equal(2, resolved.Skipped.Count(s => s.Reason == SkipReason.HiddenKind));
    }

    [Fact]
    public void Gui_type_last_loaded_file_wins()
    {
        PutVanilla("in_game/gui/shared.gui", "types T {\n type btn = button { size = { 1 1 } }\n}\n");
        PutA("in_game/gui/a.gui", "types A {\n type btn = button { size = { 2 2 } }\n}\n");
        PutB("in_game/gui/b.gui", "types B {\n type btn = button { size = { 3 3 } }\n}\n");

        var info = Resolve().Addresses[EntryAddress.ForKey("in_game/gui", "type btn")];

        Assert.Equal(MergeSide.B, info.NoPatch.Side);
        Assert.Contains("3", Text(info.NoPatch.Entries));
    }

    [Fact]
    public void Game_without_operators_marks_plain_duplicates_uncertain()
    {
        PutA("common/buildings/a.txt", "farm = { cost = 2 }\n");
        PutB("common/buildings/b.txt", "farm = { cost = 3 }\n");

        var info = Resolve(euV: false).Addresses[EntryAddress.ForKey("common/buildings", "farm")];

        Assert.Equal(MergeSide.B, info.NoPatch.Side);
        Assert.True(info.Uncertain);
    }

    [Fact]
    public void Missing_mod_folder_throws_input_exception()
    {
        Directory.Delete(BDir);
        Assert.Throws<CompatchInputException>(() => Resolve());
    }

    [Fact]
    public void Cancellation_is_honoured()
    {
        PutA(Buildings + "a.txt", "farm = { cost = 2 }\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => EffectiveEntryResolver.Resolve(Input(), null, cts.Token));
    }
}
