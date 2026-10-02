using ParadoxModTool.ErrorLog;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DumpLeadAttributionTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pmt-dump-leads");

    private static string Folder(string mod) => Path.Combine(Root, mod);

    private static string FileIn(string mod, string relative) =>
        Path.Combine(Folder(mod), relative.Replace('/', Path.DirectorySeparatorChar));

    private static ErrorEntry Error(string? filePath) => new()
    {
        Message = "x",
        Line = new LogLine("00:00:00", "jomini_script_system.cpp", 1, "x", 1),
        FilePath = filePath,
        FileLine = filePath is null ? null : 7,
    };

    private static Func<string, bool> Exists(params string[] paths)
    {
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    private static MachineSummary Machine(int id, params string[] mods) =>
        new(id, "Host", "Alice", "0123456789abcdef", "fedcba9876543210",
            new SaveHeader("1425.2.4.8", "1.0.3", 1, 2, []), mods.Select(m => new DumpMod(m, ModSourceKind.Local, null, m, null)).ToList(), false, false);

    private static OosSummary Summary(params MachineSummary[] machines) =>
        new("11c0de175691b2bb", null, null, null, null, [], machines, []);

    [Fact]
    public void Later_dump_mod_wins()
    {
        var installed = new[]
        {
            new ModSource("Late", Folder("Late"), false, 0),
            new ModSource("Early", Folder("Early"), true, 5),
        };
        var exists = Exists(FileIn("Early", "common/a.txt"), FileIn("Late", "common/a.txt"));

        var attribute = DumpLeadAttribution.For(new[] { "Early", "Late" }, installed, exists);
        var result = attribute(Error("common/a.txt"));

        Assert.Equal(new LeadAttribution("Late", false, FileIn("Late", "common/a.txt")), result);
    }

    [Fact]
    public void Mod_outside_the_local_playset_still_resolves()
    {
        var installed = new[] { new ModSource("Other", Folder("Other"), true, -1) };
        var exists = Exists(FileIn("Other", "events/e.txt"));

        var result = DumpLeadAttribution.For(new[] { "other" }, installed, exists)(Error("events/e.txt"));

        Assert.Equal(new LeadAttribution("Other", true, FileIn("Other", "events/e.txt")), result);
    }

    [Fact]
    public void Dump_mod_that_is_not_installed_is_skipped()
    {
        var installed = new[] { new ModSource("Here", Folder("Here"), false, 0) };
        var exists = Exists(FileIn("Here", "common/a.txt"), FileIn("Gone", "common/a.txt"));

        var result = DumpLeadAttribution.For(new[] { "Here", "Gone" }, installed, exists)(Error("common/a.txt"));

        Assert.Equal(new LeadAttribution("Here", false, FileIn("Here", "common/a.txt")), result);
    }

    [Fact]
    public void Path_in_no_mod_gives_no_attribution()
    {
        var installed = new[] { new ModSource("Here", Folder("Here"), false, 0) };
        var attribute = DumpLeadAttribution.For(new[] { "Here" }, installed, Exists());

        Assert.Equal(new LeadAttribution(null, false, null), attribute(Error("common/missing.txt")));
        Assert.Equal(new LeadAttribution(null, false, null), attribute(Error(null)));
    }

    [Fact]
    public void Installed_mod_not_in_the_dump_does_not_count()
    {
        var installed = new[] { new ModSource("Here", Folder("Here"), false, 0), new ModSource("Local only", Folder("Local only"), false, 1) };
        var exists = Exists(FileIn("Here", "common/a.txt"), FileIn("Local only", "common/a.txt"));

        var result = DumpLeadAttribution.For(new[] { "Here" }, installed, exists)(Error("common/a.txt"));

        Assert.Equal("Here", result.ModName);
    }

    [Fact]
    public void Phase_folders_are_tried()
    {
        var installed = new[] { new ModSource("Mod", Folder("Mod"), false, 0) };
        var exists = Exists(FileIn("Mod", "in_game/common/a.txt"));

        var result = DumpLeadAttribution.For(new[] { "Mod" }, installed, exists)(Error("common/a.txt"));

        Assert.Equal(FileIn("Mod", "in_game/common/a.txt"), result.FullPath);
    }

    [Fact]
    public void Same_name_prefers_the_mod_in_the_playset()
    {
        var installed = new[]
        {
            new ModSource("Mod", Folder("Copy"), false, -1),
            new ModSource("Mod", Folder("Mod"), false, 3),
        };

        var match = DumpLeadAttribution.Installed("mod", installed);

        Assert.Equal(Folder("Mod"), match?.Directory);
        Assert.Null(DumpLeadAttribution.Installed("none", installed));
    }

    [Fact]
    public void Mod_order_follows_the_reference_machine_then_appends_the_rest()
    {
        var summary = Summary(Machine(1, "A", "X", "B"), Machine(2, "B", "C", "a"));

        Assert.Equal(new[] { "B", "C", "a", "X" }, DumpLeadAttribution.ModOrder(summary, referenceMachineId: 2));
        Assert.Equal(new[] { "A", "X", "B", "C" }, DumpLeadAttribution.ModOrder(summary, referenceMachineId: null));
        Assert.Equal(new[] { "A", "X", "B", "C" }, DumpLeadAttribution.ModOrder(summary, referenceMachineId: 9));
    }
}
