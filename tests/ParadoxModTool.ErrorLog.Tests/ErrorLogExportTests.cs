using Xunit;
using static ParadoxModTool.ErrorLog.Tests.ErrorLogSessionTests;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogExportTests
{
    private static readonly DateTime FutureRun = DateTime.UtcNow.AddDays(1);

    private const string UnknownEffect = "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2";
    private const string UnknownEffectOtherLine = "[19:49:45][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:3";
    private const string Quoted = "[10:00:00][x.cpp:1]: Wert \"a;b\" unbekannt";

    [Fact]
    public void CsvHasOneRowPerEntryAndQuotesSpecialFields()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, UnknownEffectOtherLine, Quoted), FutureRun);

        var csv = ErrorLogExport.Write(session.Sections(ErrorFilter.All), ExportFormat.Csv, "de", "error.log", withStatus: false);
        var rows = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(ErrorLogTexts.ExportColumns, rows[0]);
        Assert.Equal(4, rows.Length);
        Assert.StartsWith("Eigene Mod: Own;Own;EFF-001;Unbekannter Effekt;Fehler;;common/scripted_effects/purger.txt;2;cmf_suppress;Unknown effect cmf_suppress", rows[1]);
        Assert.EndsWith(";\"Wert \"\"a;b\"\" unbekannt\"", rows[3]);
    }

    [Fact]
    public void TextListsSectionsGroupsAndLocations()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, UnknownEffectOtherLine), FutureRun);
        session.LoadComparison("error.1.log", Lines(UnknownEffect));

        var text = ErrorLogExport.Write(session.Sections(ErrorFilter.All), ExportFormat.Text, "de", "error.log", withStatus: true);

        Assert.StartsWith("Fehlerprotokoll error.log\r\n", text);
        Assert.Contains("\r\nEigene Mod: Own (2)\r\n", text);
        Assert.Contains("  ⛔ Unbekannter Effekt (EFF-001): cmf_suppress  2×  1 → 2\r\n", text);
        Assert.Contains("    common/scripted_effects/purger.txt:3  (Log-Zeile 2)\r\n", text);
    }

    [Fact]
    public void TextNamesTheLogFileForRunsWithSeveralFiles()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, UnknownEffectOtherLine), FutureRun);

        var text = ErrorLogExport.Write(session.Sections(ErrorFilter.All), ExportFormat.Text, "de", "error.log", withStatus: false, runFiles: new[] { "error.1.log", "error.log" });

        Assert.Contains("    common/scripted_effects/purger.txt:3  (error.1.log, Zeile 2)\r\n", text);
    }
}
