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

    // Auszug aus dem modifiers.log von Victoria 3 (deutsche Spieloberfläche, 2026-10-10): Blöcke statt Tag-Zeilen, drei Abschnitte.
    private const string Vic3Modifiers =
        "--- Static modifier types ---\n" +
        "battle_casualties_mult:\n" +
        "  Mask: battle\n" +
        "  Name: Erlittene Verluste\n" +
        "  Description: Ein Bonus oder Abzug auf die Zahl der Verluste\n" +
        "unit_command_limit_mult:\n" +
        "  Mask: unit\n" +
        "  Name: Kommando-Schwelle\n" +
        "  Description: Erste Zeile.\n" +
        "Zweite Zeile ohne Einrückung.\n" +
        "\n" +
        "--- Dynamic modifier types ---\n" +
        "building_academics_mortality_mult:\n" +
        "  Mask: building\n" +
        "  Name: Sterblichkeit\n" +
        "  Description: Eine Erhöhung\n" +
        "odd_modifier_add:\n" +
        "  Mask: \n" +
        "  Name: Ohne Maske\n" +
        "\n" +
        "--- Potential dynamic modifier types ---\n" +
        "building_academics_fertility_mult:\n" +
        "  Mask: building\n" +
        "building_aristocrats_mortality_mult:\n" +
        "  Mask: building\n";

    [Fact]
    public void Vic3_block_format_reads_all_sections()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, Vic3Modifiers);

        Assert.Equal(new[] { "battle_casualties_mult", "unit_command_limit_mult", "building_academics_mortality_mult", "odd_modifier_add", "building_academics_fertility_mult", "building_aristocrats_mortality_mult" },
            entries.Select(e => e.Name));
        Assert.Equal(new[] { "battle" }, entries[0].Scopes);
        Assert.Equal("Erlittene Verluste", entries[0].Extra["Name"]);
        Assert.Equal("Static", entries[0].Extra["Section"]);
        Assert.Equal("Erste Zeile.\nZweite Zeile ohne Einrückung.", entries[1].Description);
        Assert.Equal("Dynamic", entries[2].Extra["Section"]);
        Assert.Empty(entries[3].Scopes);
        Assert.Equal("Potential dynamic", entries[5].Extra["Section"]);
        Assert.Equal(string.Empty, entries[5].Description);
        Assert.False(entries[5].Extra.ContainsKey("Name"));
    }

    [Fact]
    public void Vic3_description_with_several_paragraphs_is_kept_whole()
    {
        const string Log =
            "--- Static modifier types ---\n" +
            "character_command_limit_add:\n" +
            "  Mask: character\n" +
            "  Name: Kommando-Schwelle\n" +
            "  Description: Erster Absatz.\n" +
            "\n" +
            "Zweiter Absatz.\n" +
            "-50% Täglicher Organisations-Zugewinn\n" +
            "next_modifier_add:\n" +
            "  Mask: country\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, Log);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Erster Absatz.\n\nZweiter Absatz.\n-50% Täglicher Organisations-Zugewinn", entries[0].Description);
    }

    [Fact]
    public void Vic3_blank_line_before_the_next_modifier_does_not_split_its_description()
    {
        const string Log =
            "first_add:\n  Mask: country\n  Description: Eins.\n\n" +
            "second_add:\n  Mask: country\n  Description: Zwei.\nweiter\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, Log);

        Assert.Equal("Zwei.\nweiter", entries[1].Description);
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
    public void NoneAnyScope_OfNewerGameBuilds_IsDroppedLikeNone()
    {
        // Neuere Spiel-Builds schreiben "none/any", wo ältere "none" schrieben.
        var onActions = ScriptDocLogParser.Parse(ScriptDocKind.OnAction, OnActions.Replace("Expected Scope: none", "Expected Scope: none/any"));
        var customLoc = ScriptDocLogParser.Parse(ScriptDocKind.CustomLocalization, CustomLoc.Replace("Scope: country", "Scope: none/any"));

        Assert.Empty(onActions[0].Scopes);
        Assert.Empty(customLoc[0].Scopes);
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

    // Auszüge aus den echten Log-Dateien von Crusader Kings III (script_docs, 2026-09-30).

    private const string Sep = "--------------------";

    [Fact]
    public void Classic_effect_with_target_and_multiline_description()
    {
        var text = "Effect Documentation:\n\n" + Sep + "\n\n" +
            "cancel_great_project - Cancels a great project\n" +
            "\tcancel_great_project = {\n" +
            "\t\tcancel_great_project = this\n" +
            "\t}\n" +
            "Supported Scopes: none\n" +
            "Supported Targets: great_project\n\n" + Sep + "\n";

        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Effect, text));

        Assert.Equal("cancel_great_project", entry.Name);
        Assert.Contains("Cancels a great project", entry.Description);
        Assert.Contains("cancel_great_project = this", entry.Description);
        Assert.Equal(new[] { "none" }, entry.Scopes);
        Assert.Equal(new[] { "great_project" }, entry.Targets);
    }

    [Fact]
    public void Classic_trigger_log_skips_empty_first_entry()
    {
        var text = "Trigger Documentation:\n\n" + Sep + "\n\n\n\n" + Sep + "\n\n" +
            "add_to_temporary_list - Saves a temporary target for use during the trigger execution\n" +
            "This is used to build lists in triggers.\n" +
            "Supported Scopes: none\n\n" + Sep + "\n";

        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Trigger, text));

        Assert.Equal("add_to_temporary_list", entry.Name);
        Assert.Contains("This is used to build lists in triggers.", entry.Description);
    }

    [Fact]
    public void Classic_name_splits_at_first_dash()
    {
        var text = "Effect Documentation:\n\n" + Sep + "\n\n" +
            "make_story_owner -  = character_target  makes the character the new owner of the story\n" +
            "Supported Scopes: story\n" +
            "Supported Targets: character\n";

        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Effect, text));

        Assert.Equal("make_story_owner", entry.Name);
        Assert.StartsWith("= character_target", entry.Description);
        Assert.Equal(new[] { "story" }, entry.Scopes);
    }

    [Fact]
    public void Classic_event_target()
    {
        var text = "Event Target Documentation:\n\n" + Sep + "\n\n" +
            "array_define - Reference the value of a numeric value in an array define: array_define:Namespace|Name|Index. Index is 0-based.\n" +
            "Requires Data: yes\n" +
            "Global Link: yes\n" +
            "Output Scopes: value\n\n" + Sep + "\n";

        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.EventTarget, text));

        Assert.Equal("array_define", entry.Name);
        Assert.Equal("yes", entry.Extra["Requires Data"]);
        Assert.Equal("yes", entry.Extra["Global Link"]);
        Assert.Equal(new[] { "value" }, entry.Targets);
    }

    [Fact]
    public void Ck3_modifier_with_use_areas()
    {
        var text = "Printing Modifier Definitions:\n" +
            "Tag: stationed_$MEN_AT_ARMS_TYPE$_pursuit_add\n" +
            "Use areas: character, province, and county\n\n" +
            "Tag: travel_speed\n" +
            "Use areas: character and terrain\n\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, text);

        Assert.Equal(2, entries.Count);
        Assert.Equal("stationed_$MEN_AT_ARMS_TYPE$_pursuit_add", entries[0].Name);
        Assert.Equal(new[] { "character", "province", "county" }, entries[0].Scopes);
        Assert.Equal(new[] { "character", "terrain" }, entries[1].Scopes);
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
