using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiSaveBlockWindowTests
{
    private static GuiSaveBlockWindow Dialog(string code, bool modAvailable = true) =>
        new(code, new[] { "Fenster", "Eigene Knöpfe" }, modAvailable, null);

    [Fact]
    public void Dialog_DetectsKeysAndComposesAValidBlock() => Sta.Run(() =>
    {
        var dialog = Dialog("\t\tbutton = {\r\n\t\t\tname = \"b\"\r\n\t\t}\r\n");
        Assert.Equal("button = {\n\tname = \"b\"\n}\n", dialog.CodeEditor.Text);

        dialog.CodeEditor.Text = dialog.CodeEditor.Text.Replace("\"b\"", "\"$name$\"");
        dialog.NameBox.Text = "Knopf";
        dialog.RefreshNow();

        Assert.Equal(new[] { new GuiBlockDraftKey("name", false) }, dialog.DetectedKeys);
        Assert.True(dialog.CanSave);
        Assert.Equal("Knopf", dialog.BlockName);
        var block = GuiBlockParser.Parse(dialog.ComposedText, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        Assert.Equal("name", block.Parameters[0].Key);
        Assert.Contains("button = {", GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    });

    [Fact]
    public void Dialog_KeepsRowValuesWhenTheCodeChanges() => Sta.Run(() =>
    {
        var dialog = Dialog("x = $$a$$");
        dialog.NameBox.Text = "A";
        dialog.CodeEditor.Text = "x = $name$\n$repeat rows$\ny = $rows.id$\n$end rows$\n";
        dialog.RefreshNow();
        dialog.SetRow("name", label: "Name", defaultValue: "my_name");
        dialog.SetRow("rows", label: "Zeilen", count: 3);

        dialog.CodeEditor.Text += "z = $name$\n";
        dialog.RefreshNow();

        var block = GuiBlockParser.Parse(dialog.ComposedText, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        Assert.Equal("Name", block.Parameters[0].Label);
        Assert.Equal("my_name", block.Parameters[0].Default);
        Assert.Equal(3, block.FindRepeat("rows")!.DefaultCount);
        Assert.Equal("Zeilen", block.FindRepeat("rows")!.Label);
    });

    [Fact]
    public void Dialog_WithLoneDollar_CannotSave() => Sta.Run(() =>
    {
        var dialog = Dialog("x = 5");
        dialog.NameBox.Text = "A";
        dialog.CodeEditor.Text = "x = 5$";
        dialog.RefreshNow();

        Assert.False(dialog.CanSave);
    });

    [Fact]
    public void Dialog_WithoutName_CannotSave() => Sta.Run(() =>
    {
        var dialog = Dialog("x = 5");
        dialog.RefreshNow();

        Assert.False(dialog.CanSave);
    });

    [Fact]
    public void Dialog_ModLayerDisabledWithoutMod() => Sta.Run(() =>
    {
        var dialog = Dialog("x = 5", modAvailable: false);

        Assert.Equal(GuiBlockLayer.Game, dialog.Layer);
        Assert.False(dialog.ModOption.IsEnabled);
    });

    [Fact]
    public void SaveBlock_ExistingName_AsksBeforeOverwrite()
    {
        var game = Path.Combine(Path.GetTempPath(), "pmt-saveblock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(game);
        try
        {
            File.WriteAllText(Path.Combine(game, "k.guiblock"), "# block: Knopf\nx\n");
            var library = new GuiBlockLibrary(null, game, null);
            var asked = new List<string>();

            Assert.False(GuiSaveBlockWindow.ConfirmOverwrite(library, "knopf", GuiBlockLayer.Game, name => { asked.Add(name); return false; }));
            Assert.True(GuiSaveBlockWindow.ConfirmOverwrite(library, "Knopf", GuiBlockLayer.Game, name => { asked.Add(name); return true; }));
            Assert.True(GuiSaveBlockWindow.ConfirmOverwrite(library, "Neu", GuiBlockLayer.Game, name => { asked.Add(name); return false; }));
            Assert.Equal(new[] { "knopf", "Knopf" }, asked);
        }
        finally
        {
            Directory.Delete(game, recursive: true);
        }
    }

    [Fact]
    public void Dialog_KeepsItsSize() => Sta.Run(() =>
    {
        var dialog = Dialog("x = $name$");
        dialog.NameBox.Text = "A";
        dialog.RefreshNow();

        WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(dialog);
    });
}
