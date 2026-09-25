using ParadoxModTool.Templates;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class TemplateAnalyzerTests
{
    private const string Single = "idea_1 = {\n\tname = idea_1\n\tdesc = idea_1_desc\n\tcost = 10\n}";

    [Fact]
    public void Single_instance_counts_identifier_digits_but_not_plain_numbers()
    {
        var model = TemplateAnalyzer.Analyze(Single);

        var idea = Assert.Single(model.Points, p => p.Kind == PointKind.Identifier);
        Assert.True(idea.Enabled);
        Assert.Equal(3, idea.Occurrences.Count);
        Assert.Equal(1, idea.Start);
        Assert.Equal(1, idea.Step);

        var cost = Assert.Single(model.Points, p => p.Kind == PointKind.Number);
        Assert.False(cost.Enabled);
        Assert.Equal("cost = 10", cost.Label);
    }

    [Fact]
    public void Render_counts_every_occurrence_and_leaves_disabled_numbers()
    {
        var model = TemplateAnalyzer.Analyze(Single);

        var text = TemplateEngine.Render(model, 3);

        Assert.Contains("idea_1 = {", text);
        Assert.Contains("idea_2 = {\n\tname = idea_2\n\tdesc = idea_2_desc", text);
        Assert.Contains("idea_3 = {", text);
        Assert.Equal(3, text.Split("cost = 10").Length - 1);
        Assert.Contains("}\n\nidea_2", text);
    }

    [Fact]
    public void Examples_are_merged_and_start_and_step_are_learned()
    {
        var snippet = "idea_1 = { cost = 10 }\nidea_2 = { cost = 20 }\nidea_3 = { cost = 30 }";

        var model = TemplateAnalyzer.Analyze(snippet);

        Assert.Equal(3, model.ExampleCount);
        Assert.Equal("idea_1 = { cost = 10 }", model.Text);
        var cost = Assert.Single(model.Points, p => p.Kind == PointKind.Number);
        Assert.True(cost.Enabled);
        Assert.Equal(10, cost.Start);
        Assert.Equal(10, cost.Step);

        var text = TemplateEngine.Render(model, 4);
        Assert.Contains("idea_4 = { cost = 40 }", text);
    }

    [Fact]
    public void Learned_step_can_differ_from_one_and_start_from_the_first_example()
    {
        var model = TemplateAnalyzer.Analyze("tier_5 = { }\ntier_7 = { }\ntier_9 = { }");

        var point = Assert.Single(model.Points);
        Assert.Equal(5, point.Start);
        Assert.Equal(2, point.Step);
    }

    [Fact]
    public void Irregular_counting_is_switched_off_and_flagged()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { }\nidea_2 = { }\nidea_4 = { }");

        var point = Assert.Single(model.Points);
        Assert.True(point.Irregular);
        Assert.False(point.Enabled);
    }

    [Fact]
    public void Values_that_stay_equal_across_examples_do_not_count()
    {
        var model = TemplateAnalyzer.Analyze("a_1 = { cost = 10 }\na_2 = { cost = 10 }");

        var cost = Assert.Single(model.Points, p => p.Kind == PointKind.Number);
        Assert.False(cost.Enabled);
        Assert.True(cost.ConstantInExamples);
        Assert.Contains("cost = 10", TemplateEngine.Render(model, 3));
    }

    [Fact]
    public void Zero_padding_is_kept()
    {
        var model = TemplateAnalyzer.Analyze("namespace = my_mod\nmy_mod.0001 = { }");

        var text = TemplateEngine.Render(model, 3);

        Assert.Contains("my_mod.0002 = { }", text);
        Assert.Contains("my_mod.0003 = { }", text);
    }

    [Fact]
    public void Namespace_line_with_two_events_is_not_mistaken_for_examples()
    {
        var model = TemplateAnalyzer.Analyze("namespace = x\nx.1 = { a = 1 }\nx.2 = { a = 1 }");

        Assert.Equal(2, model.ExampleCount);
        Assert.StartsWith("namespace = x", model.Text);
        Assert.DoesNotContain("x.2", model.Text);
    }

    [Fact]
    public void Two_different_blocks_are_both_generated_per_run()
    {
        var snippet = "idea_1 = { }\nevent_1 = { }";

        var model = TemplateAnalyzer.Analyze(snippet);
        var text = TemplateEngine.Render(model, 2);

        Assert.Equal(0, model.ExampleCount);
        Assert.Contains("idea_2 = { }", text);
        Assert.Contains("event_2 = { }", text);
    }

    [Fact]
    public void Interleaved_examples_are_recognised_as_a_period()
    {
        var snippet = "idea_1 = { }\nevent_1 = { }\nidea_2 = { }\nevent_2 = { }";

        var model = TemplateAnalyzer.Analyze(snippet);

        Assert.Equal(2, model.ExampleCount);
        Assert.Equal("idea_1 = { }\nevent_1 = { }", model.Text);
    }

    [Fact]
    public void Repeated_inner_blocks_become_a_repeat_group_with_inner_step()
    {
        var snippet = "event_1 = {\n\toption = { name = opt_1 }\n\toption = { name = opt_2 }\n\toption = { name = opt_3 }\n}";

        var model = TemplateAnalyzer.Analyze(snippet);

        var group = Assert.Single(model.Groups);
        Assert.Equal(3, group.Count);
        var option = Assert.Single(model.Points, p => p.RepeatGroup == 0);
        Assert.Equal(1, option.InnerStep);
        Assert.Equal(0, option.Step);

        var text = TemplateEngine.Render(model, 2);
        Assert.Equal(1, text.Split("event_1 = {").Length - 1);
        Assert.Contains("event_2 = {\n\toption = { name = opt_1 }\n\toption = { name = opt_2 }\n\toption = { name = opt_3 }\n}", text);
    }

    [Fact]
    public void Repeat_count_is_adjustable()
    {
        var snippet = "e_1 = {\n\toption = { }\n\toption = { }\n}";
        var model = TemplateAnalyzer.Analyze(snippet);

        model.Groups[0].Count = 4;
        var text = TemplateEngine.Render(model, 1);

        Assert.Equal(4, text.Split("option = { }").Length - 1);
    }

    [Fact]
    public void Collapsing_can_be_switched_off()
    {
        var snippet = "idea_1 = { }\nidea_2 = { }";

        var model = TemplateAnalyzer.Analyze(snippet, new AnalyzeOptions(CollapseExamples: false));

        Assert.Equal(snippet, model.Text);
        Assert.Equal(0, model.ExampleCount);
        Assert.Empty(model.Groups);
    }

    [Fact]
    public void Overrides_change_start_step_and_switch()
    {
        var first = TemplateAnalyzer.Analyze(Single);
        var cost = first.Points.Single(p => p.Kind == PointKind.Number);
        var idea = first.Points.Single(p => p.Kind == PointKind.Identifier);

        var model = TemplateAnalyzer.Analyze(Single, null, new[]
        {
            new PointOverride(cost.Key, Enabled: true, Start: 100, Step: 25),
            new PointOverride(idea.Key, Start: 7),
        });

        var text = TemplateEngine.Render(model, 2);
        Assert.Contains("idea_7 = {", text);
        Assert.Contains("idea_8 = {", text);
        Assert.Contains("cost = 100", text);
        Assert.Contains("cost = 125", text);
    }

    [Fact]
    public void Decimals_dates_and_negative_numbers_are_ignored()
    {
        var model = TemplateAnalyzer.Analyze("x = { a = 1.5 b = -3 c = 1444.11.11 }");

        Assert.Empty(model.Points);
    }

    [Fact]
    public void Digits_inside_strings_count_as_text_points()
    {
        var model = TemplateAnalyzer.Analyze("x_1 = { title = \"Idee 1\" }");

        var text = Assert.Single(model.Points, p => p.Kind == PointKind.Text);
        Assert.True(text.Enabled);
        Assert.Contains("\"Idee 2\"", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void Comments_are_kept_and_not_counted()
    {
        var model = TemplateAnalyzer.Analyze("# idea 1 fuer Test\nidea_1 = { }");

        var text = TemplateEngine.Render(model, 2);

        Assert.Equal(2, text.Split("# idea 1 fuer Test").Length - 1);
        Assert.Contains("idea_2 = { }", text);
    }

    [Fact]
    public void Windows_newlines_are_kept()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = {\r\n\tcost = 5\r\n}");

        var text = TemplateEngine.Render(model, 2);

        Assert.DoesNotContain("\n\n", text.Replace("\r\n", "|"));
        Assert.Contains("}\r\n\r\nidea_2", text);
    }

    private const string Levels = "iv2_ga_select_historic_ARA_1_1 = {\n\tname = iv2_ga_select_historic_ARA_1_1\n\tdesc = iv2_ga_select_historic_ARA_1_1_desc\n}";

    [Fact]
    public void Only_the_first_free_standing_number_of_an_identifier_counts_by_default()
    {
        var model = TemplateAnalyzer.Analyze(Levels);

        var enabled = Assert.Single(model.Points, p => p.Enabled);
        Assert.Equal("iv2_ga_select_historic_ARA_[1]_1", enabled.Label);
        Assert.Equal(3, enabled.Occurrences.Count);
        Assert.Contains(model.Points, p => !p.Enabled && p.RuleDisabled && p.Label.Contains("iv[2]"));
        Assert.Contains(model.Points, p => !p.Enabled && p.RuleDisabled && p.Label.EndsWith("ARA_1_[1]"));

        var text = TemplateEngine.Render(model, 3);
        Assert.Contains("iv2_ga_select_historic_ARA_2_1 = {", text);
        Assert.Contains("name = iv2_ga_select_historic_ARA_3_1", text);
        Assert.Contains("desc = iv2_ga_select_historic_ARA_2_1_desc", text);
        Assert.DoesNotContain("iv3", text);
    }

    [Theory]
    [InlineData("a = iv2_ga_select_historic_ARA\nb = iv2_ga_select_historic_ARA_1")]
    [InlineData("b = iv2_ga_select_historic_ARA_1\na = iv2_ga_select_historic_ARA")]
    [InlineData("b = \"iv2_ga_select_historic_ARA_1\"\na = \"iv2_ga_select_historic_ARA\"")]
    public void Name_digits_never_count_even_when_the_same_name_appears_without_a_second_number(string snippet)
    {
        var model = TemplateAnalyzer.Analyze(snippet);

        var text = TemplateEngine.Render(model, 3);

        Assert.DoesNotContain("iv3", text);
        Assert.DoesNotContain("iv4", text);
        Assert.Contains("ARA_3", text);
    }

    [Fact]
    public void Shapes_are_written_with_a_hash_for_every_number()
    {
        var model = TemplateAnalyzer.Analyze(Levels);

        Assert.Contains(model.Points, p => p.Shape == "iv#_ga_select_historic_ARA_#_#" && p.RunCount == 3);
    }

    private const string LevelShape = "iv#_ga_select_historic_ARA_#_#";

    private static AnalyzeOptions Numbers(params int[] counted) =>
        new(ShapeNumbers: new Dictionary<string, IReadOnlyCollection<int>> { [LevelShape] = counted });

    [Theory]
    [InlineData(new[] { 2 }, "ARA_1_2 = {", "ARA_2_")]
    [InlineData(new[] { 1 }, "ARA_2_1 = {", "ARA_1_2")]
    [InlineData(new[] { 0 }, "iv3_ga_select_historic_ARA_1_1 = {", "ARA_2")]
    [InlineData(new[] { 0, 1, 2 }, "iv3_ga_select_historic_ARA_2_2 = {", "NICHT_VORHANDEN")]
    public void Checked_numbers_of_one_shape_override_the_general_rule(int[] counted, string expected, string unexpected)
    {
        var model = TemplateAnalyzer.Analyze(Levels, Numbers(counted));
        var text = TemplateEngine.Render(model, 2);

        Assert.Contains(expected, text);
        Assert.DoesNotContain(unexpected, text);
        Assert.All(model.Points.Where(p => p.Shape == LevelShape), p => Assert.True(p.HasCustomRule));
    }

    [Fact]
    public void No_checked_number_stops_the_counting()
    {
        var model = TemplateAnalyzer.Analyze(Levels, Numbers());

        Assert.DoesNotContain(model.Points, p => p.Enabled);
        Assert.Equal(TemplateEngine.RenderInstance(model, 0), TemplateEngine.RenderInstance(model, 1));
    }

    [Fact]
    public void A_custom_choice_only_affects_its_own_shape()
    {
        var text = TemplateEngine.Render(TemplateAnalyzer.Analyze(Levels + "\nother_5 = { }", Numbers()), 2);

        Assert.Contains("other_6 = { }", text);
    }

    [Fact]
    public void Shape_info_lists_the_numbers_and_whether_each_counts()
    {
        var automatic = TemplateAnalyzer.Analyze(Levels).Shapes.Single(s => s.Shape == LevelShape);
        var custom = TemplateAnalyzer.Analyze(Levels, Numbers(2)).Shapes.Single(s => s.Shape == LevelShape);

        Assert.Equal(new[] { "2", "1", "1" }, automatic.Digits);
        Assert.Equal(new[] { false, true, false }, automatic.Counted);
        Assert.False(automatic.HasCustomRule);
        Assert.Equal(new[] { false, false, true }, custom.Counted);
        Assert.True(custom.HasCustomRule);
    }

    [Fact]
    public void Points_know_which_number_of_their_identifier_they_are()
    {
        var model = TemplateAnalyzer.Analyze(Levels);

        Assert.Equal(new[] { 0, 1, 2 }, model.Points.Where(p => p.Shape == LevelShape).Select(p => p.RunIndex).OrderBy(i => i));
    }

    [Fact]
    public void The_rule_also_applies_to_quoted_strings()
    {
        var model = TemplateAnalyzer.Analyze("x = { key = \"iv2_ga_select_historic_ARA_1_1\" }");

        var enabled = Assert.Single(model.Points, p => p.Enabled);
        Assert.Equal("iv2_ga_select_historic_ARA_[1]_1", enabled.Label);
        var text = TemplateEngine.Render(model, 2);
        Assert.Contains("\"iv2_ga_select_historic_ARA_2_1\"", text);
        Assert.DoesNotContain("iv3", text);
    }

    [Fact]
    public void The_last_number_can_be_chosen_instead()
    {
        var model = TemplateAnalyzer.Analyze(Levels, new AnalyzeOptions(MultiNumbers: MultiNumberRule.Last));

        var text = TemplateEngine.Render(model, 2);

        Assert.Contains("iv2_ga_select_historic_ARA_1_2 = {", text);
        Assert.DoesNotContain("ARA_2_", text);
    }

    [Fact]
    public void All_free_standing_numbers_can_count_but_not_the_glued_one()
    {
        var model = TemplateAnalyzer.Analyze(Levels, new AnalyzeOptions(MultiNumbers: MultiNumberRule.All));

        var text = TemplateEngine.Render(model, 2);

        Assert.Contains("iv2_ga_select_historic_ARA_2_2 = {", text);
    }

    [Fact]
    public void A_single_number_still_counts_even_when_glued()
    {
        var model = TemplateAnalyzer.Analyze("idea1 = { }");

        Assert.Contains("idea2 = { }", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void Examples_decide_which_number_counts_regardless_of_the_rule()
    {
        var snippet = "a_ARA_1_1 = { }\na_ARA_1_2 = { }\na_ARA_1_3 = { }";

        var model = TemplateAnalyzer.Analyze(snippet);

        var text = TemplateEngine.Render(model, 4);
        Assert.Contains("a_ARA_1_4 = { }", text);
        Assert.DoesNotContain("a_ARA_2_", text);
    }

    [Fact]
    public void The_indentation_of_the_first_line_is_kept()
    {
        var snippet = "\n\n\t\tidea_1 = {\n\t\t\tcost = 5\n\t\t}\n";

        var text = TemplateEngine.Render(TemplateAnalyzer.Analyze(snippet), 2);

        Assert.Equal("\t\tidea_1 = {\n\t\t\tcost = 5\n\t\t}\n\n\t\tidea_2 = {\n\t\t\tcost = 5\n\t\t}\n", text);
    }

    [Fact]
    public void The_indentation_is_kept_when_examples_are_merged()
    {
        var snippet = "\tidea_1 = {\n\t}\n\tidea_2 = {\n\t}";

        var text = TemplateEngine.Render(TemplateAnalyzer.Analyze(snippet), 3);

        Assert.StartsWith("\tidea_1 = {\n\t}\n\n\tidea_2 = {", text);
        Assert.Contains("\n\n\tidea_3 = {\n\t}\n", text);
    }

    [Fact]
    public void The_number_behind_a_name_prefix_never_counts_including_the_underscore_form()
    {
        var model = TemplateAnalyzer.Analyze("iv_2_tooltip_template_idea_adm_1_1 = { }");

        var text = TemplateEngine.Render(model, 2);

        Assert.Contains("iv_2_tooltip_template_idea_adm_2_1 = { }", text);
        Assert.Contains(model.Points, p => p.IgnoredReason == "Regel „Namenspräfix“ (mitgeliefert)" && !p.Enabled);
    }

    [Fact]
    public void A_prefix_only_counts_at_the_start_of_a_word()
    {
        var model = TemplateAnalyzer.Analyze("x = { gfx/progressbar_iv2_3.dds }");

        Assert.DoesNotContain(model.Points, p => p.IgnoredReason.Contains("Namenspräfix"));
    }

    [Fact]
    public void Points_know_whether_the_examples_taught_them()
    {
        var learned = TemplateAnalyzer.Analyze("idea_1 = { cost = 10 }\nidea_2 = { cost = 20 }");
        var plain = TemplateAnalyzer.Analyze("idea_1 = { cost = 10 }");

        Assert.All(learned.Points.Where(p => p.Enabled), p => Assert.True(p.LearnedFromExamples));
        Assert.All(plain.Points, p => Assert.False(p.LearnedFromExamples));
    }

    [Fact]
    public void Source_offsets_point_back_into_the_original_snippet()
    {
        var snippet = "\n  idea_1 = { cost = 10 }\nidea_2 = { cost = 20 }\n";

        var model = TemplateAnalyzer.Analyze(snippet);

        Assert.Equal("  idea_1 = { cost = 10 }", model.Text);
        Assert.Equal(model.Text.Length, model.SourceOffsets.Length);
        for (var i = 0; i < model.Text.Length; i++)
        {
            Assert.Equal(model.Text[i], snippet[model.SourceOffsets[i]]);
        }
    }

    [Fact]
    public void Empty_input_gives_empty_model()
    {
        var model = TemplateAnalyzer.Analyze("   ");

        Assert.Empty(model.Points);
        Assert.Equal(string.Empty, TemplateEngine.Render(model, 0));
    }

    [Fact]
    public void Nested_repeats_inside_repeated_examples_use_inner_and_outer_steps()
    {
        var snippet =
            "ev_1 = { option = { n = o_1 } option = { n = o_2 } }\n" +
            "ev_2 = { option = { n = o_1 } option = { n = o_2 } }";

        var model = TemplateAnalyzer.Analyze(snippet);

        Assert.Equal(2, model.ExampleCount);
        var text = TemplateEngine.Render(model, 3);
        Assert.Contains("ev_3 = { option = { n = o_1 } option = { n = o_2 } }", text);
    }
}
