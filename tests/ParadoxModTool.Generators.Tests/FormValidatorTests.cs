using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Formularprüfung: Pflichtfelder, Muster, Anzahl je Ebene und Paket-Warnungen.</summary>
public sealed class FormValidatorTests : IDisposable
{
    private readonly SlicePackage _slice = new();

    public void Dispose() => _slice.Dispose();

    private IReadOnlyList<FormIssue> Check(GeneratorState state) =>
        FormValidator.Check(_slice.Package, state, new GeneratorRenderer(_slice.Package));

    [Fact]
    public void Package_error_blocks()
    {
        const string Functions = "{{ func total_ideas\n  t = 0\n  for c in categories\n    t = t + c.ideas.size\n  end\n  ret t\nend }}";
        var package = SamplePackages.Ideas(functions: Functions, errors: new[] { new WarningDef("total_ideas == 0", "Keine Ideen") });
        var renderer = new GeneratorRenderer(package);
        var empty = GeneratorState.CreateEmpty(package, new[] { "english" });
        empty.Head.Values["tag"] = "BYZ";

        var issue = Assert.Single(FormValidator.Check(package, empty, renderer));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("Keine Ideen", issue.Message);
        Assert.Null(issue.Path);

        var idea = FormNode.Create(package.Root!.Child!.Fields, package.Root.Child.Child, package.Constants);
        idea.Values["name"] = "A";
        empty.Head.Children["categories"][0].Children["ideas"].Add(idea);
        Assert.Empty(FormValidator.Check(package, empty, renderer));
    }

    [Fact]
    public void Package_error_shows_alongside_field_errors()
    {
        const string Functions = "{{ func total_ideas\n  ret 0\nend }}";
        var package = SamplePackages.Ideas(functions: Functions, errors: new[] { new WarningDef("total_ideas == 0", "Keine Ideen") });
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Head.Values["tag"] = "byz!";

        var issues = FormValidator.Check(package, state, new GeneratorRenderer(package));
        Assert.Equal(2, issues.Count(i => i.Severity == IssueSeverity.Error));
    }

    [Fact]
    public void Bad_tag_pattern_is_an_error()
    {
        var state = _slice.State("BYZ", 9, 1);
        state.Head.Values["tag"] = "byz!";

        var issue = Assert.Single(Check(state));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("tag", issue.Path);
    }

    [Fact]
    public void Missing_required_name_is_an_error_with_path()
    {
        var state = _slice.State("BYZ", 9, 1);
        state.Head.Children["categories"][0].Children["ideas"][1].Values["name"] = " ";

        var issue = Assert.Single(Check(state));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("categories[1].ideas[2].name", issue.Path);
    }

    [Fact]
    public void Too_many_items_is_an_error()
    {
        var state = _slice.State("BYZ", 10, 0);
        Assert.Contains(Check(state), i => i.Severity == IssueSeverity.Error && i.Path == "categories[1].ideas");
    }

    private IReadOnlyList<FormIssue> CheckModifiers(string modifiers)
    {
        var package = SamplePackages.Ideas();
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Head.Values["tag"] = "BYZ";
        var idea = FormNode.Create(package.Root!.Child!.Fields, package.Root.Child.Child, package.Constants);
        idea.Values["name"] = "A";
        idea.Children["levels"] = new List<FormNode> { FormNode.Create(package.Root.Child.Child!.Fields, null, package.Constants) };
        idea.Children["levels"][0].Values["modifiers"] = modifiers;
        state.Head.Children["categories"][0].Children["ideas"].Add(idea);
        return FormValidator.Check(package, state, new GeneratorRenderer(package));
    }

    [Fact]
    public void Modifier_line_without_value_is_an_error()
    {
        var issue = Assert.Single(CheckModifiers("a = 1\nland_morale = \nc = 2"));

        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("categories[1].ideas[1].levels[1].modifiers", issue.Path);
        Assert.Equal(GeneratorsTexts.ModifierWithoutValue("Modifikatoren", "land_morale"), issue.Message);
        Assert.Contains("land_morale", issue.Message);
    }

    [Fact]
    public void Modifier_lines_with_values_and_comments_are_fine()
    {
        Assert.Empty(CheckModifiers("a = 1\n# b =\n\nc = { x = 1 }\nd = 2 # e ="));
        Assert.Empty(CheckModifiers(string.Empty));
    }

    [Fact]
    public void Package_warning_fires()
    {
        var issue = Assert.Single(Check(_slice.State("BYZ", 3, 2)));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal("Weniger als 10 Ideen", issue.Message);
        Assert.Empty(Check(_slice.State("BYZ", 9, 1)));
    }
}
