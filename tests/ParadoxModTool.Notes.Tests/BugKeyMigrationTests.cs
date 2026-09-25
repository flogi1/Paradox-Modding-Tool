using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugKeyMigrationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-bugkeys-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static BugReport Report(string? modId, params string[] keys)
    {
        var report = new BugReport { ModId = modId };
        report.Links.AddRange(keys.Select(k => BugLink.Create(k, 1, null)));
        return report;
    }

    [Fact]
    public void Rekey_ModAngelegt_VollerPfadWirdModSchluessel_MeldungBekommtProjekt()
    {
        var report = Report(null, @"C:\mods\beta\a.txt", @"C:\vanilla\b.txt");
        var data = new BugData { Reports = { report } };

        var changed = BugKeyMigration.Rekey(data, key => key == @"C:\mods\beta\a.txt" ? "mod:beta/a.txt" : null);

        Assert.True(changed);
        Assert.Equal(new[] { "mod:beta/a.txt", @"C:\vanilla\b.txt" }, report.Links.Select(l => l.Key));
        Assert.Equal("beta", report.ModId);
        Assert.False(BugKeyMigration.Rekey(data, key => key == @"C:\mods\beta\a.txt" ? "mod:beta/a.txt" : null));
    }

    [Fact]
    public void Rekey_BranchFreigabe_ZeigtAufHauptmod()
    {
        var report = Report("beta", "mod:beta/a.txt", "mod:main/a.txt");
        report.Links[1].Line = 1;
        var data = new BugData { Reports = { report } };

        BugKeyMigration.Rekey(
            data,
            key => NoteKeys.TryParseModKey(key, out var id, out var rel) && id == "beta" ? NoteKeys.ModKey("main", rel) : null,
            id => id == "beta" ? "main" : null);

        Assert.Equal("main", report.ModId);
        Assert.Equal("mod:main/a.txt", Assert.Single(report.Links).Key);
    }

    [Fact]
    public void Rekey_HauptmodFreigabe_WirdVollerPfad_UndUebernimmtAndereMod()
    {
        var onlyMain = Report("main", "mod:main/a.txt");
        var withChild = Report("main", "mod:main/a.txt", "mod:beta/b.txt");
        var data = new BugData { Reports = { onlyMain, withChild } };

        BugKeyMigration.Rekey(
            data,
            key => NoteKeys.TryParseModKey(key, out var id, out var rel) && id == "main" ? @"C:\mods\main\" + rel.Replace('/', '\\') : null,
            id => id == "main" ? string.Empty : null);

        Assert.Null(onlyMain.ModId);
        Assert.Equal(@"C:\mods\main\a.txt", Assert.Single(onlyMain.Links).Key);
        Assert.Equal("beta", withChild.ModId);
    }

    [Fact]
    public void Run_SchreibtNurBeiAenderung()
    {
        var path = Path.Combine(_dir, BugReportStore.FileName);
        BugReportStore.Save(new BugData { Reports = { Report(null, @"C:\x\a.txt") } }, path);
        var stamp = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(50);

        Assert.False(BugKeyMigration.Run(path, _ => null));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));

        Assert.True(BugKeyMigration.Run(path, key => key == @"C:\x\a.txt" ? "mod:m/a.txt" : null));
        Assert.Equal("mod:m/a.txt", Assert.Single(Assert.Single(BugReportStore.Load(path).Reports).Links).Key);
    }

    [Fact]
    public void Run_OhneDateien_LegtNichtsAn()
    {
        var path = Path.Combine(_dir, BugReportStore.FileName);

        Assert.False(BugKeyMigration.Run(path, key => "mod:m/a.txt"));
        Assert.False(File.Exists(path));
    }
}
