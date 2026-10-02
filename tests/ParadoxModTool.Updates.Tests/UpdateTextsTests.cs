using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using ParadoxModTool.Updates;
using Xunit;

namespace ParadoxModTool.Updates.Tests;

public class UpdateTextsTests
{
    [Fact]
    public void Jeder_Key_steht_in_Deutsch_und_Englisch_mit_gleichem_deutschen_Text()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Updates", "UpdateTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>update\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 45, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = match.Groups["text"].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        }
    }

    [Fact]
    public void Rueckfall_Text_verspricht_keine_vollstaendige_Sicherung()
    {
        // Ein halb gescheiterter Rückfall hat schon Teile zurückverschoben; die Sicherung kann auch ganz fehlen.
        var text = UpdateTexts.RollbackFailed(@"C:\PMT\.update-backup", "update.log");

        Assert.DoesNotContain("liegt vollständig", text);
        Assert.Contains("falls vorhanden", text);
    }

    [Fact]
    public void Keine_Auslassungspunkte()
    {
        var root = FindRepositoryRoot();
        foreach (var language in new[] { "de", "en" })
        {
            var table = UiTextTable.Read(Path.Combine(root, "locales", language + ".json"));
            var bad = table.Where(pair => pair.Key.StartsWith("update.", StringComparison.Ordinal) && (pair.Value.Contains('…') || pair.Value.Contains("...")))
                .Select(pair => $"{language}: {pair.Key}")
                .ToList();
            Assert.Empty(bad);
        }
    }

    [Fact]
    public void Jedes_Problem_und_jede_Phase_hat_einen_eigenen_Text()
    {
        var problems = Enum.GetValues<UpdateProblem>().Where(p => p != UpdateProblem.None).Select(p => UpdateTexts.Problem(p, "x")).ToList();
        Assert.Equal(problems.Count, problems.Distinct().Count());
        var phases = Enum.GetValues<UpdateRunnerPhase>().Select(p => UpdateTexts.Phase(p, 2)).ToList();
        Assert.Equal(phases.Count, phases.Distinct().Count());
        Assert.Contains("2", UpdateTexts.Phase(UpdateRunnerPhase.Waiting, 2));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden");
    }
}
