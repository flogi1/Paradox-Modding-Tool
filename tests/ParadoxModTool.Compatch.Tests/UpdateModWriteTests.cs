using System.Text;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Ziel <see cref="PatchTarget.UpdateMod"/>: Ergebnis steht in der eigenen Datei der Mod, keine PMT-eigenen Dateien, nichts gelöscht.</summary>
public class UpdateModWriteTests : UpdateModTestBase
{
    private const string Buildings = "common/buildings/00_buildings.txt";

    private void Case(string oldText, string newText, string modText, string path = Buildings)
    {
        PutVanilla(path, oldText);
        PutA(path, newText);
        PutB(path, modText);
    }

    private string Updated(string path = Buildings)
    {
        var input = UpdateInput();
        WriteUpdate(input, RunUpdate(input));
        return ReadMod(path);
    }

    private void AssertNoOwnFiles() =>
        Assert.Empty(Directory.EnumerateFiles(BDir, "*pmt_compatch*", SearchOption.AllDirectories));

    [Fact]
    public void Vanilla_only_change_is_written_into_the_mod_file()
    {
        Case("temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 50\n}\n",
             "temple = {\n\tcost = 120\n}\nmarket = {\n\tcost = 50\n}\n",
             "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 75\n}\n");

        var text = Updated();

        Assert.Contains("cost = 120", text);
        Assert.Contains("cost = 75", text);
        AssertNoOwnFiles();
    }

    [Fact]
    public void Mod_only_change_stays()
    {
        Case("temple = {\n\tcost = 100\n}\n", "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 50\n}\n", "temple = {\n\tcost = 90\n}\n");

        Assert.Contains("cost = 90", Updated());
    }

    [Fact]
    public void Both_changed_is_a_conflict_preselected_with_the_mod()
    {
        Case("temple = {\n\tcost = 100\n}\n", "temple = {\n\tcost = 120\n}\n", "temple = {\n\tcost = 90\n}\n");
        var input = UpdateInput();
        var run = RunUpdate(input);

        var action = Assert.Single(run.Actions, a => a.DisplayPath.Contains("cost", StringComparison.Ordinal));
        Assert.Equal(MergeStage.Conflict, action.Stage);
        Assert.Equal(DecisionKind.TakeB, action.DefaultDecision);

        WriteUpdate(input, run);
        Assert.Contains("cost = 90", ReadMod(Buildings));
    }

    [Fact]
    public void New_vanilla_key_goes_into_the_mod_file()
    {
        Case("temple = {\n\tcost = 100\n}\n", "temple = {\n\tcost = 100\n}\nelven_hall = {\n\tcost = 300\n}\n", "temple = {\n\tcost = 90\n}\n");

        var text = Updated();

        Assert.Contains("elven_hall = {", text);
        Assert.Contains("cost = 90", text);
        AssertNoOwnFiles();
    }

    [Fact]
    public void Removed_vanilla_key_is_removed_from_the_mod_file()
    {
        Case("temple = {\n\tcost = 100\n}\nold_hall = {\n\tcost = 5\n}\n", "temple = {\n\tcost = 100\n}\n", "temple = {\n\tcost = 90\n}\nold_hall = {\n\tcost = 5\n}\n");

        var text = Updated();

        Assert.DoesNotContain("old_hall", text);
        Assert.Contains("cost = 90", text);
    }

    [Fact]
    public void Localization_line_changed_in_vanilla_is_replaced_in_the_mod_file()
    {
        const string Loc = "main_menu/localization/english/x_l_english.yml";
        var bom = Encoding.UTF8.GetPreamble();
        PutBytes(VanillaDir, Loc, bom.Concat(Encoding.UTF8.GetBytes("l_english:\n a:0 \"Alt\"\n b:0 \"Bee\"\n")).ToArray());
        PutBytes(ADir, Loc, bom.Concat(Encoding.UTF8.GetBytes("l_english:\n a:0 \"Neu\"\n b:0 \"Bee\"\n")).ToArray());
        PutBytes(BDir, Loc, bom.Concat(Encoding.UTF8.GetBytes("l_english:\n a:0 \"Alt\"\n b:0 \"Mine\"\n")).ToArray());
        var input = UpdateInput(euV: true);

        WriteUpdate(input, RunUpdate(input));

        var bytes = ModBytes(Loc);
        Assert.Equal(bom, bytes.Take(3).ToArray());
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("a:0 \"Neu\"", text);
        Assert.Contains("b:0 \"Mine\"", text);
        AssertNoOwnFiles();
    }

    [Fact]
    public void Unchanged_non_script_file_is_replaced_by_new_vanilla()
    {
        const string Data = "common/buildings/table.csv";
        PutBytes(VanillaDir, Data, new byte[] { 1, 2, 3 });
        PutBytes(ADir, Data, new byte[] { 1, 2, 4 });
        PutBytes(BDir, Data, new byte[] { 1, 2, 3 });
        var input = UpdateInput();

        WriteUpdate(input, RunUpdate(input));

        Assert.Equal(new byte[] { 1, 2, 4 }, ModBytes(Data));
    }

    [Fact]
    public void Second_run_deletes_nothing()
    {
        Case("temple = {\n\tcost = 100\n}\n", "temple = {\n\tcost = 120\n}\n", "temple = {\n\tcost = 100\n}\n");
        var input = UpdateInput();
        var first = WriteUpdate(input, RunUpdate(input));
        Assert.Empty(first.WholeFiles);

        var second = WriteUpdate(input, RunUpdate(input), previousFiles: new Dictionary<string, string> { [Buildings] = "egal" });

        Assert.DoesNotContain(second.Changes, c => c.Kind == FileChangeKind.Delete);
        Assert.True(File.Exists(ModPath(Buildings)));
        Assert.Contains("cost = 120", ReadMod(Buildings));
    }

    [Fact]
    public void No_bom_stays_no_bom_and_crlf_stays()
    {
        const string Gui = "interface/x.gui";
        Case("guiTypes = {\r\n\twindowType = {\r\n\t\tname = \"a\"\r\n\t\tsize = { x = 1 y = 1 }\r\n\t}\r\n}\r\n",
             "guiTypes = {\r\n\twindowType = {\r\n\t\tname = \"a\"\r\n\t\tsize = { x = 2 y = 2 }\r\n\t}\r\n}\r\n",
             "guiTypes = {\r\n\twindowType = {\r\n\t\tname = \"a\"\r\n\t\tsize = { x = 1 y = 1 }\r\n\t}\r\n\twindowType = {\r\n\t\tname = \"mine\"\r\n\t}\r\n}\r\n", Gui);

        var input = UpdateInput();
        WriteUpdate(input, RunUpdate(input));

        var bytes = ModBytes(Gui);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("x = 2 y = 2", text);
        Assert.Contains("\"mine\"", text);
        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\r\r", text);
        Assert.Equal(text.Split('\n').Length - 1, text.Split("\r\n").Length - 1);
    }

    [Fact]
    public void Key_defined_twice_in_mod_keeps_both_when_unchanged()
    {
        Case("temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 50\n}\n",
             "temple = {\n\tcost = 100\n}\nmarket = {\n\tcost = 60\n}\n",
             "temple = {\n\tcost = 100\n}\ntemple = {\n\tcost = 100\n\tupkeep = 1\n}\nmarket = {\n\tcost = 50\n}\n");

        var text = Updated();

        Assert.Equal(2, text.Split("temple = {").Length - 1);
        Assert.Contains("cost = 60", text);
    }
}
