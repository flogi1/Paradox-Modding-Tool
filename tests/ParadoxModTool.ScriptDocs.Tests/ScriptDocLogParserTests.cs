using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public class ScriptDocLogParserTests
{
    // Auszüge aus den echten Log-Dateien von Europa Universalis V.

    private const string Modifiers =
        "Printing Modifier Definitions:\r\n" +
        "Tag: ai_require_cb_for_war, Categories: Country, , All, \r\n" +
        "Tag: army_logistics_distance, Categories: Unit, , All, \r\n" +
        "Tag: monthly_imperial_authority, Categories: None, , Location, , Country, , Unit, , All, \r\n";

    private const string Triggers =
        "# Trigger Documentation\n" +
        "## active_religious_focus\n" +
        "Checks if a country is researching a certain religious focus\n" +
        "Reads gamestate for all scopes.\n" +
        "**Supported Scopes**: country  \n" +
        "\n" +
        "## any_rented_out_mercenary\n" +
        "Iterate through mercenaries a country has rented out to the market  \n" +
        "any_rented_out_mercenary = { <count=num/all> / <percent=fixed_point> <triggers> }\n" +
        "Reads nothing for all scopes.\n" +
        "**Supported Scopes**: country  \n" +
        "**Supported Targets**: mercenary  \n" +
        "\n" +
        "## add_estate_satisfaction_utility\n" +
        "Utility of adding estate satisfaction\n" +
        "Traits: <, <=, =, !=, >, >=\n" +
        "Reads gamestate for all scopes.\n" +
        "**Supported Scopes**: location, country, character  \n";

    private const string Effects =
        "# Effect Documentation\n" +
        "## abandon_location\n" +
        "Abandons the target location!\n" +
        "**Supported Scopes**: country  \n" +
        "**Supported Targets**: location  \n" +
        "\n" +
        "## activate_situation\n" +
        "activates a situation\n" +
        "**Supported Scopes**: none  \n" +
        "**Supported Targets**: situation  \n";

    private const string EventTargets =
        "# Event Target Documentation\n" +
        "### province_definition\n" +
        "Unknown, add something in code registration\n" +
        "Input Scopes: location, colonial_charter, province\n" +
        "Output Scopes: province_definition\n" +
        "\n" +
        "### advance_type\n" +
        "Unknown, add something in code registration\n" +
        "Requires Data: yes\n" +
        "Global Link: yes\n" +
        "Output Scopes: advance_type\n" +
        "\n" +
        "### compare_date\n" +
        "A comparison trigger eg: root.gold\n" +
        "Wild Card: yes\n" +
        "Output Scopes: date\n";

    private const string OnActions =
        "On Action Documentation:\n" +
        "\n" +
        "--------------------\n" +
        "\n" +
        "on_tamil_rebel_death:\n" +
        "From Code: No\n" +
        "Expected Scope: none\n" +
        "\n" +
        "--------------------\n" +
        "\n" +
        "yearly_country_pulse:\n" +
        "From Code: Yes\n" +
        "Expected Scope: country\n" +
        "\n" +
        "--------------------\n";

    private const string CustomLoc =
        "Custom Localization Documentation:\n" +
        "\n" +
        "--------------------\n" +
        "\n" +
        "GetHeathenInsult:\n" +
        "\n" +
        "Scope: country\n" +
        "Random Valid: no\n" +
        "\n" +
        "Entries:\n" +
        "religious_insult_devil\n" +
        "religious_insult_fool\n" +
        "\n" +
        "--------------------\n" +
        "\n" +
        "derry_ADJ:\n" +
        "\n" +
        "Scope: location\n" +
        "Random Valid: no\n" +
        "Parent: derry\n" +
        "Suffix: _ADJ\n" +
        "\n" +
        "Entries:\n" +
        "\n" +
        "--------------------\n";

    [Fact]
    public void Modifiers_ReadNameAndCategoriesAsScopes_DroppingAllAndNone()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, Modifiers);

        Assert.Equal(new[] { "ai_require_cb_for_war", "army_logistics_distance", "monthly_imperial_authority" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Modifier, e.Kind));
        Assert.Equal(new[] { "Country" }, entries[0].Scopes);
        Assert.Equal(new[] { "Unit" }, entries[1].Scopes);
        Assert.Equal(new[] { "Location", "Country", "Unit" }, entries[2].Scopes);
    }

    [Fact]
    public void Triggers_ReadDescriptionScopesAndTargets()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Trigger, Triggers);

        Assert.Equal(3, entries.Count);

        var focus = entries[0];
        Assert.Equal("active_religious_focus", focus.Name);
        Assert.Equal("Checks if a country is researching a certain religious focus\nReads gamestate for all scopes.", focus.Description);
        Assert.Equal(new[] { "country" }, focus.Scopes);
        Assert.Empty(focus.Targets);

        var merc = entries[1];
        Assert.Contains("any_rented_out_mercenary = { <count=num/all>", merc.Description);
        Assert.DoesNotContain("  \n", merc.Description);
        Assert.Equal(new[] { "mercenary" }, merc.Targets);

        Assert.Equal(new[] { "location", "country", "character" }, entries[2].Scopes);
        Assert.Contains("Traits: <, <=, =, !=, >, >=", entries[2].Description);
    }

    [Fact]
    public void Effects_ReadScopesAndTargets()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Effect, Effects);

        Assert.Equal(new[] { "abandon_location", "activate_situation" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Effect, e.Kind));
        Assert.Equal(new[] { "country" }, entries[0].Scopes);
        Assert.Equal(new[] { "location" }, entries[0].Targets);
        Assert.Equal("Abandons the target location!", entries[0].Description);
    }

    [Fact]
    public void EventTargets_ReadInputAndOutputScopesAndFlags()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.EventTarget, EventTargets);

        Assert.Equal(3, entries.Count);
        Assert.Equal(new[] { "location", "colonial_charter", "province" }, entries[0].Scopes);
        Assert.Equal(new[] { "province_definition" }, entries[0].Targets);
        Assert.Equal("Unknown, add something in code registration", entries[0].Description);

        Assert.Equal("yes", entries[1].Extra["Requires Data"]);
        Assert.Equal("yes", entries[1].Extra["Global Link"]);
        Assert.Empty(entries[1].Scopes);

        Assert.Equal("yes", entries[2].Extra["Wild Card"]);
        Assert.Equal("A comparison trigger eg: root.gold", entries[2].Description);
    }

    [Fact]
    public void OnActions_ReadOriginAndScope_SkippingHeaderBlock()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.OnAction, OnActions);

        Assert.Equal(new[] { "on_tamil_rebel_death", "yearly_country_pulse" }, entries.Select(e => e.Name));
        Assert.Equal("No", entries[0].Extra["From Code"]);
        Assert.Empty(entries[0].Scopes);
        Assert.Equal("Yes", entries[1].Extra["From Code"]);
        Assert.Equal(new[] { "country" }, entries[1].Scopes);
    }

    [Fact]
    public void CustomLocalization_ReadScopeEntriesAndParent_SkippingHeaderBlock()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.CustomLocalization, CustomLoc);

        Assert.Equal(new[] { "GetHeathenInsult", "derry_ADJ" }, entries.Select(e => e.Name));
        Assert.Equal(new[] { "country" }, entries[0].Scopes);
        Assert.Equal("religious_insult_devil\nreligious_insult_fool", entries[0].Description);
        Assert.Equal("no", entries[0].Extra["Random Valid"]);

        Assert.Equal("derry", entries[1].Extra["Parent"]);
        Assert.Equal("_ADJ", entries[1].Extra["Suffix"]);
        Assert.Equal(string.Empty, entries[1].Description);
    }

    [Fact]
    public void EmptyOrUnknownText_GivesNoEntries()
    {
        foreach (var kind in Enum.GetValues<ScriptDocKind>())
        {
            Assert.Empty(ScriptDocLogParser.Parse(kind, string.Empty));
            Assert.Empty(ScriptDocLogParser.Parse(kind, "irgendein anderer Text\nohne bekanntes Format"));
        }
    }

    [Fact]
    public void RealGameLogs_WhenPresent_ParseToPlausibleCounts()
    {
        // Prüft die Parser gegen die echten Logs dieses Rechners; ohne die Dateien (z. B. auf einem anderen Rechner) läuft nichts.
        var docs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            "Europa Universalis V",
            "docs");
        if (!Directory.Exists(docs))
        {
            return;
        }

        foreach (var kind in Enum.GetValues<ScriptDocKind>())
        {
            var path = Path.Combine(docs, ScriptDocLogFiles.FileNameOf(kind));
            if (!File.Exists(path))
            {
                continue;
            }

            var entries = ScriptDocImporter.MergeDuplicates(ScriptDocLogParser.Parse(kind, File.ReadAllText(path)));
            Assert.True(entries.Count > 100, $"{kind}: nur {entries.Count} Einträge");
            Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
            Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
        }
    }
}
