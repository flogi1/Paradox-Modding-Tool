using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class ScriptDifferTests
{
    [Fact]
    public void Diff_IdentischerInhaltAndersFormatiert_ErgibtKeineAenderung()
    {
        var left = ScriptDocument.Parse("idea = {\n    category = adm\n    cost = 100\n}\n");
        var right = ScriptDocument.Parse("idea={category=adm cost=100}\n");

        var diff = ScriptDiffer.Diff(left, right);

        Assert.Single(diff);
        Assert.Equal(DiffKind.Unchanged, diff[0].Kind);
        Assert.Null(diff[0].Children);
    }

    [Fact]
    public void Diff_GeaenderterVerschachtelterWert_LiefertRekursivenKindDiff()
    {
        var left = ScriptDocument.Parse("idea = {\n cost = 100\n unrelated = 5\n}\n");
        var right = ScriptDocument.Parse("idea = {\n cost = 150\n unrelated = 5\n}\n");

        var diff = ScriptDiffer.Diff(left, right);

        Assert.Single(diff);
        var ideaDiff = diff[0];
        Assert.Equal(DiffKind.Modified, ideaDiff.Kind);
        Assert.NotNull(ideaDiff.Children);
        Assert.Equal(2, ideaDiff.Children!.Count);

        var costDiff = ideaDiff.Children[0];
        Assert.Equal(DiffKind.Modified, costDiff.Kind);
        Assert.Null(costDiff.Children);
        Assert.Equal("100", Assert.IsType<ScriptScalar>(((ScriptStatement)costDiff.Left!).Value).Text);
        Assert.Equal("150", Assert.IsType<ScriptScalar>(((ScriptStatement)costDiff.Right!).Value).Text);

        var unrelatedDiff = ideaDiff.Children[1];
        Assert.Equal(DiffKind.Unchanged, unrelatedDiff.Kind);
    }

    [Fact]
    public void Diff_HinzugefuegteUndEntfernteSchluessel_WerdenKorrektErkannt()
    {
        var left = ScriptDocument.Parse("a = 1\nb = 2\n");
        var right = ScriptDocument.Parse("a = 1\nc = 3\n");

        var diff = ScriptDiffer.Diff(left, right);

        Assert.Equal(3, diff.Count);
        Assert.Equal(DiffKind.Unchanged, diff[0].Kind);
        Assert.Equal(DiffKind.Removed, diff[1].Kind);
        Assert.Equal("b", ((ScriptStatement)diff[1].Left!).Key);
        Assert.Equal(DiffKind.Added, diff[2].Kind);
        Assert.Equal("c", ((ScriptStatement)diff[2].Right!).Key);
    }

    [Fact]
    public void Diff_GleicheListenblockInhalte_ErgibtKeineAenderung()
    {
        var left = ScriptDocument.Parse("tags = { alpha beta gamma }\n");
        var right = ScriptDocument.Parse("tags = {\n    alpha\n    beta\n    gamma\n}\n");

        var diff = ScriptDiffer.Diff(left, right);

        Assert.Single(diff);
        Assert.Equal(DiffKind.Unchanged, diff[0].Kind);
    }
}
