using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Crusader Kings III und Europa Universalis V (gemessen 2026-09-30): <c>replace_path</c> einer Mod blendet auch die Dateien der Mods aus, die im
/// Playset vor ihr laden; spätere Mods und die eigenen Dateien bleiben. EU V nennt die Ordner ohne <c>in_game/</c> usw. davor.
/// </summary>
public class ReplacePathModsTests : CompatTestBase
{
    private const string Types = "in_game/common/building_types/";
    private static readonly string[] Eu5Roots = { "in_game", "main_menu", "loading_screen" };

    private CompatResult Run(IReadOnlyList<CompatParticipant> all, IReadOnlyDictionary<string, IReadOnlyCollection<string>> hidden, string? focus = null) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = all, Rules = Rules(), FocusId = focus, HiddenModFolders = hidden, ReplacePathRoots = Eu5Roots });

    [Fact]
    public void File_of_earlier_mod_in_hidden_folder_gives_no_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");

        var result = Run(new[] { a, b }, new Dictionary<string, IReadOnlyCollection<string>> { ["A"] = new[] { "common/building_types" } });

        Assert.DoesNotContain(result.Findings, f => f.Key == "farm");
    }

    [Fact]
    public void Without_hidden_folders_the_conflict_stays()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");

        var result = Run(new[] { a, b }, new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Contains(result.Findings, f => f.Key == "farm" && f.Kind == CompatKind.KeyConflict);
    }

    [Fact]
    public void Hidden_earlier_mod_file_is_no_base_for_redundant_override()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "a.txt", "farm = { cost = 1 }\n");

        var result = Run(new[] { a, b }, new Dictionary<string, IReadOnlyCollection<string>> { ["A"] = new[] { "common/building_types" } }, focus: "B");

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.RedundantOverride);
    }

    [Fact]
    public void Hidden_folders_for_earlier_mods_follow_the_load_order()
    {
        var replacePaths = new Dictionary<string, IReadOnlyList<string>>
        {
            ["A"] = new[] { "gui/pmt_rp2" },
            ["B"] = new[] { "gui/pmt_rp1" },
        };
        var ranks = new[] { ("A", 1), ("B", 2) };

        var hidden = CompatRequest.HiddenFoldersOfEarlierMods(ranks, replacePaths);

        Assert.Equal(new[] { "gui/pmt_rp1" }, hidden["A"]);
        Assert.Empty(hidden["B"]);
    }
}
