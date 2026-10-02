using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncComparisonTests
{
    private const string TestProfile = """
        {
          "objects": [ "*/database/{id}" ],
          "nodeDepth": 4,
          "sizeCapBytes": 200,
          "names": { "countries/database/{id}": { "tagList": "countries/tags" } },
          "labelFields": [ "name", "subunit_name_2" ]
        }
        """;

    private static DesyncProfile Profile() => DesyncProfile.Parse(TestProfile);

    /// <summary>Host (Maschine 1) und Client (Maschine 2) als Zip-Dumps mit den angegebenen Spielständen.</summary>
    private static (OosDump Reference, OosDump Desynced) Pair(TempTree tree, string referenceSave, string desyncedSave)
    {
        var hostZip = tree.PathOf("11c0de175691b2bb_0.zip");
        var clientZip = tree.PathOf("11c0de175691b2bb_0 client.zip");
        Dumps.Zip(hostZip, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0, (DumpFileKind.OosSave, referenceSave));
        Dumps.Zip(clientZip, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0, (DumpFileKind.OosSave, desyncedSave));
        return (OosDump.Open(hostZip), OosDump.Open(clientZip));
    }

    private static DesyncDiffResult Run(string referenceSave, string desyncedSave, IProgress<DesyncProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        using var tree = new TempTree();
        var (reference, desynced) = Pair(tree, referenceSave, desyncedSave);
        using (reference)
        using (desynced)
        {
            return DesyncComparison.Run(reference, desynced, Profile(), progress, cancellation);
        }
    }

    private const string CountriesTemplate = """
        random_count=5
        countries={
        	tags={ 1384=BLG 1385=FRA }
        	database={
        		1384={ name="Bulgaria" government={ parliament_issue_support=SUPPORT } }
        		1385={ name="France" gold=6 }
        	}
        }
        """;

    [Fact]
    public void A_run_reports_changed_units_with_fields_labels_and_name_tables()
    {
        var progress = new RecordingProgress();

        var result = Run(CountriesTemplate.Replace("SUPPORT", "0.47488"), CountriesTemplate.Replace("SUPPORT", "0.2453"), progress);

        Assert.Equal(DesyncComparison.FormatVersion, result.FormatVersion);
        Assert.Equal("11c0de175691b2bb", result.OosId16);
        Assert.Equal(1, result.ReferenceMachine);
        Assert.Equal(2, result.DesyncedMachine);
        Assert.Equal(SaveReadState.Complete, result.ReferenceState);
        Assert.Equal(SaveReadState.Complete, result.DesyncedState);

        var unit = Assert.Single(result.Units);
        Assert.Equal("countries/database/1384", unit.Path);
        Assert.Equal(UnitChange.Changed, unit.Change);
        Assert.Equal("Bulgaria", unit.ReferenceLabel);
        Assert.Equal("Bulgaria", unit.DesyncedLabel);
        Assert.True(unit.ReferenceSize > 0);
        Assert.Equal(unit.ReferenceSize - 1, unit.DesyncedSize);
        var field = Assert.Single(unit.Fields);
        Assert.Equal("government/parliament_issue_support", field.Path);
        Assert.Equal("0.47488", field.ReferenceValue);
        Assert.Equal("0.2453", field.DesyncedValue);
        Assert.False(unit.FieldsTruncated);
        Assert.False(unit.LineFallback);

        Assert.Equal("BLG", result.Names.Tables["countries/database/{id}"]["1384"]);
        Assert.Equal("FRA", result.Names.Tables["countries/database/{id}"]["1385"]);
        Assert.True(result.Duration > TimeSpan.Zero);

        var reports = progress.Reports;
        Assert.All(reports, report => Assert.InRange(report.Fraction, 0.0, 1.0));
        foreach (var phase in Enum.GetValues<DesyncPhase>())
        {
            Assert.Contains(reports, report => report.Phase == phase && report.Fraction == 1.0);
        }
    }

    [Fact]
    public void Slot_reused_units_carry_both_labels_and_generations_but_no_fields()
    {
        // 0x26000b55 und 0x28000b55: gleicher Slot 0xb55, Generation 0x26 bzw. 0x28.
        var result = Run(
            """subunit_manager={ database={ 637537109={ subunit_name_2="arran" strength=1 } 5={ subunit_name_2="x" } } }""",
            """subunit_manager={ database={ 671091541={ subunit_name_2="suffolk_province" strength=0.5 } 5={ subunit_name_2="x" } } }""");

        var unit = Assert.Single(result.Units);
        Assert.Equal(UnitChange.SlotReused, unit.Change);
        Assert.Equal("arran", unit.ReferenceLabel);
        Assert.Equal("suffolk_province", unit.DesyncedLabel);
        Assert.Equal(0x26, unit.ReferenceGeneration);
        Assert.Equal(0x28, unit.DesyncedGeneration);
        Assert.Empty(unit.Fields);
        Assert.False(unit.FieldsTruncated);
    }

    [Fact]
    public void Units_on_one_side_carry_their_label_and_scalar_units_compare_as_values()
    {
        var result = Run(
            """construction_manager={ database={ 3003121664=none 7={ name="a" } } }""",
            """construction_manager={ database={ 3003121664={ progress=0.5 } 8={ name="b" } } }""");

        var scalar = Assert.Single(result.Units, u => u.Path == "construction_manager/database/3003121664");
        Assert.Equal(UnitChange.Changed, scalar.Change);
        Assert.Null(scalar.ReferenceLabel);
        var field = Assert.Single(scalar.Fields);
        Assert.Equal("none", field.ReferenceValue);
        Assert.NotNull(field.DesyncedValue);

        var onlyReference = Assert.Single(result.Units, u => u.Change == UnitChange.OnlyReference);
        Assert.Equal("construction_manager/database/7", onlyReference.Path);
        Assert.Equal("a", onlyReference.ReferenceLabel);
        Assert.Equal(0, onlyReference.DesyncedSize);
        var onlyDesynced = Assert.Single(result.Units, u => u.Change == UnitChange.OnlyDesynced);
        Assert.Equal("b", onlyDesynced.DesyncedLabel);
        Assert.Empty(onlyDesynced.Fields);
    }

    [Fact]
    public void Blocks_that_are_not_objects_get_no_label_but_keep_their_fields()
    {
        var result = Run(
            "a={ x=1 }\nb={ name=\"x\" y=1 }\nc={ name=\"z\" v=1 }\n",
            "a={ x=1 }\nc={ name=\"z\" v=2 }\n");

        var onlyReference = Assert.Single(result.Units, u => u.Path == "b");
        Assert.Equal(UnitChange.OnlyReference, onlyReference.Change);
        Assert.Null(onlyReference.ReferenceLabel);
        var changed = Assert.Single(result.Units, u => u.Path == "c");
        Assert.Equal(UnitChange.Changed, changed.Change);
        Assert.Null(changed.ReferenceLabel);
        Assert.Null(changed.DesyncedLabel);
        Assert.Equal("v", Assert.Single(changed.Fields).Path);
    }

    [Fact]
    public void A_one_sided_block_over_the_size_cap_stays_an_entry_without_text()
    {
        var filler = string.Join(' ', Enumerable.Range(0, 60).Select(i => "k" + i + "=1"));

        var result = Run($"a={{ x=1 }}\nbig={{ name=\"x\" {filler} }}\n", "a={ x=1 }\n");

        var unit = Assert.Single(result.Units);
        Assert.Equal("big", unit.Path);
        Assert.Equal(UnitChange.OnlyReference, unit.Change);
        Assert.True(unit.ReferenceSize > 200);
        Assert.Null(unit.ReferenceLabel);
        Assert.Empty(unit.Fields);
    }

    [Fact]
    public void Own_scalars_of_the_top_level_are_diffed_under_the_empty_path()
    {
        var result = Run("random_seed=1\nrandom_count=5\nwar_manager={ x=1 }\n", "random_seed=1\nrandom_count=6\nwar_manager={ x=1 }\n");

        var unit = Assert.Single(result.Units);
        Assert.Equal("", unit.Path);
        Assert.Equal(UnitChange.OwnScalarsChanged, unit.Change);
        var field = Assert.Single(unit.Fields);
        Assert.Equal("random_count", field.Path);
        Assert.Equal("5", field.ReferenceValue);
        Assert.Equal("6", field.DesyncedValue);
    }

    [Fact]
    public void Own_scalar_changes_without_a_field_difference_are_dropped()
    {
        // Kommentare zählen im Eigen-Hash, der Feldvergleich überspringt sie.
        var result = Run("a=1 #x\nwar_manager={ x=1 }\n", "a=1 #y\nwar_manager={ x=1 }\n");

        Assert.Empty(result.Units);
    }

    [Fact]
    public void Own_text_over_the_size_cap_is_not_field_diffed()
    {
        var padding = new string('p', 250);
        var result = Run($"big=\"{padding}1\"\nwar_manager={{ x=1 }}\n", $"big=\"{padding}2\"\nwar_manager={{ x=1 }}\n");

        var unit = Assert.Single(result.Units);
        Assert.Equal(UnitChange.OwnScalarsChanged, unit.Change);
        Assert.True(unit.FieldsTruncated);
        Assert.Empty(unit.Fields);
        Assert.False(unit.LineFallback);
    }

    [Fact]
    public void A_too_large_leaf_block_is_reported_without_fields()
    {
        var reference = "war_manager={ list={ " + string.Join(' ', Enumerable.Range(0, 80)) + " } }";
        var desynced = "war_manager={ list={ " + string.Join(' ', Enumerable.Range(1, 80)) + " } }";

        var result = Run(reference, desynced);

        var unit = Assert.Single(result.Units);
        Assert.Equal(UnitChange.TooLarge, unit.Change);
        Assert.Equal("war_manager/list", unit.Path);
        Assert.Empty(unit.Fields);
        Assert.True(unit.ReferenceSize > 200);
    }

    [Fact]
    public void An_incomplete_desynced_save_is_reported_in_the_result_state()
    {
        var complete = CountriesTemplate.Replace("SUPPORT", "0.47488");
        var cut = complete.TrimEnd()[..^1].TrimEnd()[..^1];

        var result = Run(complete, cut);

        Assert.Equal(SaveReadState.Complete, result.ReferenceState);
        Assert.Equal(SaveReadState.Incomplete, result.DesyncedState);
        Assert.Empty(result.Units);
        Assert.Null(result.ReferenceStoppedAt);
        Assert.Equal(Encoding.UTF8.GetByteCount(cut), result.DesyncedStoppedAt);
    }

    [Fact]
    public void A_binary_save_gives_no_units_and_state_binary()
    {
        var binary = "SAV01030000\n" + "\u0001\u0002binary" + new string('x', 64);

        var result = Run(binary, binary);

        Assert.Equal(SaveReadState.Binary, result.ReferenceState);
        Assert.Equal(SaveReadState.Binary, result.DesyncedState);
        Assert.Empty(result.Units);
        Assert.Null(result.ReferenceStoppedAt);
        Assert.Null(result.DesyncedStoppedAt);
    }

    [Fact]
    public void A_binary_save_on_one_side_gives_no_units_and_keeps_the_other_side_complete()
    {
        var binary = "SAV01030000\n" + "\u0001\u0002binary" + new string('x', 64);

        var result = Run(CountriesTemplate.Replace("SUPPORT", "1"), binary);

        Assert.Equal(SaveReadState.Complete, result.ReferenceState);
        Assert.Equal(SaveReadState.Binary, result.DesyncedState);
        Assert.Empty(result.Units);
        Assert.Equal("BLG", result.Names.Tables["countries/database/{id}"]["1384"]);
    }

    [Fact]
    public void The_reference_is_indexed_before_the_desynced_save_and_cancel_stops_the_run()
    {
        var progress = new RecordingProgress();
        var result = Run(CountriesTemplate.Replace("SUPPORT", "1"), CountriesTemplate.Replace("SUPPORT", "2"), progress);
        Assert.Single(result.Units);
        var phases = progress.Reports.Select(report => report.Phase).ToList();
        Assert.True(phases.LastIndexOf(DesyncPhase.IndexReference) < phases.IndexOf(DesyncPhase.IndexDesynced));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            Run(CountriesTemplate.Replace("SUPPORT", "1"), CountriesTemplate.Replace("SUPPORT", "2"), null, cancelled.Token));

        // Abbruch bei der ersten Meldung des Referenz-Index: die abweichende Seite wird gar nicht mehr gelesen.
        using var duringIndex = new CancellationTokenSource();
        var seen = new RecordingProgress();
        var cancelOnFirstReport = new CallbackProgress(report =>
        {
            seen.Report(report);
            duringIndex.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() =>
            Run(CountriesTemplate.Replace("SUPPORT", "1"), CountriesTemplate.Replace("SUPPORT", "2"), cancelOnFirstReport, duringIndex.Token));
        Assert.All(seen.Reports, report => Assert.Equal(DesyncPhase.IndexReference, report.Phase));
    }

    /// <summary>Synchrone Fortschrittsaufzeichnung (aus beliebigen Threads).</summary>
    private sealed class RecordingProgress : IProgress<DesyncProgress>
    {
        private readonly List<DesyncProgress> _reports = new();

        public IReadOnlyList<DesyncProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return _reports.ToList();
                }
            }
        }

        public void Report(DesyncProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }

    private sealed class CallbackProgress(Action<DesyncProgress> callback) : IProgress<DesyncProgress>
    {
        public void Report(DesyncProgress value) => callback(value);
    }
}
