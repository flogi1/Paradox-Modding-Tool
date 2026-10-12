using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Formulardaten als Scriban-Modell, Paket-Funktionen, Fehler mit Datei und Zeile, Normalisierung.</summary>
public sealed class GeneratorRendererTests
{
    private static GeneratorState State(GeneratorPackage package)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english", "german", "french" });
        state.Head.Values["tag"] = "BYZ";
        var dip = state.Head.Children["categories"][1];
        for (var i = 0; i < 2; i++)
        {
            var idea = FormNode.Create(package.Root!.Child!.Fields, package.Root.Child.Child, package.Constants);
            idea.Values["name"] = "Idea " + (i + 1);
            idea.Children["levels"].Add(FormNode.Create(package.Root.Child.Child!.Fields, null, package.Constants));
            dip.Children["ideas"].Add(idea);
        }

        return state;
    }

    private static string Render(GeneratorPackage package, GeneratorState state, string template, string? language = null) =>
        new GeneratorRenderer(package).Render("test.sbn", template, TemplateModel.Build(package, state, language));

    [Fact]
    public void Model_exposes_language_rules()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        state.LanguageRules = new LanguageDefaults(true, false, true);

        Assert.True(new GeneratorRenderer(package).Evaluate(
            "language_rules.extend_english && !language_rules.copy_english && language_rules.english_for_missing", TemplateModel.Build(package, state, null)));
    }

    [Fact]
    public void Nested_levels_get_index_number_and_suffix()
    {
        var package = SamplePackages.Ideas();

        var text = Render(package, State(package), "{{ for c in categories }}{{ for i in c.ideas }}{{ for l in i.levels }}{{ i.number }}{{ l.suffix }};{{ end }}{{ end }}{{ end }}");

        Assert.Equal("11;11_1;12;12_1;", text);
    }

    [Fact]
    public void Fixed_groups_expose_constants_row_values_and_flags()
    {
        var package = SamplePackages.Ideas();

        var text = Render(package, State(package), "{{ for c in categories }}{{ c.key }}{{ c.index }}{{ if c.first }}F{{ end }}{{ if c.last }}L{{ end }} {{ end }}{{ constants.categories[1].key }} {{ tag }}");

        Assert.Equal("adm1F dip2L dip BYZ", text);
    }

    [Fact]
    public void Bool_fields_are_booleans_in_templates()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        state.Head.Children["categories"][1].Children["ideas"][0].Children["levels"][0].Values["expensive"] = "yes";

        var text = Render(package, state, "{{ for i in categories[1].ideas }}{{ if i.levels[0].expensive }}T{{ else }}F{{ end }}{{ end }}");

        Assert.Equal("TF", text);
    }

    [Fact]
    public void Package_functions_are_available_in_templates()
    {
        var package = SamplePackages.Ideas(functions: "{{ func expensive_key(ck)\n  ret \"iv2_tt_\" + ck + \"_expensive\"\nend }}");

        Assert.Equal("iv2_tt_adm_expensive", Render(package, State(package), "{{ expensive_key categories[0].key }}"));
    }

    [Fact]
    public void User_values_are_never_template_code()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        state.Head.Children["categories"][1].Children["ideas"][0].Values["name"] = "{{ 1+1 }} {% x %} { a = b }";

        Assert.Equal("{{ 1+1 }} {% x %} { a = b }", Render(package, state, "{{ categories[1].ideas[0].name }}"));
    }

    [Fact]
    public void Unknown_variable_is_an_error_with_file_and_line()
    {
        var package = SamplePackages.Ideas();

        var error = Assert.Throws<GeneratorTemplateException>(() => Render(package, State(package), "a\n{{ nope }}"));

        Assert.Equal("test.sbn", error.TemplateName);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void Syntax_error_is_an_error_with_file_and_line()
    {
        var package = SamplePackages.Ideas();

        var error = Assert.Throws<GeneratorTemplateException>(() => Render(package, State(package), "a\nb\n{{ for x in }}"));

        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void Localization_model_resolves_language_and_escapes()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        var idea = state.Head.Children["categories"][1].Children["ideas"][0];
        idea.Translations["name"] = new Dictionary<string, string> { ["german"] = "Der \"Kaiser\"" };
        state.LanguageRules = new LanguageDefaults(CopyEnglish: true);
        const string Template = "{{ categories[1].ideas[0].name }}|{{ categories[1].ideas[1].name }}|{{ lang }}";

        Assert.Equal("Der \\\"Kaiser\\\"|Idea 2|german", Render(package, state, Template, "german"));
        Assert.Equal("Idea 1|Idea 2|french", Render(package, state, Template, "french"));
    }

    [Fact]
    public void By_lang_gives_raw_text_per_language()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        var idea = state.Head.Children["categories"][1].Children["ideas"][0];
        idea.Values["name"] = "Say \"hi\"";
        idea.Translations["name"] = new Dictionary<string, string> { ["german"] = "Sag \"hi\"" };

        // unabhängig von der Sprache des Modells, ohne Maskierung; "name" selbst bleibt maskiert
        foreach (var language in new string?[] { null, "english", "german" })
        {
            Assert.Equal("Say \"hi\"|Sag \"hi\"|", Render(package, state, "{{ i = categories[1].ideas[0] }}{{ i.name_by_lang.english }}|{{ i.name_by_lang.german }}|{{ i.name_by_lang.french }}", language));
        }

        Assert.Equal("Sag \\\"hi\\\"", Render(package, state, "{{ categories[1].ideas[0].name }}", "german"));
    }

    [Fact]
    public void By_lang_follows_the_language_rules()
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        state.LanguageRules = new LanguageDefaults(CopyEnglish: true);

        Assert.Equal("Idea 1|Idea 1|Idea 1", Render(package, state, "{{ i = categories[1].ideas[0] }}{{ i.name_by_lang.english }}|{{ i.name_by_lang.german }}|{{ i.name_by_lang.french }}"));
    }

    [Fact]
    public void Evaluate_reads_a_condition()
    {
        var package = SamplePackages.Ideas();
        var renderer = new GeneratorRenderer(package);
        var model = TemplateModel.Build(package, State(package), null);

        Assert.True(renderer.Evaluate("categories[1].ideas.size == 2", model));
        Assert.False(renderer.Evaluate("categories[0].ideas.size > 0", model));
    }

    [Fact]
    public void Object_to_json_escapes_quotes_and_backslashes() // documented for jsonEntry templates
    {
        var package = SamplePackages.Ideas();
        var state = State(package);
        state.Head.Values["tag"] = "a\"b\\c";

        var rendered = Render(package, state, "{{ tag | object.to_json }}");

        // Scriban schreibt " als "; der Eintrag wird ohnehin neu im Python-Stil geschrieben
        Assert.Equal("a\"b\\c", System.Text.Json.Nodes.JsonNode.Parse(rendered)!.GetValue<string>());
        Assert.Contains("\"k\": \"a\\\"b\\\\c\"", JsonEntryWriter.Set("{\n \"x\": 1\n}\n", string.Empty, "k", rendered));
    }

    [Fact]
    public void Normalize_trims_trailing_whitespace_and_sets_line_endings()
    {
        Assert.Equal("a\r\n\tb\r\n", TextNormalizer.Normalize("a  \n\tb\t\n\n\n", "\r\n"));
        Assert.Equal(string.Empty, TextNormalizer.Normalize("  \n", "\n"));
    }
}
