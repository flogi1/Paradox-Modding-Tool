using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Blocktext aus markierten Zeilen behält die Formatierung der Quelle. Nutzertest 2026-10-01 (CK III resources_top_right_bar): die Spalten
/// zeigen einheitlich formatiert, der gespeicherte Blocktext verlor dadurch Kommentare und Leerzeilen, <c>size = { 144 70 }</c> stand über
/// vier Zeilen und <c>blockoverride "income"</c> mit <c>{</c> in einer Zeile.
/// </summary>
public class BlockTextFormatTests : UpdateModTestBase
{
    private const string Hud = "gui/hud.gui";

    // Rohtext ohne Wagenrücklauf, auch wenn Git die Datei mit CRLF auscheckt (die Replace-Muster unten nutzen "\n").
    private static readonly string Old = """
        types HUD
        {
        	type resources_bar = hbox {
        		layer = top

        		## Resources
        		widget = {
        			name = "gold"
        			size = { 144 70 }

        			tooltipwidget = {
        				widget_gold_breakdown_tooltip = {
        					blockoverride "income"
        					{
        						datacontext = "[Income]"
        					}
        				}
        			}
        		}
        	}
        }

        """.ReplaceLineEndings("\n");

    /// <summary>Neue Vanilla ändert die Ebene, die Mod ergänzt eine Zeile im Widget: das Ergebnis gleicht keiner Quelle.</summary>
    private static readonly string NewVanilla = Old.Replace("layer = top", "layer = middle");

    private static readonly string Mod = Old.Replace("\t\t\tname = \"gold\"\n", "\t\t\tname = \"gold\"\n\t\t\tmine = yes\n");

    private CompatchInput Input() => UpdateInput(rules: UpdateModRules.From(new OverrideRules(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) })));

    private CompatchSession Session(CompatchInput input, string oldText, string newText, string modText)
    {
        PutVanilla(Hud, oldText);
        PutA(Hud, newText);
        PutB(Hud, modText);
        var session = new CompatchSession(input, new CompatchState { Target = PatchTarget.UpdateMod, TargetFolder = BDir, ModBFolder = BDir }, null);
        session.Load();
        return session;
    }

    private static EntryAddress Bar(CompatchSession session) => session.Actions.Select(a => a.Address).First(a => a.Key == "type resources_bar");

    /// <summary>Text wie im Kompatch-Fenster: ausgerichtete Zeilen ohne Kommentare, alle aus <paramref name="column"/>.</summary>
    private static string Taken(CompatchSession session, EntryAddress address, CompatchColumn column)
    {
        var info = session.Run.Resolved.Addresses[address];
        var rows = EntryAligner.Align(info.A.Entries, session.Run.Applied.Desired[address].Entries, info.B.Entries, info.Vanilla.Entries, hideComments: true);
        return CompareNavigation.TakeRows(rows, 0, rows.Count - 1, column);
    }

    private static string Stored(CompatchSession session, EntryAddress address) => session.State.BlockTexts[address.Id].Text;

    [Fact]
    public void Block_text_taken_from_a_column_keeps_its_comments_blank_lines_and_short_blocks()
    {
        var session = Session(Input(), Old, NewVanilla, Mod);
        var bar = Bar(session);
        var taken = Taken(session, bar, CompatchColumn.B);
        Assert.DoesNotContain("## Resources", taken);

        Assert.Null(session.SetBlockText(bar, taken, CompatchColumn.B));

        var text = Stored(session, bar);
        Assert.Contains("layer = top\n\n    ## Resources\n    widget = {", text);
        Assert.Contains("size = { 144 70 }\n\n", text);
        Assert.Contains("blockoverride \"income\"\n                {", text);
        Assert.Contains("mine = yes", text);
        Assert.DoesNotContain("\t", text);
    }

    [Fact]
    public void Block_text_that_matches_no_source_keeps_the_formatting_of_its_unchanged_parts()
    {
        var session = Session(Input(), Old, NewVanilla, Mod);
        var bar = Bar(session);
        var taken = Taken(session, bar, CompatchColumn.Result);

        Assert.Null(session.SetBlockText(bar, taken, CompatchColumn.Result));

        var text = Stored(session, bar);
        Assert.Contains("layer = middle\n\n    ## Resources\n    widget = {", text);
        Assert.Contains("mine = yes", text);
        Assert.Contains("size = { 144 70 }\n\n", text);
        Assert.Contains("blockoverride \"income\"\n                {", text);
    }

    [Fact]
    public void Block_text_is_written_into_the_mod_like_vanilla()
    {
        var input = Input();
        var session = Session(input, Old, NewVanilla, Mod);
        var bar = Bar(session);

        Assert.Null(session.SetBlockText(bar, Taken(session, bar, CompatchColumn.Result), CompatchColumn.Result));
        PatchWriter.Commit(PatchWriter.Prepare(Request(input, session.Run, PatchTarget.UpdateMod, BDir)), BDir, null);
        var text = ReadMod(Hud);

        Assert.Contains("\t\tlayer = middle\n\n\t\t## Resources\n\t\twidget = {\n\t\t\tname = \"gold\"\n\t\t\tmine = yes\n\t\t\tsize = { 144 70 }\n\n\t\t\ttooltipwidget = {", text);
        Assert.Contains("\t\t\t\t\tblockoverride \"income\"\n\t\t\t\t\t{\n\t\t\t\t\t\tdatacontext = \"[Income]\"\n\t\t\t\t\t}", text);
        Assert.DoesNotContain("\n    ", text);
    }

    [Fact]
    public void The_taken_column_decides_between_differently_formatted_sides()
    {
        var modShort = Mod.Replace("size = { 144 70 }", "size = {\n\t\t\t\t144\n\t\t\t\t70\n\t\t\t}");
        var session = Session(Input(), Old, NewVanilla, modShort);
        var bar = Bar(session);

        Assert.Null(session.SetBlockText(bar, Taken(session, bar, CompatchColumn.A), CompatchColumn.A));
        Assert.Contains("size = { 144 70 }", Stored(session, bar));

        Assert.Null(session.SetBlockText(bar, Taken(session, bar, CompatchColumn.B), CompatchColumn.B));
        Assert.DoesNotContain("size = { 144 70 }", Stored(session, bar));
        Assert.Contains("mine = yes", Stored(session, bar));
    }

    [Fact]
    public void Block_text_from_crlf_files_has_plain_line_ends()
    {
        static string Crlf(string text) => text.Replace("\n", "\r\n");
        var session = Session(Input(), Crlf(Old), Crlf(NewVanilla), Crlf(Mod));
        var bar = Bar(session);

        Assert.Null(session.SetBlockText(bar, Taken(session, bar, CompatchColumn.Result), CompatchColumn.Result));

        var text = Stored(session, bar);
        Assert.DoesNotContain("\r", text);
        Assert.Contains("size = { 144 70 }\n\n", text);
    }

    [Fact]
    public void Block_text_keeps_the_taken_content()
    {
        var session = Session(Input(), Old, NewVanilla, Mod);
        var bar = Bar(session);
        var taken = Taken(session, bar, CompatchColumn.Result).Replace("name = \"gold\"", "name = \"silver\"");

        Assert.Null(session.SetBlockText(bar, taken, CompatchColumn.Result));

        var stored = ScriptDocument.Parse(Stored(session, bar)).Entries;
        Assert.True(StructuralEquality.EntriesEqual(ScriptDocument.Parse(taken).Entries, stored));
        Assert.Contains("size = { 144 70 }", Stored(session, bar));
    }

    [Fact]
    public void Stored_block_text_from_an_older_run_gets_the_source_formatting_on_load()
    {
        var input = Input();
        var first = Session(input, Old, NewVanilla, Mod);
        var bar = Bar(first);
        var taken = Taken(first, bar, CompatchColumn.Result);
        first.SetBlockText(bar, taken, CompatchColumn.Result);
        var state = first.State;
        // So stand der Text vor dieser Korrektur im Arbeitsstand: wie angezeigt.
        state.BlockTexts[bar.Id] = state.BlockTexts[bar.Id] with { Text = taken };

        var second = new CompatchSession(input, state, null);
        second.Load();

        Assert.Contains("size = { 144 70 }", Stored(second, Bar(second)));
        Assert.Equal(0, second.OpenCount);
    }

    [Fact]
    public void Block_text_taken_with_comments_shown_has_each_comment_once()
    {
        var input = Input();
        var session = Session(input, Old, NewVanilla, Mod);
        var bar = Bar(session);
        var info = session.Run.Resolved.Addresses[bar];
        var rows = EntryAligner.Align(info.A.Entries, session.Run.Applied.Desired[bar].Entries, info.B.Entries, info.Vanilla.Entries, hideComments: false);
        var taken = CompareNavigation.TakeRows(rows, 0, rows.Count - 1, CompatchColumn.Result);

        Assert.Null(session.SetBlockText(bar, taken, CompatchColumn.Result));
        PatchWriter.Commit(PatchWriter.Prepare(Request(input, session.Run, PatchTarget.UpdateMod, BDir)), BDir, null);

        Assert.Equal(1, Stored(session, bar).Split("## Resources").Length - 1);
        Assert.Equal(1, ReadMod(Hud).Split("## Resources").Length - 1);
    }

    [Fact]
    public void Own_text_of_a_place_keeps_unchanged_children_relative_to_its_first_line()
    {
        // Neue Vanilla ändert das Widget, die Mod löscht es: eine Stelle für das ganze Widget. Übernommen wird eine Mischung aus neuer und alter
        // Vanilla, das ergibt einen eigenen Text der Stelle mit dem unveränderten, mehrzeiligen tooltipwidget aus der Datei (Tabs, Tiefe 3).
        const string Types = "types HUD\n{\n\ttype bar = hbox {\n\t\tlayer = top\n\t}\n}\n";
        static string With(string last) => Types.Replace("\t\tlayer = top\n", "\t\tlayer = top\n\t\twidget = {\n\t\t\tname = \"x\"\n\t\t\ttooltipwidget = {\n\t\t\t\ttext = \"a\"\n\t\t\t}\n\t\t\t" + last + "\n\t\t}\n");
        var input = Input();
        var session = Session(input, With("a = 1"), With("a = 2\n\t\t\tc = 5"), Types);
        var bar = session.Actions.Select(a => a.Address).First(a => a.Key == "type bar");
        var place = session.Actions.Single(a => a.Address == bar);

        Assert.Null(session.TakeBlockLines(bar, "type bar = hbox {\n    layer = top\n    widget = {\n        name = \"x\"\n        tooltipwidget = {\n            text = \"a\"\n        }\n        a = 1\n        c = 5\n    }\n}", CompatchColumn.A));

        Assert.False(session.HasBlockText(bar));
        Assert.Equal(DecisionKind.Manual, session.EffectiveDecision(place).Kind);
        Assert.Contains("\n    tooltipwidget = {\n        text = \"a\"\n    }\n", session.EffectiveDecision(place).ManualText);
        PatchWriter.Commit(PatchWriter.Prepare(Request(input, session.Run, PatchTarget.UpdateMod, BDir)), BDir, null);
        Assert.Contains("\n\t\t\ttooltipwidget = {\n\t\t\t\ttext = \"a\"\n\t\t\t}\n\t\t\ta = 1\n\t\t\tc = 5\n\t\t}\n", ReadMod(Hud));
    }
}
