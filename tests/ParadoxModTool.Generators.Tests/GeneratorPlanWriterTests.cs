using ParadoxModTool.Patches;
using ParadoxModTool.UndoRedo;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Ein Plan als ein Rückgängig-Schritt (Texte und Bilder) und die Prüfung auf seit der Vorschau geänderte Dateien.</summary>
public sealed class GeneratorPlanWriterTests : IDisposable
{
    private readonly SlicePackage _slice = new();

    public void Dispose() => _slice.Dispose();

    [Fact]
    public void Plan_action_writes_texts_and_binaries_and_undo_restores_all()
    {
        var plan = _slice.Plan(_slice.State("BYZ", 1, 1));
        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");

        action.Do();
        Assert.True(File.Exists(_slice.Temp.PathOf("mod/main_menu/gfx/interface/coa/BYZ.dds")));
        Assert.Contains("has_or_had_tag = BYZ", _slice.ReadMod("in_game/common/scripted_triggers/setup.txt"));
        Assert.All(_slice.Plan(_slice.State("BYZ", 1, 1)).Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));

        action.Undo();
        Assert.False(File.Exists(_slice.Temp.PathOf("mod/main_menu/gfx/interface/coa/BYZ.dds")));
        Assert.False(File.Exists(_slice.Temp.PathOf("mod/in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt")));
        Assert.Equal(SlicePackage.Triggers, _slice.ReadMod("in_game/common/scripted_triggers/setup.txt"));
        Assert.Equal(SlicePackage.SetupGui, _slice.ReadMod("in_game/gui/setup.gui"));
    }

    [Fact]
    public void Unchanged_files_are_not_written()
    {
        var state = _slice.State("BYZ", 1, 0);
        GeneratorPlanWriter.CreateAction(_slice.Plan(state), "Erzeugen").Do();

        var again = _slice.Plan(state);

        Assert.Empty(GeneratorPlanWriter.WrittenPaths(again));
        Assert.Empty(GeneratorPlanWriter.CreateAction(again, "Erzeugen").Actions);
    }

    [Fact]
    public void Changed_file_on_disk_is_stale()
    {
        var plan = _slice.Plan(_slice.State("BYZ", 1, 1));
        Assert.Empty(GeneratorPlanWriter.StaleFiles(plan));

        _slice.Temp.Write("mod/in_game/gui/setup.gui", SlicePackage.SetupGui + "# Handänderung\n");
        _slice.Temp.Write("mod/in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt", "neu von Hand\n");

        Assert.Equal(
            new[] { "in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt", "in_game/gui/setup.gui" },
            GeneratorPlanWriter.StaleFiles(plan).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Written_paths_are_full_paths_below_the_write_root()
    {
        var plan = _slice.Plan(_slice.State("BYZ", 1, 1));

        Assert.All(GeneratorPlanWriter.WrittenPaths(plan), p => Assert.StartsWith(_slice.ModRoot, p));
        Assert.Contains(Path.Combine(_slice.ModRoot, "in_game", "gui", "setup.gui"), GeneratorPlanWriter.WrittenPaths(plan));
    }

    private const string OldTags = "{\n \"entries\": {}\n}\n";

    private const string NewTags = "{\n \"entries\": {\n  \"nat:x\": {}\n }\n}\n";

    /// <summary>Ein Plan mit einer externen Datei außerhalb der Mod (Ordner <c>ext</c> im Temp-Ordner), wie ihn der Planer für "jsonEntry" baut.</summary>
    private (GeneratorPlan Plan, string Path) ExternalPlan()
    {
        var path = _slice.Temp.Write("ext/tags.json", OldTags);
        var file = new GeneratorPlannedFile("extern/tags.json", PlannedFileStatus.Changed, OldTags, NewTags, null, null, false, TextFile.Utf8, Array.Empty<string>(), Array.Empty<string>(), ExternalPath: path);
        return (new GeneratorPlan { PackageId = "p", Key = "k", WriteRoot = _slice.ModRoot, Files = new[] { file }, Warnings = Array.Empty<string>() }, path);
    }

    [Fact]
    public void Stale_external_file_blocks_write()
    {
        var (plan, path) = ExternalPlan();
        Assert.Empty(GeneratorPlanWriter.StaleFiles(plan));

        File.WriteAllText(path, OldTags + " ");

        Assert.Equal(new[] { "extern/tags.json" }, GeneratorPlanWriter.StaleFiles(plan));
        Assert.Equal(new[] { path }, GeneratorPlanWriter.WrittenPaths(plan));
    }

    [Fact]
    public void External_write_creates_backup_first()
    {
        var (plan, path) = ExternalPlan();

        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");
        Assert.IsType<ExternalBackupAction>(action.Actions[0]);
        Assert.Equal(path, ((FileWriteUndoableAction)action.Actions[1]).FilePath);
        action.Do();

        Assert.Equal(NewTags, File.ReadAllText(path));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "backups")));
        Assert.Matches(@"^tags\.\d{4}-\d{2}-\d{2}_\d{6}\.json$", Path.GetFileName(backup));
        Assert.Equal(OldTags, File.ReadAllText(backup));
        Assert.False(File.Exists(Path.Combine(_slice.ModRoot, "extern", "tags.json")));

        action.Undo();

        Assert.Equal(OldTags, File.ReadAllText(path));
    }

    [Fact]
    public void External_backup_is_a_byte_copy_and_survives_undo()
    {
        var (plan, path) = ExternalPlan();
        // BOM und ein Windows-1252-Byte: ein neu kodierter OriginalText (UTF-8 ohne BOM) wäre nicht bytegleich
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.ASCII.GetBytes("{\n \"entries\": {}\n}")).Append((byte)0xE9).Append((byte)'\n').ToArray();
        File.WriteAllBytes(path, bytes);

        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");
        action.Do();
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "backups")));
        Assert.Equal(bytes, File.ReadAllBytes(backup));

        action.Undo();

        Assert.True(File.Exists(backup));
        Assert.Equal(bytes, File.ReadAllBytes(backup));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void Two_backups_in_the_same_second_both_exist()
    {
        var (plan, path) = ExternalPlan();
        var stamp = new DateTime(2026, 10, 10, 12, 0, 0);

        GeneratorPlanWriter.CreateAction(plan, "Erzeugen", stamp).Do();
        File.WriteAllText(path, OldTags);
        GeneratorPlanWriter.CreateAction(plan, "Erzeugen", stamp).Do();

        var names = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "backups")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "tags.2026-10-10_120000.json", "tags.2026-10-10_120000_2.json" }, names);
    }

    [Fact]
    public void Redo_does_not_create_a_second_backup()
    {
        var (plan, path) = ExternalPlan();
        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");

        action.Do();
        action.Undo();
        action.Do();

        Assert.Single(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "backups")));
        Assert.Equal(NewTags, File.ReadAllText(path));
    }

    [Fact]
    public void New_external_file_needs_no_backup()
    {
        var path = _slice.Temp.PathOf("ext/neu.json");
        var file = new GeneratorPlannedFile("extern/neu.json", PlannedFileStatus.New, null, NewTags, null, null, false, TextFile.Utf8, Array.Empty<string>(), Array.Empty<string>(), ExternalPath: path);
        var plan = new GeneratorPlan { PackageId = "p", Key = "k", WriteRoot = _slice.ModRoot, Files = new[] { file }, Warnings = Array.Empty<string>() };

        var action = GeneratorPlanWriter.CreateAction(plan, "Erzeugen");

        Assert.Single(action.Actions);
    }
}
