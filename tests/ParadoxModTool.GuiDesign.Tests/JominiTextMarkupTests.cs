using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Jomini-Lokalisierungsauszeichnung: Formate, Schlüssel, Bindings, Icons, Zeilenumbrüche.</summary>
public sealed class JominiTextMarkupTests
{
    private static string? None(string key) => null;

    private static Func<string, string?> Table(params (string Key, string Value)[] entries)
    {
        var map = entries.ToDictionary(e => e.Key, e => e.Value);
        return key => map.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>Kurzschreibweise für Vergleiche: T:text, F+spec, F-, I:name, P:anzeige, C:anzeige (Konzept), BR.</summary>
    private static string[] Show(IReadOnlyList<JominiTextToken> tokens) => tokens.Select(t => t switch
    {
        TextToken x => "T:" + x.Text,
        FormatStart x => "F+" + x.Spec,
        FormatEnd => "F-",
        IconToken x => "I:" + (x.Name ?? "<dyn>"),
        PlaceholderToken x => (x.Concept ? "C:" : "P:") + x.Display,
        LineBreakToken => "BR",
        _ => "?",
    }).ToArray();

    [Fact]
    public void Format_tags_nest_and_close()
    {
        var tokens = JominiTextMarkup.Parse("a #high b #bold c#! d#! e", None);
        Assert.Equal(new[] { "T:a ", "F+high", "T:b ", "F+bold", "T:c", "F-", "T: d", "F-", "T: e" }, Show(tokens));
    }

    [Fact]
    public void Format_spec_keeps_semicolon_and_colon_parts()
    {
        var tokens = JominiTextMarkup.Parse("#tooltippable;tooltip:foo,bar text#!", None);
        Assert.Equal(new[] { "F+tooltippable;tooltip:foo,bar", "T:text", "F-" }, Show(tokens));
    }

    [Fact]
    public void Hash_without_letter_is_text()
    {
        var tokens = JominiTextMarkup.Parse("Nr. # 5 und ##", None);
        Assert.Equal(new[] { "T:Nr. # 5 und ##" }, Show(tokens));
    }

    [Fact]
    public void Dollar_key_is_replaced_once()
    {
        var localise = Table(("A", "x $B$ y"), ("B", "never"));
        Assert.Equal(new[] { "T:x $B$ y" }, Show(JominiTextMarkup.Parse("$A$", localise)));
    }

    [Fact]
    public void Dollar_expansion_still_parses_other_markup()
    {
        var localise = Table(("A", "#bold x#! @gold!"));
        Assert.Equal(new[] { "F+bold", "T:x", "F-", "T: ", "I:gold" }, Show(JominiTextMarkup.Parse("$A$", localise)));
    }

    [Fact]
    public void Unknown_dollar_key_becomes_placeholder()
    {
        var tokens = JominiTextMarkup.Parse("Wert $VALUE|0$ Ende", None);
        Assert.Equal(new[] { "T:Wert ", "P:VALUE", "T: Ende" }, Show(tokens));
        var ph = Assert.IsType<PlaceholderToken>(tokens[1]);
        Assert.False(ph.Concept);
    }

    [Fact]
    public void Dollar_key_with_format_part_looks_up_the_key_only()
    {
        var tokens = JominiTextMarkup.Parse("$VALUE|+=0$", Table(("VALUE", "7")));
        Assert.Equal(new[] { "T:7" }, Show(tokens));
    }

    [Theory]
    [InlineData("[GetPlayer.GetName]", "GetName")]
    [InlineData("[A.GetTitle.GetName|U]", "GetName")]
    [InlineData("[Select_CString(A.B, 'x')]", "Select_CString")]
    [InlineData("[GetName]", "GetName")]
    [InlineData("[Concat(A.B, C.D).Foo]", "Foo")]
    public void Binding_shows_last_name_part(string input, string expected)
    {
        var tokens = JominiTextMarkup.Parse(input, None);
        var ph = Assert.IsType<PlaceholderToken>(Assert.Single(tokens));
        Assert.Equal(expected, ph.Display);
        Assert.False(ph.Concept);
    }

    [Fact]
    public void Concept_link_shows_concept_name()
    {
        var localise = Table(("game_concept_legitimacy", "Legitimacy"), ("concept_pop", "Pop"));

        var ck = Assert.IsType<PlaceholderToken>(Assert.Single(JominiTextMarkup.Parse("[legitimacy|E]", localise)));
        Assert.Equal("Legitimacy", ck.Display);
        Assert.True(ck.Concept);

        var eu = Assert.IsType<PlaceholderToken>(Assert.Single(JominiTextMarkup.Parse("[legitimacy|e]", localise)));
        Assert.Equal("Legitimacy", eu.Display);
        Assert.True(eu.Concept);

        var vic = Assert.IsType<PlaceholderToken>(Assert.Single(JominiTextMarkup.Parse("[concept_pop]", localise)));
        Assert.Equal("Pop", vic.Display);
        Assert.True(vic.Concept);
    }

    [Fact]
    public void Concept_without_localisation_falls_back_to_name()
    {
        var ck = Assert.IsType<PlaceholderToken>(Assert.Single(JominiTextMarkup.Parse("[glory|E]", None)));
        Assert.Equal("glory", ck.Display);
        Assert.True(ck.Concept);
    }

    [Fact]
    public void Icons_and_line_breaks()
    {
        var tokens = JominiTextMarkup.Parse("a @gold! b\nc @[GetIcon]!", None);
        Assert.Equal(new[] { "T:a ", "I:gold", "T: b", "BR", "T:c ", "I:<dyn>" }, Show(tokens));
        Assert.Null(((IconToken)tokens[5]).Name);
    }

    [Fact]
    public void Broken_markup_degrades_to_text()
    {
        // Nichts davon darf werfen; alle Buchstaben bleiben als Text, Formatname oder Platzhalter erhalten.
        foreach (var input in new[] { "#bold x", "y#!", "[open", "$open", "@open", "a [b", "[]", "@[x", "@[x]", "$", "@", "#", "#!", "Preis $5 und $6", "[a\nb]" })
        {
            var tokens = JominiTextMarkup.Parse(input, None);
            var kept = string.Concat(tokens.SelectMany(t => t switch
            {
                TextToken x => x.Text,
                FormatStart x => x.Spec,
                PlaceholderToken x => x.Display,
                _ => "",
            }).Where(char.IsLetter));
            Assert.Equal(string.Concat(input.Where(char.IsLetter)), kept);
        }

        Assert.Equal(new[] { "T:[open" }, Show(JominiTextMarkup.Parse("[open", None)));
        Assert.Equal(new[] { "T:$open" }, Show(JominiTextMarkup.Parse("$open", None)));
        Assert.Equal(new[] { "T:@open" }, Show(JominiTextMarkup.Parse("@open", None)));
        Assert.Equal(new[] { "T:y" }, Show(JominiTextMarkup.Parse("y#!", None)));
        Assert.Equal(new[] { "T:Preis $5 und $6" }, Show(JominiTextMarkup.Parse("Preis $5 und $6", None)));
        Assert.Equal(new[] { "T:[a", "BR", "T:b]" }, Show(JominiTextMarkup.Parse("[a\nb]", None)));
    }

    [Fact]
    public void Unclosed_format_ends_at_end_of_text()
    {
        var tokens = JominiTextMarkup.Parse("#bold x", None);
        Assert.Equal(new[] { "F+bold", "T:x", "F-" }, Show(tokens));
    }

    [Fact]
    public void Stray_format_end_is_dropped()
    {
        Assert.Equal(new[] { "T:a", "F+b", "T:c", "F-", "T:d" }, Show(JominiTextMarkup.Parse("a#!#b c#!d#!", None)));
    }

    [Fact]
    public void Carriage_return_is_ignored_and_empty_localisation_gives_no_token()
    {
        Assert.Equal(new[] { "T:a", "BR", "T:b" }, Show(JominiTextMarkup.Parse("a\r\nb", None)));
        Assert.Equal(new[] { "T:xy" }, Show(JominiTextMarkup.Parse("x$E$y", Table(("E", "")))));
    }
}
