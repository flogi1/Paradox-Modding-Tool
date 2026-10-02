using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Prüft den Parser gegen die echten Doku-Logs von Crusader Kings III auf diesem Rechner; ohne die Dateien läuft nichts.</summary>
public class RealCk3LogsTests
{
    private static readonly string Logs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Crusader Kings III",
        "logs");

    [Fact]
    public void Parsed_counts_match_the_real_logs()
    {
        if (!File.Exists(Path.Combine(Logs, "effects.log")))
        {
            return;
        }

        // Nicht leere Blöcke bzw. Tag-Zeilen in den Logs vom 2026-09-30 (Spielversion 1.20.0.2); eine Abweichung ist ein Parser-Befund.
        int Count(ScriptDocKind kind) => ScriptDocLogParser.Parse(kind, File.ReadAllText(Path.Combine(Logs, ScriptDocLogFiles.FileNameOf(kind)))).Count;

        Assert.Equal(2127, Count(ScriptDocKind.Effect));
        Assert.Equal(1935, Count(ScriptDocKind.Trigger));
        Assert.Equal(328, Count(ScriptDocKind.EventTarget));
        Assert.Equal(761, Count(ScriptDocKind.Modifier));
        Assert.NotNull(ScriptDocImporter.Import(new ScriptDocDatabase(), Logs, DateTime.UtcNow));
    }
}
