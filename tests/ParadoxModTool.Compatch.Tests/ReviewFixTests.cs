using System.Text;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Regressionstests für die Befunde der Abschlussprüfung von Plan 1.</summary>
public class ReviewFixTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private void PutLatin1(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
    }

    [Fact]
    public void Windows1252_changes_are_detected_and_text_is_kept()
    {
        const string Vanilla = "c1 = { n = \"México\" }\r\nc2 = { x = 1 }\r\n";
        PutLatin1(VanillaDir, "common/cultures/00_c.txt", Vanilla);
        PutLatin1(ADir, "common/cultures/00_c.txt", Vanilla.Replace("México", "Mèxico"));
        PutLatin1(BDir, "common/cultures/00_c.txt", Vanilla.Replace("x = 1", "x = 2"));

        var run = CompatchEngine.Run(Input(euV: false), PatchTarget.Compatch);

        var file = Assert.Single(run.Plan.Files);
        Assert.Contains("Mèxico", file.Text);
        Assert.Contains("x = 2", file.Text);
        Assert.Equal("windows-1252", file.EncodingName);
    }

    [Fact]
    public void Constants_are_inlined_so_patch_entries_do_not_reference_undefined_constants()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 1 time = 2 }\n");
        PutB(Buildings + "b.txt", "@d = 3\nREPLACE:farm = { cost = @d time = 1 }\n");

        var op = Assert.Single(CompatchEngine.Run(Input(), PatchTarget.Compatch).Plan.Entries);

        Assert.Contains("cost = 3", op.Text);
        Assert.DoesNotContain("@d", op.Text);
    }

    [Fact]
    public void Redefined_constant_is_a_real_change()
    {
        PutVanilla(Buildings + "00_x.txt", "@c = 10\nfarm = { cost = @c }\n");
        PutA(Buildings + "a.txt", "@c = 20\nREPLACE:farm = { cost = @c }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 15 }\n");

        var action = Assert.Single(CompatchEngine.Run(Input(), PatchTarget.Compatch).Actions);

        Assert.Equal(MergeStage.Conflict, action.Stage);
    }

    [Fact]
    public void Key_removed_by_one_mods_file_replacement_is_compared_with_the_other_mod()
    {
        PutVanilla(Buildings + "00_x.txt", "k1 = { v = 1 }\nk2 = { v = 1 }\n");
        PutA(Buildings + "00_x.txt", "k1 = { v = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:k2 = { w = 1 }\n");

        var action = Assert.Single(CompatchEngine.Run(Input(), PatchTarget.Compatch).Actions);

        Assert.Equal(EntryAddress.ForKey("in_game/common/building_types", "k2"), action.Address);
        Assert.Equal(MergeStage.Conflict, action.Stage);
    }

    [Theory]
    [InlineData("in_game/common/scripted_effects/")]
    [InlineData("in_game/common/scripted_triggers/")]
    public void Automatic_merges_in_scripted_effects_and_triggers_need_review(string folder)
    {
        PutVanilla(folder + "00_e.txt", "eff = { if = { limit = { a = 1 } x = 1 } else = { x = 2 } }\n");
        PutA(folder + "a.txt", "REPLACE:eff = { if = { limit = { a = 1 } x = 1 } else_if = { limit = { b = 1 } x = 3 } else = { x = 2 } }\n");
        PutB(folder + "b.txt", "REPLACE:eff = { if = { limit = { a = 1 } x = 1 } else = { x = 5 } }\n");

        var actions = CompatchEngine.Run(Input(), PatchTarget.Compatch).Actions;

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(MergeStage.Review, a.Stage));
    }
}
