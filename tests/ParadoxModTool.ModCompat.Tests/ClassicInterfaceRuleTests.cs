using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Klassisches <c>interface/</c> (HOI IV, EU IV, gemessen 2026-10-17): Die Dateien direkt in <c>interface/</c> laden nach Dateinamen über Vanilla und Mods
/// hinweg, danach die Unterordner. HOI IV: Fenster und Sprites, die zuletzt geladene Definition gewinnt. EU IV: Sprites ebenso, Fenster dagegen die zuerst
/// geladene (jede spätere Kopie meldet das Spiel als "Already Defined"). Das Verhalten hängt also an der Hülle (<c>guiTypes</c>, <c>spriteTypes</c>).
/// </summary>
public sealed class ClassicInterfaceRuleTests : CompatTestBase
{
    private static GameProfile Profile(string id)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        return GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", id + ".json"));
    }

    private static CompatResult Run(OverrideRules rules, params CompatParticipant[] participants) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = participants, Rules = rules });

    private static string Window(string name, int x) =>
        "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"" + name + "\"\n\t\tposition = { x = " + x + " y = 0 }\n\t}\n}\n";

    private static string Sprite(string name, string texture) =>
        "spriteTypes = {\n\tspriteType = { name = \"" + name + "\" texturefile = \"" + texture + "\" }\n}\n";

    [Fact]
    public void EuIvInterface_WindowsFirstWins_SpritesLastWins()
    {
        var rules = OverrideRules.FromProfile(Profile("eu4"));
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        // Fenster w: A lädt zuerst (a.gui vor b.gui), obwohl B den höheren Rang hat. Fenster w2: B lädt zuerst (b.gui vor z.gui).
        Put(a, "interface/a.gui", Window("w", 1));
        Put(b, "interface/b.gui", Window("w", 2) + Window("w2", 2));
        Put(a, "interface/z.gui", Window("w2", 1));
        // Sprite s: A lädt zuletzt (z.gfx nach a.gfx), obwohl B den höheren Rang hat.
        Put(a, "interface/z.gfx", Sprite("s", "a.dds"));
        Put(b, "interface/a.gfx", Sprite("s", "b.dds"));

        var findings = Run(rules, a, b).Findings.Where(f => f.Key.Length > 0).ToDictionary(f => f.Key);

        Assert.Equal("A", findings["guiTypes > w"].Winner?.Name);
        Assert.Equal("B", findings["guiTypes > w2"].Winner?.Name);
        Assert.Equal("A", findings["spriteTypes > s"].Winner?.Name);
        Assert.False(findings["guiTypes > w"].Uncertain);
    }

    [Fact]
    public void EuIvInterface_ModWindowLosesToEarlierVanilla()
    {
        var rules = OverrideRules.FromProfile(Profile("eu4"));
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Write("vanilla/interface/frontend.gui", Window("w", 0));
        Put(a, "interface/zz_mod.gui", Window("w", 5));

        var finding = Assert.Single(Run(rules, vanilla, a).Findings, f => f.Key == "guiTypes > w");

        Assert.Equal(CompatKind.LosesToVanilla, finding.Kind);
        Assert.True(finding.Winner?.IsVanilla);
    }

    [Fact]
    public void RootBeforeSubfolder()
    {
        // HOI IV: Unterordner laden nach den Dateien direkt in interface/; der zuletzt geladene gewinnt, auch gegen zz_ und höheren Rang.
        var rules = OverrideRules.FromProfile(Profile("hoi4"));
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/sub/a.gui", Window("w", 1));
        Put(b, "interface/zz_b.gui", Window("w", 2));

        var finding = Assert.Single(Run(rules, a, b).Findings, f => f.Key == "guiTypes > w");

        Assert.Equal("A", finding.Winner?.Name);
    }

    [Fact]
    public void OtherEuIvFolders_Unchanged()
    {
        // Momentaufnahme vor der Änderung: EU IV bekommt keine Ladereihenfolge für alle Ordner, common/ideas bleibt wie es war.
        var rules = OverrideRules.FromProfile(Profile("eu4"));
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Write("vanilla/common/ideas/00_country_ideas.txt", "SWE_ideas = { tag = SWE free = yes }\nFRA_ideas = { tag = FRA }\n");
        Put(a, "common/ideas/zz_a.txt", "SWE_ideas = { tag = SWE free = no }\nmy_ideas = { category = ADM }\n");
        Put(b, "common/ideas/aa_b.txt", "SWE_ideas = { tag = SWE free = yes trigger = { } }\nmy_ideas = { category = DIP }\nFRA_ideas = { tag = FRA x = 1 }\n");
        Put(a, "common/ideas/shared.txt", "x_ideas = { a = 1 }\n");
        Put(b, "common/ideas/shared.txt", "x_ideas = { a = 2 }\n");

        // Ohne Meldungstext: der hängt an der Sprache der Oberfläche.
        var snapshot = string.Join("\n", Run(rules, vanilla, a, b).Findings.Select(f =>
            string.Join("|", f.Kind, f.Severity, f.RelativePath, f.Key, f.Winner?.Name, f.Uncertain)));

        Assert.Equal(IdeasSnapshot, snapshot);
        Assert.All(rules.Rules.Where(r => !string.Equals(r.Folder, "interface", StringComparison.OrdinalIgnoreCase)), r => Assert.Null(r.UnwrapBehaviors));
    }

    /// <summary>Aufgenommen mit dem Stand vor der Änderung (e155a77d); FRA_ideas (nur B gegen Vanilla) bleibt ohne Befund.</summary>
    private const string IdeasSnapshot =
        "KeyConflict|Conflict|common/ideas/aa_b.txt|SWE_ideas|B|True\n"
        + "KeyConflict|Conflict|common/ideas/aa_b.txt|my_ideas|B|True\n"
        + "FileOverride|Conflict|common/ideas/shared.txt||B|True";

    [Fact]
    public void ProfileJson_ReadsUnwrapBehaviors()
    {
        var eu4 = OverrideRules.FromProfile(Profile("eu4")).Match("interface/frontend.gui");
        Assert.Equal(OverrideBehavior.FirstWins, eu4.BehaviorFor("guiTypes"));
        Assert.Equal(OverrideBehavior.LastWins, eu4.BehaviorFor("spriteTypes"));
        Assert.Equal(OverrideBehavior.LastWins, eu4.BehaviorFor("objectTypes"));
        Assert.Equal(OverrideBehavior.LastWins, eu4.BehaviorFor(null));
        Assert.True(eu4.Certain);

        var hoi4 = OverrideRules.FromProfile(Profile("hoi4")).Match("interface/topbar.gui");
        Assert.Equal(OverrideBehavior.LastWins, hoi4.BehaviorFor("guiTypes"));
        Assert.True(hoi4.Certain);

        var json = OverrideRules.FromProfile(GameProfileLoader.LoadFromJson(
            "{ \"id\": \"x\", \"overrideRules\": [ { \"folder\": \"interface\", \"behavior\": \"LastWins\", \"unwrap\": [ \"guiTypes\", \"spriteTypes\" ], \"keyField\": \"name\","
            + " \"unwrapBehaviors\": { \"guiTypes\": \"FirstWins\", \"spriteTypes\": \"LastWins\", \"broken\": \"Nonsense\" } } ] }")).Match("interface/a.gui");
        Assert.Equal(OverrideBehavior.FirstWins, json.BehaviorFor("GUITYPES"));
        Assert.Equal(OverrideBehavior.LastWins, json.BehaviorFor("broken"));
    }

    [Fact]
    public void GameRule_WithSameBehavior_KeepsUnwrapBehaviors()
    {
        var shipped = new OverrideRule("interface", OverrideBehavior.LastWins, false, new[] { "guiTypes" }, "name",
            UnwrapBehaviors: new Dictionary<string, OverrideBehavior> { ["guiTypes"] = OverrideBehavior.FirstWins });

        var rules = new OverrideRules(new[] { shipped }, new[] { new OverrideRule("interface", OverrideBehavior.LastWins, true) });

        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("interface/a.gui").BehaviorFor("guiTypes"));
    }

    [Fact]
    public void GameRule_WithOtherBehavior_DropsUnwrapBehaviors()
    {
        var shipped = new OverrideRule("interface", OverrideBehavior.LastWins, false, new[] { "guiTypes" }, "name",
            UnwrapBehaviors: new Dictionary<string, OverrideBehavior> { ["guiTypes"] = OverrideBehavior.FirstWins });

        var rules = new OverrideRules(new[] { shipped }, new[] { new OverrideRule("interface", OverrideBehavior.WholeFile) });

        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("interface/a.gui").BehaviorFor("guiTypes"));
    }
}
