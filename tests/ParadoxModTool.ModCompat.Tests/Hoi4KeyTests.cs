using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Hearts of Iron IV packt Ideen in zwei Hüllen (<c>ideas = { country = { GER_x = { … } } }</c>) und Entscheidungen in frei benannte Kategorien
/// (<c>GER_category = { GER_decision = { … } }</c>). Der Schlüssel ist der Name darin, mit den Hüllen davor.
/// </summary>
public class Hoi4KeyTests : CompatTestBase
{
    private static OverrideRules Hoi4Rules() => new(new[]
    {
        new OverrideRule("common", OverrideBehavior.LastWins, false),
        new OverrideRule("common/ideas", OverrideBehavior.PerKey, false, new[] { "ideas/*" }),
        new OverrideRule("common/decisions", OverrideBehavior.PerKey, false, new[] { "*" }),
        new OverrideRule("decisions", OverrideBehavior.PerKey, false, new[] { "country_decisions" }),
    });

    private CompatResult Run(params CompatParticipant[] mods) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = mods, Rules = Hoi4Rules() });

    private static IEnumerable<CompatFinding> KeyFindings(CompatResult result) => result.Findings.Where(f => f.Key.Length > 0);

    [Fact]
    public void Two_level_unwrap_keys_each_idea()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/ideas/GER.txt", "ideas = {\n\tcountry = { GER_a = { picture = x } GER_shared = { picture = a } }\n\thidden_ideas = { GER_h = { } }\n}\n");
        Put(b, "common/ideas/zz_mod.txt", "ideas = {\n\tcountry = { GER_b = { picture = y } GER_shared = { picture = b } }\n}\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));

        Assert.Equal("ideas > GER_shared", finding.Key);
    }

    [Fact]
    public void Law_category_flags_are_not_keys()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var path = Put(a, "common/ideas/_economic.txt", "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tundisturbed_isolation = { allowed = { always = no } }\n\t}\n}\n");
        Put(b, "common/ideas/my_laws.txt", "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tmy_economy = { allowed = { always = yes } }\n\t}\n}\n");

        Assert.Empty(KeyFindings(Run(a, b)));
        const string relative = "common/ideas/_economic.txt";
        var keys = ContributionExtractor.Extract(a, relative, path, Hoi4Rules().Match(relative), new ParseCache(), out _)
            .Where(c => c.Key.Length > 0).Select(c => c.Key).ToList();
        Assert.Equal(new[] { "ideas > undisturbed_isolation" }, keys);
    }

    [Fact]
    public void Wildcard_unwrap_keys_decisions_per_category()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/decisions/GER.txt", "GER_cat = {\n\tGER_dec_a = { cost = 5 }\n\tGER_dec = { cost = 1 }\n}\n");
        Put(b, "common/decisions/mod.txt", "GER_cat = {\n\tGER_dec_b = { cost = 5 }\n\tGER_dec = { cost = 2 }\n}\nOTHER_cat = { OTHER_dec = { } }\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));

        Assert.Equal("GER_dec", finding.Key);
    }

    [Fact]
    public void Same_idea_in_another_category_is_reported()
    {
        // Florian 2026-10-17: HOI IV kennt Ideen über alle Kategorien hinweg nur nach ihrem Namen ("Duplicate idea. X is a duplicate.").
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/ideas/a.txt", "ideas = {\n\tcountry = { GER_x = { picture = a } }\n}\n");
        Put(b, "common/ideas/b.txt", "ideas = {\n\thidden_ideas = { GER_x = { picture = b } }\n}\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));

        Assert.Equal("ideas > GER_x", finding.Key);
    }

    [Fact]
    public void Same_decision_in_another_category_is_reported()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/decisions/a.txt", "GER_cat = {\n\tGER_dec = { cost = 1 }\n}\n");
        Put(b, "common/decisions/b.txt", "MY_cat = {\n\tGER_dec = { cost = 2 }\n}\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));

        Assert.Equal("GER_dec", finding.Key);
    }

    [Fact]
    public void Wildcard_skips_scalars_and_unnamed_blocks()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/decisions/a.txt", "@cost = 5\nnamespace = x\nGER_cat = { GER_dec_a = { } }\n");
        Put(b, "common/decisions/b.txt", "@cost = 7\nnamespace = x\nGER_cat = { GER_dec_b = { } }\n");

        Assert.Empty(KeyFindings(Run(a, b)));
    }

    [Fact]
    public void Technology_constants_inside_the_wrapper_are_not_keys()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/decisions/NSB_armor.txt", "technologies = {\n\t@1918 = 0\n\t@1934 = 2\n\tgwtank = { research_cost = 1 }\n}\n");
        Put(b, "common/decisions/zz_armor.txt", "technologies = {\n\t@1918 = 1\n\tmy_tank = { research_cost = 2 }\n}\n");

        Assert.Empty(KeyFindings(Run(a, b)));
    }

    [Fact]
    public void Classic_gui_types_are_keys_by_name()
    {
        // Klassisches interface (HOI IV, EU IV, Stellaris): guiTypes = { containerWindowType = { name = "x" } }; Schlüssel ist der Name, nicht das
        // Jomini-Format der GUI-Einheiten (Befund 2026-10-17 aus der GUI-Editor-Recherche).
        var rules = new OverrideRules(new[]
        {
            new OverrideRule("interface", OverrideBehavior.LastWins, false, new[] { "guiTypes", "spriteTypes" }, "name"),
        });
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/a_topbar.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t\tposition = { x = 0 y = 0 }\n\t}\n}\n");
        Put(b, "interface/b_topbar.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t\tposition = { x = 5 y = 0 }\n\t}\n\tcontainerWindowType = {\n\t\tname = \"b_only\"\n\t}\n}\n");

        var finding = Assert.Single(KeyFindings(CompatAnalyzer.Analyze(new CompatRequest { Participants = new[] { a, b }, Rules = rules })));

        Assert.Equal("guiTypes > topbar", finding.Key);
    }

    [Fact]
    public void Archive_only_mod_does_not_break_the_check()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "no_mans_land.zip", "PK");
        Put(b, "common/ideas/a.txt", "ideas = { country = { GER_a = { } } }\n");

        var result = Run(a, b);

        Assert.Empty(KeyFindings(result));
    }

    [Fact]
    public void One_level_unwrap_unchanged()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "decisions/a.txt", "country_decisions = { dec = { major = yes } flag = yes }\n");
        Put(b, "decisions/b.txt", "country_decisions = { dec = { major = no } flag = yes }\n");

        // Bei einer Ebene zählen Werte in der Hülle weiter als eigene Schlüssel (bisheriges Verhalten, EU IV).
        Assert.Equal(
            new[] { "country_decisions > dec", "country_decisions > flag" },
            KeyFindings(Run(a, b)).Select(f => f.Key).Order(StringComparer.Ordinal));
    }
}
