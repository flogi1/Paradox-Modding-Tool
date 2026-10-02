using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.ErrorLog/ErrorLogTexts.cs"));
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>errorlog\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count > 100, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = Regex.Unescape(match.Groups["text"].Value);
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
            Assert.False(en!.EndsWith("…") || en.EndsWith("...") || de!.EndsWith("…") || de.EndsWith("..."), $"{key} endet mit Auslassungspunkten");
            Assert.Equal(Placeholders(de!), Placeholders(en));
        }
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("Fehlerprotokoll", ErrorLogTexts.Menu);
        Assert.Equal("3 Meldungen in 2 Gruppen, 1 neu, 0 behoben", ErrorLogTexts.Counts(new ErrorCounts(3, 2, 1, 0)));
        Assert.Equal("Unbekannter Effekt", ErrorLogTexts.GroupTitle("Unbekannter Effekt", string.Empty));
        Assert.Equal("Unbekannter Effekt: cmf_suppress", ErrorLogTexts.GroupTitle("Unbekannter Effekt", "cmf_suppress"));
        Assert.Equal("Symbolindex: Dateien werden gesucht", ErrorLogTexts.IndexStatus(0, 0));
        Assert.Equal("Symbolindex: 5 von 9 Dateien", ErrorLogTexts.IndexStatus(5, 9));
        Assert.Equal(
            "cmf_suppress ist in Community Mod Framework definiert, diese Mod fehlt im Playset.",
            ErrorLogTexts.Hint(new ErrorHint(
                HintKind.DefinedOutsidePlayset,
                "cmf_suppress",
                new[] { new SymbolOccurrence("cmf_suppress", SymbolRole.Definition, "a.txt", 1, new ModSource("Community Mod Framework", "c", false, -1)) },
                Array.Empty<string>())));
        Assert.Equal(
            "Vermutlich harmlos: x wird in GUI-Dateien genutzt (das Spiel zählt das nicht mit).",
            ErrorLogTexts.Hint(new ErrorHint(HintKind.UsedInGui, "x", Array.Empty<SymbolOccurrence>(), Array.Empty<string>())));
        var country = new ParadoxModTool.SaveGames.SaveObjectKind(1, "Länder", "countries/database/*");
        var character = new ParadoxModTool.SaveGames.SaveObjectKind(2, "Charaktere", "character_db/database/*");
        Assert.Equal(
            "Im Spielstand 1363.11.17: bei 3 Objekten gesetzt (Länder SWE, Länder DAN, Charaktere name_birger (0))",
            ErrorLogTexts.EvidenceLine(new SaveEvidence("1363.11.17", "x", 3, new[]
            {
                new ParadoxModTool.SaveGames.SaveObjectRef("countries/database/3", country, "SWE", "3"),
                new ParadoxModTool.SaveGames.SaveObjectRef("countries/database/4", country, "DAN", "4"),
                new ParadoxModTool.SaveGames.SaveObjectRef("character_db/database/0", character, "name_birger (0)", "0"),
            }, null, 0)));
        Assert.Equal("Im Spielstand 1363.11.17 nirgends gesetzt", ErrorLogTexts.EvidenceLine(new SaveEvidence("1363.11.17", "x", 0, Array.Empty<ParadoxModTool.SaveGames.SaveObjectRef>(), null, 0)));
        Assert.Equal("set_variable auskommentieren", ErrorLogTexts.Fix("commentOutSet", Catalogs.Eu5.Find("VAR-001")));
        Assert.Equal("Alle Meldungen der Datei a.txt", ErrorLogTexts.SuppressOption(new Suppression(SuppressionScope.File, Path.Combine("x", "a.txt")), "Art", null));
    }

    [Fact]
    public void FixTextsFallBackToGerman()
    {
        Assert.Equal("Umbenennen iv2_x → iv2_y", ErrorLogTexts.RenameAction("iv2_x", "iv2_y"));
        Assert.Equal("3 Stellen in 2 Dateien, 1 Mods", ErrorLogTexts.RenameSummary(3, 2, 1));
        Assert.StartsWith("Community Mod Framework steht jetzt im Playset „Idea Variation“.", ErrorLogTexts.AddModAdded("Community Mod Framework", "Idea Variation"));
    }

    [Fact]
    public void LogPositionNamesTheFileOnlyForRunsWithSeveralFiles()
    {
        var entry = Catalogs.Classify("[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2")[0];
        var plain = Catalogs.Classify("[19:49:44][x.cpp:1]: irgendwas")[0];

        Assert.Equal("common/scripted_effects/purger.txt:2  (Log-Zeile 1)", ErrorLogTexts.LogPosition(entry, new[] { "error.log" }));
        Assert.Equal("common/scripted_effects/purger.txt:2  (error.1.log, Zeile 1)", ErrorLogTexts.LogPosition(entry, new[] { "error.1.log", "error.log" }));
        Assert.Equal("error.1.log, Zeile 1", ErrorLogTexts.LogPosition(plain, new[] { "error.1.log", "error.log" }));
        Assert.Equal("Log-Zeile 1", ErrorLogTexts.LogPosition(plain, Array.Empty<string>()));
    }

    [Fact]
    public void RunTextNamesTheFileRangeAndAPossiblyIncompleteRun()
    {
        var time = new DateTime(2026, 9, 26, 13, 20, 0, DateTimeKind.Utc);
        LogRunFile Part(int i) => new($"error.{i}.log", i, time);

        var single = new LogRun(new[] { new LogRunFile("error.log", 0, time) }, null, false);
        var parts = new LogRun(new[] { Part(2), Part(1), new LogRunFile("error.log", 0, time) }, null, false);
        var incomplete = new LogRun(new[] { Part(5), Part(4) }, null, true);

        Assert.StartsWith("error.log (", ErrorLogTexts.RunText(single));
        Assert.StartsWith("error.2.log bis error.log (", ErrorLogTexts.RunText(parts));
        Assert.EndsWith(", möglicherweise unvollständig", ErrorLogTexts.RunText(incomplete));
    }

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, "\\{(\\d+)").Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v).ToArray();
}
