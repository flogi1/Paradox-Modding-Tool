using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.Desync.Tests;

public class SafetyScannerTests(ITestOutputHelper output)
{
    private static SafetyRules Rules { get; } =
        DesyncProfile.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json")).Safety!;

    private static SafetyVerdicts NoVerdicts() =>
        new(Path.Combine(Path.GetTempPath(), "pmt-none-" + Guid.NewGuid().ToString("N") + ".json"));

    private static SafetyScanResult Scan(TempTree tree, SafetyVerdicts? verdicts = null) =>
        SafetyScanner.Scan(
            new[] { new SafetyMod("Mod", tree.Root, false) },
            Rules,
            verdicts ?? NoVerdicts(),
            null,
            CancellationToken.None);

    [Fact]
    public void Local_dlc_by_context()
    {
        using var tree = new TempTree(
            ("in_game/events/e.txt", "namespace = x\ne = {\n  trigger = { has_local_dlc = sacred_sites }\n}\n"),
            ("in_game/gfx/portraits/p.txt", "a = { has_local_dlc = sacred_sites }\n"));

        var result = Scan(tree);

        var synced = Assert.Single(result.Findings, f => f.RelativePath == "in_game/events/e.txt");
        Assert.Equal("local_dlc", synced.RuleId);
        Assert.Equal(SafetySeverity.Risk, synced.Severity);
        Assert.Equal(SafetyContext.Synced, synced.Context);
        Assert.Equal("sacred_sites", synced.Token);
        Assert.Equal(3, synced.Line);
        Assert.True(synced.Parsed);

        var local = Assert.Single(result.Findings, f => f.RelativePath == "in_game/gfx/portraits/p.txt");
        Assert.Equal(SafetySeverity.Allowed, local.Severity);
        Assert.Equal(SafetyContext.Local, local.Context);
    }

    [Fact]
    public void Scripted_gui_blocks()
    {
        using var tree = new TempTree(
            ("in_game/common/scripted_guis/g.txt",
                "my_gui = {\n  is_shown = {\n    has_local_dlc = x\n  }\n  effect = {\n    if = {\n      limit = { has_local_dlc = y }\n    }\n  }\n}\n"));

        var result = Scan(tree);

        Assert.Equal(2, result.Findings.Count);
        var shown = Assert.Single(result.Findings, f => f.Token == "x");
        Assert.Equal(SafetySeverity.Allowed, shown.Severity);
        Assert.Equal(SafetyContext.Local, shown.Context);
        var effect = Assert.Single(result.Findings, f => f.Token == "y");
        Assert.Equal(SafetySeverity.Risk, effect.Severity);
        Assert.Equal(SafetyContext.Synced, effect.Context);
        Assert.Equal(7, effect.Line);
    }

    [Fact]
    public void Gui_console_commands()
    {
        using var tree = new TempTree(("in_game/gui/w.gui", string.Join('\n',
            "widget = {",
            "  onclick = \"[ExecuteConsoleCommand('GUI.CreateWidget gui/a.gui a')]\"",
            "  onclick = \"[ExecuteConsoleCommand('gui.clearwidgets a')]\"",
            "  onclick = \"[ExecuteConsoleCommand('debug_mode')]\"",
            "  onclick = \"[ExecuteConsoleCommand('add_gold 100')]\"",
            "  #onclick = \"[ExecuteConsoleCommand('add_gold 5')]\"",
            "  onclick = \"[ExecuteConsoleCommand(Concatenate('a','b'))]\"",
            "}")));

        var result = Scan(tree);

        Assert.Equal(5, result.Findings.Count);
        var create = Assert.Single(result.Findings, f => f.Token == "gui.createwidget");
        Assert.Equal("console_ui", create.RuleId);
        Assert.Equal(SafetySeverity.Allowed, create.Severity);
        Assert.Equal(2, create.Line);
        Assert.Equal(SafetySeverity.Allowed, Assert.Single(result.Findings, f => f.Token == "gui.clearwidgets").Severity);
        Assert.Equal(SafetySeverity.Allowed, Assert.Single(result.Findings, f => f.Token == "debug_mode").Severity);

        var gold = Assert.Single(result.Findings, f => f.Token == "add_gold");
        Assert.Equal("console_other", gold.RuleId);
        Assert.Equal(SafetySeverity.Check, gold.Severity);
        Assert.Equal(5, gold.Line);

        var dynamic = Assert.Single(result.Findings, f => f.RuleId == "console_dynamic");
        Assert.Equal("concatenate('a','b')", dynamic.Token);
        Assert.Equal(SafetySeverity.Check, dynamic.Severity);
        Assert.Equal(7, dynamic.Line);
        Assert.All(result.Findings, f => Assert.True(f.Parsed));
    }

    [Fact]
    public void Select_CString_literal_branches_are_commands()
    {
        using var tree = new TempTree(("in_game/gui/w.gui",
            "onclick = \"[ExecuteConsoleCommand( Select_CString( GetVariableSystem.Exists('ideas_window_open'), 'gui.ClearWidgets ideas_window', 'gui.createwidget gui/x.gui ideas_window' ) )]\"\n"));

        var result = Scan(tree);

        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(new[] { "gui.clearwidgets", "gui.createwidget" }, result.Findings.Select(f => f.Token).OrderBy(t => t, StringComparer.Ordinal));
        Assert.All(result.Findings, f =>
        {
            Assert.Equal("console_ui", f.RuleId);
            Assert.Equal(SafetySeverity.Allowed, f.Severity);
            Assert.Equal(1, f.Line);
        });
    }

    [Fact]
    public void Select_CString_branch_is_judged_like_a_literal_call()
    {
        using var tree = new TempTree(("in_game/gui/w.gui",
            "onclick = \"[ExecuteConsoleCommand(Select_CString(IsX('a'), 'gui.clearwidgets a', 'add_gold 1'))]\"\n"));

        var result = Scan(tree);

        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(SafetySeverity.Allowed, Assert.Single(result.Findings, f => f.Token == "gui.clearwidgets").Severity);
        var gold = Assert.Single(result.Findings, f => f.Token == "add_gold");
        Assert.Equal("console_other", gold.RuleId);
        Assert.Equal(SafetySeverity.Check, gold.Severity);
    }

    [Fact]
    public void Select_CString_with_non_literal_branch_is_dynamic()
    {
        using var tree = new TempTree(("in_game/gui/w.gui",
            "onclick = \"[ExecuteConsoleCommand(Select_CString(cond, GetSomething(), 'x'))]\"\n"));

        var finding = Assert.Single(Scan(tree).Findings);

        Assert.Equal("console_dynamic", finding.RuleId);
        Assert.Equal("select_cstring(cond, getsomething(), 'x')", finding.Token);
        Assert.Equal(SafetySeverity.Check, finding.Severity);
    }

    [Fact]
    public void Dynamic_token_is_the_normalized_argument_so_verdicts_stay_apart()
    {
        using var tree = new TempTree(("in_game/gui/w.gui", string.Join('\n',
            "onclick = \"[ExecuteConsoleCommand( Concatenate( 'Add_Gold ',   GetAmount() ) )]\"",
            "onclick = \"[ExecuteConsoleCommand(Concatenate('gui.clearwidgets ', GetName()))]\" # ExecuteConsoleCommand(x)",
            "onclick = \"[ExecuteConsoleCommand(Concatenate('a(', GetName()",
            "")));
        var verdicts = NoVerdicts();
        verdicts.Set("console_dynamic", "concatenate( 'add_gold ', getamount() )", SafetyVerdict.Harmless);

        var findings = Scan(tree, verdicts).Findings;

        Assert.Equal(3, findings.Count);
        Assert.All(findings, f => Assert.Equal("console_dynamic", f.RuleId));
        var gold = Assert.Single(findings, f => f.Line == 1);
        Assert.Equal("concatenate( 'add_gold ', getamount() )", gold.Token);
        Assert.Equal(SafetySeverity.Allowed, gold.Severity);
        var clear = Assert.Single(findings, f => f.Line == 2);
        Assert.Equal("concatenate('gui.clearwidgets ', getname())", clear.Token);
        Assert.Equal(SafetySeverity.Check, clear.Severity);
        // Ohne schließende Klammer auf der Zeile bleibt es beim allgemeinen Token.
        Assert.Equal("dynamic", Assert.Single(findings, f => f.Line == 3).Token);
    }

    [Fact]
    public void Dynamic_token_is_capped()
    {
        var argument = "Concatenate('" + new string('x', 200) + "', GetName())";
        using var tree = new TempTree(("in_game/gui/w.gui", "onclick = \"[ExecuteConsoleCommand(" + argument + ")]\"\n"));

        var finding = Assert.Single(Scan(tree).Findings);

        Assert.Equal(argument.ToLowerInvariant()[..120], finding.Token);
    }

    [Fact]
    public void Gui_hash_inside_quotes_is_not_a_comment()
    {
        using var tree = new TempTree(("in_game/gui/w.gui", "onclick = \"[ExecuteConsoleCommand('add_gold 1 #x')]\"\n"));

        var finding = Assert.Single(Scan(tree).Findings);

        Assert.Equal("console_other", finding.RuleId);
        Assert.Equal("add_gold", finding.Token);
    }

    [Fact]
    public void Gui_hash_inside_single_quotes_is_not_a_comment()
    {
        using var tree = new TempTree(("in_game/gui/w.gui", string.Join('\n',
            "onclick = [ExecuteConsoleCommand('gui.x #y')]",
            "onclick = [ExecuteConsoleCommand('add_gold 1')] # [ExecuteConsoleCommand('add_gold 2')]",
            "")));

        var findings = Scan(tree).Findings;

        Assert.DoesNotContain(findings, f => f.RuleId == "console_dynamic");
        var ui = Assert.Single(findings, f => f.Line == 1);
        Assert.Equal("console_ui", ui.RuleId);
        Assert.Equal("gui.x", ui.Token);
        // Nach dem schließenden ' ist # wieder ein Kommentar.
        var gold = Assert.Single(findings, f => f.Line == 2);
        Assert.Equal("add_gold", gold.Token);
    }

    [Fact]
    public void Unparsable_txt_falls_back()
    {
        using var tree = new TempTree(("in_game/events/bad.txt", "a = { has_local_dlc = x"));

        var finding = Assert.Single(Scan(tree).Findings);

        Assert.False(finding.Parsed);
        Assert.Equal(SafetyContext.Unknown, finding.Context);
        Assert.Equal(SafetySeverity.Risk, finding.Severity);
        Assert.Equal("local_dlc", finding.RuleId);
        Assert.Equal("x", finding.Token);
        Assert.Equal(1, finding.Line);
        Assert.Equal(1, finding.ParseErrorLine);
    }

    [Fact]
    public void Unparsable_txt_keeps_the_line_of_the_parse_error()
    {
        using var tree = new TempTree(("in_game/events/bad.txt", "a = {\n  has_local_dlc = x\n}\n}\nb = 1\n"));

        var finding = Assert.Single(Scan(tree).Findings);

        Assert.False(finding.Parsed);
        Assert.Equal(2, finding.Line);
        Assert.Equal(4, finding.ParseErrorLine);
    }

    [Fact]
    public void Txt_without_any_rule_pattern_gives_nothing_even_when_unparsable()
    {
        using var tree = new TempTree(
            ("in_game/events/bad.txt", "a = { b = c"),
            ("in_game/events/ok.txt", "a = { b = c }\n"));

        var result = Scan(tree);

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.Files.Count);
    }

    [Fact]
    public void Unparsable_txt_ignores_comments()
    {
        using var tree = new TempTree(("in_game/events/bad.txt", "a = {\n# has_local_dlc = x\n"));

        Assert.Empty(Scan(tree).Findings);
    }

    [Fact]
    public void Missing_folder_notice()
    {
        using var tree = new TempTree(("in_game/events/e.txt", "a = { has_local_dlc = x }\n"));
        var missing = Path.Combine(tree.Root, "does-not-exist");

        var result = SafetyScanner.Scan(
            new[] { new SafetyMod("Gone", missing, false), new SafetyMod("Here", tree.Root, true) },
            Rules,
            NoVerdicts(),
            null,
            CancellationToken.None);

        var notice = Assert.Single(result.Notices);
        Assert.Equal(SafetyNoticeKind.MissingFolder, notice.Kind);
        Assert.Equal("Gone", notice.ModName);
        Assert.Equal(missing, notice.Path);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("Here", finding.ModName);
        Assert.True(finding.IsOwnMod);
    }

    [Fact]
    public void Folder_that_cannot_be_accessed_is_unreadable_not_missing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var tree = new TempTree(("locked/in_game/events/e.txt", "a = { has_local_dlc = x }\n"));
        var parent = new DirectoryInfo(tree.Root);
        var locked = new DirectoryInfo(tree.PathOf("locked"));
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var denyParent = new System.Security.AccessControl.FileSystemAccessRule(
            user, System.Security.AccessControl.FileSystemRights.ListDirectory, System.Security.AccessControl.AccessControlType.Deny);
        var denyLocked = new System.Security.AccessControl.FileSystemAccessRule(
            user,
            System.Security.AccessControl.FileSystemRights.ListDirectory | System.Security.AccessControl.FileSystemRights.ReadAttributes,
            System.Security.AccessControl.AccessControlType.Deny);
        var parentAcl = parent.GetAccessControl();
        var lockedAcl = locked.GetAccessControl();
        // Jede Sperre wird in ihrem eigenen finally aufgehoben: scheitert das Aufheben der einen, wird die andere trotzdem aufgehoben.
        try
        {
            lockedAcl.AddAccessRule(denyLocked);
            locked.SetAccessControl(lockedAcl);
            try
            {
                parentAcl.AddAccessRule(denyParent);
                parent.SetAccessControl(parentAcl);

                // Ohne Zugriff meldet Directory.Exists "gibt es nicht"; nur dann prüft dieser Test etwas (mit Admin- oder Sicherungsrechten
                // greift die Sperre nicht).
                if (Directory.Exists(locked.FullName))
                {
                    output.WriteLine("Nicht geprüft: Die Zugriffssperre greift hier nicht (Admin- oder Sicherungsrechte), Directory.Exists sieht den Ordner.");
                    return;
                }

                var result = SafetyScanner.Scan(new[] { new SafetyMod("Locked", locked.FullName, false) }, Rules, NoVerdicts(), null, CancellationToken.None);

                var notice = Assert.Single(result.Notices);
                Assert.Equal(SafetyNoticeKind.Unreadable, notice.Kind);
                Assert.Equal("Locked", notice.ModName);
                Assert.Equal(locked.FullName, notice.Path);
                Assert.False(string.IsNullOrEmpty(notice.Message));
            }
            finally
            {
                parentAcl.RemoveAccessRule(denyParent);
                parent.SetAccessControl(parentAcl);
            }
        }
        finally
        {
            lockedAcl.RemoveAccessRule(denyLocked);
            locked.SetAccessControl(lockedAcl);
        }
    }

    [Fact]
    public void Skips_dot_folders_and_other_extensions()
    {
        using var tree = new TempTree(
            (".git/x.txt", "a = { has_local_dlc = x }\n"),
            ("in_game/events/e.yml", "a = { has_local_dlc = x }\n"),
            ("in_game/events/e.txt", "a = 1\n"),
            ("in_game/gui/w.gui", "a = 1\n"));

        var result = Scan(tree);

        Assert.Equal(
            new[] { "in_game/events/e.txt", "in_game/gui/w.gui" },
            result.Files.Select(f => f.RelativePath).OrderBy(p => p, StringComparer.Ordinal));
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Scan_reads_bom_and_windows_1252_files()
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("in_game/events"));
        File.WriteAllBytes(
            tree.PathOf("in_game/events/bom.txt"),
            new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("# käse\na = { has_local_dlc = x }\n")).ToArray());
        File.WriteAllBytes(
            tree.PathOf("in_game/events/ansi.txt"),
            new byte[] { (byte)'#', (byte)' ', (byte)'k', 0xE4, (byte)'s', (byte)'e', (byte)'\n' }
                .Concat(Encoding.ASCII.GetBytes("a = { has_local_dlc = y }\n")).ToArray());

        var result = Scan(tree);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(2, f.Line));
        Assert.All(result.Findings, f => Assert.True(f.Parsed));
    }

    [Fact]
    public void Verdict_applies()
    {
        using var tree = new TempTree(("in_game/gui/w.gui", "onclick = \"[ExecuteConsoleCommand('add_gold 1')]\"\n"));
        var verdicts = new SafetyVerdicts(Path.Combine(tree.Root, "v.json"));
        verdicts.Set("console_other", "add_gold", SafetyVerdict.Harmless);

        var result = Scan(tree, verdicts);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(SafetySeverity.Allowed, finding.Severity);
        Assert.Equal(SafetySeverity.Check, finding.ProfileSeverity);

        verdicts.Set("console_other", "add_gold", SafetyVerdict.Risky);
        Assert.Equal(SafetySeverity.Risk, Assert.Single(result.WithVerdicts(verdicts).Findings).Severity);

        verdicts.Set("console_other", "add_gold", null);
        var again = result.WithVerdicts(verdicts);
        Assert.Equal(SafetySeverity.Check, Assert.Single(again.Findings).Severity);
        Assert.Same(result.Files, again.Files);
    }

    [Fact]
    public void Findings_are_sorted()
    {
        using var tree = new TempTree(
            ("in_game/events/b.txt", "a = {\n has_local_dlc = x\n}\nb = { has_local_dlc = y }\n"),
            ("in_game/events/a.txt", "a = { has_local_dlc = z }\n"));

        var result = SafetyScanner.Scan(
            new[] { new SafetyMod("Zeta", tree.Root, false), new SafetyMod("Alpha", tree.Root, false) },
            Rules,
            NoVerdicts(),
            null,
            CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "Alpha|in_game/events/a.txt|1", "Alpha|in_game/events/b.txt|2", "Alpha|in_game/events/b.txt|4",
                "Zeta|in_game/events/a.txt|1", "Zeta|in_game/events/b.txt|2", "Zeta|in_game/events/b.txt|4",
            },
            result.Findings.Select(f => f.ModName + "|" + f.RelativePath + "|" + f.Line));
    }

    [Fact]
    public void Scan_stops_when_cancelled()
    {
        using var tree = new TempTree(("in_game/events/e.txt", "a = { has_local_dlc = x }\n"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => SafetyScanner.Scan(
            new[] { new SafetyMod("Mod", tree.Root, false) },
            Rules,
            NoVerdicts(),
            null,
            cts.Token));
    }

    [Fact]
    public void Progress_reaches_one()
    {
        using var tree = new TempTree(
            ("in_game/events/a.txt", "a = 1\n"),
            ("in_game/events/b.txt", "a = 1\n"),
            ("in_game/gui/c.gui", "a = 1\n"));
        var values = new List<double>();

        SafetyScanner.Scan(
            new[] { new SafetyMod("Mod", tree.Root, false) },
            Rules,
            NoVerdicts(),
            new SyncProgress(values.Add),
            CancellationToken.None);

        Assert.NotEmpty(values);
        Assert.Equal(1.0, values[^1]);
        Assert.Equal(values.OrderBy(v => v), values);
    }

    [Fact]
    public void ReadText_returns_null_for_missing_file()
    {
        Assert.Null(SafetyScanner.ReadText(Path.Combine(Path.GetTempPath(), "pmt-nope-" + Guid.NewGuid().ToString("N") + ".txt")));
    }

    [Fact]
    public void Real_mod_has_no_check_or_risk_findings()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_SAFETY_MOD");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var result = SafetyScanner.Scan(
            new[] { new SafetyMod("Real", folder, true) },
            Rules,
            NoVerdicts(),
            null,
            CancellationToken.None);

        var bad = result.Findings.Where(f => f.Severity != SafetySeverity.Allowed).ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad.Select(f => f.Severity + " " + f.RuleId + " " + f.Token + " " + f.RelativePath + ":" + f.Line)));
        Assert.True(result.Findings.Count(f => f.RuleId == "console_ui") >= 120);
    }

    /// <summary>Synchroner IProgress, damit der Test die Werte sofort sieht (Progress&lt;T&gt; meldet über den SynchronizationContext).</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
