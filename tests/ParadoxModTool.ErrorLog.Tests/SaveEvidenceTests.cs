using System.Text;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SaveGames;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>Kleine EU-V-Textspielstände für Tests (Kopfzeile wie ein Debug-Stand).</summary>
internal static class Saves
{
    private static readonly Lazy<SaveGameSettings> Eu5 = new(() =>
        GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu5.json")).SaveGames!);

    public static SaveSnapshot Load(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-errorlog-save-" + Guid.NewGuid().ToString("N") + ".eu5");
        File.WriteAllText(path, "SAV0300701cf8aa0004df3b00000000\nmetadata={\n\tdate=1363.11.17\n}\n" + body, new UTF8Encoding(false));
        return SaveSnapshot.Load(path, Eu5.Value, null, default);
    }

    public static string Var(string name) => $"variables={{ data={{ {{ flag={name} data={{ type=value identity=1 }} }} }} }}";

    public static string Flag(string holder, string flag) => $"variables={{ data={{ {{ flag={holder} data={{ type=flag flag=\"{flag}\" }} }} }} }}";
}

public class SaveEvidenceTests
{
    [Fact]
    public void CountsObjectsAndPicksExamplesInKindOrder()
    {
        var save = Saves.Load(
            $"countries={{ tags={{ 3=SWE 4=DAN }} database={{ 3={{ {Saves.Var("iv2_x")} }} 4={{ {Saves.Var("iv2_x")} }} }} }}\n" +
            $"character_db={{ database={{ 0={{ first_name=\"name_birger\" {Saves.Var("iv2_x")} }} }} }}\n");

        var evidence = SaveEvidence.For(save, "iv2_x", "variable");

        Assert.Equal(("1363.11.17", "iv2_x", 3), (evidence.SaveLabel, evidence.Name, evidence.ObjectCount));
        Assert.Equal(new[] { "DAN", "SWE", "name_birger (0)" }, evidence.Examples.Select(e => e.DisplayName));
        Assert.Null(evidence.SimilarName);
    }

    [Fact]
    public void MissingNameGetsTheClosestFrequentSimilarName()
    {
        var countries = string.Concat(Enumerable.Range(1, 5).Select(i => $"{i}={{ {Saves.Var("iv2_counter")} }} "));
        var save = Saves.Load($"countries={{ database={{ {countries} 9={{ {Saves.Var("iv2_countr")} }} }} }}\n");

        var evidence = SaveEvidence.For(save, "iv2_countre", "variable");

        Assert.Equal(0, evidence.ObjectCount);
        Assert.Empty(evidence.Examples);
        Assert.Equal(("iv2_counter", 5), (evidence.SimilarName, evidence.SimilarCount));
    }

    [Fact]
    public void FlagKindLooksOnlyAtFlagValues()
    {
        var save = Saves.Load($"countries={{ database={{ 1={{ {Saves.Var("x")} }} 2={{ {Saves.Flag("holder", "x")} }} 3={{ {Saves.Flag("other", "x")} }} }} }}\n");

        Assert.Equal(2, SaveEvidence.For(save, "x", "flag").ObjectCount);
        Assert.Equal(1, SaveEvidence.For(save, "x", "variable").ObjectCount);
    }
}
