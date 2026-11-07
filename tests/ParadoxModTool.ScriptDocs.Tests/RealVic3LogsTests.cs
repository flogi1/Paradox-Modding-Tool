using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Prüft den Parser gegen die echten Doku-Logs von Victoria 3 auf diesem Rechner; ohne die Dateien läuft nichts.</summary>
public class RealVic3LogsTests
{
    private static readonly string Docs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Victoria 3",
        "docs");

    [Fact]
    public void All_six_logs_give_entries()
    {
        if (!File.Exists(Path.Combine(Docs, "modifiers.log")))
        {
            return;
        }

        int Count(ScriptDocKind kind) => ScriptDocLogParser.Parse(kind, File.ReadAllText(Path.Combine(Docs, ScriptDocLogFiles.FileNameOf(kind)))).Count;

        // Logs vom 2026-10-10: 7090 Modifier in drei Abschnitten, vom 2026-11-07: 7293. Ein Spielpatch ändert die Zahl, darum nur die Untergrenze
        // (sie fällt unter 7000, wenn ein Abschnitt nicht mehr gelesen wird).
        Assert.True(Count(ScriptDocKind.Modifier) >= 7000);
        Assert.True(Count(ScriptDocKind.Trigger) > 0);
        Assert.True(Count(ScriptDocKind.Effect) > 0);
        Assert.True(Count(ScriptDocKind.EventTarget) > 0);
        Assert.True(Count(ScriptDocKind.OnAction) > 0);
        Assert.True(Count(ScriptDocKind.CustomLocalization) > 0);
        Assert.NotNull(ScriptDocImporter.Import(new ScriptDocDatabase(), Docs, DateTime.UtcNow));
    }
}
