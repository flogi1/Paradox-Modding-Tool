using System.Text.RegularExpressions;
using ParadoxModTool.Generators.Tests.DynamicIdeas;
using ParadoxModTool.Generators.Tests.IdeaGroups;
using ParadoxModTool.Generators.Tests.Iv2;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// EU V sucht für KI-Aktionen den Nachrichtentyp <c>PERFORM_&lt;key&gt;_ACTION</c> neben <c>WE_PERFORM_&lt;key&gt;_ACTION</c> (error.log 2026-10-30): die drei
/// IV2-Pakete schreiben zu jedem <c>WE_PERFORM_</c>-Eintrag den Zwilling mit gleichem Inhalt und dessen Lokalisierung (Schlüssel wie Vanilla, mit <c>_DESC</c>).
/// </summary>
public sealed class PerformTwinTests : IDisposable
{
    private const string Iv2New = "main_menu/gui/messagetypes/01_IV2_messagetypes.txt";
    private const string DynamicNew = "main_menu/gui/messagetypes/01_IV2_dynamic_ideas_messagetypes.txt";
    private const string GroupsNew = "main_menu/gui/messagetypes/01_IV2_idea_groups_messagetypes.txt";
    private const string MessagesLoc = "main_menu/localization/english/03_IV2_messages_l_english.yml";

    private static readonly string[] Languages = { "english", "german" };

    private static readonly string[] LocSuffixes = { "HEADER", "TITLE", "DESC", "EFFECTS", "LOG", "BTN1", "BTN2", "BTN3", "MAP", "SETUP" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Mod => _temp.PathOf("mod");

    /// <summary>Wie oft eine Zeile mit genau diesem Anfang im Text steht (Zeilenanfang, damit <c>PERFORM_</c> nicht in <c>WE_PERFORM_</c> zählt).</summary>
    private static int Lines(string text, string start) => Regex.Matches(text, "(?m)^[ \\t]*" + Regex.Escape(start)).Count;

    private static string Read(string mod, string path) => File.ReadAllText(Path.Combine(mod, path.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    private static void AssertTwin(string messagetypes, string loc, string action)
    {
        Assert.Equal(1, Lines(messagetypes, $"WE_PERFORM_{action}_ACTION={{"));
        Assert.Equal(1, Lines(messagetypes, $"PERFORM_{action}_ACTION={{"));
        foreach (var suffix in LocSuffixes)
        {
            Assert.Equal(1, Lines(loc, $"WE_PERFORM_{action}_ACTION_{suffix}:"));
            Assert.Equal(1, Lines(loc, $"PERFORM_{action}_ACTION_{suffix}:"));
        }
    }

    /// <summary>Gleiche Einstellungen wie das Gegenstück: der Zwilling ist der Eintrag mit anderem Namen.</summary>
    private static void AssertSameBody(string messagetypes, string action)
    {
        string Body(string head)
        {
            var start = messagetypes.IndexOf(head, StringComparison.Ordinal);
            Assert.True(start >= 0, head);
            var open = start + head.Length;
            return messagetypes[open..messagetypes.IndexOf('}', open)];
        }

        Assert.Equal(Body($"\nWE_PERFORM_{action}_ACTION={{"), Body($"\nPERFORM_{action}_ACTION={{"));
    }

    private static void Apply(GeneratorPlan plan) => SlicePackage.Write(plan);

    private static void AssertNoChanges(GeneratorPlan plan) =>
        Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, f.RelativePath + " " + f.Status));

    [Fact]
    public void Iv2_WritesPerformTwin()
    {
        using var fixture = new Iv2Fixture();

        Apply(fixture.Plan(fixture.State(), fixture.FreshMod));

        var messagetypes = Read(fixture.FreshMod, Iv2New);
        var loc = Read(fixture.FreshMod, "main_menu/localization/english/04_IV2_TST_l_english.yml");
        AssertTwin(messagetypes, loc, "iv2_ga_select_historic_TST_1");
        AssertTwin(messagetypes, loc, "iv2_ga_deselect_historic_TST_1");
        AssertSameBody(messagetypes, "iv2_ga_select_historic_TST_1");
    }

    [Fact]
    public void Iv2_SecondRun_NoChanges()
    {
        using var fixture = new Iv2Fixture();
        Apply(fixture.Plan(fixture.State(), fixture.FreshMod));

        AssertNoChanges(fixture.Plan(fixture.State(), fixture.FreshMod));
    }

    private (GeneratorPackage Package, GeneratorState State) DynamicIdeas()
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), _temp.PathOf("data-dyn"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in DynamicIdeasPackageTests.Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }

        var state = GeneratorState.CreateEmpty(package, Languages);
        PackageKey.Apply(package, state, "army_1");
        state.Head.Values["name"] = "Test Idea 1";
        var levels = state.Head.Children["levels"];
        for (var i = 0; i < levels.Count; i++)
        {
            levels[i].Values["modifiers"] = $"global_tax_modifier = 0.{i + 1}";
        }

        return (package, state);
    }

    private (GeneratorPackage Package, GeneratorState State) IdeaGroups()
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-idea-groups"), _temp.PathOf("data-grp"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in IdeaGroupsPackageTests.Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }

        var state = GeneratorState.CreateEmpty(package, Languages);
        PackageKey.Apply(package, state, "adm_1");
        state.Head.Values["title"] = "Test Ideas 1";
        var ideas = state.Head.Children["ideas"];
        for (var i = 0; i < ideas.Count; i++)
        {
            ideas[i].Values["name"] = $"Idea 1-{i + 1}";
            ideas[i].Values["desc"] = $"Description {i + 1}";
            ideas[i].Values["modifiers"] = $"global_tax_modifier = 0.{i + 1}";
        }

        return (package, state);
    }

    private GeneratorPlan Plan((GeneratorPackage Package, GeneratorState State) run) =>
        GeneratorPlanner.Plan(run.Package, run.State, new GeneratorTarget(Mod, null, Languages));

    [Fact]
    public void DynamicIdeas_WritesPerformTwin()
    {
        var run = DynamicIdeas();

        Apply(Plan(run));

        var messagetypes = Read(Mod, DynamicNew);
        var loc = Read(Mod, MessagesLoc);
        AssertTwin(messagetypes, loc, "iv2_ga_select_national_idea_army_1");
        AssertTwin(messagetypes, loc, "iv2_ga_deselect_nat_idea_army_1");
        AssertSameBody(messagetypes, "iv2_ga_deselect_nat_idea_army_1");
        AssertNoChanges(Plan(run));
    }

    [Fact]
    public void IdeaGroups_WritesPerformTwin()
    {
        var run = IdeaGroups();

        Apply(Plan(run));

        var messagetypes = Read(Mod, GroupsNew);
        var loc = Read(Mod, MessagesLoc);
        AssertTwin(messagetypes, loc, "iv2_ga_select_ideagroup_adm_1");
        AssertTwin(messagetypes, loc, "iv2_ga_deselect_ideagroup_adm_1");
        AssertTwin(messagetypes, loc, "iv2_ga_ideagroup_adm_1_1");
        AssertSameBody(messagetypes, "iv2_ga_ideagroup_adm_1_1");
        AssertNoChanges(Plan(run));
    }

    /// <summary>Mod aus der Zeit vor den Zwillingen: der erste Lauf ergänzt sie genau einmal, der zweite ändert nichts.</summary>
    [Fact]
    public void DynamicIdeas_ExistingModWithOnlyWePerform_AddsTwinsOnce()
    {
        var run = DynamicIdeas();
        Apply(Plan(run));
        var path = Path.Combine(Mod, DynamicNew.Replace('/', Path.DirectorySeparatorChar));
        var withoutTwins = Regex.Replace(Read(Mod, DynamicNew), "(?m)^PERFORM_[^\n]*=\\{\n(?:[^\n]*\n)*?\\}\n", string.Empty);
        Assert.Equal(0, Lines(withoutTwins, "PERFORM_iv2_ga_select_national_idea_army_1_ACTION={"));
        File.WriteAllText(path, withoutTwins);

        Apply(Plan(run));

        var messagetypes = Read(Mod, DynamicNew);
        Assert.Equal(1, Lines(messagetypes, "PERFORM_iv2_ga_select_national_idea_army_1_ACTION={"));
        Assert.Equal(1, Lines(messagetypes, "WE_PERFORM_iv2_ga_select_national_idea_army_1_ACTION={"));
        AssertNoChanges(Plan(run));
    }

    [Fact]
    public void IdeaGroups_ExistingModWithOnlyWePerform_AddsTwinsOnce()
    {
        var run = IdeaGroups();
        Apply(Plan(run));
        var path = Path.Combine(Mod, GroupsNew.Replace('/', Path.DirectorySeparatorChar));
        var withoutTwins = Regex.Replace(Read(Mod, GroupsNew), "(?m)^PERFORM_[^\n]*=\\{\n(?:[^\n]*\n)*?\\}\n", string.Empty);
        File.WriteAllText(path, withoutTwins);

        Apply(Plan(run));

        var messagetypes = Read(Mod, GroupsNew);
        Assert.Equal(1, Lines(messagetypes, "PERFORM_iv2_ga_ideagroup_adm_1_1_ACTION={"));
        Assert.Equal(1, Lines(messagetypes, "WE_PERFORM_iv2_ga_ideagroup_adm_1_1_ACTION={"));
        AssertNoChanges(Plan(run));
    }
}
