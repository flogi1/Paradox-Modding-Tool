using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditViewTests : IDisposable
{
    private static readonly IReadOnlyList<GfxFinding> Sample = new[]
    {
        Find(GfxFindingKind.UnusedImage, "gfx/old.dds", "gfx/old.dds", 0),
        Find(GfxFindingKind.UnusedSprite, "GFX_old"),
        Find(GfxFindingKind.MissingTexture, "gfx/gone.dds", "interface/b.gfx", 7, "GFX_b"),
        Find(GfxFindingKind.MissingTexture, "gfx/gone.dds", "interface/c.gfx", 9, "GFX_c"),
    };

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-gfxview-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private static GfxFinding Find(GfxFindingKind kind, string name, string path = "interface/a.gfx", int line = 1, params string[] related) =>
        GfxFinding.Create(kind, FindingSeverity.Warning, name, new SiteLocation("r", path, line), related);

    private ScriptCheckHideList Hide(params string[] keys)
    {
        var list = ScriptCheckHideList.Load(_temp, Path.Combine(_temp, "mod"));
        foreach (var key in keys)
        {
            list.Add(key);
        }

        return list;
    }

    [Fact]
    public void RowsFilterByKindAndQuery()
    {
        var byKind = GfxAuditView.Rows(Sample, null, false, GfxFindingKind.MissingTexture, string.Empty);
        var byName = GfxAuditView.Rows(Sample, null, false, null, " OLD ");
        var byPlace = GfxAuditView.Rows(Sample, null, false, null, "b.gfx:7");
        var byRelated = GfxAuditView.Rows(Sample, null, false, null, "gfx_c");

        Assert.Equal(2, byKind.Count);
        Assert.Equal(new[] { "gfx/old.dds", "GFX_old" }, byName.Select(r => r.Name));
        Assert.Equal("gfx/gone.dds", Assert.Single(byPlace).Name);
        Assert.Equal("interface/c.gfx:9", Assert.Single(byRelated).Place);
        Assert.Equal(Sample.Count, GfxAuditView.Rows(Sample, null, false, null, string.Empty).Count);
    }

    [Fact]
    public void RowsHideHiddenUnlessShown()
    {
        var hide = Hide("UnusedSprite|GFX_old");

        var visible = GfxAuditView.Rows(Sample, hide, false, null, string.Empty);
        var shown = GfxAuditView.Rows(Sample, hide, true, null, string.Empty);

        Assert.DoesNotContain(visible, r => r.Name == "GFX_old");
        Assert.All(visible, r => Assert.False(r.IsHidden));
        Assert.Equal(Sample.Count, shown.Count);
        Assert.Equal(new[] { "GFX_old" }, shown.Where(r => r.IsHidden).Select(r => r.Name));
    }

    [Fact]
    public void RelatedTruncatedAfterFive()
    {
        var many = Find(GfxFindingKind.UnusedImage, "gfx/x.dds", "gfx/x.dds", 0, "a", "b", "c", "d", "e", "f", "g");
        var five = Find(GfxFindingKind.DuplicateSprite, "S", "i.gfx", 1, "a", "b", "c", "d", "e");

        var rows = GfxAuditView.Rows(new[] { many, five }, null, false, null, string.Empty);

        Assert.Equal("a, b, c, d, e, … (+2)", rows[0].Related);
        Assert.Equal("a, b, c, d, e", rows[1].Related);
    }

    [Fact]
    public void UnusedImageWithoutRelatedShowsNobodyUses()
    {
        var rows = GfxAuditView.Rows(Sample, null, false, null, string.Empty);
        var missing = GfxAuditView.Rows(new[] { Find(GfxFindingKind.MissingSprite, "GFX_x") }, null, false, null, string.Empty);

        Assert.Equal(GfxAuditTexts.NobodyUses, rows[0].Related);
        Assert.Equal(GfxAuditTexts.NobodyUses, rows[1].Related);
        Assert.Equal("GFX_b", rows[2].Related);
        Assert.Equal(string.Empty, missing[0].Related);
    }

    [Fact]
    public void ImagePlaceHasNoLine()
    {
        var rows = GfxAuditView.Rows(Sample, null, false, null, string.Empty);

        Assert.Equal("gfx/old.dds", rows[0].Place);
        Assert.True(rows[0].IsImage);
        Assert.Equal("interface/a.gfx:1", rows[1].Place);
        Assert.False(rows[1].IsImage);
        Assert.Equal(GfxAuditTexts.Kind(GfxFindingKind.UnusedImage), rows[0].KindText);
        Assert.Equal(GfxAuditTexts.Severity(FindingSeverity.Warning), rows[0].SeverityText);
    }

    [Fact]
    public void KindFiltersCountVisibleOnly()
    {
        var hide = Hide("UnusedSprite|GFX_old");

        var visible = GfxAuditView.KindFilters(Sample, hide, false);
        var all = GfxAuditView.KindFilters(Sample, hide, true);

        Assert.Equal(
            new (GfxFindingKind?, int)[] { (null, 3), (GfxFindingKind.UnusedImage, 1), (GfxFindingKind.MissingTexture, 2) },
            visible.Select(f => (f.Kind, f.Count)));
        Assert.Equal(GfxAuditTexts.FilterAll, visible[0].Text);
        Assert.Equal(4, all[0].Count);
        Assert.Contains(all, f => f.Kind == GfxFindingKind.UnusedSprite && f.Count == 1);
    }

    [Fact]
    public void HiddenKeyHidesAllSites()
    {
        var hide = Hide();
        Assert.True(hide.Add(Sample[2].Key));

        var rows = GfxAuditView.Rows(Sample, hide, false, null, string.Empty);

        Assert.DoesNotContain(rows, r => r.Finding.Kind == GfxFindingKind.MissingTexture);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void HasVanillaNeedsAVanillaRoot()
    {
        using var roots = new GfxTestRoots();
        var withVanilla = new GfxAuditIndex(roots.Sources(), null, Array.Empty<GfxFile>(), Array.Empty<GfxImage>(), Array.Empty<GfxMentionSite>(), Array.Empty<string>(), DateTime.UtcNow);
        var without = new GfxAuditIndex(
            FlagSourceSet.ForPlayset(RepoFiles.Profile("ck3"), Array.Empty<ModSource>(), roots.Mod, Array.Empty<string>()),
            null, Array.Empty<GfxFile>(), Array.Empty<GfxImage>(), Array.Empty<GfxMentionSite>(), Array.Empty<string>(), DateTime.UtcNow);

        Assert.True(GfxAuditView.HasVanilla(withVanilla));
        Assert.False(GfxAuditView.HasVanilla(without));
        Assert.False(GfxAuditView.HasVanilla(null));
    }

    [Fact]
    public void MoveOutBlockOrder()
    {
        using var roots = new GfxTestRoots();
        var clean = new GfxAuditIndex(roots.Sources(), null, Array.Empty<GfxFile>(), Array.Empty<GfxImage>(), Array.Empty<GfxMentionSite>(), Array.Empty<string>(), DateTime.UtcNow);
        var unread = new GfxAuditIndex(roots.Sources(), null, Array.Empty<GfxFile>(), Array.Empty<GfxImage>(), Array.Empty<GfxMentionSite>(), new[] { "a", "b" }, DateTime.UtcNow);

        Assert.Null(GfxAuditView.MoveOutBlock(false, true, clean, true));
        Assert.Equal(GfxAuditTexts.MoveBlockedBuilding, GfxAuditView.MoveOutBlock(true, true, clean, true));
        Assert.Equal(GfxAuditTexts.MoveBlockedNotLocal, GfxAuditView.MoveOutBlock(false, false, clean, true));
        Assert.Equal(GfxAuditTexts.MoveBlockedNoVanilla, GfxAuditView.MoveOutBlock(false, true, null, true));
        Assert.Equal(GfxAuditTexts.MoveBlockedUnread(2), GfxAuditView.MoveOutBlock(false, true, unread, true));
        Assert.Equal(GfxAuditTexts.MoveBlockedStale, GfxAuditView.MoveOutBlock(false, true, clean, false));
        Assert.Equal(GfxAuditTexts.MoveBlockedBuilding, GfxAuditView.MoveOutBlock(true, false, null, false));
        Assert.Equal(GfxAuditTexts.MoveBlockedNotLocal, GfxAuditView.MoveOutBlock(false, false, null, false));
        Assert.Equal(GfxAuditTexts.MoveBlockedNoVanilla, GfxAuditView.MoveOutBlock(false, true, null, false));
    }

    [Fact]
    public void ConfirmListCapsAtThirty()
    {
        var paths = Enumerable.Range(1, 32).Select(i => "gfx/p" + i + ".dds").ToList();

        var lines = GfxAuditView.ConfirmList(paths).Split(Environment.NewLine);

        Assert.Equal(31, lines.Length);
        Assert.Equal("gfx/p1.dds", lines[0]);
        Assert.Equal("gfx/p30.dds", lines[29]);
        Assert.Equal("… (+2)", lines[30]);
        Assert.Equal("a.dds" + Environment.NewLine + "b.dds", GfxAuditView.ConfirmList(new[] { "a.dds", "b.dds" }));
        Assert.Equal(30, GfxAuditView.ConfirmList(paths.Take(30).ToList()).Split(Environment.NewLine).Length);
    }

    [Fact]
    public void FailureTipCapsAtForty()
    {
        Assert.Null(GfxAuditView.FailureTip(Array.Empty<GfxArchiveFailure>()));

        var failed = Enumerable.Range(1, 41).Select(i => new GfxArchiveFailure("gfx/f" + i + ".dds", GfxArchiveFailureReason.Missing, null)).ToList();
        failed[1] = new GfxArchiveFailure("gfx/f2.dds", GfxArchiveFailureReason.IoError, "busy");

        var lines = GfxAuditView.FailureTip(failed)!.Split(Environment.NewLine);

        Assert.Equal(40, lines.Length);
        Assert.Equal("gfx/f1.dds: " + GfxAuditTexts.Failure(GfxArchiveFailureReason.Missing), lines[0]);
        Assert.Equal("gfx/f2.dds: " + GfxAuditTexts.Failure(GfxArchiveFailureReason.IoError) + " (busy)", lines[1]);
        Assert.Equal("gfx/f40.dds: " + GfxAuditTexts.Failure(GfxArchiveFailureReason.Missing), lines[39]);
    }
}
