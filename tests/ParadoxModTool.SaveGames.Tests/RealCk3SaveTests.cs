using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.SaveGames.Tests;

/// <summary>
/// Die beiden echten Crusader-Kings-III-Spielstände des Nutzers vom 2026-09-30 (Spielversion 1.19.0.6): <c>Debugmode Save.ck3</c> (komprimierter Text,
/// <c>SAV…02</c>) und <c>autosave_exit.ck3</c> (Text, <c>SAV…00</c>). Ohne die Dateien läuft nichts.
/// </summary>
public class RealCk3SaveTests
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Crusader Kings III", "save games");

    private readonly ITestOutputHelper _output;

    public RealCk3SaveTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Both_saves_read()
    {
        var debug = Path.Combine(Folder, "Debugmode Save.ck3");
        var autosave = Path.Combine(Folder, "autosave_exit.ck3");
        if (!File.Exists(debug) || !File.Exists(autosave))
        {
            return;
        }

        var settings = Profiles.Ck3.SaveGames!;
        var saves = SaveFiles.List(Folder, settings);
        var debugInfo = saves.Single(s => s.Path.EndsWith("Debugmode Save.ck3", StringComparison.OrdinalIgnoreCase));
        Assert.True(debugInfo.Readable);
        Assert.True(debugInfo.Compressed);
        Assert.True(saves.Single(s => s.Path.EndsWith("autosave_exit.ck3", StringComparison.OrdinalIgnoreCase)).Readable);

        foreach (var path in new[] { debug, autosave })
        {
            var watch = Stopwatch.StartNew();
            var snapshot = SaveSnapshot.Load(path, settings, null, CancellationToken.None);
            var perKind = snapshot.ObjectsWithVariables.GroupBy(o => o.Kind.Label).Select(g => $"{g.Key}: {g.Count()}");
            _output.WriteLine($"{Path.GetFileName(path)}: {snapshot.Entries.Count} Einträge, {watch.Elapsed.TotalSeconds:0.0} s; {string.Join(", ", perKind)}");

            Assert.Equal(SaveReadStatus.Complete, snapshot.Status);
            Assert.True(snapshot.Entries.Count > 1000, $"nur {snapshot.Entries.Count} Einträge");
            Assert.DoesNotContain(snapshot.Entries, e => e.OwnerPath.EndsWith("/motto", StringComparison.Ordinal));
            Assert.Contains(snapshot.Entries, e => e.OwnerPath.Length == 0 && e.Name == "seljuk_invasion_happened");
            Assert.Contains(snapshot.ObjectsWithVariables, o => o.Kind.Order == 1 && !o.DisplayName.All(char.IsAsciiDigit));
        }
    }
}
