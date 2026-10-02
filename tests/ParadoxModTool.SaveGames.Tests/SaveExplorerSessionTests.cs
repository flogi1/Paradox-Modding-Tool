using System.Collections.Concurrent;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveExplorerSessionTests
{
    private static SaveGameSettings Settings => Profiles.Eu5.SaveGames!;

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static SaveFileInfo AddSave(string folder, string name, int day, string body = "variables={ data={ { flag=a data={ type=value identity=1 } } } }\n", bool readable = true, string playthrough = "p1")
    {
        var path = Path.Combine(folder, name + ".eu5");
        var header = readable ? SaveText.TextHeader : SaveText.CompressedHeader;
        File.WriteAllText(path, header + "\nmetadata={\n\tdate=1337.4." + day + "\n\tplaythrough_id=\"" + playthrough + "\"\n}\n" + body);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc));
        return SaveFiles.Inspect(path, Settings.TextMarker);
    }

    [Fact]
    public void RefreshSavesReportsAMissingFolder()
    {
        var session = new SaveExplorerSession(Settings, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        session.RefreshSaves();
        Assert.Empty(session.Saves);
        Assert.Equal(SaveNotice.FolderMissing, session.Notice);

        var none = new SaveExplorerSession(Settings, null);
        none.RefreshSaves();
        Assert.Equal(SaveNotice.FolderMissing, none.Notice);
    }

    [Fact]
    public void RefreshSavesReportsAFolderWithoutReadableSaves()
    {
        var folder = NewFolder();
        AddSave(folder, "packed", 1, "PK", readable: false);
        var session = new SaveExplorerSession(Settings, folder);

        session.RefreshSaves();

        Assert.Single(session.Saves);
        Assert.Equal(SaveNotice.NoReadableSave, session.Notice);
    }

    [Fact]
    public async Task LoadAsyncShowsTheSaveAndClearsTheNotice()
    {
        var folder = NewFolder();
        var save = AddSave(folder, "a", 1);
        var session = new SaveExplorerSession(Settings, folder);
        session.RefreshSaves();
        var changes = 0;
        session.Changed += (_, _) => changes++;

        await session.LoadAsync(save, null);

        Assert.Equal(save.Path, session.View!.Current.File.Path);
        Assert.Equal(save, session.CurrentFile);
        Assert.Equal(SaveNotice.None, session.Notice);
        Assert.False(session.IsLoading);
        Assert.True(changes >= 2);
    }

    [Fact]
    public async Task NewestLoadWinsAndCancelsTheRunningOne()
    {
        var folder = NewFolder();
        var a = AddSave(folder, "a", 1);
        var b = AddSave(folder, "b", 2);
        var gates = new ConcurrentDictionary<string, TaskCompletionSource>();
        var tokens = new ConcurrentDictionary<string, CancellationToken>();
        SaveSnapshot Loader(string path, SaveGameSettings settings, IProgress<double>? progress, CancellationToken token)
        {
            tokens[path] = token;
            gates.GetOrAdd(path, _ => new TaskCompletionSource()).Task.Wait(CancellationToken.None);
            return SaveSnapshot.Load(path, settings, progress, CancellationToken.None);
        }

        var session = new SaveExplorerSession(Settings, folder, Loader);
        var first = session.LoadAsync(a, null);
        var second = session.LoadAsync(b, null);
        gates.GetOrAdd(a.Path, _ => new TaskCompletionSource()).SetResult();
        await first;

        Assert.Null(session.View);
        Assert.True(tokens[a.Path].IsCancellationRequested);

        gates.GetOrAdd(b.Path, _ => new TaskCompletionSource()).SetResult();
        await second;

        Assert.Equal(b.Path, session.View!.Current.File.Path);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task StillWritingOrLockedSavesKeepTheShownSave()
    {
        var folder = NewFolder();
        var a = AddSave(folder, "a", 1);
        var b = AddSave(folder, "b", 2);
        var c = AddSave(folder, "c", 3);
        SaveSnapshot Loader(string path, SaveGameSettings settings, IProgress<double>? progress, CancellationToken token)
        {
            var snapshot = SaveSnapshot.Load(path, settings, progress, token);
            if (path == b.Path)
            {
                return SaveSnapshot.Build(snapshot.File, new SaveReadResult(snapshot.Entries, new Dictionary<string, string>(), SaveReadStatus.Changed), settings);
            }

            return path == c.Path ? throw new IOException("gesperrt") : snapshot;
        }

        var session = new SaveExplorerSession(Settings, folder, Loader);
        await session.LoadAsync(a, null);

        await session.LoadAsync(b, null);
        Assert.Equal(a.Path, session.View!.Current.File.Path);
        Assert.Equal(SaveNotice.StillWriting, session.Notice);

        await session.LoadAsync(c, null);
        Assert.Equal(a.Path, session.View!.Current.File.Path);
        Assert.Equal(SaveNotice.CannotRead, session.Notice);
        Assert.Equal("gesperrt", session.NoticeDetail);
        Assert.Equal(a, session.CurrentFile);
    }

    [Fact]
    public async Task IncompleteSaveIsShownWithANotice()
    {
        var folder = NewFolder();
        var save = AddSave(folder, "cut", 1, "variables={ data={ { flag=a data={ type=value identity=1 } }");
        var session = new SaveExplorerSession(Settings, folder);

        await session.LoadAsync(save, null);

        Assert.Equal(save.Path, session.View!.Current.File.Path);
        Assert.Equal(SaveNotice.Incomplete, session.Notice);
    }

    [Fact]
    public async Task UnreadableSaveIsNeverLoaded()
    {
        var folder = NewFolder();
        var packed = AddSave(folder, "packed", 1, "PK", readable: false);
        var calls = 0;
        var session = new SaveExplorerSession(Settings, folder, (path, settings, progress, token) =>
        {
            calls++;
            return SaveSnapshot.Load(path, settings, progress, token);
        });

        await session.LoadAsync(packed, null);

        Assert.Equal(0, calls);
        Assert.Null(session.View);
    }

    [Fact]
    public async Task ComparisonWithTheSameSaveOrADeletedSaveIsNoComparison()
    {
        var folder = NewFolder();
        var a = AddSave(folder, "a", 1);
        var b = AddSave(folder, "b", 2);
        var session = new SaveExplorerSession(Settings, folder);
        await session.LoadAsync(b, null);

        await session.SetComparisonAsync(b, null);
        Assert.Null(session.View!.Comparison);
        Assert.Null(session.ComparisonFile);

        File.Delete(a.Path);
        await session.SetComparisonAsync(a, null);
        Assert.Null(session.View!.Comparison);
        Assert.Null(session.ComparisonFile);
    }

    [Fact]
    public async Task ComparisonBuildsBeforeAndChanges()
    {
        var folder = NewFolder();
        var a = AddSave(folder, "a", 1, "variables={ data={ { flag=x data={ type=value identity=1 } } } }\n");
        var b = AddSave(folder, "b", 2, "variables={ data={ { flag=x data={ type=value identity=2 } } } }\n");
        var session = new SaveExplorerSession(Settings, folder);
        await session.LoadAsync(b, null);

        await session.SetComparisonAsync(a, null);

        Assert.Equal(a.Path, session.View!.Before!.File.Path);
        Assert.Equal(SaveChange.Changed, session.View.Comparison!.ChangeOfName("x"));
        Assert.Equal(a, session.ComparisonFile);

        await session.LoadAsync(b, null);
        Assert.NotNull(session.View!.Comparison);

        await session.SetComparisonAsync(null, null);
        Assert.Null(session.View!.Comparison);
        Assert.Null(session.View.Before);
    }

    [Fact]
    public async Task NewerSaveIsTheNewestReadableSaveWrittenAfterTheCurrentOne()
    {
        var folder = NewFolder();
        var a = AddSave(folder, "a", 1);
        var session = new SaveExplorerSession(Settings, folder);
        session.RefreshSaves();
        Assert.Null(session.NewerSave());

        await session.LoadAsync(a, null);
        Assert.Null(session.NewerSave());

        AddSave(folder, "packed", 3, "PK", readable: false);
        var b = AddSave(folder, "b", 2);
        session.RefreshSaves();

        Assert.Equal(b.Path, session.NewerSave()!.Path);
    }
}
