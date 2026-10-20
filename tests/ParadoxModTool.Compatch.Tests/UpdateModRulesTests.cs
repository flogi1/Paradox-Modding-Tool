using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Update-Modus: Dateirest bleibt Text, on_action je Block, Dateien ohne alte Fassung, nur Dateien des Laufs.</summary>
public class UpdateModRulesTests : UpdateModTestBase
{
    [Fact]
    public void Update_rules_keep_inject_append_mode()
    {
        var rules = new ParadoxModTool.ModCompat.OverrideRules(new[] { new ParadoxModTool.ModCompat.OverrideRule("common/on_actions", ParadoxModTool.ModCompat.OverrideBehavior.AppendOnly) }) { InjectAppends = true };

        Assert.True(UpdateModRules.From(rules).InjectAppends);
    }

    [Fact]
    public void Update_rules_keep_last_wins_load_order()
    {
        var rules = new ParadoxModTool.ModCompat.OverrideRules(new[] { new ParadoxModTool.ModCompat.OverrideRule("common/buildings", ParadoxModTool.ModCompat.OverrideBehavior.LastWins) }) { LastWinsInLoadOrder = true };

        Assert.True(UpdateModRules.From(rules).LastWinsInLoadOrder);
    }

    [Fact]
    public void Update_rules_keep_case_sensitive_load_order()
    {
        var rules = new ParadoxModTool.ModCompat.OverrideRules(new[] { new ParadoxModTool.ModCompat.OverrideRule("common/buildings", ParadoxModTool.ModCompat.OverrideBehavior.LastWins) }) { LoadOrderCaseSensitive = true };

        Assert.True(UpdateModRules.From(rules).LoadOrderCaseSensitive);
    }

    [Fact]
    public void Comments_and_namespace_stay_byte_identical()
    {
        const string Events = "in_game/events/test.txt";
        PutVanilla(Events, "namespace = test\n\ntest.1 = {\n\ttype = country_event\n\tweight = 1\n}\n\ntest.2 = {\n\ttype = country_event\n}\n");
        PutA(Events, "namespace = test\nnamespace = test2\n\ntest.1 = {\n\ttype = country_event\n\tweight = 2\n}\n\ntest.2 = {\n\ttype = country_event\n}\n");
        const string Mod = "# Mein Kommentar\nnamespace = test\n\n\ntest.1 = {\n\ttype = country_event\n\tweight = 1\n}\n\n# vor test.2\ntest.2 = {\n\ttype = country_event\n\tmine = yes\n}\n";
        PutB(Events, Mod);
        var input = UpdateInput(euV: true);

        WriteUpdate(input, RunUpdate(input));

        // Neuer Dateirest kommt hinter seinen Vorgänger (hier den ersten namespace), nicht mehr an den Anfang des ersten Schlüssels.
        var expected = "# Mein Kommentar\nnamespace = test\nnamespace = test2\n\n\ntest.1 = {\n\ttype = country_event\n\tweight = 2\n}\n\n# vor test.2\ntest.2 = {\n\ttype = country_event\n\tmine = yes\n}\n";
        Assert.Equal(expected, ReadMod(Events));
    }

    [Fact]
    public void On_action_blocks_are_separate_places()
    {
        const string OnActions = "in_game/common/on_action/x.txt";
        PutVanilla(OnActions, "on_a = {\n\ton_actions = { a1 }\n}\non_b = {\n\ton_actions = { b1 }\n}\n");
        PutA(OnActions, "on_a = {\n\ton_actions = { a1 a2 }\n}\non_b = {\n\ton_actions = { b1 }\n}\n");
        PutB(OnActions, "on_a = {\n\ton_actions = { a1 }\n}\non_b = {\n\ton_actions = { b1 mine }\n}\n");
        var input = UpdateInput(euV: true, rules: UpdateModRules.From(EuVRules()));

        var run = RunUpdate(input);
        WriteUpdate(input, run);

        Assert.Contains(run.Actions, a => a.Address.Key == "on_a");
        Assert.Contains(run.Actions, a => a.Address.Key == "on_b");
        Assert.DoesNotContain(run.Actions, a => a.Stage == MergeStage.Conflict);
        var text = ReadMod(OnActions);
        Assert.Contains("a1 a2", text);
        Assert.Contains("b1 mine", text);
    }

    [Fact]
    public void File_without_old_version_has_no_automatic_action()
    {
        const string Buildings = "common/buildings/00_buildings.txt";
        PutA(Buildings, "temple = {\n\tcost = 120\n}\nelven_hall = {\n\tcost = 300\n}\nsame = {\n\tcost = 1\n}\n");
        PutB(Buildings, "temple = {\n\tcost = 90\n}\nmine = {\n\tcost = 5\n}\nsame = {\n\tcost = 1\n}\n");
        var input = UpdateInput(noBase: new[] { Buildings });

        var run = RunUpdate(input);

        Assert.NotEmpty(run.Actions);
        Assert.DoesNotContain(run.Actions, a => a.Stage == MergeStage.Automatic && a.Origin != MergeOrigin.Both);
        Assert.All(run.Actions.Where(a => a.Origin != MergeOrigin.Both), a => Assert.Equal(DecisionKind.TakeB, a.DefaultDecision));
        Assert.Contains(run.Actions, a => a.Note is { } note && note.Contains(CompatchTexts.NoteNoOldVersion, StringComparison.Ordinal));
    }

    [Fact]
    public void File_without_old_version_lists_only_real_differences()
    {
        // CK III 00_early_medieval_innovations.txt (Nutzerbefund 2026-09-30): ohne alte Fassung wurden gleiche Stellen zu Stellen im Baum,
        // in Logikblöcken sogar zum Prüfen.
        const string Innovations = "common/culture/innovations/00_x.txt";
        const string Same = "@knight = \"gfx/knight.dds\"\ninnovation_saddle = {\n\tskill = martial\n\ticon = @knight\n\tai_weight_for_fascination = {\n\t\tvalue = 100\n\t\tif = {\n\t\t\tlimit = { NOT = { has_cultural_era_or_later = culture_era_early_medieval } }\n\t\t\tmultiply = 0\n\t\t}\n\t}\n}\n";
        PutA(Innovations, Same + "innovation_bows = {\n\tskill = martial\n}\n");
        PutB(Innovations, Same + "innovation_bows = {\n\tskill = stewardship\n}\n");

        var input = UpdateInput(noBase: new[] { Innovations });
        var run = RunUpdate(input);

        var action = Assert.Single(run.Actions);
        Assert.Equal("innovation_bows", action.Address.Key);
        Assert.Equal(DecisionKind.TakeB, action.DefaultDecision);

        WriteUpdate(input, run);
        Assert.Equal(Same + "innovation_bows = {\n\tskill = stewardship\n}\n", ReadMod(Innovations));
    }

    [Fact]
    public void Only_paths_skips_other_mod_files()
    {
        const string In = "common/buildings/00_buildings.txt";
        const string Out = "common/buildings/01_other.txt";
        PutVanilla(In, "temple = {\n\tcost = 100\n}\n");
        PutA(In, "temple = {\n\tcost = 120\n}\n");
        PutB(In, "temple = {\n\tcost = 100\n}\n");
        PutB(Out, "market = {\n\tcost = 5\n}\n");
        var input = UpdateInput(filter: new CompatchFileFilter(CompatchFileFilter.DefaultHiddenExtensions, Array.Empty<string>(), skipLocked: false, onlyPaths: new[] { In }));

        var resolved = EffectiveEntryResolver.Resolve(input);

        Assert.Contains(resolved.Skipped, s => s.RelativePath == Out && s.Reason == SkipReason.NotInRun);
        Assert.DoesNotContain(resolved.Addresses.Keys, a => a.Key == "market");
    }
}
