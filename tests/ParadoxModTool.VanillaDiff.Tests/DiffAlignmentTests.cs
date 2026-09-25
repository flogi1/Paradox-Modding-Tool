using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class DiffAlignmentTests
{
    private static Func<int, int> LineOf(string text) =>
        offset => 1 + text.Take(Math.Clamp(offset, 0, text.Length)).Count(c => c == '\n');

    private static IReadOnlyList<AlignmentGap> Gaps(string left, string right)
    {
        var diffs = ScriptDiffer.Diff(ScriptDocument.Parse(left), ScriptDocument.Parse(right));
        return DiffAlignment.Compute(diffs, LineOf(left), LineOf(right));
    }

    [Fact]
    public void IdentischeDateien_HabenKeineLuecken()
    {
        Assert.Empty(Gaps("a = 1\nb = {\n x = 2\n}\n", "a = 1\nb = {\n x = 2\n}\n"));
    }

    [Fact]
    public void NurRechtsVorhandenerBlock_ErzeugtLuckeLinks()
    {
        var gaps = Gaps("a = 1\nc = 3\n", "a = 1\nb = {\n x = 1\n y = 2\n}\nc = 3\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Left, 2, 4) }, gaps);
    }

    [Fact]
    public void NurLinksVorhandenerBlock_ErzeugtLuckeRechts()
    {
        var gaps = Gaps("a = 1\nb = {\n x = 1\n y = 2\n}\nc = 3\n", "a = 1\nc = 3\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Right, 2, 4) }, gaps);
    }

    [Fact]
    public void ZusaetzlicherBlockInnerhalbEinesGeaendertenBlocks_ErzeugtLuckeVorDerSchlussklammer()
    {
        var left = "idea = {\n cost = 1\n}\n";
        var right = "idea = {\n cost = 1\n extra = {\n  a = 1\n  b = 2\n }\n}\n";

        var gaps = Gaps(left, right);

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Left, 3, 4) }, gaps);
    }

    [Fact]
    public void ZweiHintereinanderHinzugefuegteEintraege_ErgebenEineSummierteLuecke()
    {
        var gaps = Gaps("a = 1\nz = 9\n", "a = 1\nb = 2\nc = 3\nz = 9\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Left, 2, 2) }, gaps);
    }

    [Fact]
    public void GleicherInhaltAndersFormatiert_GleichtDieZeilenAmNaechstenAnkerAus()
    {
        var gaps = Gaps("idea = {\n cost = 100\n}\nnext = 1\n", "idea={cost=100}\nnext = 1\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Right, 2, 2) }, gaps);
    }

    [Fact]
    public void KommentarzeilenZwischenEintraegen_WerdenAmNaechstenAnkerAusgeglichen()
    {
        var gaps = Gaps("a = 1\n# c1\n# c2\nb = 2\n", "a = 1\nb = 2\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Right, 2, 2) }, gaps);
    }

    [Fact]
    public void EinseitigerBlockAmDateiende_ErzeugtKeineLuecke()
    {
        Assert.Empty(Gaps("a = 1\n", "a = 1\nb = {\n x = 1\n}\n"));
        Assert.Empty(Gaps("a = 1\nb = {\n x = 1\n}\n", "a = 1\n"));
    }

    [Fact]
    public void LeereDatei_ErzeugtKeineLuecke()
    {
        Assert.Empty(Gaps(string.Empty, "a = 1\n"));
        Assert.Empty(Gaps("a = 1\n", string.Empty));
        Assert.Empty(Gaps(string.Empty, string.Empty));
    }

    [Fact]
    public void ErsterEintragVersetzt_GleichtVonDerDateiAnfangsZeileAus()
    {
        var gaps = Gaps("# Kopf\n# Kopf\na = 1\nb = 2\n", "a = 1\nb = 2\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Right, 1, 2) }, gaps);
    }

    [Fact]
    public void GeaenderterWertMitMehrZeilenAufEinerSeite_GleichtVorDemNaechstenEintragAus()
    {
        var gaps = Gaps("a = {\n x = 1\n y = 2\n z = 3\n}\nb = 1\n", "a = { x = 9 y = 2 z = 3 }\nb = 1\n");

        Assert.Equal(new[] { new AlignmentGap(DiffSide.Right, 2, 4) }, gaps);
    }
}
