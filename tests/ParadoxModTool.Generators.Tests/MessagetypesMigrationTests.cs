using ParadoxModTool.Generators.Tests.DynamicIdeas;
using ParadoxModTool.Generators.Tests.IdeaGroups;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// Die drei IV2-Pakete schreiben ihre EU-V-Nachrichtentypen in eigene Dateien im Ordner <c>main_menu/gui/messagetypes/</c> (das Spiel liest die alte
/// <c>messagetypes.txt</c> nicht mehr) und räumen ihre Einträge per <c>migrate</c> aus der alten Datei ab; fremde Einträge bleiben dort.
/// </summary>
public sealed class MessagetypesMigrationTests : IDisposable
{
    private const string Old = "main_menu/gui/messagetypes.txt";
    private const string Iv2New = "main_menu/gui/messagetypes/01_IV2_messagetypes.txt";
    private const string DynamicNew = "main_menu/gui/messagetypes/01_IV2_dynamic_ideas_messagetypes.txt";
    private const string GroupsNew = "main_menu/gui/messagetypes/01_IV2_idea_groups_messagetypes.txt";

    private static readonly string[] Languages = { "english", "german" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Mod => _temp.PathOf("mod");

    private static string Entry(string action) =>
        $"WE_PERFORM_{action}_ACTION={{\nlog=yes\nonmap=no\npopup=no\nidle=no\noption=yes\npausepopup=no\nsound=no\nmessage_category = economy\n}}\n";

    /// <summary>Wie oft der Eintrag (Kopfzeile) im Text steht.</summary>
    private static int Count(string text, string action) => text.Split($"WE_PERFORM_{action}_ACTION={{").Length - 1;

    /// <summary>Alte Datei: zwei Einträge je Paket (einer davon nicht im Lauf), dazu ein fremder Eintrag.</summary>
    private static string OldFile() =>
        Entry("iv2_ga_select_historic_TST_1") + Entry("iv2_ga_deselect_historic_TST_1") + Entry("iv2_ga_select_historic_OTH_1") +
        Entry("iv2_ga_select_national_idea_army_1") + Entry("iv2_ga_deselect_nat_idea_army_1") + Entry("iv2_ga_select_national_idea_navy_1") +
        Entry("iv2_ga_select_ideagroup_adm_1") + Entry("iv2_ga_ideagroup_adm_1_1") + Entry("iv2_ga_select_ideagroup_adm_2") +
        "FOREIGN_ACTION={\n}\n";

    private static void Apply(GeneratorPlan plan)
    {
        SlicePackage.Write(plan);
        foreach (var file in plan.Files.Where(f => f.Delete))
        {
            File.Delete(Path.Combine(plan.WriteRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    private static void WriteOld(string mod, string text)
    {
        var path = Path.Combine(mod, Old.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string Read(string mod, string path) => File.ReadAllText(Path.Combine(mod, path.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    [Fact]
    public void Iv2_MovesOwnEntries_KeepsOthers()
    {
        using var fixture = new Iv2Fixture();
        WriteOld(fixture.FreshMod, OldFile());

        var plan = fixture.Plan(fixture.State(), fixture.FreshMod);
        Apply(plan);

        var created = Read(fixture.FreshMod, Iv2New);
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_TST_1"));
        Assert.Equal(1, Count(created, "iv2_ga_deselect_historic_TST_1"));
        Assert.Contains(Entry("iv2_ga_select_historic_OTH_1"), created);
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_OTH_1"));
        Assert.DoesNotContain("national_idea", created);
        Assert.DoesNotContain("ideagroup", created);
        Assert.DoesNotContain("FOREIGN", created);
        var old = Read(fixture.FreshMod, Old);
        Assert.DoesNotContain("historic", old);
        Assert.Contains(Entry("iv2_ga_select_national_idea_army_1"), old);
        Assert.Contains(Entry("iv2_ga_select_ideagroup_adm_1"), old);
        Assert.Contains("FOREIGN_ACTION={\n}", old);
        Assert.Contains(plan.Warnings, w => w.Contains(Old, StringComparison.Ordinal));
    }

    [Fact]
    public void Iv2_OldFileOnlyOwn_IsDeleted()
    {
        using var fixture = new Iv2Fixture();
        WriteOld(fixture.FreshMod, Entry("iv2_ga_select_historic_OTH_1") + Entry("iv2_ga_select_historic_TST_1"));

        Apply(fixture.Plan(fixture.State(), fixture.FreshMod));

        Assert.False(File.Exists(Path.Combine(fixture.FreshMod, "main_menu", "gui", "messagetypes.txt")));
        var created = Read(fixture.FreshMod, Iv2New);
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_OTH_1"));
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_TST_1"));
    }

    [Fact]
    public void Iv2_SecondRunForOtherTag_ReplacesMovedEntries()
    {
        using var fixture = new Iv2Fixture();
        WriteOld(fixture.FreshMod, OldFile());
        Apply(fixture.Plan(fixture.State(), fixture.FreshMod));

        // Lauf für den Tag OTH: dessen verschobene Einträge werden an Ort und Stelle ersetzt, nicht verdoppelt
        var other = fixture.State();
        other.Key = "OTH";
        other.Head.Values["tag"] = "OTH";
        Apply(fixture.Plan(other, fixture.FreshMod));

        var created = Read(fixture.FreshMod, Iv2New);
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_OTH_1"));
        Assert.Equal(1, Count(created, "iv2_ga_select_historic_TST_1"));
        var start = created.IndexOf("# PMT-Generator iv2/messagetypes/OTH", StringComparison.Ordinal);
        Assert.True(start >= 0, created);
        Assert.Contains("WE_PERFORM_iv2_ga_select_historic_OTH_1_ACTION={", created[start..]);
    }

    [Fact]
    public void DynamicIdeas_MovesOwnEntries_KeepsOthers()
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), _temp.PathOf("data-dyn"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in DynamicIdeasPackageTests.Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }

        WriteOld(Mod, OldFile());
        var state = GeneratorState.CreateEmpty(package, Languages);
        PackageKey.Apply(package, state, "army_1");
        state.Head.Values["name"] = "Test Idea 1";
        var levels = state.Head.Children["levels"];
        for (var i = 0; i < levels.Count; i++)
        {
            levels[i].Values["modifiers"] = $"global_tax_modifier = 0.{i + 1}";
        }

        Apply(GeneratorPlanner.Plan(package, state, new GeneratorTarget(Mod, null, Languages)));

        var created = Read(Mod, DynamicNew);
        Assert.Equal(1, Count(created, "iv2_ga_select_national_idea_army_1"));
        Assert.Equal(1, Count(created, "iv2_ga_deselect_nat_idea_army_1"));
        Assert.Contains(Entry("iv2_ga_select_national_idea_navy_1"), created);
        Assert.DoesNotContain("historic", created);
        Assert.DoesNotContain("ideagroup", created);
        Assert.DoesNotContain("FOREIGN", created);
        var old = Read(Mod, Old);
        Assert.DoesNotContain("national_idea", old);
        Assert.DoesNotContain("nat_idea", old);
        Assert.Contains(Entry("iv2_ga_select_historic_TST_1"), old);
        Assert.Contains(Entry("iv2_ga_select_ideagroup_adm_1"), old);
        Assert.Contains("FOREIGN_ACTION={\n}", old);
    }

    [Fact]
    public void IdeaGroups_MovesOwnEntries_KeepsOthers()
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-idea-groups"), _temp.PathOf("data-grp"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in IdeaGroupsPackageTests.Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }

        WriteOld(Mod, OldFile());
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

        Apply(GeneratorPlanner.Plan(package, state, new GeneratorTarget(Mod, null, Languages)));

        var created = Read(Mod, GroupsNew);
        Assert.Equal(1, Count(created, "iv2_ga_select_ideagroup_adm_1"));
        Assert.Equal(1, Count(created, "iv2_ga_ideagroup_adm_1_1"));
        Assert.Contains(Entry("iv2_ga_select_ideagroup_adm_2"), created);
        Assert.DoesNotContain("historic", created);
        Assert.DoesNotContain("national_idea", created);
        Assert.DoesNotContain("FOREIGN", created);
        var old = Read(Mod, Old);
        Assert.DoesNotContain("ideagroup", old);
        Assert.Contains(Entry("iv2_ga_select_historic_TST_1"), old);
        Assert.Contains(Entry("iv2_ga_select_national_idea_army_1"), old);
        Assert.Contains("FOREIGN_ACTION={\n}", old);
    }
}
