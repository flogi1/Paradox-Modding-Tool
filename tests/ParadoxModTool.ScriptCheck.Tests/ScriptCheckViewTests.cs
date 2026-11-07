using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckViewTests : IDisposable
{
    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    private static Finding Make(FindingKind kind, string name, string path = "events/a.txt", int line = 3, params string[] related) =>
        Finding.Create(kind, FindingSeverity.Warning, name, CheckKind.Event, new SiteLocation("C:/mod", path, line), related);

    private ScriptCheckHideList Hide() => ScriptCheckHideList.Load(Path.Combine(_roots.Temp, "hidden"), _roots.Mod);

    [Fact]
    public void RowsFilterByKindAndQuery()
    {
        var findings = new[]
        {
            Make(FindingKind.DeadEvent, "a.1"),
            Make(FindingKind.UnusedBlock, "my_fx", "common/scripted_effects/fx.txt"),
            Make(FindingKind.DeadEvent, "b.1", "events/b.txt"),
        };

        Assert.Equal(new[] { "a.1", "b.1" }, ScriptCheckView.Rows(findings, null, false, FindingKind.DeadEvent, "").Select(r => r.Name));
        Assert.Equal(new[] { "my_fx" }, ScriptCheckView.Rows(findings, null, false, null, "MY_F").Select(r => r.Name));
        Assert.Equal(new[] { "b.1" }, ScriptCheckView.Rows(findings, null, false, null, "events/b").Select(r => r.Name));
    }

    [Fact]
    public void RowsCarryPlaceAndTexts()
    {
        var row = Assert.Single(ScriptCheckView.Rows(new[] { Make(FindingKind.DeadEvent, "a.1", "events/a.txt", 12) }, null, false, null, ""));

        Assert.Equal("events/a.txt:12", row.Place);
        Assert.Equal("Totes Event", row.KindText);
        Assert.Equal("Warnung", row.SeverityText);
        Assert.False(row.IsHidden);
    }

    [Fact]
    public void RowsHideHiddenUnlessShown()
    {
        var finding = Make(FindingKind.DeadEvent, "a.1");
        var other = Make(FindingKind.DeadEvent, "b.1");
        var hide = Hide();
        hide.Add(finding.Key);

        Assert.Equal(new[] { "b.1" }, ScriptCheckView.Rows(new[] { finding, other }, hide, false, null, "").Select(r => r.Name));
        var shown = ScriptCheckView.Rows(new[] { finding, other }, hide, true, null, "");
        Assert.True(shown.Single(r => r.Name == "a.1").IsHidden);
        Assert.False(shown.Single(r => r.Name == "b.1").IsHidden);
    }

    [Fact]
    public void RowsPerformanceRow()
    {
        var path = new[] { "a", "b", "c", "d", "e", "f", "g" };
        Finding Perf(string? inner) =>
            new(FindingKind.ExpensiveIterator, FindingSeverity.Warning, "every_country", CheckKind.Effect, new SiteLocation("C:/mod", "events/a.txt", 3), path, "ExpensiveIterator|x|every_country")
            {
                Performance = new PerformanceInfo(PerformanceRate.Monthly, path, inner, new[] { "every_neighboring_country" }, new[] { 3 }),
            };
        var broken = Make(FindingKind.BrokenReference, "x", "events/b.txt", 1, "1", "2");

        var rows = ScriptCheckView.Rows(new[] { Perf(null), Perf("every_province"), broken }, null, false, null, "");

        Assert.Equal("a \u2192 b \u2192 c \u2192 d \u2192 e \u2192 f \u2192 g", rows[0].Related);
        Assert.Equal(ScriptCheckTexts.Kind(FindingKind.ExpensiveIterator), rows[0].KindText);
        Assert.Equal(ScriptCheckTexts.Kind(FindingKind.ExpensiveIterator) + " \u00b7 Verschachtelt", rows[1].KindText);
        Assert.Equal(string.Join("\n", PerformanceTexts.Suggestions(rows[0].Finding)), rows[0].Suggestion);
        Assert.Contains("every_neighboring_country", rows[0].Suggestion);
        Assert.Null(rows[2].Suggestion);
        Assert.Equal(ScriptCheckTexts.Kind(FindingKind.BrokenReference), rows[2].KindText);
        Assert.Equal("1, 2", rows[2].Related);
    }

    [Fact]
    public void RelatedTruncatedAfterFive()
    {
        var row = ScriptCheckView.Rows(new[] { Make(FindingKind.BrokenReference, "x", "events/a.txt", 1, "1", "2", "3", "4", "5", "6", "7") }, null, false, null, "").Single();

        Assert.Equal("1, 2, 3, 4, 5, … (+2)", row.Related);
    }

    [Fact]
    public void DeadEventShowsNobodyCalls()
    {
        var rows = ScriptCheckView.Rows(new[] { Make(FindingKind.DeadEvent, "a.1"), Make(FindingKind.UnusedBlock, "fx"), Make(FindingKind.EventCycle, "c") }, null, false, null, "");

        Assert.Equal(ScriptCheckTexts.NobodyCalls, rows[0].Related);
        Assert.Equal(ScriptCheckTexts.NobodyCalls, rows[1].Related);
        Assert.Equal(string.Empty, rows[2].Related);
    }

    [Fact]
    public void KindFiltersCountVisibleOnly()
    {
        var hidden = Make(FindingKind.UnusedBlock, "fx");
        var findings = new[] { Make(FindingKind.DeadEvent, "a.1"), Make(FindingKind.DeadEvent, "b.1"), hidden };
        var hide = Hide();
        hide.Add(hidden.Key);

        var filters = ScriptCheckView.KindFilters(findings, hide, false);

        Assert.Equal(2, filters.Count);
        Assert.Null(filters[0].Kind);
        Assert.Equal(ScriptCheckTexts.FilterAll, filters[0].Text);
        Assert.Equal(2, filters[0].Count);
        Assert.Equal(FindingKind.DeadEvent, filters[1].Kind);
        Assert.Equal(2, filters[1].Count);
        Assert.Equal(3, ScriptCheckView.KindFilters(findings, hide, true)[0].Count);
    }

    [Fact]
    public void BrokenBlockKindTextIsEffectOrTrigger()
    {
        Assert.Equal("Effekt/Trigger", ScriptCheckTexts.NameKind(CheckKind.Effect));
        Assert.All(Enum.GetValues<CheckKind>(), v => Assert.False(string.IsNullOrWhiteSpace(ScriptCheckTexts.NameKind(v))));
    }

    [Fact]
    public void CallsMarkOnlyEventCallersForVanillaName()
    {
        ScriptCheckTestRoots.Write(_roots.Vanilla, "common/scripted_effects/fx.txt", "vanilla_fx = {\n\tadd_gold = 1\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/mine.txt", "mine_fx = {\n\tadd_gold = 1\n}");

        var view = ScriptCheckView.Calls(new ScriptCheckBuilder(_roots.Sources(), ScriptCheckTestRoots.Rules()).Build(), "vanilla_fx");

        Assert.True(view.OnlyEventCallers);
    }

    [Fact]
    public void CallsForModNameAreComplete()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/mine.txt", "mine_fx = {\n\tadd_gold = 1\n}");
        ScriptCheckTestRoots.Write(_roots.Vanilla, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tmine_fx = yes\n\t\ttrigger_event = a.2\n\t}\n}");

        var index = new ScriptCheckBuilder(_roots.Sources(), ScriptCheckTestRoots.Rules()).Build();
        var view = ScriptCheckView.Calls(index, "mine_fx");

        Assert.False(view.OnlyEventCallers);
        Assert.Contains(view.Callers, r => r.Owner == "a.1");
        Assert.Contains(ScriptCheckView.Calls(index, "a.1").Callees, r => r.Name == "a.2");
    }

    [Fact]
    public void ResultOfTheChosenModIsUsable()
    {
        var index = new ScriptCheckBuilder(_roots.Sources(), ScriptCheckTestRoots.Rules()).Build();

        Assert.Equal(ScriptCheckResultState.Usable, ScriptCheckView.StateOf(index, _roots.Mod));
        Assert.Equal(ScriptCheckResultState.Usable, ScriptCheckView.StateOf(index, _roots.Mod.ToUpperInvariant() + "\\"));
    }

    [Fact]
    public void ResultOfAnotherModIsNotUsable()
    {
        var index = new ScriptCheckBuilder(_roots.Sources(), ScriptCheckTestRoots.Rules()).Build();

        Assert.Equal(ScriptCheckResultState.OtherMod, ScriptCheckView.StateOf(index, _roots.Other));
        Assert.Equal(ScriptCheckResultState.OtherMod, ScriptCheckView.StateOf(index, null));
    }

    [Fact]
    public void ResultWithoutVanillaIsNotUsable()
    {
        var sources = FlagSourceSet.ForPlayset(RepoFiles.Profile("ck3"), new List<ParadoxModTool.ErrorLog.ModSource> { new("My Mod", _roots.Mod, true, 0) }, _roots.Mod, Array.Empty<string>());
        var index = new ScriptCheckBuilder(sources, ScriptCheckTestRoots.Rules()).Build();

        Assert.Equal(ScriptCheckResultState.NoVanilla, ScriptCheckView.StateOf(index, _roots.Mod));
    }

    [Fact]
    public void NoResultYet()
    {
        Assert.Equal(ScriptCheckResultState.None, ScriptCheckView.StateOf(null, _roots.Mod));
    }

    [Fact]
    public void CallsNameOfACycleIsItsFirstMember()
    {
        var cycle = Finding.Create(FindingKind.EffectCycle, FindingSeverity.Error, "a_fx \u2192 b_fx", CheckKind.Effect, new SiteLocation("C:/mod", "x.txt", 1), new[] { "a_fx", "b_fx" });

        Assert.Equal("a_fx", ScriptCheckView.CallsName(cycle));
        Assert.Equal("a.1", ScriptCheckView.CallsName(Make(FindingKind.DeadEvent, "a.1")));
    }
}
