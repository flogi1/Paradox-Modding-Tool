using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.SaveGames.Tests;

/// <summary>
/// Der echte Text-Spielstand von Hearts of Iron IV vom 2026-10-16 (<c>PMT test.hoi4</c>, 1.19.3, mit <c>save_as_binary=no</c> gespeichert, Deutschland
/// 1936). Ohne die Datei läuft nichts.
/// </summary>
public class RealHoi4SaveTests
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Hearts of Iron IV", "save games");

    private readonly ITestOutputHelper _output;

    public RealHoi4SaveTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Text_save_lists_countries_states_and_globals()
    {
        var path = Path.Combine(Folder, "PMT test.hoi4");
        if (!File.Exists(path))
        {
            return;
        }

        var settings = Profiles.Hoi4.SaveGames!;
        var info = SaveFiles.List(Folder, settings).Single(s => s.Path.EndsWith("PMT test.hoi4", StringComparison.OrdinalIgnoreCase));
        Assert.True(info.Readable);
        Assert.Equal("1936.1.1.12", info.GameDate);

        var watch = Stopwatch.StartNew();
        var snapshot = SaveSnapshot.Load(path, settings, null, CancellationToken.None);
        var perKind = snapshot.ObjectsWithVariables.GroupBy(o => o.Kind.Label).Select(g => $"{g.Key}: {g.Count()}");
        _output.WriteLine($"{snapshot.Entries.Count} Einträge, {watch.Elapsed.TotalSeconds:0.0} s; {string.Join(", ", perKind)}");

        Assert.Equal(SaveReadStatus.Complete, snapshot.Status);
        Assert.True(snapshot.Entries.Count > 1000, $"nur {snapshot.Entries.Count} Einträge");
        Assert.Contains(snapshot.Entries, e => e.OwnerPath.Length == 0 && e.Name == "asian_and_oceanian_countries" && e.Kind == SaveEntryKind.ListItem);
        Assert.Contains(snapshot.Entries, e => e.OwnerPath == "states/2" && e.Name == "historical_capital_for_country");
        Assert.Contains(snapshot.ObjectsWithVariables, o => o.Kind.Pattern == "countries/*" && o.DisplayName == "GER");
        Assert.DoesNotContain(snapshot.Entries, e => e.Name.Contains('^'));
    }
}
