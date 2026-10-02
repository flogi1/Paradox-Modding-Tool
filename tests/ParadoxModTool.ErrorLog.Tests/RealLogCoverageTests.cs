using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.ErrorLog.Tests;

public class RealLogCoverageTests
{
    private readonly ITestOutputHelper _output;

    public RealLogCoverageTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void LessThanOnePercentOfTheUsersLogLinesStayUnmatched()
    {
        if (!Directory.Exists(Catalogs.UserLogDirectory))
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
