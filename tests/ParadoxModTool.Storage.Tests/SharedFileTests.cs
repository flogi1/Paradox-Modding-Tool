using ParadoxModTool.Storage;
using Xunit;

namespace ParadoxModTool.Storage.Tests;

public sealed class SharedFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void WriteAllTextAtomic_CreatesFolderAndOverwrites()
    {
        var path = Path.Combine(_dir, "sub", "a.json");

        SharedFile.WriteAllTextAtomic(path, "eins");
        SharedFile.WriteAllTextAtomic(path, "zwei");

        Assert.Equal("zwei", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllTextAtomic_LeavesNoTempFileBehind()
    {
        var path = Path.Combine(_dir, "a.json");

        SharedFile.WriteAllTextAtomic(path, "x");

        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void WriteAllTextAtomic_RetriesWhileForeignHandleBlocksReplace()
    {
        // Ein fremder Prozess (Virenscanner, Indexer) hält die Zieldatei kurz ohne Löschfreigabe offen;
        // File.Move scheitert dann unter Windows mit UnauthorizedAccessException, obwohl keine Sperre verletzt wurde.
        var path = Path.Combine(_dir, "a.json");
        SharedFile.WriteAllTextAtomic(path, "alt");
        var foreignHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(async () =>
        {
            await Task.Delay(150);
            foreignHandle.Dispose();
        });

        SharedFile.WriteAllTextAtomic(path, "neu");

        release.Wait();
        Assert.Equal("neu", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void Locked_ReturnsActionResult()
    {
        Assert.Equal(42, SharedFile.Locked(Path.Combine(_dir, "a.json"), () => 42));
    }

    [Fact]
    public void Locked_AllowsNestedCallOnSamePath()
    {
        var path = Path.Combine(_dir, "a.json");

        var result = SharedFile.Locked(path, () => SharedFile.Locked(path, () => 7));

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task Locked_SerializesReadModifyWriteAcrossThreads()
    {
        // Ohne Sperre gingen bei diesem "Zähler in einer Datei" Erhöhungen verloren.
        var path = Path.Combine(_dir, "counter.txt");
        SharedFile.WriteAllTextAtomic(path, "0");

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
            {
                SharedFile.Locked(path, () =>
                {
                    var value = int.Parse(File.ReadAllText(path));
                    SharedFile.WriteAllTextAtomic(path, (value + 1).ToString());
                });
            }
        })).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal("320", File.ReadAllText(path));
    }

    [Fact]
    public async Task Locked_RunsActionEvenWhenLockCannotBeAcquired()
    {
        var path = Path.Combine(_dir, "a.json");
        var release = new ManualResetEventSlim();
        var holding = new ManualResetEventSlim();
        var holder = Task.Run(() => SharedFile.Locked(path, () =>
        {
            holding.Set();
            release.Wait();
        }));
        holding.Wait();

        var ran = SharedFile.Locked(path, () => true, TimeSpan.FromMilliseconds(100));

        release.Set();
        await holder;
        Assert.True(ran);
    }
}
