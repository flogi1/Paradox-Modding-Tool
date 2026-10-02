using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Lauf "Mod an neue Vanilla anpassen": Dateien finden, alte Fassung je Datei wählen, Ordner des Laufs anlegen.</summary>
public class VanillaUpdateRunTests : CompatchTestBase
{
    private const string X = "common/buildings/x.txt";
    private const string Y = "common/buildings/y.txt";

    private string History => Path.Combine(Dir, "history");

    private string RunDir => Path.Combine(Dir, "run");

    private string LedgerDir => Path.Combine(Dir, "ledger");

    private VanillaHistoryStore Store => VanillaHistoryStore.FromHistoryRoot(History);

    /// <summary>Legt eine Vanilla-Fassung (VanillaDir = installiertes Spiel) an und speichert sie als Snapshot dieser Version.</summary>
    private void Snapshot(string relative, string version, string text)
    {
        PutVanilla(relative, text);
        Store.RecordSnapshot(relative, version, Path.Combine(VanillaDir, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private IReadOnlyList<OverriddenFile> Overridden(params string[] paths) =>
        paths.Select(p => new OverriddenFile
        {
            RelativePath = p,
            VanillaFullPath = Path.Combine(VanillaDir, p.Replace('/', Path.DirectorySeparatorChar)),
            ModFullPath = Path.Combine(BDir, p.Replace('/', Path.DirectorySeparatorChar)),
        }).ToList();

    private UpdateScan Scan(IReadOnlyList<OverriddenFile> files, string current, string? pattern = null, string? forced = null, IEnumerable<string>? modPaths = null) =>
        VanillaUpdateRun.Scan(files, modPaths ?? files.Select(f => f.RelativePath), VanillaDir, Store, current, VanillaVersionLedger.Load(LedgerDir), pattern, forced);

    [Fact]
    public void Scan_splits_changed_unchanged_and_without_old_version()
    {
        const string Z = "common/buildings/z.txt";
        Snapshot(X, "1.37", "a = 1\n");
        PutVanilla(X, "a = 2\n");
        Snapshot(Y, "1.37", "b = 1\n");
        PutVanilla(Z, "c = 1\n");

        var scan = Scan(Overridden(X, Y, Z), "1.38");

        Assert.Equal(new[] { X }, scan.Changed.Select(f => f.RelativePath));
        Assert.Equal("1.37", scan.Changed[0].OldVersion);
        Assert.Equal(new[] { Z }, scan.WithoutOldVersion.Select(f => f.RelativePath));
        Assert.Null(scan.WithoutOldVersion[0].OldVersion);
        Assert.Equal(new[] { "1.37" }, scan.RecordedVersionsBelowCurrent);
    }

    [Fact]
    public void Scan_lists_removed_vanilla_files()
    {
        Snapshot(X, "1.37", "a = 1\n");
        File.Delete(Path.Combine(VanillaDir, X.Replace('/', Path.DirectorySeparatorChar)));

        var scan = Scan(Overridden(), "1.38", modPaths: new[] { X, Y });

        Assert.Equal(new[] { X }, scan.RemovedInVanilla);
        Assert.Empty(scan.Changed);
    }

    [Fact]
    public void Scan_uses_the_ledger_per_file()
    {
        Snapshot(X, "1.37", "a = 1\n");
        Snapshot(X, "1.38", "a = 2\n");
        Snapshot(Y, "1.37", "b = 1\n");
        Snapshot(Y, "1.38", "b = 2\n");
        PutVanilla(X, "a = 3\n");
        PutVanilla(Y, "b = 3\n");
        var ledger = VanillaVersionLedger.Load(LedgerDir);
        ledger.Set(new[] { X }, "1.38");
        ledger.Save();

        var scan = Scan(Overridden(X, Y), "1.39", pattern: "1.37.*");

        Assert.Equal("1.38", scan.Changed.Single(f => f.RelativePath == X).OldVersion);
        Assert.Equal("1.37", scan.Changed.Single(f => f.RelativePath == Y).OldVersion);
    }

    [Fact]
    public void File_already_updated_to_the_installed_version_is_not_listed()
    {
        Snapshot(X, "1.37", "a = 1\n");
        Snapshot(X, "1.38", "a = 2\n");
        var ledger = VanillaVersionLedger.Load(LedgerDir);
        ledger.Set(new[] { X }, "1.38");
        ledger.Save();

        var scan = Scan(Overridden(X), "1.38");

        Assert.Empty(scan.Changed);
        Assert.Empty(scan.WithoutOldVersion);
    }

    [Fact]
    public void Updated_files_leave_out_open_files_and_files_the_writer_skipped()
    {
        var prepared = new PreparedWrite();
        prepared.Issues.Add(new WriteIssue(WriteIssueKind.Locked, "common/buildings/locked.txt", "gesperrt"));

        var updated = VanillaUpdateRun.UpdatedFiles(
            new[] { X, Y, "common/buildings/locked.txt" },
            openFiles: new[] { "Common/Buildings/Y.txt" },
            prepared);

        Assert.Equal(new[] { X }, updated);
    }

    [Fact]
    public void Forced_version_applies_to_every_file_that_has_it()
    {
        Snapshot(X, "1.36", "a = 0\n");
        Snapshot(X, "1.37", "a = 1\n");
        Snapshot(Y, "1.37", "b = 1\n");
        PutVanilla(X, "a = 2\n");
        PutVanilla(Y, "b = 2\n");

        var scan = Scan(Overridden(X, Y), "1.38", forced: "1.36");

        Assert.Equal("1.36", scan.Changed.Single().OldVersion);
        Assert.Equal(new[] { Y }, scan.WithoutOldVersion.Select(f => f.RelativePath));
    }

    private (CompatchInput Input, CompatchState State) Prepare(UpdateScan scan) =>
        VanillaUpdateRun.Prepare(RunDir, scan.Changed.Concat(scan.WithoutOldVersion).ToList(), Store, BDir, "Meine Mod", "mine", "1.38", EuIVRules(), Array.Empty<string>());

    [Fact]
    public void Prepare_builds_the_run_folders_and_a_session_that_writes_into_the_mod()
    {
        Snapshot(X, "1.37", "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 5\n}\n");
        PutVanilla(X, "temple = {\n\tcost = 120\n}\nmarket = {\n\tcost = 5\n}\n");
        PutB(X, "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 9\n}\n");
        PutB("common/buildings/other.txt", "foo = {\n\tcost = 1\n}\n");

        var (input, state) = Prepare(Scan(Overridden(X), "1.38"));
        var session = new CompatchSession(input, state, null);
        session.Load();
        PatchWriter.Commit(session.PrepareWrite(Array.Empty<string>(), _ => "utf-8"), state.TargetFolder, null);

        var text = File.ReadAllText(Path.Combine(BDir, X.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("cost = 120", text);
        Assert.Contains("cost = 9", text);
        Assert.True(File.Exists(Path.Combine(RunDir, "old", "common", "buildings", "x.txt")));
        Assert.True(File.Exists(Path.Combine(RunDir, "new", "common", "buildings", "x.txt")));
        Assert.Equal(PatchTarget.UpdateMod, state.Target);
        Assert.Equal("1.38", state.UpdateNewVersion);
        Assert.Equal("1.37", state.UpdateOldVersions[X]);
        Assert.Equal(new[] { X }, state.UpdatePaths);
        Assert.DoesNotContain(session.Run.Resolved.Addresses.Keys, a => a.Key == "foo");
    }

    [Fact]
    public void Prepare_twice_gives_the_same_folders()
    {
        Snapshot(X, "1.37", "a = 1\n");
        PutVanilla(X, "a = 2\n");
        PutB(X, "a = 1\n");
        var scan = Scan(Overridden(X), "1.38");

        var first = Prepare(scan).Input;
        var second = Prepare(scan).Input;

        Assert.Equal(first.ModAFolder, second.ModAFolder);
        Assert.Equal(first.VanillaFolder, second.VanillaFolder);
        Assert.Equal("a = 1\n", File.ReadAllText(Path.Combine(RunDir, "old", "common", "buildings", "x.txt")));
    }
}
