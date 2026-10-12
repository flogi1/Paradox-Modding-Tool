using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>"Prüfen" im Paket-Editor: Ladeprüfung plus Scriban-Prüfung aller Vorlagen (Spec Teil 5a, Abschnitt 1).</summary>
public sealed class PackageCheckerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string TagHead = "[ { \"id\": \"tag\", \"type\": \"text\" } ]";

    private (PackageDocument Document, PackageFiles Files) Package(string outputs, string template = "x = 1\n", string extra = "", string head = TagHead)
    {
        _temp.Write("p/package.json", "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", " + extra +
            "\"head\": " + head + ", \"outputs\": " + outputs + " }");
        _temp.Write("p/templates/a.sbn", template);
        return (PackageDocument.Load(_temp.PathOf("p")), new PackageFiles(_temp.PathOf("p")));
    }

    private const string OwnFile = "[ { \"id\": \"o\", \"kind\": \"ownFile\", \"path\": \"x_{{ tag }}.txt\", \"template\": \"a.sbn\" } ]";

    [Fact]
    public void Template_syntax_error_has_file_and_line()
    {
        var (document, files) = Package(OwnFile, "a\nb\n{{ for x in y }}\nc\n");

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("a.sbn", issue.TemplateName);
        Assert.Equal("a.sbn", issue.Where);
        Assert.NotNull(issue.Line);
        Assert.InRange(issue.Line!.Value, 3, 5);
    }

    [Fact]
    public void Pending_template_text_is_checked_instead_of_file()
    {
        var (document, files) = Package(OwnFile);

        var issues = PackageChecker.Check(document, files, "de", new Dictionary<string, string> { ["a.sbn"] = "{{ if }}" });

        Assert.Equal("a.sbn", Assert.Single(issues).TemplateName);
    }

    [Fact]
    public void Output_path_error_names_output()
    {
        var (document, files) = Package("[ { \"id\": \"o\", \"kind\": \"ownFile\", \"path\": \"x_{{ tag + }}\", \"template\": \"a.sbn\" } ]");

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("outputs[o].path", issue.Where);
        Assert.Null(issue.TemplateName);
    }

    [Fact]
    public void Load_error_passed_through()
    {
        var (document, files) = Package(OwnFile);
        document.KeyField = "nope";

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("package.json", issue.Where);
        Assert.Contains("nope", issue.Message);
    }

    [Fact]
    public void Iv2_checks_clean()
    {
        var folder = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2");

        Assert.Empty(PackageChecker.Check(PackageDocument.Load(folder), new PackageFiles(folder), "de"));
    }

    [Fact]
    public void Warning_condition_is_parsed_as_expression()
    {
        var (document, files) = Package(OwnFile, extra: "\"warnings\": [ { \"when\": \"x <\", \"message\": \"M\" } ], \"errors\": [ { \"when\": \"tag == ''\", \"message\": \"E\" } ], ");

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("warnings[1]", issue.Where);
    }

    [Fact]
    public void Clean_package_gives_no_issues_and_keeps_the_document_unsaved()
    {
        var (document, files) = Package(OwnFile);
        document.Name = LocalizedText.Plain("Geändert");

        Assert.Empty(PackageChecker.Check(document, files, "de"));
        Assert.True(document.IsDirty);
        Assert.DoesNotContain("Geändert", File.ReadAllText(_temp.PathOf("p/package.json")));
    }

    [Fact]
    public void Unknown_name_in_template_is_found_by_sample_render()
    {
        var (document, files) = Package(OwnFile, "x = 1\ny = {{ groupName }}\n");

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("a.sbn", issue.Where);
        Assert.Equal("a.sbn", issue.TemplateName);
        Assert.Equal(2, issue.Line);
        Assert.Contains("groupName", issue.Message);
    }

    [Fact]
    public void Sample_render_enters_loops_of_levels_that_start_empty()
    {
        const string root = "\"root\": { \"id\": \"ideas\", \"min\": 0, \"max\": 5, \"fields\": [ { \"id\": \"name\", \"type\": \"text\" } ] }, ";
        var (document, files) = Package(OwnFile, "{{ for i in ideas }}{{ nope }}{{ end }}", root);

        Assert.Equal("a.sbn", Assert.Single(PackageChecker.Check(document, files, "de")).TemplateName);
    }

    [Fact]
    public void Sample_values_fit_field_types()
    {
        const string head = "[ { \"id\": \"tag\", \"type\": \"text\" }, { \"id\": \"n\", \"type\": \"number\", \"required\": true }, " +
            "{ \"id\": \"c\", \"type\": \"choice\", \"choices\": [ \"a\", \"b\" ] }, { \"id\": \"flag\", \"type\": \"bool\" } ]";
        const string root = "\"root\": { \"id\": \"ideas\", \"min\": 1, \"max\": 5, \"fields\": [ { \"id\": \"name\", \"type\": \"text\", \"multilingual\": true } ] }, ";
        var template = "{{ n - 1 }} {{ n * 2 }} {{ c | string.upcase }} {{ if flag }}y{{ end }}\n{{ for i in ideas }}{{ i.number - 1 }} {{ i.name | string.size }}{{ end }}\n";
        var (document, files) = Package(OwnFile, template, root, head);

        Assert.Empty(PackageChecker.Check(document, files, "de"));
    }

    [Fact]
    public void Unknown_name_in_localization_and_for_each_output_is_found()
    {
        const string root = "\"root\": { \"id\": \"ideas\", \"min\": 1, \"max\": 5, \"fields\": [ { \"id\": \"name\", \"type\": \"text\", \"multilingual\": true } ] }, ";
        const string outputs = "[ { \"id\": \"loc\", \"kind\": \"localization\", \"path\": \"localization/{{ lang }}/x_l_{{ lang }}.yml\", \"template\": \"a.sbn\" }, " +
            "{ \"id\": \"each\", \"kind\": \"ownFile\", \"forEach\": \"ideas\", \"path\": \"x_{{ item.number }}_{{ nope }}.txt\", \"template\": \"a.sbn\" } ]";
        var (document, files) = Package(outputs, " k: \"{{ for i in ideas }}{{ i.name }}{{ missing }}{{ end }}\"\n", root);

        var where = PackageChecker.Check(document, files, "de").Select(i => i.Where).ToList();

        Assert.Contains("a.sbn", where);
        Assert.Contains("outputs[each].path", where);
    }

    [Fact]
    public void Unknown_name_in_condition_is_found()
    {
        var (document, files) = Package(OwnFile, extra: "\"errors\": [ { \"when\": \"total == 0\", \"message\": \"E\" } ], ");

        Assert.Equal("errors[1]", Assert.Single(PackageChecker.Check(document, files, "de")).Where);
    }

    [Fact]
    public void Yml_output_that_is_not_localization_is_reported()
    {
        var (document, files) = Package("[ { \"id\": \"o\", \"kind\": \"insertSection\", \"path\": \"localization/english/x_l_english.yml\", \"template\": \"a.sbn\" } ]");

        var issue = Assert.Single(PackageChecker.Check(document, files, "de"));

        Assert.Equal("outputs[o].kind", issue.Where);
        Assert.Contains("localization", issue.Message);
    }

    [Fact]
    public void Check_reports_read_and_lookups_issues()
    {
        const string extra =
            "\"root\": { \"id\": \"ideas\", \"fields\": [ { \"id\": \"name\", \"type\": \"text\", " +
            "\"lookup\": { \"sources\": [ \"s\", \"gone\" ], \"fills\": { \"name\": \"nope\" } } } ] }, " +
            "\"read\": { \"keys\": \"x\", \"elements\": { \"file\": \"m.txt\", \"block\": \"b_(?<number>\\\\d+)\", \"level\": \"ideas\" }, " +
            "\"localization\": [ { \"key\": \"l_(?<number>\\\\d+)\", \"field\": \"ideas.nope\" } ], " +
            "\"flags\": [ { \"file\": \"f.txt\", \"block\": \"g_(?<number>\\\\d+)\", \"contains\": \"c\", \"field\": \"ideas.name\" } ] }, " +
            "\"lookups\": [ { \"id\": \"s\", \"kind\": \"gameKeys\", \"folder\": \"../x\" } ], ";
        var (document, files) = Package(OwnFile, extra: extra);

        var where = PackageChecker.Check(document, files, "de").Select(i => i.Where).ToList();

        Assert.Contains("read.keys", where);
        Assert.Contains("read.localization[1].field", where);
        Assert.Contains("read.flags[1].field", where);
        Assert.Contains("lookups[s].folder", where);
        Assert.Contains("fields[ideas.name].lookup", where);
        Assert.DoesNotContain("read.elements.block", where);
    }

    [Fact]
    public void Suffix_alone_pattern_is_ok_for_a_countable_root_only()
    {
        const string Read = "\"read\": { \"keys\": \"x(?<key>y)\", \"elements\": { \"file\": \"m.txt\", \"block\": \"m_{{ tag }}(?<suffix>_\\\\d)?\", \"level\": \"levels\" } }, ";
        var countable = "\"root\": { \"id\": \"levels\", \"fields\": [] }, " + Read;
        var fixedGroups = "\"constants\": { \"cats\": [ { \"key\": \"a\" } ] }, \"root\": { \"id\": \"cats\", \"constantsTable\": \"cats\", \"fields\": [], \"child\": { \"id\": \"levels\", \"fields\": [] } }, " + Read;

        var (okDocument, okFiles) = Package(OwnFile, extra: countable);
        var (badDocument, badFiles) = Package(OwnFile, extra: fixedGroups);

        Assert.DoesNotContain("read.elements.block", PackageChecker.Check(okDocument, okFiles, "de").Select(i => i.Where));
        Assert.Contains("read.elements.block", PackageChecker.Check(badDocument, badFiles, "de").Select(i => i.Where));
    }
}
