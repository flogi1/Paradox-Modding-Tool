using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Ausgabeart "check" (Spec 2026-10-06-generator-packages-gov-rank-exclusive, Abschnitt 1): Muster gegen den geplanten Text, sonst Warnung.</summary>
public sealed class CheckOutputTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Check = """{ "id": "c", "kind": "check", "path": "x.txt", "template": "c.sbn", "message": { "de": "fehlt {{ arg }} bei {{ tag }}", "en": "missing {{ arg }} for {{ tag }}" } }""";

    private const string Sync = """{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_{{ tag }}(?![A-Za-z0-9])" }""";

    private GeneratorPackage Load(string outputs, string check = "a = 1 => A\n")
    {
        _temp.Write("pkg/package.json", $$"""
            {
              "id": "ck", "name": "CK", "gameId": "eu5", "keyField": "tag",
              "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
              "outputs": [ {{outputs}} ],
              "targets": [ "mod", "output" ]
            }
            """);
        _temp.Write("pkg/templates/c.sbn", check);
        _temp.Write("pkg/templates/b.sbn", "b_{{ tag }} = {}\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"), uiLanguage: "de");
    }

    private GeneratorPlan Plan(GeneratorPackage package, string? writeRoot = null)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = "A";
        state.Head.Values["tag"] = "A";
        return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), writeRoot));
    }

    [Fact]
    public void Check_needs_template_and_message()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("""{ "id": "c", "kind": "check", "path": "x.txt", "template": "c.sbn" }"""));

        Assert.Contains("outputs[c].message", error.Message);
    }

    [Fact]
    public void Check_rejects_match_and_block()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load(Check.Replace("\"template\"", "\"match\": \"x\", \"template\"")));

        Assert.Contains("outputs[c].match", error.Message);
    }

    [Fact]
    public void Check_without_hit_adds_message_with_arg()
    {
        _temp.Write("mod/x.txt", "a = 1\n");

        var plan = Plan(Load(Check, "b = 1 => B\n"));

        Assert.Contains("fehlt B bei A", plan.Warnings);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void Check_with_hit_adds_nothing()
    {
        _temp.Write("mod/x.txt", "a = 1\n");

        Assert.Empty(Plan(Load(Check)).Warnings);
    }

    [Fact]
    public void Check_sees_text_planned_by_a_later_output()
    {
        _temp.Write("mod/x.txt", "c = 1\n");

        var plan = Plan(Load(Check + ", " + Sync, "b_A => A\n"));

        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Check_prefers_write_root_over_mod()
    {
        _temp.Write("mod/x.txt", "a = 1\n");
        _temp.Write("out/x.txt", "b = 1\n");

        Assert.Empty(Plan(Load(Check, "b = 1 => B\n"), _temp.PathOf("out")).Warnings);
    }

    [Fact]
    public void Check_on_missing_file_adds_nothing() =>
        Assert.Empty(Plan(Load(Check, "b = 1 => B\n")).Warnings);

    [Fact]
    public void Check_invalid_regex_is_an_output_error()
    {
        _temp.Write("mod/x.txt", "a = 1\n");

        var error = Assert.Throws<GeneratorOutputException>(() => Plan(Load(Check, "( => X\n")));

        Assert.Contains("x.txt", error.Message);
        Assert.Contains("c", error.Message);
    }

    [Fact]
    public void Document_keys_of_check_are_template_and_message() =>
        Assert.Equal(new[] { "template", "message" }, OutputNode.KeysOf(OutputKind.Check));

    [Fact]
    public void Document_message_is_localized_text()
    {
        Load(Check);
        var output = PackageDocument.Load(_temp.PathOf("pkg")).Outputs.First(o => o.Id == "c");

        Assert.Equal("fehlt {{ arg }} bei {{ tag }}", output.Message.Values["de"]);
        Assert.Equal("missing {{ arg }} for {{ tag }}", output.Message.Values["en"]);
    }

    [Fact]
    public void Checker_reports_invalid_check_regex()
    {
        Load(Check, "( => X\n");

        var issues = PackageChecker.Check(PackageDocument.Load(_temp.PathOf("pkg")), new PackageFiles(_temp.PathOf("pkg")), "en");

        Assert.Contains(issues, i => i.Where == "outputs[c].template" && i.Message.Contains("\"c\""));
    }

    [Fact]
    public void Checker_renders_message_with_arg()
    {
        Load(Check.Replace("{{ arg }}", "{{ argx }}"));

        var issues = PackageChecker.Check(PackageDocument.Load(_temp.PathOf("pkg")), new PackageFiles(_temp.PathOf("pkg")), "en");

        Assert.Contains(issues, i => i.Where == "outputs[c].message");
    }

    [Fact]
    public void Check_line_split_uses_last_arrow() =>
        Assert.Equal(new[] { ("a => b", "C"), ("plain", string.Empty) }, CheckLines.Parse("a => b => C\nplain\n\n"));
}
