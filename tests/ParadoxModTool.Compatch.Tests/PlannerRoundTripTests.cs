using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Der geplante Patch muss nach dem Einspielen genau den gewünschten Zustand ergeben (Befunde C1, I1, I4, I5, I6 der Abschlussprüfung).</summary>
public class PlannerRoundTripTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private CompatchRun RunAndCheck(bool euV = true, IReadOnlyDictionary<EntryAddress, PatchForm>? forms = null)
    {
        var input = Input(euV);
        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, forms);
        Assert.NotEmpty(run.Plan.Entries.Cast<object>().Concat(run.Plan.Files));
        AssertRoundTrip(input, run);
        return run;
    }

    [Fact]
    public void Addition_to_a_replaced_key_survives_the_other_mods_replace()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 1 a = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 b = 1 }\n");

        Assert.Equal(PatchForm.Replace, Assert.Single(RunAndCheck().Plan.Entries).Form);
    }

    [Fact]
    public void Inject_is_used_when_no_replace_reaches_the_key()
    {
        const string Vanilla = "farm = { cost = 1 }\nmine = { cost = 1 }\n";
        PutVanilla(Buildings + "00_x.txt", Vanilla);
        PutA(Buildings + "00_x.txt", Vanilla.Replace("farm = { cost = 1 }", "farm = { cost = 1 a = 1 }"));
        PutB(Buildings + "00_x.txt", Vanilla.Replace("mine = { cost = 1 }", "mine = { cost = 3 }"));

        Assert.Equal(PatchForm.Inject, Assert.Single(RunAndCheck().Plan.Entries).Form);
    }

    [Fact]
    public void Own_file_sorts_after_mod_files_with_zzz_prefix()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "zzz_b.txt", "REPLACE:farm = { cost = 1 time = 3 }\n");

        RunAndCheck();
    }

    [Fact]
    public void Inserted_else_if_keeps_its_position()
    {
        const string Vanilla = "eff = { if = { limit = { a = 1 } x = 1 } else = { x = 2 } }\n";
        PutVanilla(Buildings + "00_x.txt", Vanilla);
        PutA(Buildings + "00_x.txt", Vanilla.Replace("else = {", "else_if = { limit = { b = 1 } x = 3 } else = {"));
        PutB(Buildings + "00_x.txt", Vanilla.Replace("x = 1", "x = 5"));

        RunAndCheck();
    }

    [Fact]
    public void Reordered_else_if_branches_of_A_are_kept()
    {
        const string Vanilla = "eff = { if = { limit = { a = 1 } } else_if = { limit = { b = 1 } } else_if = { limit = { c = 1 } } q = 1 }\n";
        PutVanilla(Buildings + "00_x.txt", Vanilla);
        PutA(Buildings + "00_x.txt", "eff = { if = { limit = { a = 1 } } else_if = { limit = { c = 1 } } else_if = { limit = { b = 1 } } q = 1 }\n");
        PutB(Buildings + "00_x.txt", Vanilla.Replace("q = 1", "q = 2"));

        RunAndCheck();
    }

    [Fact]
    public void Reordered_else_if_branches_of_B_are_kept()
    {
        const string Vanilla = "eff = { if = { limit = { a = 1 } } else_if = { limit = { b = 1 } } else_if = { limit = { c = 1 } } q = 1 }\n";
        PutVanilla(Buildings + "00_x.txt", Vanilla);
        PutA(Buildings + "00_x.txt", Vanilla.Replace("q = 1", "q = 2"));
        PutB(Buildings + "00_x.txt", "eff = { if = { limit = { a = 1 } } else_if = { limit = { c = 1 } } else_if = { limit = { b = 1 } } q = 1 }\n");

        var run = RunAndCheck();

        var desired = Text(run.Applied.Desired[EntryAddress.ForKey("in_game/common/building_types", "eff")].Entries);
        Assert.True(desired.IndexOf("c = 1", StringComparison.Ordinal) < desired.IndexOf("b = 1", StringComparison.Ordinal), desired);
    }

    [Fact]
    public void Defines_keep_their_parent_block_when_the_file_is_rewritten()
    {
        const string Vanilla = "X = 1\nNGame = {\n A = 1\n B = 1\n}\n";
        PutVanilla("in_game/common/defines/00.txt", Vanilla);
        PutA("in_game/common/defines/00.txt", Vanilla.Replace("X = 1", "X = 2"));
        PutB("in_game/common/defines/00.txt", Vanilla.Replace("B = 1", "B = 2"));

        RunAndCheck();
    }

    [Fact]
    public void New_gui_type_in_whole_file_goes_into_the_types_block()
    {
        const string Vanilla = "types T {\n type a = button { x = 1 }\n}\n";
        PutVanilla("in_game/gui/w.gui", Vanilla);
        PutA("in_game/gui/w.gui", "types T {\n type a = button { x = 1 }\n type neu = widget { y = 1 }\n}\n");
        PutB("in_game/gui/w.gui", Vanilla.Replace("x = 1", "x = 2"));
        var neu = EntryAddress.ForKey("in_game/gui", "type neu");

        RunAndCheck(forms: new Dictionary<EntryAddress, PatchForm> { [neu] = PatchForm.WholeFile });
    }
}
