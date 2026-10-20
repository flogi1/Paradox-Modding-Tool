using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiSettingsClassicTests
{
    private static readonly GuiLayout Classic = GuiLayout.Default with { Dialect = GuiDialect.Classic };

    [Fact]
    public void Classic_vanilla_gui_under_install_is_read_only()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-classic-vanilla-" + Guid.NewGuid().ToString("N"));
        var previousInstall = GuiSettings.InstallDirectory;
        var previousLayout = GuiSettings.Layout;
        GuiSettings.InstallDirectory = install;
        try
        {
            var vanilla = new OpenDocument(Path.Combine(install, "interface", "topbar.gui"), "guiTypes = { }", null);
            var mod = new OpenDocument(Path.Combine(install + "-mod", "interface", "topbar.gui"), "guiTypes = { }", null);

            // Jomini kennt nur game/jomini/clausewitz: interface/ direkt in der Installation ist dort keine Vanilla-Datei.
            GuiSettings.Layout = GuiLayout.Default;
            Assert.False(GuiSettings.IsVanillaGui(vanilla));

            GuiSettings.Layout = Classic;
            Assert.True(GuiSettings.IsVanillaGui(vanilla));
            Assert.False(GuiSettings.IsVanillaGui(mod));
        }
        finally
        {
            GuiSettings.InstallDirectory = previousInstall;
            GuiSettings.Layout = previousLayout;
        }
    }

    [Fact]
    public void Classic_engine_without_workspace_lays_out_schematic_boxes()
    {
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = Classic;
            var doc = GuiParser.Parse("guiTypes = { containerWindowType = { name = \"w\" size = { width = 200 height = 100 } iconType = { name = \"i\" spriteType = \"GFX_missing\" } } }");
            var window = doc.Root.Descendants().First(n => n.Name == "containerWindowType");
            var rules = new GuiRules(new[] { "position", "size" }, Array.Empty<string>(), Array.Empty<string>(), Classic);
            var element = new GuiResolver(GuiIndex.Empty, rules, doc, null).Resolve(window);

            var engine = GuiSettings.LayoutEngineFactory(null)();
            var box = engine.Layout(element);

            Assert.IsType<ClassicLayoutEngine>(engine);
            Assert.Equal(200, box.Width);
            Assert.Equal(100, box.Height);

            GuiSettings.Layout = GuiLayout.Default;
            Assert.IsType<GuiLayoutEngine>(GuiSettings.LayoutEngineFactory(null)());
        }
        finally
        {
            GuiSettings.Layout = previous;
        }
    }

    [Fact]
    public void Classic_install_without_interface_folder_is_reported()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-classic-nointerface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(install);
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = Classic;
            Assert.True(GuiSettings.ClassicInterfaceMissing(install));
            Assert.True(GuiSettings.ClassicInterfaceMissing(null));

            Directory.CreateDirectory(Path.Combine(install, "interface"));
            Assert.False(GuiSettings.ClassicInterfaceMissing(install));

            // Jomini: kein Hinweis, dort liegt die GUI unter game/gui.
            Directory.Delete(Path.Combine(install, "interface"));
            GuiSettings.Layout = GuiLayout.Default;
            Assert.False(GuiSettings.ClassicInterfaceMissing(install));
        }
        finally
        {
            GuiSettings.Layout = previous;
            Directory.Delete(install, recursive: true);
        }
    }

    [Fact]
    public void Tree_menu_offers_no_jomini_edits_for_classic()
    {
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = GuiLayout.Default;
            Assert.True(GuiDesignerView.OffersWrapIn(GuiTreeItemKind.Widget));
            Assert.True(GuiDesignerView.OffersWrapIn(GuiTreeItemKind.Holder));
            Assert.True(GuiDesignerView.OffersTypeEdits(GuiTreeItemKind.Widget));
            Assert.False(GuiDesignerView.OffersTypeEdits(GuiTreeItemKind.Holder));

            // Klassisch: "In hbox/vbox einpacken" schriebe hbox = { iconType = { … } }, das die Engine nicht kennt.
            GuiSettings.Layout = Classic;
            Assert.False(GuiDesignerView.OffersWrapIn(GuiTreeItemKind.Widget));
            Assert.False(GuiDesignerView.OffersWrapIn(GuiTreeItemKind.Holder));
            Assert.False(GuiDesignerView.OffersTypeEdits(GuiTreeItemKind.Widget));
        }
        finally
        {
            GuiSettings.Layout = previous;
        }
    }

    [Fact]
    public void Data_helper_not_offered_for_classic()
    {
        var previous = GuiSettings.Layout;
        var previousTools = GuiSettings.ToolsEnabled;
        try
        {
            GuiSettings.SetToolsEnabled(true);
            GuiSettings.Layout = GuiLayout.Default;
            Assert.True(GuiSettings.DataHelperOffered);

            GuiSettings.Layout = Classic;
            Assert.False(GuiSettings.DataHelperOffered);
        }
        finally
        {
            GuiSettings.Layout = previous;
            GuiSettings.SetToolsEnabled(previousTools);
        }
    }

    [Fact]
    public void Drafts_offered_for_classic()
    {
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = GuiLayout.Default;
            Assert.True(GuiSettings.DraftsOffered);

            // Klassisch übernimmt der klassische Planer die geänderten Fenster (Plan 2, Task 11).
            GuiSettings.Layout = Classic;
            Assert.True(GuiSettings.DraftsOffered);
        }
        finally
        {
            GuiSettings.Layout = previous;
        }
    }

    [Fact]
    public void Type_copy_not_offered_for_classic()
    {
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = GuiLayout.Default;
            Assert.True(GuiSettings.TypeCopyOffered);

            // HOI IV und EU IV kennen keine Typen: "Als neuen Typ übernehmen" bleibt Jomini.
            GuiSettings.Layout = Classic;
            Assert.False(GuiSettings.TypeCopyOffered);
        }
        finally
        {
            GuiSettings.Layout = previous;
        }
    }

    [Fact]
    public void Blocks_and_new_gui_file_not_offered_for_classic()
    {
        var previous = GuiSettings.Layout;
        var previousTools = GuiSettings.ToolsEnabled;
        try
        {
            GuiSettings.SetToolsEnabled(true);
            GuiSettings.Layout = GuiLayout.Default;
            Assert.True(GuiSettings.BlocksOffered);
            Assert.True(GuiSettings.NewFileOffered);

            // Klassisch: Bausteine sind Jomini-Code, "Neue GUI-Datei" schriebe nach <mod>/gui, das weder Spiel noch Index lesen (Plan 3).
            GuiSettings.Layout = Classic;
            Assert.False(GuiSettings.BlocksOffered);
            Assert.False(GuiSettings.NewFileOffered);

            // Ausgeschaltete GUI-Werkzeuge (Hauptschalter) prüft der Test nicht: SetToolsEnabled(false) meldet Changed an Arbeitsbereiche anderer Tests
            // auf deren Thread.
        }
        finally
        {
            GuiSettings.Layout = previous;
            GuiSettings.SetToolsEnabled(previousTools);
        }
    }

    [Fact]
    public void Designer_not_offered_without_gui_section()
    {
        var previous = GuiSettings.ProfileHasGui;
        try
        {
            GuiSettings.ProfileHasGui = true;
            Assert.True(GuiSettings.OffersDesigner(@"C:\mod\interface\topbar.gui"));
            Assert.False(GuiSettings.OffersDesigner(@"C:\mod\common\x.txt"));

            // Stellaris: kein gui-Abschnitt im Profil, .gui bleibt reiner Code.
            GuiSettings.ProfileHasGui = false;
            Assert.False(GuiSettings.OffersDesigner(@"C:\mod\interface\topbar.gui"));
        }
        finally
        {
            GuiSettings.ProfileHasGui = previous;
        }
    }
}
