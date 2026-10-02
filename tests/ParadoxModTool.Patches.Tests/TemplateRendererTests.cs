using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class TemplateRendererTests
{
    private static Dictionary<string, string> Values(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    [Fact]
    public void Replaces_known_placeholders_and_leaves_script_braces()
    {
        Assert.Equal("a = angkor_wat_01\nb = { c }", TemplateRenderer.Render("a = {key}\nb = { c }", Values(("key", "angkor_wat_01"))));
    }

    [Fact]
    public void Unknown_placeholder_stays_literal_and_is_reported()
    {
        Assert.Equal("x = {castle_01}", TemplateRenderer.Render("x = {castle_01}", Values(("key", "k"))));
        Assert.Equal(new[] { "castle_01" }, TemplateRenderer.UnknownPlaceholders("x = {castle_01} {key}", new[] { "key" }));
    }

    [Fact]
    public void Typos_in_section_names_are_unknown_placeholders()
    {
        Assert.Equal(new[] { "barny", "bar" }, TemplateRenderer.UnknownPlaceholders("{*barny}x{/barny} {?bar}y{/bar} {!barony}z{/barony}", new[] { "barony" }));
    }

    [Fact]
    public void Key_is_always_a_known_column()
    {
        Assert.Empty(TemplateRenderer.UnknownPlaceholders("{key}", Array.Empty<string>()));
    }

    [Fact]
    public void Conditional_sections_follow_emptiness()
    {
        const string template = "{?barony}barony = title:{barony}{/barony}{!barony}holy = yes{/barony}";

        Assert.Equal("barony = title:b_x", TemplateRenderer.Render(template, Values(("barony", "b_x"))));
        Assert.Equal("holy = yes", TemplateRenderer.Render(template, Values(("barony", ""))));
    }

    private const string OrTemplate = "{?barony}OR = {\n{*barony}\tbarony = title:{barony}\n{/barony}}{/barony}";

    [Fact]
    public void Repeat_section_inside_same_name_condition()
    {
        Assert.Equal(string.Empty, TemplateRenderer.Render(OrTemplate, Values(("barony", ""))));
        Assert.Equal("OR = {\n\tbarony = title:b_a\n}", TemplateRenderer.Render(OrTemplate, Values(("barony", "b_a"))));
        Assert.Equal("OR = {\n\tbarony = title:b_a\n\tbarony = title:b_b\n\tbarony = title:b_c\n}", TemplateRenderer.Render(OrTemplate, Values(("barony", "b_a, b_b, b_c"))));
    }

    [Fact]
    public void Repeat_section_does_not_count_as_empty_placeholder()
    {
        Assert.Empty(TemplateRenderer.EmptyPlaceholders(OrTemplate, Values(("barony", ""))));
        Assert.Empty(TemplateRenderer.EmptyPlaceholders("{*barony}x = {barony}{/barony}", Values(("barony", ""))));
        Assert.Empty(TemplateRenderer.UnknownPlaceholders(OrTemplate, new[] { "barony" }));
    }

    [Fact]
    public void Empty_value_inside_a_conditional_section_does_not_skip_the_row()
    {
        // Die Anleitung zeigt {?barony}…{/barony} für Gebäude ohne feste Baronie; die Zeile muss dann laufen, nicht übersprungen werden.
        const string template = "{?barony}barony = title:{barony}{/barony}{!barony}holy = yes{/barony}";

        Assert.Empty(TemplateRenderer.EmptyPlaceholders(template, Values(("barony", ""))));
        Assert.Equal(new[] { "barony" }, TemplateRenderer.EmptyPlaceholders("barony = title:{barony}", Values(("barony", ""))));
    }

    [Fact]
    public void Conditional_section_on_unknown_column_counts_as_empty()
    {
        Assert.Equal("b", TemplateRenderer.Render("{?nope}a{/nope}{!nope}b{/nope}", Values(("key", "k"))));
    }

    [Fact]
    public void List_column_expands_to_one_instance_per_value()
    {
        var instances = TemplateRenderer.RenderEach(
            "{holdings}/buildings",
            Values(("holdings", "castle_holding, city_holding")),
            new HashSet<string> { "holdings" });

        Assert.Equal(
            new[] { new RenderedInstance("castle_holding", "castle_holding/buildings"), new RenderedInstance("city_holding", "city_holding/buildings") },
            instances);
    }

    [Fact]
    public void Two_list_columns_give_the_cartesian_product()
    {
        var instances = TemplateRenderer.RenderEach(
            "{a}-{b}",
            Values(("a", "1,2"), ("b", "x")),
            new HashSet<string> { "a", "b" });

        Assert.Equal(new[] { "1-x", "2-x" }, instances.Select(i => i.Text));
        Assert.Equal(new[] { "1 x", "2 x" }, instances.Select(i => i.Suffix));
    }

    [Fact]
    public void Template_without_list_column_renders_once_with_empty_suffix()
    {
        var instance = Assert.Single(TemplateRenderer.RenderEach("{key}", Values(("key", "a"), ("holdings", "x, y")), new HashSet<string> { "holdings" }));
        Assert.Equal(new RenderedInstance("", "a"), instance);
    }

    [Fact]
    public void Repeated_list_values_render_once()
    {
        var instances = TemplateRenderer.RenderEach("{holdings}", Values(("holdings", "castle_holding, castle_holding")), new HashSet<string> { "holdings" });
        Assert.Equal(new[] { "castle_holding" }, instances.Select(i => i.Text));
    }

    [Fact]
    public void Empty_list_value_renders_no_instance()
    {
        Assert.Empty(TemplateRenderer.RenderEach("{holdings}", Values(("holdings", " ")), new HashSet<string> { "holdings" }));
    }
}
