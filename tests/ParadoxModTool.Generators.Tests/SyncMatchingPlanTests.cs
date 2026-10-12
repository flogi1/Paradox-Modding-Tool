using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Ausgabeart "syncMatching" in Lader, Planer, Prüfung und Editor-Dokument (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 1).</summary>
public sealed class SyncMatchingPlanTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Json(string output) => $$"""
        {
          "id": "sm", "name": "SM", "gameId": "eu5", "keyField": "tag",
          "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
          "root": { "id": "ideas", "label": "Ideen", "min": 0, "max": 9, "fields": [ { "id": "name", "label": "Name", "type": "text" } ] },
          "outputs": [ {{output}} ],
          "targets": [ "mod", "output" ]
        }
        """;

    private const string Output = """{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_{{ tag }}(?![A-Za-z0-9])" }""";

    private GeneratorPackage Load(string output = Output, string template = "b_{{ tag }} = { v = {{ ideas.size }} }\n", string? skeleton = null)
    {
        _temp.Write("pkg/package.json", Json(output));
        _temp.Write("pkg/templates/b.sbn", template);
        if (skeleton is not null)
        {
            _temp.Write("pkg/templates/skel.sbn", skeleton);
        }

        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    private GeneratorPlan Plan(GeneratorPackage package, string tag)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = tag;
        state.Head.Values["tag"] = tag;
        return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), null));
    }

    [Fact]
    public void SyncMatching_needs_match()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn" }"""));

        Assert.Contains("outputs[b].match", error.Message);
    }

    [Fact]
    public void Plan_sync_matching_replaces_group_units()
    {
        _temp.Write("mod/x.txt", "b_A = { v = 9 }\nb_AB = { v = 9 }\nb_B = { v = 9 }\n");

        var file = Plan(Load(), "A").Files.Single();

        Assert.Equal("b_A = { v = 0 }\r\nb_AB = { v = 9 }\r\nb_B = { v = 9 }\r\n".Replace("\r\n", "\n"), file.NewText!.Replace("\r\n", "\n"));
        Assert.Equal(PlannedFileStatus.Changed, file.Status);
    }

    [Fact]
    public void Plan_sync_matching_skeleton_for_missing_file()
    {
        var package = Load("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "block": "list", "skeleton": "skel.sbn", "match": "b_{{ tag }}(?![A-Za-z0-9])" }""",
            "\tb_{{ tag }} = {}\n", "list = {\n}\n");

        var file = Plan(package, "A").Files.Single();

        Assert.Equal("list = {\n\tb_A = {}\n}\n", file.NewText!.Replace("\r\n", "\n"));
        Assert.Equal(PlannedFileStatus.New, file.Status);
    }

    [Fact]
    public void Empty_render_lists_removed_runs_in_plan()
    {
        _temp.Write("mod/x.txt", "a = {}\nb_A = { v = 9 }\n");

        var plan = Plan(Load(template: string.Empty), "A");

        Assert.Equal(new[] { "b_A = { v = 9 }" }, plan.Files.Single().RemovedBlocks);
        Assert.NotEmpty(plan.Warnings);
    }

    [Fact]
    public void Unparsable_file_names_the_file()
    {
        _temp.Write("mod/x.txt", "b_A = { v = 9 \n");

        var error = Assert.Throws<GeneratorOutputException>(() => Plan(Load(), "A"));

        Assert.Contains("x.txt", error.Message);
    }

    [Fact]
    public void Invalid_match_regex_names_output()
    {
        _temp.Write("mod/x.txt", "a = {}\n");
        var package = Load("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_({{ tag }}" }""");

        var error = Assert.Throws<GeneratorOutputException>(() => Plan(package, "A"));

        Assert.Contains("\"b\"", error.Message);
    }

    [Fact]
    public void Kind_switch_to_sync_matching_keeps_template_and_block()
    {
        _temp.Write("doc/package.json", Json("""{ "id": "s", "kind": "insertSection", "path": "x.txt", "template": "b.sbn", "block": "list", "legacyBegin": "x" }"""));
        var document = PackageDocument.Load(_temp.PathOf("doc"));
        var output = document.Outputs.Single();

        output.Kind = OutputKind.SyncMatching;

        Assert.Equal("syncMatching", output.Raw["kind"]!.GetValue<string>());
        Assert.Equal("b.sbn", output.Get("template"));
        Assert.Equal("list", output.Get("block"));
        Assert.Null(output.Raw["legacyBegin"]);
    }

    [Fact]
    public void Check_reports_invalid_match_regex()
    {
        _temp.Write("doc/package.json", Json("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_({{ tag }}" }"""));
        _temp.Write("doc/templates/b.sbn", "x\n");

        var issues = PackageChecker.Check(PackageDocument.Load(_temp.PathOf("doc")), new PackageFiles(_temp.PathOf("doc")), "en");

        Assert.Contains(issues, i => i.Where == "outputs[b].match");
    }

    [Fact]
    public void InsertAfter_is_rendered_with_the_state()
    {
        _temp.Write("mod/x.txt", "A_x = { v = 9 }\nA_y = { v = 9 }\nz = {}\n");
        var package = Load("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_{{ tag }}(?![A-Za-z0-9])", "insertAfter": "^{{ tag }}_x" }""");

        var file = Plan(package, "A").Files.Single();

        Assert.Equal("A_x = { v = 9 }\nb_A = { v = 0 }\nA_y = { v = 9 }\nz = {}\n", file.NewText!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void IfLimit_is_rendered_per_state()
    {
        _temp.Write("mod/x.txt", "fx = {\n\tb_A = 1\n}\n");
        var output = """{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "block": "fx", "match": "b_{{ tag }}(?![A-Za-z0-9])", "ifLimit": "{{ cond }}" }""";
        _temp.Write("pkg/package.json", Json(output).Replace("""{ "id": "tag", "label": "Tag", "type": "text" } ]""",
            """{ "id": "tag", "label": "Tag", "type": "text" }, { "id": "cond", "label": "Cond", "type": "text" } ]"""));
        _temp.Write("pkg/templates/b.sbn", "{{ if cond != \"\" }}\t{{ end }}\tb_{{ tag }} = 1\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));

        string PlanText(string tag, string cond)
        {
            var state = GeneratorState.CreateEmpty(package, new[] { "english" });
            state.Key = tag;
            state.Head.Values["tag"] = tag;
            state.Head.Values["cond"] = cond;
            return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), null)).Files.Single().NewText!.Replace("\r\n", "\n");
        }

        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tb_A = 1\n\t}\n}\n", PlanText("A", "c = yes"));
        Assert.Equal("fx = {\n\tb_A = 1\n\tb_B = 1\n}\n", PlanText("B", string.Empty));
    }

    [Fact]
    public void IfLimit_not_allowed_on_insertSection()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("""{ "id": "b", "kind": "insertSection", "path": "x.txt", "template": "b.sbn", "ifLimit": "a = yes" }"""));

        Assert.Contains("ifLimit", error.Message);
    }

    [Fact]
    public void InsertAfter_not_allowed_on_ownFile()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("""{ "id": "b", "kind": "ownFile", "path": "x.txt", "template": "b.sbn", "insertAfter": "^a" }"""));

        Assert.Contains("insertAfter", error.Message);
    }

    [Fact]
    public void InsertAfter_not_allowed_on_localization_without_keys()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("""{ "id": "b", "kind": "localization", "path": "x.yml", "template": "b.sbn", "insertAfter": "^a" }"""));

        Assert.Contains("insertAfter", error.Message);
    }

    [Fact]
    public void Invalid_insertAfter_regex_names_output()
    {
        _temp.Write("mod/x.txt", "z = {}\n");
        var package = Load("""{ "id": "b", "kind": "syncMatching", "path": "x.txt", "template": "b.sbn", "match": "b_{{ tag }}", "insertAfter": "^({{ tag }}" }""");

        var error = Assert.Throws<GeneratorOutputException>(() => Plan(package, "A"));

        Assert.Contains("b", error.Message);
    }
}
