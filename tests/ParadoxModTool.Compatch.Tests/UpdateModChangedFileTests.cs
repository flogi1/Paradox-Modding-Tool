using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Update-Modus: Eine Mod-Datei, die seit dem letzten Schreiben nicht mehr den geschriebenen Inhalt hat (Nutzertest 2026-10-01: hud.gui von Hand
/// zurückkopiert), verliert beim nächsten Laden ihre gespeicherten Entscheidungen und ihre Vergleichsbasis. Sonst galten ihre Stellen über die
/// Prüfsummen der Basis als erledigt, obwohl das Ergebnis nicht mehr in der Datei stand.
/// </summary>
public class UpdateModChangedFileTests : UpdateModTestBase
{
    private const string Buildings = "common/buildings/00_buildings.txt";
    private const string Other = "common/buildings/01_other.txt";

    private const string OldText = "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 50\n}\n";
    private const string NewText = "temple = {\n\tcost = 120\n}\nmarket = {\n\tcost = 60\n}\n";
    private const string ModText = "temple = {\n\tcost = 90\n}\nmarket = {\n\tcost = 50\n}\n";

    private readonly CompatchState _state = new() { Target = PatchTarget.UpdateMod };

    public UpdateModChangedFileTests()
    {
        _state.TargetFolder = BDir;
        _state.ModBFolder = BDir;
        foreach (var (path, rename) in new[] { (Buildings, false), (Other, true) })
        {
            string Keys(string text) => rename ? text.Replace("temple", "farm").Replace("market", "mill") : text;
            PutVanilla(path, Keys(OldText));
            PutA(path, Keys(NewText));
            PutB(path, Keys(ModText));
        }
    }

    private CompatchSession Open(CompatchBase? baseline)
    {
        var session = new CompatchSession(UpdateInput(), _state, baseline);
        session.Load();
        return session;
    }

    /// <summary>Erster Lauf: alle Stellen entscheiden (Konflikt temple → neue Vanilla), schreiben, Basis festhalten.</summary>
    private CompatchBase WriteFirstRun()
    {
        var session = Open(null);
        session.DecideMany(session.Actions.Select(a => a.Id), _ => new MergeDecision(DecisionKind.TakeA));
        var prepared = session.PrepareWrite(Array.Empty<string>(), _ => "utf-8");
        PatchWriter.Commit(prepared, BDir, null);
        Assert.Contains("cost = 120", ReadMod(Buildings));
        return session.CaptureBase(prepared, null, null);
    }

    private static string Norm(string path) => path.Replace('\\', '/').Trim('/').ToLowerInvariant();

    private IEnumerable<string> DecisionsOf(CompatchSession session, string file) =>
        session.Actions.Where(a => Norm(a.File) == file).Select(a => a.Id).Where(_state.Decisions.ContainsKey);

    [Fact]
    public void Restored_file_loses_its_decisions_and_baseline()
    {
        var baseline = WriteFirstRun();
        PutB(Buildings, ModText);

        var session = Open(baseline);

        Assert.Equal(new[] { Buildings }, session.ForgottenFiles);
        Assert.Empty(DecisionsOf(session, Buildings));
        var restored = session.Actions.Where(a => Norm(a.File) == Buildings).ToList();
        Assert.NotEmpty(restored);
        Assert.DoesNotContain(restored, a => a.Stage == MergeStage.Decided || session.IsExplicit(a.Id));
        Assert.DoesNotContain(restored, a => baseline.AddressHashes.ContainsKey(a.Address.Id));
        Assert.DoesNotContain(Buildings, baseline.UpdateFiles.Keys);
    }

    [Fact]
    public void Other_file_of_the_run_keeps_its_state()
    {
        var baseline = WriteFirstRun();
        var before = _state.Decisions.Count;
        PutB(Buildings, ModText);

        var session = Open(baseline);

        Assert.NotEmpty(DecisionsOf(session, Other));
        Assert.Contains(Other, baseline.UpdateFiles.Keys);
        Assert.True(_state.Decisions.Count < before);
    }

    [Fact]
    public void File_as_written_keeps_its_decisions()
    {
        var baseline = WriteFirstRun();
        var before = _state.Decisions.Keys.ToList();

        var session = Open(baseline);

        Assert.Empty(session.ForgottenFiles);
        Assert.Equal(before, _state.Decisions.Keys.ToList());
    }

    [Fact]
    public void Base_records_the_written_files_with_their_hash()
    {
        var baseline = WriteFirstRun();

        Assert.Equal(TextEncodings.Sha256(ModBytes(Buildings)), baseline.UpdateFiles[Buildings]);
        Assert.Equal(TextEncodings.Sha256(ModBytes(Other)), baseline.UpdateFiles[Other]);
    }
}
