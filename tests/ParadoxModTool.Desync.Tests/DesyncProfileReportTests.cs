using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncProfileReportTests
{
    private static string ProfilePath => Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json");

    [Fact]
    public void The_shipped_eu5_profile_defines_the_paradox_form_in_form_order()
    {
        var p = DesyncProfile.Load(ProfilePath);

        Assert.Equal(new[]
        {
            "I have verified my game files (on Steam)", "I have disabled all mods", "Summary", "Description", "Issue Type",
            "Steps to reproduce", "Game Version", "Bug Type", "Attachments", "Screenshot",
        }, p.ParadoxForm!.Select(f => f.Label));
        Assert.Equal("Out of Sync", p.ParadoxForm!.Single(f => f.Label == "Bug Type").Value);
        Assert.Equal(
            new[]
            {
                FormSource.VerifiedFiles, FormSource.ModsDisabled, FormSource.Summary, FormSource.Description, FormSource.IssueType,
                FormSource.Steps, FormSource.GameVersion, FormSource.Fixed, FormSource.Dumps, FormSource.Screenshot,
            },
            p.ParadoxForm!.Select(f => f.Source));
        Assert.Equal(
            new[]
            {
                FormFieldKind.Hint, FormFieldKind.Option, FormFieldKind.Text, FormFieldKind.Multiline, FormFieldKind.Hint,
                FormFieldKind.Multiline, FormFieldKind.Option, FormFieldKind.Option, FormFieldKind.Attachments, FormFieldKind.Hint,
            },
            p.ParadoxForm!.Select(f => f.Kind));
    }

    [Fact]
    public void An_unknown_form_source_is_a_load_error_naming_the_field()
    {
        var ex = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "paradoxForm": { "fields": [ { "label": "Platform", "kind": "option", "source": "platform" } ] } }"""));

        Assert.Contains("Platform", ex.Message);
    }

    [Fact]
    public void An_unknown_field_kind_is_a_load_error_naming_the_field()
    {
        var ex = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "paradoxForm": { "fields": [ { "label": "Summary", "kind": "dropdown", "source": "summary" } ] } }"""));

        Assert.Contains("Summary", ex.Message);
    }

    [Fact]
    public void A_form_field_without_label_is_a_load_error()
    {
        Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "paradoxForm": { "fields": [ { "kind": "text", "source": "summary" } ] } }"""));
    }

    [Fact]
    public void An_advice_rule_without_trigger_or_with_two_triggers_is_a_load_error_naming_the_rule()
    {
        var none = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "when": { "warning": "DifferentDlcs" }, "text": "a" }, { "when": { }, "text": "b" } ] }"""));
        var two = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "when": { "warning": "DifferentDlcs", "checksum": "RANDOM_COUNT" }, "text": "a" } ] }"""));
        var missing = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "text": "a" } ] }"""));
        var noText = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "when": { "leadInMod": true } } ] }"""));

        Assert.Contains("2", none.Message);
        Assert.Contains("1", two.Message);
        Assert.Contains("1", missing.Message);
        Assert.Contains("1", noText.Message);
    }

    [Fact]
    public void An_advice_rule_with_an_unknown_warning_name_or_a_false_lead_flag_is_a_load_error()
    {
        var warning = Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "when": { "warning": "NoSuchWarning" }, "text": "a" } ] }"""));
        Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse(
            """{ "advice": [ { "when": { "leadInMod": false }, "text": "a" } ] }"""));

        Assert.Contains("NoSuchWarning", warning.Message);
    }

    [Fact]
    public void A_profile_without_paradox_form_has_none_and_no_advice()
    {
        var p = DesyncProfile.Parse("{ }");

        Assert.Null(p.ParadoxForm);
        Assert.Empty(p.Advice);
    }

    [Fact]
    public void The_shipped_advice_rules_cover_dlcs_mods_game_commit_random_count_and_mod_leads()
    {
        var advice = DesyncProfile.Load(ProfilePath).Advice;

        Assert.Equal(
            new (AdviceTrigger, string?)[]
            {
                (AdviceTrigger.Warning, "DifferentGameCommit"),
                (AdviceTrigger.Warning, "DifferentDlcs"),
                (AdviceTrigger.Warning, "DifferentMods"),
                (AdviceTrigger.Checksum, "RANDOM_COUNT"),
                (AdviceTrigger.LeadInMod, null),
            },
            advice.Select(rule => (rule.Trigger, rule.Key)));
        Assert.Equal("All players must run the same game build: {detail}.", advice[0].Text);
        Assert.Equal("Make sure all players have the same DLCs enabled. Differing: {detail}.", advice[1].Text);
        Assert.Equal("Make sure all players use the same mods and mod versions. Differing: {detail}.", advice[2].Text);
        Assert.Equal("Check these script locations first: {leads}.", advice[4].Text);
        Assert.Contains("random, random_list, weights", advice[3].Text);
    }
}
