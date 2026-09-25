using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ResultDocumentTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types";
    private const string DocFile = Buildings + "/b.txt";

    /// <summary>
    /// farm: cost Konflikt (A 2, B 3), time aus B, size aus A. mine: x aus A, y aus B (Aktionen gibt es nur, wo beide Mods beitragen).
    /// Mehrzeilig, damit die Zeilen einzeln ausgerichtet werden.
    /// </summary>
    private CompatchSession Session()
    {
        PutVanilla(Buildings + "/00_x.txt", "farm = {\n\tcost = 1\n\ttime = 1\n\tsize = 1\n}\nmine = {\n\tx = 1\n}\n");
        PutA(Buildings + "/a.txt", "REPLACE:farm = {\n\tcost = 2\n\ttime = 1\n\tsize = 5\n}\nREPLACE:mine = {\n\tx = 2\n}\n");
        PutB(Buildings + "/b.txt", "REPLACE:farm = {\n\tcost = 3\n\ttime = 2\n\tsize = 1\n}\nREPLACE:mine = {\n\tx = 1\n\ty = 3\n}\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    [Fact]
    public void Files_are_the_winning_files()
    {
        var session = Session();

        var file = Assert.Single(ResultDocument.Files(session.Actions));
        Assert.Equal(DocFile, file);
    }

    [Fact]
    public void Build_lists_addresses_with_header_choice_rows_and_colored_code()
    {
        var session = Session();
        var conflict = session.Actions.Single(a => a.Origin is null);

        var lines = ResultDocument.Build(session.Run, DocFile);

        var farmHeader = lines.ToList().FindIndex(l => l.Kind == DocLineKind.Header && l.Text == "farm");
        var mineHeader = lines.ToList().FindIndex(l => l.Kind == DocLineKind.Header && l.Text == "mine");
        Assert.True(farmHeader >= 0 && mineHeader > farmHeader);
        Assert.Equal(DocLineKind.Choice, lines[farmHeader + 1].Kind);
        Assert.Equal(conflict.Id, lines[farmHeader + 1].ActionId);
        Assert.Contains(lines, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "size = 5" && l.Origin == RowOrigin.FromA);
        Assert.Contains(lines, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "time = 2" && l.Origin == RowOrigin.FromB);
        Assert.Contains(lines, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "x = 2" && l.Address.Key == "mine");
        Assert.All(lines.Where(l => l.Kind == DocLineKind.Code), l => Assert.False(string.IsNullOrEmpty(l.ActionId)));
    }

    [Fact]
    public void Build_follows_decisions()
    {
        var session = Session();
        var conflict = session.Actions.Single(a => a.Origin is null);

        session.Decide(conflict.Id, new MergeDecision(DecisionKind.TakeA));
        var takeA = ResultDocument.Build(session.Run, DocFile);
        session.Decide(conflict.Id, new MergeDecision(DecisionKind.TakeB));
        var takeB = ResultDocument.Build(session.Run, DocFile);

        Assert.Contains(takeA, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "cost = 2");
        Assert.Contains(takeB, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "cost = 3");
    }

    [Fact]
    public void Line_of_points_to_choice_row_or_address_header()
    {
        var session = Session();
        var lines = ResultDocument.Build(session.Run, DocFile);
        var conflict = session.Actions.Single(a => a.Origin is null);
        var mine = session.Actions.First(a => a.Address.Key == "mine");

        Assert.Equal(DocLineKind.Choice, lines[ResultDocument.LineOf(lines, conflict)].Kind);
        var mineLine = lines[ResultDocument.LineOf(lines, mine)];
        Assert.Equal(DocLineKind.Header, mineLine.Kind);
        Assert.Equal("mine", mineLine.Text);
    }

    [Fact]
    public void Build_returns_only_the_requested_scope_and_nothing_for_unknown_scopes()
    {
        var session = Session();
        var foreign = new MergeAction
        {
            Id = "anders|x::y",
            Address = EntryAddress.ForKey("anders", "x"),
            Path = new[] { "y" },
            DisplayPath = "x › y",
            Stage = MergeStage.Conflict,
            DefaultDecision = DecisionKind.TakeA,
        };

        Assert.Empty(ResultDocument.Build(session.Run, "gibt/es/nicht"));
        Assert.All(ResultDocument.Build(session.Run, DocFile), l => Assert.Equal(Buildings, l.Address.Scope));
        Assert.Equal(-1, ResultDocument.LineOf(ResultDocument.Build(session.Run, DocFile), foreign));
    }

    [Fact]
    public void Build_can_hide_comment_lines()
    {
        PutVanilla(Buildings + "/00_x.txt", "farm = {\n\tcost = 1\n}\n");
        PutA(Buildings + "/a.txt", "REPLACE:farm = {\n\tcost = 2\n}\n");
        PutB(Buildings + "/b.txt", "# alter Stand\n# REPLACE:farm = {\n#\tcost = 9\n# }\nREPLACE:farm = {\n\t# cost = 7\n\tcost = 3\n\ttime = 2\n}\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        var file = Assert.Single(ResultDocument.Files(session.Actions));

        var hidden = ResultDocument.Build(session.Run, file, hideComments: true);

        Assert.DoesNotContain(hidden, l => l.Kind == DocLineKind.Code && l.Text.TrimStart().StartsWith('#'));
        Assert.Contains(hidden, l => l.Kind == DocLineKind.Code && l.Text.Trim() == "time = 2");
    }

    [Fact]
    public void Whole_file_shows_the_chosen_side()
    {
        PutVanilla("in_game/gfx/x.txt", "v = 1\n");
        PutA("in_game/gfx/x.txt", "a = 1\n");
        PutB("in_game/gfx/x.txt", "b = 1\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();

        var lines = ResultDocument.Build(session.Run, "in_game/gfx/x.txt");

        Assert.Equal(DocLineKind.Header, lines[0].Kind);
        Assert.Equal(CompatchTexts.DocWholeFile, lines[0].Text);
        Assert.Contains(lines, l => l.Kind == DocLineKind.WholeFile);
    }
}
