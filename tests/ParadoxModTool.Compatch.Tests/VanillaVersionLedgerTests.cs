using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class VanillaVersionLedgerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-ledger-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Set_only_touches_the_given_files()
    {
        var ledger = VanillaVersionLedger.Load(_dir);
        ledger.Set(new[] { "common/x/a.txt" }, "1.38");
        ledger.Save();

        var loaded = VanillaVersionLedger.Load(_dir);

        Assert.Equal("1.38", loaded.Get("common/x/a.txt"));
        Assert.Null(loaded.Get("common/x/b.txt"));
    }

    [Fact]
    public void Paths_are_case_and_slash_insensitive()
    {
        var ledger = VanillaVersionLedger.Load(_dir);
        ledger.Set(new[] { "Common\\X\\A.txt" }, "1.38");

        Assert.Equal("1.38", ledger.Get("common/x/a.txt"));
    }

    [Fact]
    public void Unreadable_file_gives_empty_ledger()
    {
        File.WriteAllText(Path.Combine(_dir, "vanilla-versions.json"), "{ kaputt");

        Assert.Null(VanillaVersionLedger.Load(_dir).Get("common/x/a.txt"));
    }
}
