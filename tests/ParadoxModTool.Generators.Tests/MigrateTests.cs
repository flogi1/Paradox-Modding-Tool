using System.Text;
using System.Text.Json;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary><c>migrate</c>: Einträge wandern aus einer alten Datei in die der Ausgabe; eine danach leere Datei wird gelöscht (geplantes Löschen).</summary>
public sealed class MigrateTests : IDisposable
{
    private const string Own = @"^KEY_\w+_\d+\s*=\s*\{";
    private const string OwnOrCommented = @"^#?KEY_\w+_\d+\s*=\s*\{";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private GeneratorPackage Load(string kind = "syncMatching", string pattern = Own, string path = "new/a.txt", string newLine = "lf", string? skeleton = null)
    {
        var output = kind == "insertSection"
            ? new { id = "o", kind, path, template = "t.sbn", newLine, skeleton, legacyPatterns = new[] { @"(?m)^KEY_{{ tag }}_\d+\s*=\s*\{" }, migrate = new { from = "old.txt", patterns = new[] { pattern } } }
            : (object)new { id = "o", kind, path, template = "t.sbn", newLine, skeleton, match = @"KEY_{{ tag }}_\d+(?![0-9])", migrate = new { from = "old.txt", patterns = new[] { pattern } } };
        var json = JsonSerializer.Serialize(new
        {
            id = "mig",
            name = "Migrate",
            gameId = "eu5",
            keyField = "tag",
            head = new[] { new { id = "tag", label = "Tag", type = "text" } },
            outputs = new[] { output },
            targets = new[] { "mod", "output" },
        });
        _temp.Write("package/package.json", json);
        _temp.Write("package/templates/t.sbn", "KEY_{{ tag }}_1={\n a=1\n}\n");
        _temp.Write("package/templates/sk.sbn", "# header\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("package"));
    }

    private GeneratorPlan Plan(GeneratorPackage package, string? outputFolder = null)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = "X";
        state.Head.Values["tag"] = "X";
        return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), outputFolder, new[] { "english" }));
    }

    private static GeneratorPlannedFile File(GeneratorPlan plan, string path) => plan.Files.Single(f => f.RelativePath == path);

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    [Fact]
    public void Migrate_MovesOwnEntries_KeepsForeign()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n a=1\n}\nKEY_Y_1={\n c=3\n}\nOTHER_1={\n b=2\n}\n");

        var plan = Plan(Load());

        var old = File(plan, "old.txt");
        Assert.Equal("OTHER_1={\n b=2\n}\n", old.NewText);
        Assert.False(old.Delete);
        Assert.Equal(2, old.LegacyRemoved);
        var created = File(plan, "new/a.txt").NewText!;
        Assert.Equal(1, Count(created, "KEY_Y_1={\n c=3\n}"));
        Assert.Equal(1, Count(created, "KEY_X_1="));
        Assert.Contains(GeneratorsTexts.Migrated("old.txt", 2), plan.Warnings);
    }

    [Fact]
    public void Migrate_InsertSection_RegeneratesOwnEntryOnce()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n a=1\n}\nKEY_Y_1={\n c=3\n}\nOTHER_1={\n b=2\n}\n");

        var plan = Plan(Load("insertSection"));

        var created = File(plan, "new/a.txt").NewText!;
        Assert.Equal(1, Count(created, "KEY_Y_1={\n c=3\n}"));
        Assert.Equal(1, Count(created, "KEY_X_1="));
    }

    [Fact]
    public void Migrate_DeletesFileWhenOnlyWhitespaceLeft()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n\n");

        var plan = Plan(Load());

        var old = File(plan, "old.txt");
        Assert.True(old.Delete);
        Assert.Equal(PlannedFileStatus.Changed, old.Status);
        Assert.Equal(string.Empty, old.NewText);
        Assert.Contains(GeneratorsTexts.MigratedDelete, old.Notes);
    }

    [Fact]
    public void Migrate_KeepsFileWithForeignComment()
    {
        _temp.Write("mod/old.txt", "# mine\nKEY_X_1={\n}\n");

        var old = File(Plan(Load()), "old.txt");

        Assert.False(old.Delete);
        Assert.Equal("# mine\n", old.NewText);
    }

    [Fact]
    public void Migrate_CommentedEntry_MovedWhole()
    {
        _temp.Write("mod/old.txt", "#KEY_Y_2={\n#a=1\n#}\nOTHER_1={\n}\n");

        var plan = Plan(Load(pattern: OwnOrCommented));

        Assert.Equal("OTHER_1={\n}\n", File(plan, "old.txt").NewText);
        var created = File(plan, "new/a.txt").NewText!;
        Assert.Contains("#KEY_Y_2={", created);
        Assert.Contains("#a=1", created);
        Assert.Contains("#}", created);
    }

    [Fact]
    public void Migrate_OutputFolder_NoDelete()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n");
        var folder = _temp.PathOf("out");

        var plan = Plan(Load(), folder);

        var old = File(plan, "old.txt");
        Assert.False(old.Delete);
        Assert.Equal(PlannedFileStatus.New, old.Status);
        Assert.Equal(string.Empty, old.NewText);
        Assert.All(plan.Files, f => Assert.False(f.Delete));
    }

    [Fact]
    public void Migrate_NoOldFile_PlansNothingForIt()
    {
        var plan = Plan(Load());

        Assert.DoesNotContain(plan.Files, f => f.RelativePath == "old.txt");
        Assert.Contains(plan.Files, f => f.RelativePath == "new/a.txt");
    }

    [Fact]
    public void Migrate_ReadOnlyOldFile_SkippedWithWarning_NewFileStillWritten()
    {
        var path = _temp.Write("mod/old.txt", "KEY_Y_1={\n c=3\n}\n");
        System.IO.File.SetAttributes(path, System.IO.FileAttributes.ReadOnly);
        try
        {
            var plan = Plan(Load());

            Assert.DoesNotContain(plan.Files, f => f.RelativePath == "old.txt");
            Assert.Contains(GeneratorsTexts.MigrateReadOnly("old.txt"), plan.Warnings);
            var created = File(plan, "new/a.txt").NewText!;
            Assert.Contains("KEY_X_1=", created);
            Assert.DoesNotContain("KEY_Y_1", created);

            GeneratorPlanWriter.CreateAction(plan, "Erzeugen").Do();

            Assert.True(System.IO.File.Exists(_temp.PathOf("mod/new/a.txt")));
            Assert.True(System.IO.File.Exists(path));
        }
        finally
        {
            System.IO.File.SetAttributes(path, System.IO.FileAttributes.Normal);
        }
    }

    [Fact]
    public void Migrate_NothingMatches_FileUnchanged()
    {
        _temp.Write("mod/old.txt", "OTHER_1={\n b=2\n}\n");

        var plan = Plan(Load());

        Assert.True(plan.Files.All(f => f.RelativePath != "old.txt" || f.Status == PlannedFileStatus.Unchanged));
        Assert.DoesNotContain(plan.Warnings, w => w == GeneratorsTexts.Migrated("old.txt", 0));
    }

    [Fact]
    public void Migrate_OldFileWrittenBySameRun_IsSkipped()
    {
        _temp.Write("mod/old.txt", "KEY_Y_1={\n}\n");

        var plan = Plan(Load(path: "old.txt"));

        // Die Ausgabe schreibt old.txt selbst: Fremdes bleibt, nichts wird verschoben oder gelöscht
        var old = File(plan, "old.txt");
        Assert.False(old.Delete);
        Assert.Contains("KEY_Y_1={", old.NewText);
        Assert.DoesNotContain(plan.Warnings, w => w.Contains("old.txt") && w == GeneratorsTexts.Migrated("old.txt", 1));
    }

    [Fact]
    public void Migrate_ConvertsLineEndingsToNewFile()
    {
        _temp.Write("mod/old.txt", "KEY_Y_1={\n c=3\n}\nOTHER_1={\n}\n");

        var created = File(Plan(Load(newLine: "crlf")), "new/a.txt").NewText!;

        Assert.Contains("KEY_Y_1={\r\n c=3\r\n}", created);
    }

    [Fact]
    public void Writer_DeletesAndUndoRestores()
    {
        const string OldText = "KEY_X_1={\n}\n\n";
        var path = _temp.Write("mod/old.txt", OldText, new UTF8Encoding(true));
        var before = System.IO.File.ReadAllBytes(path);
        var plan = Plan(Load());

        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");
        action.Do();

        Assert.False(System.IO.File.Exists(path));
        Assert.True(System.IO.File.Exists(_temp.PathOf("mod/new/a.txt")));
        action.Undo();
        Assert.Equal(before, System.IO.File.ReadAllBytes(path));
        Assert.False(System.IO.File.Exists(_temp.PathOf("mod/new/a.txt")));
    }

    [Fact]
    public void Writer_StaleDelete_IsRefused()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n\n");
        var plan = Plan(Load());
        Assert.Empty(GeneratorPlanWriter.StaleFiles(plan));

        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n\n# von Hand\n");

        Assert.Contains("old.txt", GeneratorPlanWriter.StaleFiles(plan));
    }

    [Fact]
    public void Writer_WrittenPaths_IncludesDeletedFile()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n");

        var paths = GeneratorPlanWriter.WrittenPaths(Plan(Load()));

        Assert.Contains(_temp.PathOf("mod/old.txt"), paths);
    }

    [Fact]
    public void WithEditedFile_ClearsDelete()
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\n");
        var plan = Plan(Load());
        Assert.True(File(plan, "old.txt").Delete);

        var edited = File(plan.WithEditedFile("old.txt", "x"), "old.txt");

        Assert.False(edited.Delete);
        Assert.Equal("x", edited.NewText);
    }

    [Fact]
    public void Loader_RejectsMigrateOnOwnFile()
    {
        _temp.Write("package/package.json", """
            { "id": "mig", "name": "M", "gameId": "eu5", "keyField": "tag",
              "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
              "outputs": [ { "id": "o", "kind": "ownFile", "path": "a.txt", "template": "t.sbn", "migrate": { "from": "old.txt", "patterns": [ "x" ] } } ],
              "targets": [ "mod" ] }
            """);
        _temp.Write("package/templates/t.sbn", "x");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(_temp.PathOf("package")));
        Assert.Contains("migrate", error.Message);
    }

    [Fact]
    public void Loader_RejectsMigrateWithItemPath()
    {
        _temp.Write("package/package.json", """
            { "id": "mig", "name": "M", "gameId": "eu5", "keyField": "tag",
              "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
              "root": { "id": "things", "label": "Things", "min": 0, "max": 3 },
              "outputs": [ { "id": "o", "kind": "syncMatching", "path": "a_{{ item.index }}.txt", "forEach": "things", "template": "t.sbn", "match": "x",
                             "migrate": { "from": "old.txt", "patterns": [ "x" ] } } ],
              "targets": [ "mod" ] }
            """);
        _temp.Write("package/templates/t.sbn", "x");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(_temp.PathOf("package")));
        Assert.Equal(GeneratorsTexts.MigrateItemPath("o"), error.Message);
    }

    [Theory]
    [InlineData("syncMatching")]
    [InlineData("insertSection")]
    public void Migrate_NewFile_KeepsSkeletonOnce(string kind)
    {
        _temp.Write("mod/old.txt", "KEY_X_1={\n}\nKEY_Y_1={\n c=3\n}\n");

        var created = File(Plan(Load(kind, skeleton: "sk.sbn")), "new/a.txt").NewText!;

        Assert.Equal(1, Count(created, "# header"));
        Assert.StartsWith("# header\n", created);
        Assert.Equal(1, Count(created, "KEY_Y_1={\n c=3\n}"));
        Assert.Equal(1, Count(created, "KEY_X_1="));
    }


    [Theory]
    [InlineData("syncMatching")]
    [InlineData("insertSection")]
    public void Migrate_SkeletonLineInOldFile_NotMovedTwice(string kind)
    {
        _temp.Write("mod/old.txt", "# header\nKEY_Y_1={\n c=3\n}\n");

        var plan = Plan(Load(kind, pattern: @"^# header$|" + Own, skeleton: "sk.sbn"));

        var created = File(plan, "new/a.txt").NewText!;
        Assert.Equal(1, Count(created, "# header"));
        Assert.Equal(1, Count(created, "KEY_Y_1={\n c=3\n}"));
        Assert.True(File(plan, "old.txt").Delete);
    }

    [Fact]
    public void Loader_ReadsMigrate()
    {
        var migrate = Load().Outputs[0].Migrate!;

        Assert.Equal("old.txt", migrate.From);
        Assert.Equal(new[] { Own }, migrate.Patterns);
    }
}
