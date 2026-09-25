using ParadoxModTool.ModCompat;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ProgressReportTests : CompatchTestBase
{
    private sealed class Collector : IProgress<CompatProgress>
    {
        public List<CompatProgress> Reports { get; } = new();

        public void Report(CompatProgress value) => Reports.Add(value);
    }

    [Fact]
    public void Reading_a_mod_reports_file_counts_up_to_the_total()
    {
        PutVanilla("in_game/common/building_types/00_x.txt", "farm = { cost = 1 }\n");
        for (var i = 0; i < 45; i++)
        {
            PutA($"in_game/common/building_types/a{i}.txt", $"a{i} = {{ x = 1 }}\n");
        }

        PutB("in_game/common/building_types/b.txt", "farm = { cost = 2 }\n");
        var collector = new Collector();

        EffectiveEntryResolver.Resolve(Input(), collector);

        var readA = collector.Reports.Where(r => r.Text == CompatchTexts.ProgressReadMod("A")).ToList();
        // Erst "Ordner wird aufgebaut" (ohne Gesamtzahl), dann Dateizähler.
        Assert.Equal(new[] { (0, 0), (0, 45), (20, 45), (40, 45), (45, 45) }, readA.Select(r => (r.Done, r.Total)));
        Assert.Contains(collector.Reports, r => r.Text == CompatchTexts.ProgressReadMod("B") && r.Done == r.Total && r.Total == 1);
        Assert.Contains(collector.Reports, r => r.Text == CompatchTexts.ProgressReadVanilla);
    }

    [Fact]
    public void Engine_reports_the_compare_phase_with_the_localized_text()
    {
        PutVanilla("in_game/common/building_types/00_x.txt", "farm = { cost = 1 }\n");
        PutA("in_game/common/building_types/a.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB("in_game/common/building_types/b.txt", "REPLACE:farm = { cost = 3 }\n");
        var collector = new Collector();

        CompatchEngine.Run(Input(), PatchTarget.Compatch, null, null, collector);

        Assert.Contains(collector.Reports, r => r.Text == CompatchTexts.ProgressCompare && r.Total == 0);
    }
}
