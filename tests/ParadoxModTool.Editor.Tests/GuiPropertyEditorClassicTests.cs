using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Eigenschaftenfeld für klassische Elemente (HOI IV, EU IV): Eingabefelder, Aktionen und "Grundwert ändern".</summary>
public sealed class GuiPropertyEditorClassicTests
{
    private static readonly GuiLayout Classic = new(Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true)
    {
        VanillaRoots = new[] { string.Empty },
    };

    private static readonly string[] Structured = { "position", "show_position", "hide_position", "size", "borderSize", "margin", "slotsize", "if_resolution", "spacing" };

    // Vanilla für den Katalog: Orientation überwiegend so geschrieben, size ein Paar, spriteType mit Anführungszeichen.
    private const string VanillaGui =
        "guiTypes = {\n"
        + "\ticonType = { name = \"a\" Orientation = UPPER_LEFT position = { x = 1 y = 2 } size = { width = 3 height = 4 } spriteType = \"GFX_a\" }\n"
        + "\ticonType = { name = \"b\" Orientation = CENTER position = { x = 1 y = 2 } size = { width = 3 height = 4 } spriteType = \"GFX_a\" }\n"
        + "\ticonType = { name = \"c\" orientation = CENTER position = { x = 1 y = 2 } size = { width = 3 height = 4 } spriteType = \"GFX_a\" }\n"
        + "}\n";

    private const string VanillaGfx =
        "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_a\"\n\t\ttexturefile = \"gfx/interface/a.dds\"\n\t\tnoOfFrames = 3\n\t}\n}\n";

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Tree(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Stellt Format, Arbeitsbereich und die Auswahlfunktionen ein und danach alles zurück.</summary>
    private sealed class Env : IDisposable
    {
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-classic-editor-" + Guid.NewGuid().ToString("N"));
        private readonly GuiLayout _layout = GuiSettings.Layout;
        private readonly GuiWorkspace? _workspace = GuiSettings.Workspace;
        private readonly string _status = GuiSettings.Status;
        private readonly Func<string?, string?>? _picker = GuiSettings.SpritePicker;
        private readonly Func<string?, string?>? _creator = GuiSettings.SpriteCreator;

        public Env(bool workspace = true)
        {
            GuiSettings.Layout = Classic;
            if (!workspace)
            {
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(null, "test"));
                return;
            }

            Directory.CreateDirectory(Path.Combine(_temp, "install", "interface"));
            File.WriteAllText(Path.Combine(_temp, "install", "interface", "a.gui"), VanillaGui);
            File.WriteAllText(Path.Combine(_temp, "install", "interface", "a.gfx"), VanillaGfx);
            var rules = new GuiRules(Structured, Array.Empty<string>(), Array.Empty<string>(), Classic);
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(GuiWorkspace.Create(new GuiWorkspaceOptions(Path.Combine(_temp, "install"), null, Array.Empty<string>(), rules, null)), "test"));
        }

        public void Dispose()
        {
            GuiSettings.Layout = _layout;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(_workspace, _status));
            GuiSettings.SpritePicker = _picker;
            GuiSettings.SpriteCreator = _creator;
            if (Directory.Exists(_temp))
            {
                Directory.Delete(_temp, recursive: true);
            }
        }
    }

    private sealed record Shown(GuiPropertyEditor Editor, GuiNode Node, ResolvedElement Element, List<GuiDesignerAction> Actions);

    /// <summary>Editor mit dem ersten <paramref name="widget"/> der Datei und, wenn <paramref name="row"/> gesetzt ist, dessen Zeile gewählt.</summary>
    private static Shown Show(string gui, string widget, string? row, double screenHeight = 1080)
    {
        var document = GuiParser.Parse(gui);
        var node = document.Root.Descendants().First(n => n.Kind == GuiNodeKind.KeyedBlock && string.Equals(n.Name, widget, StringComparison.OrdinalIgnoreCase));
        var rules = new GuiRules(Structured, Array.Empty<string>(), Array.Empty<string>(), Classic);
        var element = new GuiResolver(GuiIndex.Empty, rules, document, null).Resolve(node);
        var editor = new GuiPropertyEditor();
        var actions = new List<GuiDesignerAction>();
        editor.ActionRequested += (_, action) => actions.Add(action);
        editor.SetElement(node, element, editable: true);
        if (row is not null)
        {
            var rows = ClassicPropertyRows.Build(element, 1920, screenHeight);
            editor.SetRow(rows.Last(r => string.Equals(r.Name, row, StringComparison.OrdinalIgnoreCase)));
        }

        return new Shown(editor, node, element, actions);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static Button ButtonLabelled(GuiPropertyEditor editor, string label) =>
        Tree(editor).OfType<Button>().Single(b => b.Content as string == label);

    private static IEnumerable<TextBlock> Texts(GuiPropertyEditor editor) => Tree(editor).OfType<TextBlock>().Where(t => t.Visibility == Visibility.Visible);

    private const string ButtonGui =
        "guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\tguiButtonType = {\n\t\t\tname = \"b\"\n\t\t\tposition = { x = 1 y = 2 }\n\t\t\tquadTextureSprite = \"GFX_old\"\n"
        + "\t\t\tif_resolution = {\n\t\t\t\tmin_height = 868\n\t\t\t\tposition = { x = 5 y = 6 }\n\t\t\t\tquadTextureSprite = \"GFX_big\"\n\t\t\t}\n\t\t}\n\t}\n}\n";

    [Fact]
    public void Anchor_combo_keeps_invalid_value_with_warning()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" Orientation = UPPERL_LEFT } }", "iconType", "Orientation");

            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            Assert.Equal("UPPERL_LEFT", combo.Text);
            Assert.Contains("UPPER_LEFT", combo.ItemsSource.Cast<string>());
            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicValueInvalid("UPPERL_LEFT"));

            // Der Hinweis folgt dem Text: ein gültiger Wert nimmt ihn weg, ein Alias bekommt seinen eigenen.
            combo.Text = "left";
            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicValueAlias("left"));
            combo.Text = "CENTER";
            Assert.DoesNotContain(Texts(shown.Editor), t => t.Text.Contains("CENTER") || t.Text == GuiTexts.ClassicValueAlias("left"));
        });
    }

    [Fact]
    public void Anchor_combo_does_not_recase_typed_text()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" Orientation = center } }", "iconType", "Orientation");
            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);

            Assert.False(combo.IsTextSearchEnabled);
            Assert.Equal("center", combo.Text);
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            Assert.Equal("center", shown.Actions.Single().Value);
        });
    }

    [Fact]
    public void Pair_apply_sends_SetPair_with_both_values()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show(ButtonGui, "guiButtonType", "position", screenHeight: 800);

            // Bei 800 Pixeln Höhe gilt der Basiswert; die Zeile gehört dem Element, die Schlüssel stehen als Beschriftung da.
            var boxes = Tree(shown.Editor).OfType<TextBox>().ToList();
            Assert.Equal(new[] { "1", "2" }, boxes.Select(b => b.Text));
            Assert.Contains(Texts(shown.Editor), t => t.Text == "x");
            Assert.Contains(Texts(shown.Editor), t => t.Text == "y");

            boxes[0].Text = "30";
            boxes[1].Text = "40%";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));

            var action = Assert.Single(shown.Actions);
            Assert.Equal(GuiDesignerActionKind.SetPair, action.Kind);
            Assert.Equal("position", action.Name);
            Assert.Equal("30", action.Value);
            Assert.Equal("40%", action.Value2);
            Assert.Equal(GuiNodeKind.KeyedBlock, action.Node!.Kind);
            Assert.Equal("position", action.Node.Name);
            Assert.Same(shown.Node, action.Node.Parent);
        });
    }

    [Fact]
    public void Pair_percent_button_cycles_and_invalid_input_is_refused()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show(ButtonGui, "guiButtonType", "position", screenHeight: 800);
            var statuses = new List<string>();
            shown.Editor.StatusChanged += (_, text) => statuses.Add(text);
            var boxes = Tree(shown.Editor).OfType<TextBox>().ToList();
            var buttons = Tree(shown.Editor).OfType<Button>().Where(b => b.ToolTip as string == GuiTexts.ClassicPercentTip).ToList();

            Assert.Equal(2, buttons.Count);
            Assert.Equal("px", buttons[0].Content);
            Click(buttons[0]);
            Assert.Equal("1%", boxes[0].Text);
            Assert.Equal("%", buttons[0].Content);
            Click(buttons[0]);
            Assert.Equal("1%%", boxes[0].Text);
            Assert.Equal("%%", buttons[0].Content);
            Click(buttons[0]);
            Assert.Equal("1", boxes[0].Text);

            boxes[1].Text = "abc";
            Assert.False(buttons[1].IsEnabled);
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            Assert.Empty(shown.Actions);
            Assert.Equal(GuiTexts.PropertyInvalid, Assert.Single(statuses));
        });
    }

    [Fact]
    public void New_pair_uses_element_as_target()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" } }", "iconType", row: null);
            Tree(shown.Editor).OfType<ComboBox>().Single().Text = "position";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyAdd));

            var boxes = Tree(shown.Editor).OfType<TextBox>().ToList();
            boxes[0].Text = "7";
            boxes[1].Text = "8";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));

            var action = Assert.Single(shown.Actions);
            Assert.Equal(GuiDesignerActionKind.SetPair, action.Kind);
            Assert.Same(shown.Node, action.Node);
            Assert.Equal(("position", "7", "8"), (action.Name, action.Value, action.Value2));
        });
    }

    [Fact]
    public void If_resolution_row_offers_base_value_button()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show(ButtonGui, "guiButtonType", "quadTextureSprite");

            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicEditsIfResolution("min_height = 868"));
            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            Assert.Equal("GFX_big", combo.Text);

            // "Übernehmen" schreibt in die Zeile im Block.
            combo.Text = "GFX_new";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            var inBlock = shown.Actions.Last();
            Assert.Equal(GuiDesignerActionKind.SetValue, inBlock.Kind);
            Assert.Equal("if_resolution", inBlock.Node!.Parent!.Name);
            Assert.Equal("\"GFX_new\"", inBlock.Value);

            // "Grundwert ändern" schickt dieselbe Eingabe an die Zeile direkt im Element.
            Click(ButtonLabelled(shown.Editor, GuiTexts.ClassicChangeBase));
            var baseValue = shown.Actions.Last();
            Assert.Equal(GuiDesignerActionKind.SetBaseValue, baseValue.Kind);
            Assert.Same(shown.Node, baseValue.Node);
            Assert.Equal("quadTextureSprite", baseValue.Name);
            Assert.Equal("\"GFX_new\"", baseValue.Value);
        });
    }

    [Fact]
    public void Plain_row_has_no_notice_and_no_base_value_button()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show(ButtonGui, "guiButtonType", "position", screenHeight: 800);

            Assert.DoesNotContain(Tree(shown.Editor).OfType<Button>(), b => b.Content as string == GuiTexts.ClassicChangeBase);
            Assert.DoesNotContain(Texts(shown.Editor), t => t.Text.Contains("if_resolution"));
        });
    }

    [Fact]
    public void If_resolution_pair_applies_into_the_block_and_base_edit_targets_the_element()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show(ButtonGui, "guiButtonType", "position");
            var boxes = Tree(shown.Editor).OfType<TextBox>().ToList();
            Assert.Equal(new[] { "5", "6" }, boxes.Select(b => b.Text));

            boxes[0].Text = "50";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            var inBlock = shown.Actions.Last();
            Assert.Equal(GuiDesignerActionKind.SetPair, inBlock.Kind);
            Assert.Equal("if_resolution", inBlock.Node!.Parent!.Name);

            // Der Grundwert eines Paars geht als SetPair auf das Element: nur die zwei Werte ändern sich, nicht der ganze Block.
            Click(ButtonLabelled(shown.Editor, GuiTexts.ClassicChangeBase));
            var baseValue = shown.Actions.Last();
            Assert.Equal(GuiDesignerActionKind.SetPair, baseValue.Kind);
            Assert.Same(shown.Node, baseValue.Node);
            Assert.Equal(("position", "50", "6"), (baseValue.Name, baseValue.Value, baseValue.Value2));
        });
    }

    /// <summary>Element mit Grundwert <paramref name="basePosition"/> (null = keiner) und einem if_resolution-Block, der bei 1080 Pixeln gilt.</summary>
    private static string PairGui(string? basePosition, string resolutionPosition) =>
        "guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\tguiButtonType = {\n\t\t\tname = \"b\"\n"
        + (basePosition is null ? string.Empty : $"\t\t\tposition = {basePosition}\n")
        + $"\t\t\tif_resolution = {{\n\t\t\t\tmin_height = 868\n\t\t\t\tposition = {resolutionPosition}\n\t\t\t}}\n\t\t}}\n\t}}\n}}\n";

    /// <summary>Gibt (10, 20) in die zwei Felder ein, klickt "Grundwert ändern" und wendet die Aktion auf den Text an.</summary>
    private static string ChangeBasePair(string gui)
    {
        var shown = Show(gui, "guiButtonType", "position");
        var boxes = Tree(shown.Editor).OfType<TextBox>().ToList();
        boxes[0].Text = "10";
        boxes[1].Text = "20";
        Click(ButtonLabelled(shown.Editor, GuiTexts.ClassicChangeBase));
        var action = shown.Actions.Last();
        return Compute(gui, d => action with { Node = Find(d, "guiButtonType") });
    }

    [Fact]
    public void Base_pair_edit_matches_keys_by_name_when_the_orders_differ()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var gui = PairGui("{ y = 1 x = 2 }", "{ x = 3 y = 4 }");

            // Angezeigt wird x = 3, y = 4 (Reihenfolge des if_resolution-Blocks); x = 10 und y = 20 landen im Grundwert bei ihren Namen.
            var result = ChangeBasePair(gui);

            Assert.Equal(gui.Replace("position = { y = 1 x = 2 }", "position = { y = 20 x = 10 }"), result);
        });
    }

    [Fact]
    public void Base_pair_edit_without_base_block_uses_the_shown_keys()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var gui = PairGui(null, "{ y = 3 x = 4 }");

            var result = ChangeBasePair(gui);

            // Neu angelegt mit den angezeigten Schlüsseln in ihrer Reihenfolge (nicht x/y aus dem Katalog).
            Assert.Contains("position = { y = 10 x = 20 }", result);
            Assert.Contains("position = { y = 3 x = 4 }", result);
        });
    }

    [Fact]
    public void Add_property_uses_preferred_spelling()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" } }", "iconType", row: null);

            var add = Tree(shown.Editor).OfType<ComboBox>().Single();
            Assert.Contains("Orientation", add.ItemsSource.Cast<string>());
            Assert.DoesNotContain("orientation", add.ItemsSource.Cast<string>());
            add.Text = "orientation";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyAdd));

            var anchor = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            anchor.Text = "CENTER";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));

            var action = Assert.Single(shown.Actions);
            Assert.Equal(GuiDesignerActionKind.SetProperty, action.Kind);
            Assert.Equal("Orientation", action.Name);
            Assert.Equal("CENTER", action.Value);
            Assert.Same(shown.Node, action.Node);
        });
    }

    [Fact]
    public void Add_property_finds_an_existing_line_without_regard_to_case()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" Orientation = CENTER } }", "iconType", row: null);
            Tree(shown.Editor).OfType<ComboBox>().Single().Text = "ORIENTATION";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyAdd));

            var anchor = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            Assert.Equal("CENTER", anchor.Text);
            anchor.Text = "UPPER_RIGHT";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));

            // Eine vorhandene Zeile wird geändert (SetValue), nicht noch einmal angelegt.
            Assert.Equal(GuiDesignerActionKind.SetValue, Assert.Single(shown.Actions).Kind);
        });
    }

    [Fact]
    public void Scalar_value_of_a_pair_name_gets_a_text_box()
    {
        Sta.Run(() =>
        {
            using var env = new Env();

            // Der Katalog kennt size als Paar; ein Skalar (size = 5) bekommt trotzdem ein brauchbares Feld.
            var shown = Show("guiTypes = { iconType = { name = \"i\" size = 5 } }", "iconType", "size");
            var box = Tree(shown.Editor).OfType<TextBox>().Single();
            Assert.Equal("5", box.Text);

            box.Text = "6";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            var action = Assert.Single(shown.Actions);
            Assert.Equal(GuiDesignerActionKind.SetValue, action.Kind);
            Assert.Equal(GuiNodeKind.Property, action.Node!.Kind);
            Assert.Equal("6", action.Value);
        });
    }

    [Fact]
    public void Block_row_keeps_show_in_code()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" if_resolution = { min_height = 900 } } }", "iconType", "if_resolution");
            var requested = new List<GuiNode>();
            shown.Editor.ShowInCodeRequested += (_, node) => requested.Add(node);

            Click(ButtonLabelled(shown.Editor, GuiTexts.TreeShowInCode));

            Assert.Equal("if_resolution", Assert.Single(requested).Name);
            Assert.False(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply).IsEnabled);
        });
    }

    [Fact]
    public void Frame_shows_range_and_hint_for_zero()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" spriteType = \"GFX_a\" frame = 0 } }", "iconType", "frame");

            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicFrameRange(3));
            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicFrameZero);

            var box = Tree(shown.Editor).OfType<TextBox>().Single();
            box.Text = "2";
            Assert.DoesNotContain(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicFrameZero);
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            Assert.Equal("2", Assert.Single(shown.Actions).Value);

            box.Text = "two";
            Click(ButtonLabelled(shown.Editor, GuiTexts.PropertyApply));
            Assert.Single(shown.Actions);
        });
    }

    [Fact]
    public void Localised_text_without_a_key_says_so()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { instantTextBoxType = { name = \"t\" text = \"Hello world\" } }", "instantTextBoxType", "text");

            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicLocNoKey);
        });
    }

    [Fact]
    public void Localised_text_with_a_key_does_not_block_the_ui_thread()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { instantTextBoxType = { name = \"t\" text = \"SOME_KEY\" } }", "instantTextBoxType", "text");

            // Der Nachschlag läuft im Hintergrund; bis er zurück ist, steht der Ladehinweis da.
            Assert.Contains(Texts(shown.Editor), t => t.Text == GuiTexts.ClassicLocLoading);
        });
    }

    [Fact]
    public void Unknown_sprite_offers_creation_and_buttons_follow_the_delegates()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            string? asked = null;
            GuiSettings.SpritePicker = null;
            GuiSettings.SpriteCreator = name => { asked = name; return "GFX_a"; };
            var shown = Show("guiTypes = { iconType = { name = \"i\" spriteType = \"GFX_nope\" } }", "iconType", "spriteType");

            var link = Tree(shown.Editor).OfType<Hyperlink>().Single();
            Assert.Equal(Visibility.Visible, ((TextBlock)link.Parent).Visibility);
            Assert.DoesNotContain(Tree(shown.Editor).OfType<Button>(), b => b.Content as string == GuiTexts.ClassicSpritePick);
            Assert.Contains(Tree(shown.Editor).OfType<Button>(), b => b.Content as string == GuiTexts.ClassicSpriteCreate);

            link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            Assert.Equal("GFX_nope", asked);
            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            Assert.Equal("GFX_a", combo.Text);

            // Ein bekannter Name braucht keinen Hinweis.
            Assert.Equal(Visibility.Collapsed, ((TextBlock)link.Parent).Visibility);
        });
    }

    [Fact]
    public void Sprite_created_while_editing_is_found_without_rebuilding_the_panel()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            GuiSettings.SpritePicker = null;
            GuiSettings.SpriteCreator = name =>
            {
                // Wie "Sprite anlegen": Die .gfx entsteht und der Arbeitsbereich wird durch einen mit neuer Sprite-Tabelle ersetzt.
                var options = GuiSettings.Workspace!.Options;
                File.WriteAllText(
                    Path.Combine(options.InstallDirectory, "interface", "new.gfx"),
                    "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_new\"\n\t\ttexturefile = \"gfx/interface/new.dds\"\n\t}\n}\n");
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(GuiWorkspace.Create(options), "test"));
                return "GFX_new";
            };
            var shown = Show("guiTypes = { iconType = { name = \"i\" spriteType = \"GFX_new\" } }", "iconType", "spriteType");
            var link = Tree(shown.Editor).OfType<Hyperlink>().Single();
            Assert.Equal(Visibility.Visible, ((TextBlock)link.Parent).Visibility);

            link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));

            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);
            Assert.Equal("GFX_new", combo.Text);
            Assert.Equal(Visibility.Collapsed, ((TextBlock)link.Parent).Visibility);
            Assert.Contains("GFX_new", combo.ItemsSource.Cast<string>());
        });
    }

    [Fact]
    public void Known_sprite_has_no_creation_link_and_picker_fills_the_box()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            GuiSettings.SpriteCreator = null;
            GuiSettings.SpritePicker = current => current == "GFX_a" ? "GFX_other" : null;
            var shown = Show("guiTypes = { iconType = { name = \"i\" spriteType = \"GFX_a\" } }", "iconType", "spriteType");

            Assert.Equal(Visibility.Collapsed, ((TextBlock)Tree(shown.Editor).OfType<Hyperlink>().Single().Parent).Visibility);
            Click(ButtonLabelled(shown.Editor, GuiTexts.ClassicSpritePick));
            Assert.Equal("GFX_other", Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180).Text);
        });
    }

    [Fact]
    public void Sprite_combo_lists_only_matching_names_and_keeps_typed_text()
    {
        Sta.Run(() =>
        {
            using var env = new Env();
            var shown = Show("guiTypes = { iconType = { name = \"i\" spriteType = \"GFX_a\" } }", "iconType", "spriteType");
            var combo = Tree(shown.Editor).OfType<ComboBox>().Single(c => c.Width != 180);

            combo.Text = "gfx";
            Assert.Equal("gfx", combo.Text);
            Assert.Equal(new[] { "GFX_a" }, combo.ItemsSource.Cast<string>());
            combo.Text = "nothing";
            Assert.Equal("nothing", combo.Text);
            Assert.Empty(combo.ItemsSource.Cast<string>());
        });
    }

    [Fact]
    public void Sprite_matches_put_prefix_first_and_cap_the_list()
    {
        var names = Enumerable.Range(0, 30000).Select(i => $"GFX_{(i % 2 == 0 ? "icon" : "button")}_{i:D5}").Order(StringComparer.Ordinal).ToList();
        names.Add("my_GFX_icon_tail");

        var matches = ClassicPropertyInputs.SpriteMatches(names, "gfx_icon");
        Assert.Equal(ClassicPropertyInputs.MaxSpriteChoices, matches.Count);
        Assert.All(matches, m => Assert.StartsWith("GFX_icon", m));

        // Reichen die Treffer am Anfang nicht, folgen die, die den Text enthalten.
        var few = ClassicPropertyInputs.SpriteMatches(new[] { "x_GFX_a", "GFX_a_1", "unrelated", "GFX_ab" }, " \"gfx_a\" ");
        Assert.Equal(new[] { "GFX_a_1", "GFX_ab", "x_GFX_a" }, few);
        Assert.Equal(3, ClassicPropertyInputs.SpriteMatches(new[] { "a", "b", "c", "d" }, string.Empty, 3).Count);
    }

    [Theory]
    [InlineData("TOPBAR_TITLE", true)]
    [InlineData("\t PROVINCEVIEW.NAME-1 ", true)]
    [InlineData("Hello world", false)]
    [InlineData("[GetName]", false)]
    [InlineData("$COUNTRY$", false)]
    [InlineData("", false)]
    public void Localisation_key_shape(string value, bool isKey) => Assert.Equal(isKey, ClassicPropertyInputs.LooksLikeKey(value));

    [Fact]
    public void Jomini_inputs_unchanged()
    {
        Sta.Run(() =>
        {
            var previous = GuiSettings.Layout;
            try
            {
                GuiSettings.Layout = GuiLayout.Default;
                var document = GuiParser.Parse("widget = {\n\tname = \"w\"\n\tsize = { 10 20 }\n\tmargin = { top = 1 }\n}\n");
                var node = document.Root.Children[0];
                var element = new GuiResolver(GuiIndex.Empty, new GuiRules(new[] { "margin" }, Array.Empty<string>(), Array.Empty<string>()), document, null).Resolve(node);
                var editor = new GuiPropertyEditor();
                var actions = new List<GuiDesignerAction>();
                editor.ActionRequested += (_, action) => actions.Add(action);
                editor.SetElement(node, element, editable: true);

                // size ist ein Zahlenpaar: zwei Felder mit %-Umschaltern wie bisher, kein px-Knopf, kein SetPair.
                editor.SetProperty(element.Property("size"));
                Assert.Equal(new[] { "10", "20" }, Tree(editor).OfType<TextBox>().Select(t => t.Text));
                Assert.Equal(2, Tree(editor).OfType<ToggleButton>().Count());
                Assert.DoesNotContain(Tree(editor).OfType<Button>(), b => b.ToolTip as string == GuiTexts.ClassicPercentTip);
                Click(ButtonLabelled(editor, GuiTexts.PropertyApply));
                Assert.Equal(GuiDesignerActionKind.SetValue, Assert.Single(actions).Kind);

                // Ein Block mit Schlüsseln bleibt ein Block: nur "Im Code zeigen".
                editor.SetProperty(element.StructuredBlocks("margin").First());
                Assert.Contains(Tree(editor).OfType<TextBlock>(), t => t.Text == GuiTexts.PropertyBlockReadOnly);
                Assert.Contains(Tree(editor).OfType<Button>(), b => b.Content as string == GuiTexts.TreeShowInCode);
            }
            finally
            {
                GuiSettings.Layout = previous;
            }
        });
    }

    [Fact]
    public void Row_selection_by_name_ignores_case_only_for_classic()
    {
        var document = GuiParser.Parse("w = { Orientation = CENTER }");
        var element = new GuiResolver(GuiIndex.Empty, new GuiRules(Structured, Array.Empty<string>(), Array.Empty<string>(), Classic), document, null).Resolve(document.Root.Children[0]);
        var rows = ClassicPropertyRows.Build(element, 1920, 1080);
        var previous = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = Classic;
            Assert.Equal("Orientation", GuiDesignerView.RowNamed(rows, "orientation")!.Name);
            Assert.Equal("Orientation", GuiDesignerView.RowNamed(rows, "Orientation")!.Name);
            GuiSettings.Layout = GuiLayout.Default;
            Assert.Null(GuiDesignerView.RowNamed(rows, "orientation"));
        }
        finally
        {
            GuiSettings.Layout = previous;
        }
    }

    private static string Compute(string source, Func<GuiDocument, GuiDesignerAction> action)
    {
        var document = GuiParser.Parse(source);
        var resolver = new GuiResolver(GuiIndex.Empty, new GuiRules(Structured, Array.Empty<string>(), Array.Empty<string>(), Classic), document, null);
        var edit = GuiDesignerController.Compute(action(document), document, document, resolver, 0);
        Assert.NotNull(edit);
        return TextEdit.Apply(document.Text, edit!.Edits);
    }

    private static GuiNode Find(GuiDocument document, string name, int index = 0) =>
        document.Root.Descendants().Where(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).ElementAt(index);

    [Fact]
    public void SetPair_keeps_the_keys_and_spacing_of_an_existing_block()
    {
        using var env = new Env(workspace: true);
        const string source = "guiTypes = { iconType = { name = \"i\" position = { left= 3 top = 4 } } }";

        // Zielknoten ist das Element: der vorhandene Block behält left/top (ohne ihn gäbe es x/y).
        var viaElement = Compute(source, d => new GuiDesignerAction(GuiDesignerActionKind.SetPair, Find(d, "iconType")) { Name = "position", Value = "30", Value2 = "40" });
        Assert.Equal("guiTypes = { iconType = { name = \"i\" position = { left= 30 top = 40 } } }", viaElement);

        // Zielknoten ist der Block selbst.
        var viaBlock = Compute(source, d => new GuiDesignerAction(GuiDesignerActionKind.SetPair, Find(d, "position")) { Name = "Position", Value = "30", Value2 = "40" });
        Assert.Equal("guiTypes = { iconType = { name = \"i\" position = { left= 30 top = 40 } } }", viaBlock);
    }

    [Fact]
    public void SetPair_in_if_resolution_block_edits_only_that_block()
    {
        using var env = new Env(workspace: true);
        var source = ButtonGui;

        var result = Compute(source, d => new GuiDesignerAction(GuiDesignerActionKind.SetPair, Find(d, "position", 1)) { Name = "position", Value = "50", Value2 = "60" });

        Assert.Equal(source.Replace("position = { x = 5 y = 6 }", "position = { x = 50 y = 60 }"), result);
    }

    [Fact]
    public void SetPair_on_a_new_size_uses_the_catalog_keys_and_on_a_scalar_writes_nothing()
    {
        using var env = new Env(workspace: true);

        var fresh = Compute("guiTypes = { iconType = { name = \"i\" } }", d => new GuiDesignerAction(GuiDesignerActionKind.SetPair, Find(d, "iconType")) { Name = "size", Value = "3", Value2 = "4" });
        Assert.Contains("size = { width = 3 height = 4 }", fresh);

        var scalar = "guiTypes = { iconType = { name = \"i\" size = 5 } }";
        Assert.Equal(scalar, Compute(scalar, d => new GuiDesignerAction(GuiDesignerActionKind.SetPair, Find(d, "size")) { Name = "size", Value = "3", Value2 = "4" }));
    }

    [Fact]
    public void SetBaseValue_changes_the_element_line_and_not_the_if_resolution_one()
    {
        using var env = new Env(workspace: false);

        var result = Compute(ButtonGui, d => new GuiDesignerAction(GuiDesignerActionKind.SetBaseValue, Find(d, "guiButtonType")) { Name = "quadtexturesprite", Value = "\"GFX_base\"" });

        Assert.Equal(ButtonGui.Replace("quadTextureSprite = \"GFX_old\"", "quadTextureSprite = \"GFX_base\""), result);
    }
}
