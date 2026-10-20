using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Format der Doku von Hearts of Iron IV (<c>documentation/*.md</c> im Installationsordner); Auszüge aus den echten Dateien von 1.19.3.</summary>
public class Hoi4DocParserTests
{
    private const string EffectsHead =
        "# Effects\n" +
        "\n" +
        "## Table of Content\n" +
        "\n" +
        "* [CHARACTER](#effects-for-scope-character)\n" +
        "* [COUNTRY](#effects-for-scope-country)\n" +
        "\n" +
        "## Effects for scope CHARACTER\n" +
        "\n" +
        "* [add_advisor_role](#add_advisor_role)\n" +
        "* [add_attack](#add_attack)\n" +
        "\n";

    private const string AddAdvisorRole =
        "## add_advisor_role\n" +
        "\n" +
        "* Supported Scopes: COUNTRY, CHARACTER\n" +
        "* Supported Targets: none\n" +
        "\n" +
        "```\n" +
        "add advisor role to character\n" +
        "May directly activate (aka hire) using activate = yes\n" +
        "\n" +
        "### Example\n" +
        "add_advisor_role = {\n" +
        "\tcharacter = \"GER_Character_Token\" # optional if inside character scope\n" +
        "\tactivate = yes\n" +
        "}\n" +
        "\n" +
        "```\n" +
        "\n";

    private const string AddAiStrategy =
        "## add_ai_strategy\n" +
        "\n" +
        "* Supported Scopes: COUNTRY\n" +
        "* Supported Targets: THIS, ROOT, PREV, FROM, OWNER, CONTROLLER, OCCUPIED, CAPITAL\n" +
        "\n" +
        "```\n" +
        "Adds strategy entry to country AI\n" +
        "```\n";

    [Fact]
    public void Effects_skip_toc_and_scope_sections()
    {
        var text = EffectsHead + AddAdvisorRole + AddAiStrategy;

        Assert.True(ScriptDocLogParser.IsHoi4Documentation(text));
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Effect, text);

        Assert.Equal(new[] { "add_advisor_role", "add_ai_strategy" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Effect, e.Kind));
        Assert.Equal(new[] { "COUNTRY", "CHARACTER" }, entries[0].Scopes);
        Assert.Empty(entries[0].Targets);
        Assert.Equal(new[] { "COUNTRY" }, entries[1].Scopes);
        Assert.Equal(new[] { "THIS", "ROOT", "PREV", "FROM", "OWNER", "CONTROLLER", "OCCUPIED", "CAPITAL" }, entries[1].Targets);
        Assert.Equal("Adds strategy entry to country AI", entries[1].Description);
    }

    [Fact]
    public void Fenced_description_keeps_inner_headings()
    {
        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Effect, EffectsHead + AddAdvisorRole));

        Assert.Contains("### Example", entry.Description);
        Assert.Contains("add_advisor_role = {", entry.Description);
        Assert.StartsWith("add advisor role to character", entry.Description);
        Assert.DoesNotContain("```", entry.Description);
    }

    [Fact]
    public void Any_and_none_scopes_mean_no_scope()
    {
        var text =
            "# Triggers\n\n## Table of Content\n\n* [any](#triggers-for-scope-any)\n\n## Triggers for scope any\n\n* [always](#always)\n\n" +
            "## always\n\n* Supported Scopes: any\n* Supported Targets: none\n\n```\nalways = yes\n```\n";

        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Trigger, text));

        Assert.Equal("always", entry.Name);
        Assert.Empty(entry.Scopes);
        Assert.Empty(entry.Targets);
    }

    private const string ModifiersHead =
        "# Modifiers\n" +
        "\n" +
        "Modifiers listed here are recognied by the game, but not necessarily used\n" +
        "in any context.\n" +
        "\n" +
        "## Table of Content\n" +
        "\n" +
        "* [army](#modifiers-for-scope-army)\n" +
        "\n" +
        "## Modifiers for scope army\n" +
        "\n" +
        "* [army_org_factor](#army_org_factor)\n" +
        "\n";

    [Fact]
    public void Modifiers_read_decimals_and_categories()
    {
        var text = ModifiersHead +
            "## army_org_factor\n\n* Number with 1 decimal places\n* **Categories**: army, defensive, aggressive\n\n" +
            "## annex_cost_factor\n\n* **Description**: Cost of annexing.\n* Number with 0 decimal places\n* **Categories**: peace\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, text);

        Assert.Equal(new[] { "army_org_factor", "annex_cost_factor" }, entries.Select(e => e.Name));
        Assert.Equal("1", entries[0].Extra["Decimals"]);
        Assert.Equal("army, defensive, aggressive", entries[0].Extra["Category"]);
        Assert.Empty(entries[0].Scopes);
        Assert.Equal("0", entries[1].Extra["Decimals"]);
        Assert.Equal("Cost of annexing.", entries[1].Description);
    }

    [Fact]
    public void Modifier_templates_expand_from_modified_types()
    {
        var text = ModifiersHead +
            "##  <span id=\"_max_level_terrain_limit\"></span><Building>_max_level_terrain_limit\n\n" +
            "* **Description**: Maximum allowed building level for terrain.\n* Number with 0 decimal places\n* **Categories**: country\n\n" +
            "* **Modified types**: air_base, arms_factory\n\n" +
            "##  <span id=\"local_resources__factor\"></span>local_resources_<Resource>_factor\n\n" +
            "* Number with 2 decimal places\n* **Categories**: state\n\n" +
            "## army_org_factor\n\n* Number with 1 decimal places\n* **Categories**: army\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, text);

        Assert.Equal(new[] { "air_base_max_level_terrain_limit", "arms_factory_max_level_terrain_limit", "army_org_factor" }, entries.Select(e => e.Name));
        Assert.Equal("0", entries[0].Extra["Decimals"]);
        Assert.Equal("Maximum allowed building level for terrain.", entries[1].Description);
    }

    [Fact]
    public void Dynamic_variables_take_scope_from_section()
    {
        var text =
            "# Dynamic Variables\n\nDynamic variables are read only variables that can be used in effects &\ntriggers that accept variables.\n\n" +
            "## Table of Content\n\n* [global](#dynamic-variables-for-scope-global)\n* [country](#dynamic-variables-for-scope-country)\n\n" +
            "## Dynamic variables for scope country\n\n" +
            "### agency_upgrade_number\n* description: Checks the number of upgrade done in the intelligence agency. \nagency_upgrade_number > 4\n* (Auto generated using the trigger with same name)\n\n" +
            "### ai_attitude_is_threatened\n* description: returns 1 if ai is threatened\n\n" +
            "## Dynamic variables for scope global\n\n### year\n* description: current year\n";

        var entries = ScriptDocLogParser.Parse(ScriptDocKind.DynamicVariable, text);

        Assert.Equal(new[] { "agency_upgrade_number", "ai_attitude_is_threatened", "year" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.DynamicVariable, e.Kind));
        Assert.Equal(new[] { "country" }, entries[0].Scopes);
        Assert.StartsWith("Checks the number of upgrade done in the intelligence agency.", entries[0].Description);
        Assert.Contains("agency_upgrade_number > 4", entries[0].Description);
        Assert.Equal("returns 1 if ai is threatened", entries[1].Description);
        Assert.Equal(new[] { "global" }, entries[2].Scopes);
    }

    [Fact]
    public void Dynamic_variables_need_hoi4_text()
    {
        Assert.Empty(ScriptDocLogParser.Parse(ScriptDocKind.DynamicVariable, "# Effect Documentation\n## foo\nBar\n"));
    }

    [Fact]
    public void Eu5_markdown_is_not_taken_for_hoi4()
    {
        var text = "# Trigger Documentation\n## active_religious_focus\nChecks if a country is researching a certain religious focus\n**Supported Scopes**: country  \n**Supported Targets**: mercenary  \n";

        Assert.False(ScriptDocLogParser.IsHoi4Documentation(text));
        var entry = Assert.Single(ScriptDocLogParser.Parse(ScriptDocKind.Trigger, text));
        Assert.Equal(new[] { "country" }, entry.Scopes);
        Assert.Equal(new[] { "mercenary" }, entry.Targets);
    }

    [Fact]
    public void Display_name_and_default_file_of_dynamic_variables()
    {
        Assert.Equal("Dynamic Variable", ScriptDocLogFiles.DisplayNameOf(ScriptDocKind.DynamicVariable));
        Assert.Equal("dynamic_variables.log", ScriptDocLogFiles.FileNameOf(ScriptDocKind.DynamicVariable));
    }
}
