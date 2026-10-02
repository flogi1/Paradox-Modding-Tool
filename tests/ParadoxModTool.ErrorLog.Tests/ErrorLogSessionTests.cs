using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogSessionTests
{
    private static readonly DateTime RunTime = DateTime.UtcNow.AddDays(1);

    private const string UnknownEffect = "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2";
    private const string UnknownEffectOtherLine = "[19:49:45][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:3";
    private const string OwnVariable = "[19:49:53][jomini_effect.cpp:1162]: Variable 'iv2_x' is set but is never used. Note that use in localization doesn't count";
    private const string VanillaGovernment = "[16:59:33][government.cpp:3751]: Removing invalid bureaucracy 'themata_bureaucracy' for 'TRE Trebizond' 40 at game start - might want to look into this";
    private const string Engine = "[17:00:05][game_gui_types.cpp:1540]: couldn't convert parameter value to something we recognise";
    private const string Unmatched = "[10:00:00][x.cpp:1]: Error building from string ( TYPE = NCoat )";

    [Fact]
    public void LoadRunReadsAllPartsAndNamesTheFiles()
    {
        using var logs = new TempTree(("logs/error.log", UnknownEffect + "\n"), ("logs/error.1.log", UnknownEffectOtherLine + "\n"));
        var run = new LogRun(new[]
        {
            new LogRunFile(logs.PathOf("logs/error.1.log"), 1, DateTime.UtcNow),
            new LogRunFile(logs.PathOf("logs/error.log"), 0, DateTime.UtcNow),
        }, null, false);
        using var tree = OwnModTree();
        var session = NewSession(tree);

        session.LoadRun(run);

        Assert.Equal(2, session.Entries.Count);
        Assert.Equal(new[] { 1, 0 }, session.Entries.Select(e => e.Line.Part).OrderDescending());
        Assert.Equal(new[] { "error.1.log", "error.log" }, session.RunFileNames);
        Assert.Equal(logs.PathOf("logs/error.log"), session.RunPath);

        session.ClearRun();
        Assert.Empty(session.RunFileNames);
    }

    [Fact]
    public void AfterManySwitchesFileNamesCountFromTheNewestPart()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", LogReader.Parse(new[] { UnknownEffect }), DateTime.UtcNow.AddDays(1));
        for (var part = 1; part <= 6; part++)
        {
            session.AppendLines(LogReader.Parse(new[] { UnknownEffect }, part: part));
            session.RenameRunFilesAfterSwitch();
        }

        Assert.Equal("error.log", session.RunFileNames[6]);
        Assert.Equal("error.5.log", session.RunFileNames[1]);
        Assert.Equal(string.Empty, session.RunFileNames[0]);
        var oldest = session.Entries.Single(e => e.Line.Part == 0);
        Assert.Equal("common/scripted_effects/purger.txt:2  (Log-Zeile 1)", ErrorLogTexts.LogPosition(oldest, session.RunFileNames));
    }

    [Fact]
    public void SectionsFollowTheAttributionWithOwnModsFirst()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.SetIndex(SymbolIndex.Build(ErrorLogSources.SymbolRoots(session.Mods, session.VanillaRoots)));

        session.LoadRun("error.log", Lines(Engine, VanillaGovernment, UnknownEffect, UnknownEffectOtherLine, Unmatched), RunTime);
        var sections = session.Sections(ErrorFilter.All);

        Assert.Equal(
            new[] { ErrorSectionKind.OwnMod, ErrorSectionKind.Vanilla, ErrorSectionKind.Engine, ErrorSectionKind.Unmatched },
            sections.Select(s => s.Kind));
        var own = sections[0];
        Assert.Equal("Own", own.Mod!.Name);
        var group = Assert.Single(own.Groups);
        Assert.Equal("EFF-001", group.Code);
        Assert.Equal("cmf_suppress", group.GroupName);
        Assert.Equal(2, group.Count);
        Assert.Equal(2, own.Total);
        Assert.Equal(RunStatus.Unchanged, group.Status);
        Assert.Equal(new ErrorCounts(5, 4, 0, 0), ErrorLogSession.CountsOf(sections));
    }

    [Fact]
    public void ComparisonMarksNewUnchangedAndFixedGroups()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, Engine), RunTime);

        session.LoadComparison("error.1.log", Lines(UnknownEffect, UnknownEffectOtherLine, VanillaGovernment));
        var sections = session.Sections(ErrorFilter.All);

        var own = sections.Single(s => s.Kind == ErrorSectionKind.OwnMod).Groups.Single();
        Assert.Equal(RunStatus.Unchanged, own.Status);
        Assert.Equal(1, own.Count);
        Assert.Equal(2, own.PreviousCount);
        Assert.Equal(RunStatus.New, sections.Single(s => s.Kind == ErrorSectionKind.Engine).Groups.Single().Status);
        var fixedSection = sections.Single(s => s.Kind == ErrorSectionKind.Fixed);
        Assert.Equal(ErrorSectionKind.Fixed, sections[^1].Kind);
        var fixedGroup = Assert.Single(fixedSection.Groups);
        Assert.Equal("GOV-001", fixedGroup.Code);
        Assert.Equal(0, fixedGroup.Count);
        Assert.Equal(1, fixedSection.Total);
        Assert.Equal(new ErrorCounts(2, 2, 1, 1), ErrorLogSession.CountsOf(sections));
    }

    [Fact]
    public void FiltersBySeverityStatusCodeSearchAndOwnMods()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, VanillaGovernment, Engine), RunTime);
        session.LoadComparison("error.1.log", Lines(Engine));

        Assert.Equal(new[] { "EFF-001" }, Codes(session.Sections(ErrorFilter.All with { Warnings = false })));
        Assert.Equal(new[] { "PRM-001" }, Codes(session.Sections(ErrorFilter.All with { New = false })));
        Assert.Equal(new[] { "GOV-001" }, Codes(session.Sections(ErrorFilter.All with { Code = "GOV-001" })));
        Assert.Equal(new[] { "EFF-001" }, Codes(session.Sections(ErrorFilter.All with { OwnModsOnly = true })));
        Assert.Equal(new[] { "EFF-001" }, Codes(session.Sections(ErrorFilter.All with { Search = "PURGER" })));
        Assert.Equal(new[] { "EFF-001" }, Codes(session.Sections(ErrorFilter.All with { Search = "unbekannter effekt" })));
        Assert.Empty(session.Sections(ErrorFilter.All with { Search = "gibt es nicht" }));
    }

    [Fact]
    public void NameOnlyErrorsMoveToTheirModOnceTheIndexIsReady()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(OwnVariable), RunTime);
        Assert.Equal(ErrorSectionKind.Unassigned, session.Sections(ErrorFilter.All).Single().Kind);
        Assert.False(session.IndexReady);

        session.SetIndex(SymbolIndex.Build(ErrorLogSources.SymbolRoots(session.Mods, session.VanillaRoots)));

        Assert.True(session.IndexReady);
        Assert.Equal(ErrorSectionKind.OwnMod, session.Sections(ErrorFilter.All).Single().Kind);
    }

    [Fact]
    public void SetSourcesDropsTheOldIndex()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.SetIndex(SymbolIndex.Build(ErrorLogSources.SymbolRoots(session.Mods, session.VanillaRoots)));
        Assert.NotEmpty(session.Index.Find("iv2_x"));

        session.SetSources(session.Mods, session.VanillaRoots);

        Assert.False(session.IndexReady);
        Assert.Empty(session.Index.Find("iv2_x"));
    }

    [Fact]
    public void ChangedFiresForEveryUpdateAndClearRunEmptiesTheList()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.LoadRun("error.log", Lines(UnknownEffect), RunTime);
        session.LoadComparison(null);
        session.ClearRun();

        Assert.Equal(3, changes);
        Assert.Null(session.RunPath);
        Assert.Empty(session.Sections(ErrorFilter.All));
    }

    [Fact]
    public void LoadRunReadsTheFileAndTakesItsWriteTimeAsRunTime()
    {
        using var tree = new TempTree(("logs/error.log", UnknownEffect + "\n" + Engine + "\n"));
        var session = new ErrorLogSession(Catalogs.Eu5, new SuppressionSet(), null, "de");
        var path = tree.PathOf("logs/error.log");

        session.LoadRun(path);

        Assert.Equal(path, session.RunPath);
        Assert.Equal(File.GetLastWriteTimeUtc(path), session.RunTimeUtc);
        Assert.Equal(2, session.Entries.Count);
    }

    [Fact]
    public void NameSeveritiesTakeTheHighestSeverityOfVisibleVariableAndFlagMessages()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun(
            "error.log",
            Lines(
                "[10:00:00][e.cpp:1]: Variable 'a' is set but is never used.",
                "[10:00:01][e.cpp:1]: Variable 'a' is used but is never set.",
                "[10:00:02][e.cpp:1]: Flag 'f' is set but is never used.",
                "[10:00:03][e.cpp:1]: Unknown effect u at common/scripted_effects/purger.txt:2",
                "[10:00:04][e.cpp:1]: Variable 's' is set but is never used."),
            RunTime);
        session.Suppress(new Suppression(SuppressionScope.CodeAndName, "VAR-001", "s"));

        var severities = session.NameSeverities();

        Assert.Equal(new Dictionary<string, ErrorSeverity> { ["a"] = ErrorSeverity.Error, ["f"] = ErrorSeverity.Warning }, severities);
    }

    internal static TempTree OwnModTree() => new(
        ("own/in_game/common/scripted_effects/purger.txt", "purger = {\n\tcmf_suppress = { v = iv2_x }\n\tcmf_suppress = { v = iv2_x }\n\tset_variable = { name = iv2_x }\n}\n"),
        ("install/game/in_game/common/governments/g.txt", "themata_bureaucracy = { }\n"));

    internal static ErrorLogSession NewSession(TempTree tree, string? suppressionsPath = null)
    {
        var session = new ErrorLogSession(Catalogs.Eu5, new SuppressionSet(), suppressionsPath, "de");
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);
        session.SetSources(new[] { own }, SourceResolver.VanillaRootsFor(tree.PathOf("install")).Where(Directory.Exists).ToList());
        return session;
    }

    internal static List<LogLine> Lines(params string[] lines) => LogReader.Parse(lines);

    private static string[] Codes(IReadOnlyList<ErrorSection> sections) =>
        sections.Where(s => s.Kind is not (ErrorSectionKind.Fixed or ErrorSectionKind.Suppressed)).SelectMany(s => s.Groups).Select(g => g.Code).ToArray();
}
