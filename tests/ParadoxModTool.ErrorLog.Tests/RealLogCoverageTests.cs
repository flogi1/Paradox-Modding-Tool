using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.ErrorLog.Tests;

public class RealLogCoverageTests
{
    private readonly ITestOutputHelper _output;

    public RealLogCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Prüft den Fehlerkatalog gegen die echten error*.log des Nutzers. Das Ergebnis hängt davon ab, was der Nutzer zuletzt gespielt hat (Testmods mit
    /// absichtlichen Fehlern, andere Mods), und ließ den Test-Schritt des Release-Werkzeugs scheitern. Deshalb nur auf Wunsch:
    /// Umgebungsvariable <c>PMT_REAL_ERROR_LOGS=1</c> setzen (wie <c>PMT_OOS_SAMPLES</c> bei den Desync-Tests).
    /// </summary>
    [Fact]
    public void LessThanOnePercentOfTheUsersLogLinesStayUnmatched()
    {
        if (Environment.GetEnvironmentVariable("PMT_REAL_ERROR_LOGS") != "1" || !Directory.Exists(Catalogs.UserLogDirectory))
        {
            return;
        }

        var total = 0;
        var unmatched = new Dictionary<string, int>();
        foreach (var path in Directory.GetFiles(Catalogs.UserLogDirectory, "error*.log"))
        {
            var lines = LogReader.ReadFile(path);
            total += lines.Count;
            foreach (var entry in ErrorClassifier.Classify(lines, Catalogs.Eu5).Where(e => e.Kind is null))
            {
                var key = Regex.Replace(entry.Message, @"\d+", "N");
                key = key.Length > 80 ? key[..80] : key;
                unmatched[key] = unmatched.GetValueOrDefault(key) + entry.Lines.Count;
            }
        }

        var unmatchedCount = unmatched.Values.Sum();
        _output.WriteLine($"{total} Zeilen, {unmatchedCount} nicht zugeordnet");
        foreach (var (message, count) in unmatched.OrderByDescending(p => p.Value).Take(15))
        {
            _output.WriteLine($"{count,6}  {message}");
        }

        if (total > 0)
        {
            Assert.True(unmatchedCount * 100.0 / total < 1.0, $"{unmatchedCount} von {total} Zeilen nicht zugeordnet");
        }
    }
}
