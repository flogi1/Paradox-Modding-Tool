using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Update-Modus mit CK-III-GUI-Dateien (oberste Fenster/Widgets sind Dateirest, Typen und Templates Schlüssel). Nutzertest 2026-10-01:
/// hud.gui und window_county_view.gui wurden kaputt geschrieben, weil geänderte oberste Widgets in den <c>types</c>-Block rutschten.
/// </summary>
public class UpdateModGuiTests : UpdateModTestBase
{
    private const string Hud = "gui/hud.gui";

    // Rohtext ohne Wagenrücklauf, auch wenn Git die Datei mit CRLF auscheckt (die Replace-Muster unten nutzen "\n").
    private static readonly string Old = """
        ######################
        ####### BOTTOM #######
        ######################

        widget = {
        	name = "meta_info"
        	size = { 1 1 }

        	text_single = {
        		name = "version"
        		text = "VERSION"
        	}
        }

        ######################
        ###### TOP RIGHT #####
        ######################

        widget = {
        	name = "topbar"
        	layer = top
        }

        ######################
        ####### TYPES ########
        ######################

        types HUD
        {
        	type hud_button = button {
        		size = { 10 10 }
        	}
        }

        """.ReplaceLineEndings("\n");

    private void Case(string newText, string modText)
    {
        PutVanilla(Hud, Old);
        PutA(Hud, newText);
        PutB(Hud, modText);
    }

    private string Updated()
    {
        var input = UpdateInput(rules: UpdateModRules.From(new OverrideRules(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) })));
        WriteUpdate(input, RunUpdate(input));
        return ReadMod(Hud);
    }

    /// <summary>Der <c>types</c>-Block enthält nur Typen, und die obersten Widgets stehen weiter auf oberster Ebene vor ihm.</summary>
    private static void AssertGuiShape(string text, params string[] topLevelNames)
    {
        var parsed = ScriptDocument.Parse(text);
        var typesUnit = EntryUnits.Build(parsed.Entries).Single(u => u.Header.Count > 0 && EntryUnits.HeaderText(u.Header).StartsWith("types", StringComparison.Ordinal));
        var inner = EntryUnits.Build(((ScriptBlock)typesUnit.Value).Entries);
        Assert.All(inner, u => Assert.StartsWith("type ", EntryUnits.HeaderText(u.Header), StringComparison.Ordinal));

        var typesAt = text.IndexOf("types HUD", StringComparison.Ordinal);
        foreach (var name in topLevelNames)
        {
            var at = text.IndexOf("name = \"" + name + "\"", StringComparison.Ordinal);
            Assert.InRange(at, 0, typesAt);
        }

        Assert.Equal(topLevelNames.Length, parsed.Entries.OfType<ScriptStatement>().Count(s => s.Key == "widget"));
    }

    [Fact]
    public void Top_level_widget_changed_by_both_stays_in_place_outside_types()
    {
        Case(Old.Replace("size = { 1 1 }", "size = { 2 2 }"),
             Old.Replace("\ttext = \"VERSION\"", "\ttext = \"VERSION\"\n\t\tmine = yes"));

        var text = Updated();

        Assert.Contains("size = { 2 2 }", text);
        Assert.Contains("mine = yes", text);
        AssertGuiShape(text, "meta_info", "topbar");
        Assert.True(text.IndexOf("meta_info", StringComparison.Ordinal) < text.IndexOf("topbar", StringComparison.Ordinal));
    }

    [Fact]
    public void Changed_top_level_widget_keeps_its_comment_header_once()
    {
        Case(Old.Replace("layer = top", "layer = middle"), Old.Replace("size = { 1 1 }", "size = { 3 3 }"));

        var text = Updated();

        Assert.Contains("layer = middle", text);
        Assert.Contains("size = { 3 3 }", text);
        Assert.Equal(1, text.Split("###### TOP RIGHT #####").Length - 1);
        Assert.DoesNotContain("# #", text);
        AssertGuiShape(text, "meta_info", "topbar");
    }

    [Fact]
    public void Merged_block_is_written_with_the_tabs_of_the_file()
    {
        Case(Old.Replace("size = { 1 1 }", "size = { 2 2 }"),
             Old.Replace("\ttext = \"VERSION\"", "\ttext = \"VERSION\"\n\t\tmine = yes"));

        var text = Updated();

        Assert.DoesNotContain("\n    ", text);
        Assert.Contains("\n\tsize = { 2 2 }", text);
        Assert.Contains("\n\t\tmine = yes", text);
    }

    [Fact]
    public void New_top_level_widget_from_vanilla_goes_to_the_top_level()
    {
        Case(Old.Replace("######################\n###### TOP RIGHT", "widget = {\n\tname = \"new_one\"\n}\n\n######################\n###### TOP RIGHT"),
             Old.Replace("layer = top", "layer = mine"));

        var text = Updated();

        Assert.Contains("layer = mine", text);
        AssertGuiShape(text, "meta_info", "new_one", "topbar");
    }

    [Fact]
    public void Merged_block_keeps_the_blank_lines_between_its_children()
    {
        Case(Old.Replace("size = { 1 1 }", "size = { 2 2 }"),
             Old.Replace("\ttext = \"VERSION\"", "\ttext = \"VERSION\"\n\t\tmine = yes"));

        var text = Updated();

        Assert.Contains("\tsize = { 2 2 }\n\n\ttext_single = {", text);
    }

    [Fact]
    public void New_vanilla_type_goes_behind_its_vanilla_predecessor()
    {
        const string Two = "\ttype hud_button = button {\n\t\tsize = { 10 10 }\n\t}\n\n\ttype hud_last = button {\n\t\tsize = { 5 5 }\n\t}\n";
        const string Three = "\ttype hud_button = button {\n\t\tsize = { 10 10 }\n\t}\n\n\ttype hud_new = button {\n\t\tsize = { 7 7 }\n\t}\n\n\ttype hud_last = button {\n\t\tsize = { 5 5 }\n\t}\n";
        var old = Old.Replace("\ttype hud_button = button {\n\t\tsize = { 10 10 }\n\t}\n", Two);
        PutVanilla(Hud, old);
        PutA(Hud, old.Replace(Two, Three));
        PutB(Hud, old.Replace("layer = top", "layer = mine"));

        var text = Updated();

        Assert.Contains(Three, text);
        AssertGuiShape(text, "meta_info", "topbar");
    }

    [Fact]
    public void Block_text_with_spaces_is_written_with_the_tabs_and_depth_of_the_types_block()
    {
        Case(Old.Replace("size = { 10 10 }", "size = { 10 10 }\n\t\talpha = 0.5"),
             Old.Replace("type hud_button = button {\n\t\tsize = { 10 10 }", "type hud_button = button {\n\t\tsize = { 10 10 }\n\t\tmine = yes"));
        var input = UpdateInput(rules: UpdateModRules.From(new OverrideRules(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) })));
        var session = new CompatchSession(input, new CompatchState { Target = PatchTarget.UpdateMod, TargetFolder = BDir, ModBFolder = BDir }, null);
        session.Load();
        var address = session.Actions.Select(a => a.Address).First(a => a.Key == "type hud_button");

        // So zeigt das Kompatch-Fenster einen Block: Tiefe 0, vier Leerzeichen.
        Assert.Null(session.SetBlockText(address, "type hud_button = button {\n    size = { 10 10 }\n    blockoverride \"x\" {\n        alpha = 0.5\n    }\n}"));
        PatchWriter.Commit(PatchWriter.Prepare(Request(input, session.Run, PatchTarget.UpdateMod, BDir)), BDir, null);
        var text = ReadMod(Hud);

        Assert.Contains("\n\ttype hud_button = button {\n\t\tsize = { 10 10 }\n\t\tblockoverride \"x\" {\n\t\t\talpha = 0.5\n\t\t}\n\t}\n}", text);
        AssertGuiShape(text, "meta_info", "topbar");
    }

    [Fact]
    public void Changed_type_is_written_with_the_tabs_and_depth_of_the_types_block()
    {
        Case(Old.Replace("size = { 10 10 }", "size = { 10 10 }\n\t\talpha = 0.5"),
             Old.Replace("type hud_button = button {\n\t\tsize = { 10 10 }", "type hud_button = button {\n\t\tsize = { 10 10 }\n\t\tmine = yes"));

        var text = Updated();

        Assert.Contains("\n\t\talpha = 0.5", text);
        Assert.Contains("\n\t\tmine = yes", text);
        Assert.Contains("\n\t}\n}", text);
        Assert.DoesNotContain("\n    ", text);
        AssertGuiShape(text, "meta_info", "topbar");
    }
}
