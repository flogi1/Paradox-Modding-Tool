using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Erklärungsfeld unter der Eingabe im klassischen Eigenschaftenfeld: Texte, Quellen, Beispiele und Skizzen.</summary>
public sealed class ClassicExplainBoxTests : IDisposable
{
    private static readonly GuiLayout Classic = new(Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true)
    {
        VanillaRoots = new[] { string.Empty },
    };

    private static readonly string[] Structured = { "position", "size", "borderSize", "if_resolution" };

    private const string Json = """
        {
          "version": 1,
          "entries": [
            {
              "kind": "property", "key": "orientation",
              "text": { "de": "Anker im Elternelement.", "en": "Anchor in the parent." },
              "default": "UPPER_LEFT",
              "pitfalls": { "de": ["Nicht gespiegelt."], "en": ["Never mirrored."] },
              "sketch": "anchors",
              "sources": [ { "kind": "measured", "date": "2026-10-17" }, { "kind": "wiki" } ]
            },
            {
              "kind": "property", "key": "origo",
              "text": { "de": "Ankerpunkt im Element.", "en": "Anchor in the element." },
              "sources": [ { "kind": "unsure", "note": "nur aus Mod Utilities" } ]
            },
            {
              "kind": "element", "key": "iconType",
              "text": { "de": "Ein Bild.", "en": "A picture." },
              "children": [ "background", "position" ],
              "sources": [ { "kind": "vanilla" } ]
            }
          ]
        }
        """;

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-explain-box-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private ClassicExplanations Explanations()
    {
        Directory.CreateDirectory(_temp);
        var path = Path.Combine(_temp, "classic.json");
        File.WriteAllText(path, Json);
        var loaded = ClassicExplanations.Load(path, "hoi4");
        Assert.Empty(loaded.Problems);
        return loaded;
    }

    private static ResolvedElement Element()
    {
        var document = GuiParser.Parse("guiTypes = { iconType = { name = \"i\" Orientation = CENTER } }");
        var node = document.Root.Descendants().First(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name == "iconType");
        return new GuiResolver(GuiIndex.Empty, new GuiRules(Structured, Array.Empty<string>(), Array.Empty<string>(), Classic), document, null).Resolve(node);
    }

    // Vanilla-Dateien mit je einem Fenster (Zeile 4: orientation); so viele Dateien, so viele Beispiele (der Index kappt auf drei).
    private static ClassicExampleIndex Examples(int files)
    {
        var list = Enumerable.Range(0, files)
            .Select(i => new GuiSourceFile($@"C:\test\Vanilla\interface\f{i}.gui", $"interface/f{i}.gui", GuiLayer.Vanilla, "Vanilla", 0, 0))
            .ToList();
        var index = GuiIndex.Build(list, file => "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w_" + file.RelativePath + "\"\n\t\torientation = UPPER_LEFT\n\t}\n}\n", Classic);
        return ClassicExampleIndex.Build(index);
    }

    private ClassicExplainBox Box(Task<ClassicExampleIndex>? examples = null, string language = "de") => new()
    {
        LanguageSource = () => language,
        ExplanationSource = Explanations,
        ExampleSource = () => examples,
    };

    private static string AllText(DependencyObject root)
    {
        var parts = new List<string>();
        void Visit(DependencyObject node)
        {
            if (node is TextBlock block && block.Visibility == Visibility.Visible)
            {
                parts.Add(block.Text);
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                if (child is not Inline)
                {
                    Visit(child);
                }
            }
        }

        Visit(root);
        return string.Join("\n", parts);
    }

    private static List<Button> Links(DependencyObject root)
    {
        var links = new List<Button>();
        void Visit(DependencyObject node)
        {
            if (node is Button { Tag: ClassicExample } link)
            {
                links.Add(link);
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Visit(child);
            }
        }

        Visit(root);
        return links;
    }

    private static void Pump(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });
            Thread.Sleep(10);
        }
    }

    private static void PumpFor(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void Shows_property_text_in_ui_language()
    {
        Sta.Run(() =>
        {
            var german = Box(language: "de");
            german.Show("Orientation", "CENTER", Element(), null);
            var text = AllText(german);
            Assert.Contains("Anker im Elternelement.", text);
            Assert.Contains("Nicht gespiegelt.", text);
            Assert.Contains(GuiTexts.ExplainDefault("UPPER_LEFT"), text);
            Assert.Contains(GuiTexts.ExplainSources(GuiTexts.ExplainSourceMeasured("2026-10-17") + ", " + GuiTexts.ExplainSourceWiki), text);
            Assert.DoesNotContain("Anchor in the parent.", text);

            var english = Box(language: "en");
            english.Show("orientation", "CENTER", Element(), null);
            Assert.Contains("Anchor in the parent.", AllText(english));
        });
    }

    [Fact]
    public void Falls_back_to_element_explanation_without_property()
    {
        Sta.Run(() =>
        {
            var box = Box(Task.FromResult(Examples(2)));

            box.Show(null, null, Element(), null);

            var text = AllText(box);
            Assert.Contains("Ein Bild.", text);
            Assert.Contains(GuiTexts.ExplainChildren("background, position"), text);
            Assert.Contains(GuiTexts.ExplainSources(GuiTexts.ExplainSourceVanilla), text);
            Assert.DoesNotContain("Anker im Elternelement.", text);
            Assert.Empty(Links(box));
        });
    }

    [Fact]
    public void Shows_none_line_without_any_entry()
    {
        Sta.Run(() =>
        {
            var box = Box();

            box.Show("zorder", "5", Element(), null);

            Assert.Contains(GuiTexts.ExplainNone, AllText(box));
        });
    }

    [Fact]
    public void Uncertain_entry_is_marked()
    {
        Sta.Run(() =>
        {
            var box = Box();

            box.Show("origo", "CENTER", Element(), null);
            Assert.Contains(GuiTexts.ExplainUncertain, AllText(box));

            box.Show("orientation", "CENTER", Element(), null);
            Assert.DoesNotContain(GuiTexts.ExplainUncertain, AllText(box));
        });
    }

    [Fact]
    public void Examples_capped_at_three_and_clickable()
    {
        Sta.Run(() =>
        {
            var examples = Examples(6);
            var box = Box(Task.FromResult(examples));
            GuiOrigin? opened = null;
            box.ExampleActivated += (_, origin) => opened = origin;

            box.Show("orientation", "UPPER_LEFT", Element(), null);

            var links = Links(box);
            Assert.Equal(3, links.Count);
            var expected = examples.For("orientation", "UPPER_LEFT")[1];
            links[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.NotNull(opened);
            Assert.Equal(expected.File.FullPath, opened!.FilePath);
            Assert.Equal(expected.Line, opened.Line);
        });
    }

    [Fact]
    public void Box_height_is_capped_so_the_property_list_keeps_its_room()
    {
        Sta.Run(() =>
        {
            var box = Box(Task.FromResult(Examples(6)));
            box.Show("orientation", "UPPER_LEFT", Element(), null);
            var scroll = (ScrollViewer)box.Content;
            var content = (StackPanel)scroll.Content;
            // Ein langer Eintrag: der Inhalt ist weit höher als die Obergrenze, das Feld scrollt.
            content.Children.Add(new TextBlock { Text = string.Join(" ", Enumerable.Repeat("Zeile", 200)), TextWrapping = TextWrapping.Wrap });

            box.Measure(new Size(300, double.PositiveInfinity));

            Assert.Equal(130, ClassicExplainBox.MaxBoxHeight);
            Assert.True(content.DesiredSize.Height > ClassicExplainBox.MaxBoxHeight, $"Inhalt {content.DesiredSize.Height}");
            Assert.True(box.DesiredSize.Height <= ClassicExplainBox.MaxBoxHeight + scroll.Margin.Top + scroll.Margin.Bottom, $"Feld {box.DesiredSize.Height}");
        });
    }

    [Fact]
    public void Examples_wait_in_background_and_stale_results_are_dropped()
    {
        Sta.Run(() =>
        {
            var first = new TaskCompletionSource<ClassicExampleIndex>();
            var box = Box(first.Task);

            box.Show("orientation", "UPPER_LEFT", Element(), null);
            Assert.Contains(GuiTexts.ExplainExamplesLoading, AllText(box));
            Assert.Empty(Links(box));

            // Die Auswahl wechselt, bevor die Suche fertig ist: das späte Ergebnis darf die neue Anzeige nicht überschreiben.
            box.Show("origo", "CENTER", Element(), null);
            first.SetResult(Examples(2));
            PumpFor(150);

            Assert.Empty(Links(box));
            Assert.Contains("Ankerpunkt im Element.", AllText(box));
        });
    }

    [Fact]
    public void Examples_fill_in_after_the_search_completes()
    {
        Sta.Run(() =>
        {
            var source = new TaskCompletionSource<ClassicExampleIndex>();
            var box = Box(source.Task);
            box.Show("orientation", "UPPER_LEFT", Element(), null);

            source.SetResult(Examples(2));
            Pump(() => Links(box).Count == 2);

            Assert.Equal(2, Links(box).Count);
            Assert.DoesNotContain(GuiTexts.ExplainExamplesLoading, AllText(box));
        });
    }

    [Fact]
    public void Faulted_example_search_shows_no_examples()
    {
        Sta.Run(() =>
        {
            var source = new TaskCompletionSource<ClassicExampleIndex>();
            var box = Box(source.Task);
            box.Show("orientation", "UPPER_LEFT", Element(), null);

            source.SetException(new InvalidOperationException("kaputt"));
            Pump(() => !AllText(box).Contains(GuiTexts.ExplainExamplesLoading));

            Assert.DoesNotContain(GuiTexts.ExplainExamplesLoading, AllText(box));
            Assert.Empty(Links(box));
            Assert.Contains("Anker im Elternelement.", AllText(box));
        });
    }

    [Fact]
    public void Property_editor_shows_the_box_for_the_selected_row_in_classic_only()
    {
        Sta.Run(() =>
        {
            // Der Editor liest die Oberflächensprache; je nach Lauf ist eine deutsche Tabelle geladen oder keine (dann gilt Englisch).
            var german = ParadoxModTool.Localization.UiText.Current.Language == "de";
            var picture = german ? "Ein Bild." : "A picture.";
            var anchor = german ? "Anker im Elternelement." : "Anchor in the parent.";
            var layout = GuiSettings.Layout;
            var explanations = GuiSettings.Explanations;
            try
            {
                GuiSettings.Explanations = Explanations();
                var element = Element();
                var node = element.Origin.Node;
                var editor = new GuiPropertyEditor();

                GuiSettings.Layout = Classic;
                editor.SetElement(node, element, editable: true);
                Assert.Contains(picture, AllText(editor));
                editor.SetRow(ClassicPropertyRows.Build(element, 1920, 1080).Last(r => string.Equals(r.Name, "orientation", StringComparison.OrdinalIgnoreCase)));
                Assert.Contains(anchor, AllText(editor));
                Assert.DoesNotContain(picture, AllText(editor));

                GuiSettings.Layout = GuiLayout.Default;
                editor.SetElement(node, element, editable: true);
                Assert.DoesNotContain(picture, AllText(editor));
            }
            finally
            {
                GuiSettings.Layout = layout;
                GuiSettings.Explanations = explanations;
            }
        });
    }

    [Theory]
    [InlineData("UPPER_LEFT", 0)]
    [InlineData("CENTER_UP", 1)]
    [InlineData("UPPER_RIGHT", 2)]
    [InlineData("CENTER_LEFT", 3)]
    [InlineData("center", 4)]
    [InlineData("\"CENTER_RIGHT\";", 5)]
    [InlineData("LOWER_LEFT", 6)]
    [InlineData("CENTER_DOWN", 7)]
    [InlineData("LOWER_RIGHT", 8)]
    [InlineData("UPPERL_LEFT", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Anchor_sketch_highlights_current_value(string? value, int? expected) =>
        Assert.Equal(expected, ClassicExplainSketch.AnchorIndex(value));

    [Fact]
    public void Sketch_renders_every_kind_without_a_theme()
    {
        Sta.Run(() =>
        {
            var sprite = new GuiSpriteVisual("GFX_a", null, ClassicSpriteKind.Sprite, 2, 3, 4, 5, false, false);
            foreach (var kind in new[] { "anchors", "percent", "frames", "nineSlice" })
            {
                var sketch = new ClassicExplainSketch { Kind = kind, Value = kind == "percent" ? "50%%" : "CENTER", Sprite = sprite };
                sketch.Measure(new Size(300, 300));
                sketch.Arrange(new Rect(sketch.DesiredSize));
                Assert.Equal(160, sketch.DesiredSize.Width);
                Assert.Equal(100, sketch.DesiredSize.Height);
                var target = new System.Windows.Media.Imaging.RenderTargetBitmap(160, 100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                target.Render(sketch);
            }
        });
    }
}
