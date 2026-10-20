using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Bedingte Elemente (<c>visible = "[…]"</c>) in Designer und Vorschau: Markierung, Ausblenden im Baum, Banner mit Zahl und "Alle einblenden"
/// (Florian 2026-10-18).
/// </summary>
public sealed class GuiConditionalUiTests
{
    private const string WindowGui =
        "window = { name = \"main\" size = { 100 100 } widget = { name = \"cond\" visible = \"[IsShown]\" size = { 10 10 } } }";

    private const string Library =
        "types T { type my_panel = widget { size = { 100 100 } widget = { name = \"inner\" visible = \"[X]\" size = { 10 10 } } } }";

    /// <summary>Eigene Datei je Test, damit der prozessweite Bestand anderer Tests unberührt bleibt.</summary>
    private static string NewFile() => Path.Combine(Path.GetTempPath(), "pmt-conditional-" + Guid.NewGuid().ToString("N"), "a.gui");

    private sealed record Setup(GuiDesignerView View, GuiDocument Document, GuiResolver Resolver, GuiPreview Preview, string? File)
    {
        public GuiNode Widget => Document.Root.Children[0].Children.First(c => c.Name == "widget");
    }

    /// <summary>Baum und Vorschau wie nach einem Vorschaulauf des Controllers (Optionen aus der Ansicht, also mit dem Schnappschuss der Schlüssel).</summary>
    private static Setup Designer(string text, string? file, string library = "", GuiDesignerView? view = null, Func<string, bool>? known = null)
    {
        var source = new GuiSourceFile(@"C:\test\Vanilla\gui\library.gui", "gui/library.gui", GuiLayer.Vanilla, "Vanilla", 0, 0);
        var index = GuiIndex.Build(new[] { source }, _ => library);
        var document = GuiParser.Parse(text);
        var resolver = new GuiResolver(index, GuiRules.Empty, document, file);
        view ??= new GuiDesignerView();
        view.SetTree(GuiElementTree.Build(document, resolver, known));
        var preview = Rerun(view, resolver, document, file);
        return new Setup(view, document, resolver, preview, file);
    }

    private static GuiPreview Rerun(GuiDesignerView view, GuiResolver resolver, GuiDocument document, string? file)
    {
        var preview = GuiPreviewBuilder.Build(
            resolver, () => new GuiLayoutEngine(GuiRules.Empty, _ => null), document, document.Root.Children[0], view.PreviewOptions, default, file);
        view.SetPreview(preview, keepView: false, viewedFile: file);
        return preview;
    }

    private static MenuItem? Entry(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>().FirstOrDefault(m => (m.Header as TextBlock)?.Text == header);

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void Hiding_from_tree_menu_adds_key_and_shows_banner()
    {
        var file = NewFile();
        try
        {
            Sta.Run(() =>
            {
                var setup = Designer(WindowGui, file);
                var key = GuiConditionalKeys.FilePrefix(file) + "KeyedBlock:window:0|widget:cond:0";
                Assert.True(setup.View.TreeItemConditional(setup.Widget));
                Assert.False(setup.View.TreeItemConditional(setup.Document.Root.Children[0]));

                var hide = Entry(setup.View.TreeMenuFor(setup.Widget), GuiTexts.DesignerHideConditional);
                Assert.NotNull(hide);
                Click(hide);

                Assert.Contains(key, GuiSettings.HiddenConditionals);
                Assert.Equal(GuiTexts.DesignerHiddenConditionals(1), setup.View.ConditionalBannerText);
                Assert.True(setup.View.ShowAllConditionalsButton.IsEnabled);
                Assert.True(setup.View.TreeItemHidden(setup.Widget));

                // Der nächste Vorschaulauf bekommt den Schlüssel als Kopie und legt das Element nicht mehr.
                var options = setup.View.PreviewOptions;
                Assert.Contains(key, options.HiddenConditionals);
                Assert.NotSame(GuiSettings.HiddenConditionals, options.HiddenConditionals);
                var rerun = Rerun(setup.View, setup.Resolver, setup.Document, file);
                Assert.Empty(rerun.Scene!.Root.Children);

                var menu = setup.View.TreeMenuFor(setup.Widget);
                Assert.Null(Entry(menu, GuiTexts.DesignerHideConditional));
                var unhide = Entry(menu, GuiTexts.DesignerUnhideConditional);
                Assert.NotNull(unhide);
                Click(unhide);

                Assert.DoesNotContain(key, GuiSettings.HiddenConditionals);
                Assert.Null(setup.View.ConditionalBannerText);
            });
        }
        finally
        {
            GuiSettings.ShowAllConditionals(file);
        }
    }

    [Fact]
    public void Show_all_clears_only_the_current_files_keys()
    {
        var file = NewFile();
        var other = NewFile();
        var otherKey = GuiConditionalKeys.FilePrefix(other) + "KeyedBlock:window:0|widget:cond:0";
        try
        {
            Sta.Run(() =>
            {
                var setup = Designer(WindowGui, file);
                Click(Entry(setup.View.TreeMenuFor(setup.Widget), GuiTexts.DesignerHideConditional)!);
                GuiSettings.SetConditionalHidden(otherKey, true);
                Assert.Equal(1, GuiSettings.HiddenCount(file));

                Click(setup.View.ConditionalBannerButton);

                Assert.Equal(0, GuiSettings.HiddenCount(file));
                Assert.Contains(otherKey, GuiSettings.HiddenConditionals);
                Assert.Null(setup.View.ConditionalBannerText);
                Assert.False(setup.View.ShowAllConditionalsButton.IsEnabled);
            });
        }
        finally
        {
            GuiSettings.ShowAllConditionals(file);
            GuiSettings.ShowAllConditionals(other);
        }
    }

    [Fact]
    public void Banner_is_hidden_when_nothing_is_hidden()
    {
        var file = NewFile();
        var otherKey = GuiConditionalKeys.FilePrefix(NewFile()) + "KeyedBlock:window:0|widget:cond:0";
        try
        {
            Sta.Run(() =>
            {
                // Ein ausgeblendetes Element einer anderen Datei zählt hier nicht.
                GuiSettings.SetConditionalHidden(otherKey, true);
                var setup = Designer(WindowGui, file);

                Assert.Null(setup.View.ConditionalBannerText);
                Assert.False(setup.View.ShowAllConditionalsButton.IsEnabled);

                // Ohne Dateipfad: weder Ausblenden noch Banner (die Schlüssel hängen an der Datei).
                var unsaved = Designer(WindowGui, null);
                Assert.Null(Entry(unsaved.View.TreeMenuFor(unsaved.Widget), GuiTexts.DesignerHideConditional));
                Assert.Null(unsaved.View.ConditionalBannerText);
                Assert.False(unsaved.View.ShowAllConditionalsButton.IsEnabled);
            });
        }
        finally
        {
            GuiSettings.SetConditionalHidden(otherKey, false);
        }
    }

    [Fact]
    public void Banner_counts_hidden_type_body_children()
    {
        var file = NewFile();
        try
        {
            Sta.Run(() =>
            {
                // Das Kind stammt aus dem Typ-Rumpf der Bibliothek, hat also keinen Baumeintrag; sein Schlüssel hängt trotzdem an der angesehenen Datei.
                var setup = Designer("my_panel = { }", file, Library);
                var key = Assert.Single(setup.Preview.Scene!.Root.ConditionalKeys.Values);
                Assert.StartsWith(GuiConditionalKeys.FilePrefix(file), key, StringComparison.Ordinal);

                GuiSettings.SetConditionalHidden(key, true);
                var rerun = Rerun(setup.View, setup.Resolver, setup.Document, file);

                Assert.Empty(rerun.Scene!.Root.Children);
                Assert.Equal(GuiTexts.DesignerHiddenConditionals(1), setup.View.ConditionalBannerText);
            });
        }
        finally
        {
            GuiSettings.ShowAllConditionals(file);
        }
    }

    [Fact]
    public void Hidden_change_reruns_the_preview_of_a_loaded_designer_only()
    {
        var file = NewFile();
        var key = GuiConditionalKeys.FilePrefix(file) + "KeyedBlock:window:0|widget:cond:0";
        try
        {
            Sta.Run(() =>
            {
                var view = new GuiDesignerView();
                var reruns = 0;
                view.PreviewOptionsChanged += (_, _) => reruns++;
                var window = new Window { Content = view, Left = -3000, Top = -3000, Width = 800, Height = 600, ShowActivated = false };
                window.Show();
                try
                {
                    WpfWindows.Pump();
                    Designer(WindowGui, file, view: view);

                    GuiSettings.SetConditionalHidden(key, true);
                    WpfWindows.Pump(3);

                    Assert.Equal(1, reruns);
                    Assert.Equal(GuiTexts.DesignerHiddenConditionals(1), view.ConditionalBannerText);
                }
                finally
                {
                    window.Close();
                }

                // Geschlossen: abgemeldet, kein weiterer Lauf.
                WpfWindows.Pump();
                GuiSettings.SetConditionalHidden(key, false);
                WpfWindows.Pump(3);
                Assert.Equal(1, reruns);
            });
        }
        finally
        {
            GuiSettings.SetConditionalHidden(key, false);
        }
    }

    [Fact]
    public void Inspector_hides_from_its_tree_and_shows_the_banner_of_the_inspected_file()
    {
        var file = NewFile();
        try
        {
            Sta.Run(() =>
            {
                // Der Inspektor setzt seine Ergebnisse über den Synchronisationskontext des Fensters zurück.
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                var document = GuiParser.Parse(WindowGui);
                var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
                inspector.Show();
                try
                {
                    inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, file));
                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (inspector.TreeElements.All(e => e.Name != "cond") || inspector.TreeElements.All(e => inspector.TreeMark(e) is null))
                    {
                        Assert.True(DateTime.UtcNow < deadline, "Inspektor hat nicht geladen");
                        WpfWindows.Pump();
                        Thread.Sleep(10);
                    }

                    var cond = inspector.TreeElements.Single(e => e.Name == "cond");
                    var main = inspector.TreeElements.Single(e => e.Name == "main");
                    Assert.False(inspector.TreeMark(cond));
                    Assert.Null(inspector.TreeMark(main));
                    Assert.Empty(inspector.TreeMenuFor(main).Items);
                    Assert.Null(inspector.ConditionalBannerText);

                    Click(Entry(inspector.TreeMenuFor(cond), GuiTexts.DesignerHideConditional)!);

                    Assert.Contains(GuiConditionalKeys.FilePrefix(file) + "KeyedBlock:window:0|widget:cond:0", GuiSettings.HiddenConditionals);
                    Assert.True(inspector.TreeMark(cond));
                    Assert.Equal(GuiTexts.DesignerHiddenConditionals(1), inspector.ConditionalBannerText);
                    Assert.NotNull(Entry(inspector.TreeMenuFor(cond), GuiTexts.DesignerUnhideConditional));

                    Click(inspector.ConditionalBannerButton);

                    Assert.Equal(0, GuiSettings.HiddenCount(file));
                    Assert.Null(inspector.ConditionalBannerText);
                    Assert.False(inspector.TreeMark(cond));
                }
                finally
                {
                    inspector.Close();
                }
            });
        }
        finally
        {
            GuiSettings.ShowAllConditionals(file);
        }
    }

    [Fact]
    public void Inspector_change_during_load_is_applied_to_the_preview()
    {
        var file = NewFile();
        var key = GuiConditionalKeys.FilePrefix(file) + "KeyedBlock:window:0|widget:cond:0";
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                var document = GuiParser.Parse(WindowGui);
                var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
                inspector.Show();
                try
                {
                    // Ausgeblendet, bevor der Lauf fertig ist: Relayout findet noch kein Element, das Ergebnis des Laufs hat den alten Schnappschuss.
                    inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, file));
                    GuiSettings.SetConditionalHidden(key, true);

                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (inspector.PreviewScene is not { } scene || scene.Root.Children.Count > 0)
                    {
                        Assert.True(DateTime.UtcNow < deadline, "Vorschau ohne das ausgeblendete Element kam nicht");
                        WpfWindows.Pump();
                        Thread.Sleep(10);
                    }
                }
                finally
                {
                    inspector.Close();
                }
            });
        }
        finally
        {
            GuiSettings.SetConditionalHidden(key, false);
        }
    }

    [Fact]
    public void Show_all_button_is_hidden_for_classic_and_has_a_tooltip()
    {
        var previousLayout = GuiSettings.Layout;
        try
        {
            Sta.Run(() =>
            {
                GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
                var classic = new GuiDesignerView();
                Assert.Equal(Visibility.Collapsed, classic.ShowAllConditionalsButton.Visibility);

                GuiSettings.Layout = GuiLayout.Default;
                var jomini = new GuiDesignerView();
                Assert.Equal(Visibility.Visible, jomini.ShowAllConditionalsButton.Visibility);
                Assert.Equal(GuiTexts.DesignerShowAllConditionalsTip, jomini.ShowAllConditionalsButton.ToolTip);

                // Formatwechsel einer offenen Ansicht: SyncScreenTools läuft vor jeder Vorschau.
                GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
                jomini.SyncScreenTools();
                Assert.Equal(Visibility.Collapsed, jomini.ShowAllConditionalsButton.Visibility);
            });
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
        }
    }

    [Fact]
    public void Banner_text_uses_the_singular_for_one_element()
    {
        Assert.Equal(GuiTexts.DesignerHiddenConditional, GuiTexts.DesignerHiddenConditionals(1));
        Assert.Contains("2", GuiTexts.DesignerHiddenConditionals(2), StringComparison.Ordinal);
        Assert.NotEqual(GuiTexts.DesignerHiddenConditional.Replace("1", "2", StringComparison.Ordinal), GuiTexts.DesignerHiddenConditionals(2));
    }

    [Fact]
    public void Hidden_unknown_tree_item_keeps_its_danger_colour()
    {
        var file = NewFile();
        try
        {
            Sta.Run(() =>
            {
                const string text = "window = { name = \"main\" size = { 100 100 } mystery = { name = \"cond\" visible = \"[X]\" size = { 10 10 } } }";
                var view = new GuiDesignerView();
                var danger = new SolidColorBrush(Colors.Red);
                var muted = new SolidColorBrush(Colors.Gray);
                view.Resources["Theme.Danger"] = danger;
                view.Resources["Theme.TextMuted"] = muted;
                var setup = Designer(text, file, view: view, known: key => key != "mystery");
                var node = setup.Document.Root.Children[0].Children.First(c => c.Name == "mystery");
                Assert.True(setup.View.TreeItemConditional(node));

                Click(Entry(setup.View.TreeMenuFor(node), GuiTexts.DesignerHideConditional)!);

                var label = setup.View.TreeLabel(node)!;
                Assert.True(setup.View.TreeItemHidden(node));
                Assert.Equal(TextDecorations.Strikethrough, label.TextDecorations);
                Assert.Same(danger, label.Foreground);

                // Ein bekanntes ausgeblendetes Element wird gedämpft.
                var known = Designer(WindowGui, file, view: new GuiDesignerView { Resources = { ["Theme.TextMuted"] = muted } });
                Click(Entry(known.View.TreeMenuFor(known.Widget), GuiTexts.DesignerHideConditional)!);
                Assert.Same(muted, known.View.TreeLabel(known.Widget)!.Foreground);
            });
        }
        finally
        {
            GuiSettings.ShowAllConditionals(file);
        }
    }

    [Fact]
    public void Inspector_banner_follows_the_new_target_right_away()
    {
        var file = NewFile();
        var key = GuiConditionalKeys.FilePrefix(file) + "KeyedBlock:window:0|widget:cond:0";
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                GuiSettings.SetConditionalHidden(key, true);
                var document = GuiParser.Parse(WindowGui);
                var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
                inspector.Show();
                try
                {
                    inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, file));
                    Assert.Equal(GuiTexts.DesignerHiddenConditionals(1), inspector.ConditionalBannerText);

                    // Neues Ziel in einer anderen Datei: das Banner passt sofort, nicht erst nach dem (vielleicht fehlschlagenden) Lauf.
                    inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, NewFile()));
                    Assert.Null(inspector.ConditionalBannerText);
                }
                finally
                {
                    inspector.Close();
                }
            });
        }
        finally
        {
            GuiSettings.SetConditionalHidden(key, false);
        }
    }

    [Fact]
    public void Conditional_box_draws_marker()
    {
        Sta.Run(() =>
        {
            var marker = Color.FromRgb(0x00, 0xC8, 0x00);
            var expected = unchecked((int)0xFF00C800);

            // Szene 50×50 auf 204×204: Zoom 3,6, Kasten auf dem Bildschirm 12..192; die Marke sitzt in der Ecke oben rechts.
            Assert.Equal(expected, Render(conditional: true, marker));
            Assert.NotEqual(expected, Render(conditional: false, marker));
        });
    }

    /// <summary>Farbe (BGRA) des Pixels 2 px links und 1 px unter der rechten oberen Kastenecke.</summary>
    private static int Render(bool conditional, Color marker)
    {
        var node = GuiParser.Parse("widget = { }").Root.Children[0];
        var element = new ResolvedElement
        {
            Key = "widget",
            Role = GuiRole.EngineWidget,
            BaseWidget = "widget",
            TypeChain = new[] { "widget" },
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
        var box = new LayoutBox { Element = element, Width = 50, Height = 50, Conditional = conditional, ConditionalKey = conditional ? "k" : null };
        var canvas = new GuiSchematicCanvas();
        canvas.Resources["Theme.Warning"] = new SolidColorBrush(marker);
        canvas.SetScene(new GuiSchematicScene(box, _ => false), keepView: false);
        canvas.Measure(new Size(204, 204));
        canvas.Arrange(new Rect(0, 0, 204, 204));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(204, 204, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new int[204 * 204];
        bitmap.CopyPixels(pixels, 204 * 4, 0);
        return pixels[13 * 204 + 190];
    }
}
