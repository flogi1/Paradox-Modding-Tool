using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Erklärungsfeld unter der Eingabe im klassischen Eigenschaftenfeld (HOI IV, EU IV): Text, Standardwert, Stolperfallen, Skizze, bis zu drei Vanilla-Beispiele
/// und die Quellen der gewählten Eigenschaft, ohne Eigenschaft die Erklärung der Elementart. Was nicht belegt ist, trägt die Warnfarbe. Die Beispiele kommen
/// aus dem Hintergrund-Index des Arbeitsbereichs: die Oberfläche wartet nie darauf, das Ergebnis eines früheren Aufrufs wird verworfen.
/// </summary>
public sealed class ClassicExplainBox : UserControl
{
    private const int MaxExamples = 3;

    /// <summary>
    /// Höchsthöhe des Felds; darüber scrollt es. Der Eigenschaftenbereich ist 320 px hoch und der Editor (Geltungsbereich, Eingabe, "Hinzufügen") braucht
    /// davon etwa 100: mit 130 px für das Feld bleibt der Liste mindestens ihre Mindesthöhe.
    /// </summary>
    internal const double MaxBoxHeight = 130;

    private readonly StackPanel _panel = new();
    private int _version;

    public ClassicExplainBox()
    {
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = MaxBoxHeight,
            Margin = new Thickness(4, 0, 4, 2),
            Content = _panel,
        };
    }

    /// <summary>Ein Vanilla-Beispiel wurde angeklickt: Datei und Zeile zum Öffnen (schreibgeschützt).</summary>
    public event EventHandler<GuiOrigin>? ExampleActivated;

    /// <summary>Sprache der Texte; Standard ist die Oberflächensprache (Tests setzen eine feste).</summary>
    internal Func<string> LanguageSource { get; set; } = () => UiText.Current.Language;

    internal Func<ClassicExplanations> ExplanationSource { get; set; } = () => GuiSettings.Explanations;

    /// <summary>Die laufende oder fertige Beispielsuche des Arbeitsbereichs; <see langword="null"/>, wenn es keine gibt (Jomini, kein Spielpfad).</summary>
    internal Func<Task<ClassicExampleIndex>?> ExampleSource { get; set; } = () => GuiSettings.Workspace?.ClassicExamples;

    /// <param name="propertyName">Die gewählte Eigenschaft oder <see langword="null"/> (dann gilt die Erklärung der Elementart).</param>
    /// <param name="value">Ihr aktueller Wert (für Skizze und Beispiele mit demselben Wert).</param>
    /// <param name="sprite">Das Sprite des gewählten Elements für die Frame- und Randskizze, wenn die Vorschau eines hat.</param>
    public void Show(string? propertyName, string? value, ResolvedElement? element, GuiSpriteVisual? sprite)
    {
        var version = ++_version;
        _panel.Children.Clear();
        var language = LanguageSource();
        var explanations = ExplanationSource();
        var hasProperty = !string.IsNullOrEmpty(propertyName);
        var entry = hasProperty ? explanations.Property(propertyName!, language)
            : element is not null ? explanations.Element(element.Key, language)
            : null;

        if (entry is null)
        {
            _panel.Children.Add(Muted(GuiTexts.ExplainNone));
        }
        else
        {
            AddEntry(entry, value, sprite);
        }

        if (hasProperty)
        {
            AddExamples(version, index => index.For(propertyName!, value), MaxExamples);
        }
        else if (entry is not null && element is not null)
        {
            AddExamples(version, index => index.ForElement(element.Key), 1);
        }

        if (entry is { Sources.Count: > 0 })
        {
            var line = Muted(GuiTexts.ExplainSources(SourceLine(entry.Sources)));
            line.Margin = new Thickness(0, 4, 0, 0);
            _panel.Children.Add(line);
        }
    }

    private void AddEntry(ClassicExplanation entry, string? value, GuiSpriteVisual? sprite)
    {
        _panel.Children.Add(new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap });
        if (entry.IsUncertain)
        {
            var warning = new TextBlock { Text = GuiTexts.ExplainUncertain, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            _panel.Children.Add(warning);
        }

        // Frame- und Randskizze brauchen das Sprite des Elements; ohne eines gäbe es nichts zu zeichnen.
        if (entry.Sketch is { } kind && (kind != "frames" || sprite is not null))
        {
            _panel.Children.Add(new ClassicExplainSketch { Kind = kind, Value = value, Sprite = sprite, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) });
        }

        if (entry.Default is { } standard)
        {
            var line = Muted(GuiTexts.ExplainDefault(standard));
            line.Margin = new Thickness(0, 2, 0, 0);
            _panel.Children.Add(line);
        }

        if (entry.Pitfalls.Count > 0)
        {
            var heading = new TextBlock { Text = GuiTexts.ExplainPitfalls, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
            _panel.Children.Add(heading);
            foreach (var pitfall in entry.Pitfalls)
            {
                _panel.Children.Add(new TextBlock { Text = "• " + pitfall, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 0, 0) });
            }
        }

        if (entry.Children.Count > 0)
        {
            var line = Muted(GuiTexts.ExplainChildren(string.Join(", ", entry.Children)));
            line.TextWrapping = TextWrapping.Wrap;
            line.Margin = new Thickness(0, 4, 0, 0);
            _panel.Children.Add(line);
        }
    }

    /// <summary>
    /// Bis zu <paramref name="max"/> Beispiele aus dem Index. Ist die Suche noch nicht fertig, steht "wird gesucht" an der Stelle; danach füllt der
    /// Dispatcher die Liste, sofern die Auswahl sich nicht geändert hat. Eine gescheiterte Suche zeigt nichts.
    /// </summary>
    private void AddExamples(int version, Func<ClassicExampleIndex, IReadOnlyList<ClassicExample>> find, int max)
    {
        if (ExampleSource() is not { } task)
        {
            return;
        }

        var section = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        _panel.Children.Add(section);
        if (task.IsCompleted)
        {
            Fill(section, task, find, max);
            return;
        }

        section.Children.Add(new TextBlock { Text = GuiTexts.ExplainExamples, FontWeight = FontWeights.SemiBold });
        section.Children.Add(Muted(GuiTexts.ExplainExamplesLoading));
        var dispatcher = Dispatcher;
        _ = task.ContinueWith(
            finished => dispatcher.BeginInvoke(() =>
            {
                if (version == _version)
                {
                    section.Children.Clear();
                    Fill(section, finished, find, max);
                }
            }),
            TaskScheduler.Default);
    }

    private void Fill(StackPanel section, Task<ClassicExampleIndex> task, Func<ClassicExampleIndex, IReadOnlyList<ClassicExample>> find, int max)
    {
        if (!task.IsCompletedSuccessfully)
        {
            return;
        }

        var examples = find(task.Result).Take(max).ToList();
        if (examples.Count == 0)
        {
            return;
        }

        section.Children.Add(new TextBlock { Text = GuiTexts.ExplainExamples, FontWeight = FontWeights.SemiBold });
        foreach (var example in examples)
        {
            section.Children.Add(Link(example));
        }
    }

    // Ein Knopf im Aussehen eines Links (kein Hyperlink: der Eigenschaftenbereich zählt Hyperlinks als "Sprite anlegen"); per Tastatur erreichbar.
    private Button Link(ClassicExample example)
    {
        var label = new TextBlock(new Run(GuiTexts.ExplainExample(example.ElementName, example.File.RelativePath, example.Line)))
        {
            TextDecorations = TextDecorations.Underline,
            TextWrapping = TextWrapping.Wrap,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Accent");
        var link = new Button
        {
            Content = label,
            Tag = example,
            ToolTip = GuiTexts.ExplainExampleTip,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
        };
        link.Click += (_, _) =>
        {
            // Der Knoten ist nur ein Platzhalter: geöffnet wird die Datei an der Zeile.
            var node = new GuiNode(GuiNodeKind.KeyedBlock) { Name = example.WidgetKey, Line = example.Line };
            ExampleActivated?.Invoke(this, new GuiOrigin(GuiOriginKind.Here, string.Empty, example.File.FullPath, example.Line, node));
        };
        return link;
    }

    /// <summary>"gemessen am 2026-10-17, Wiki": je Art einmal, Messungen mit Datum; unsichere Quellen heißen "unsicher".</summary>
    private static string SourceLine(IReadOnlyList<ClassicExplanationSource> sources)
    {
        var labels = new List<string>();
        foreach (var source in sources)
        {
            var label = source.Kind.ToLowerInvariant() switch
            {
                "wiki" => GuiTexts.ExplainSourceWiki,
                "measured" => string.IsNullOrWhiteSpace(source.Date) ? GuiTexts.ExplainSourceMeasuredNoDate : GuiTexts.ExplainSourceMeasured(source.Date),
                "vanilla" => GuiTexts.ExplainSourceVanilla,
                _ => GuiTexts.ExplainSourceUnsure,
            };
            if (!labels.Contains(label))
            {
                labels.Add(label);
            }
        }

        return string.Join(", ", labels);
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }
}
