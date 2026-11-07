using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Jomini-Helfer unter dem Eingabefeld: die Bildleiste (<see cref="JominiFrameStrip"/>) bei <c>frame</c>/<c>framesize</c>/<c>frame_grid</c> und die Randskizze
/// (<see cref="JominiBorderSketch"/>) bei <c>spriteborder</c> und den Einzelwerten, sofern das Element eine gezeichnete Textur hat. Getippte, noch nicht
/// übernommene Werte zeichnen sie live; ein Klick in die Leiste schreibt <c>frame</c> sofort (nur wenn bearbeitbar).
/// </summary>
public sealed partial class GuiPropertyEditor
{
    private static readonly HashSet<string> FrameRows = new(StringComparer.Ordinal) { "frame", "framesize", "frame_grid" };

    private static readonly HashSet<string> PairRows = new(StringComparer.Ordinal) { "framesize", "frame_grid", "spriteborder" };

    private static readonly HashSet<string> BorderRows = new(StringComparer.Ordinal)
    {
        "spriteborder", "spriteborder_left", "spriteborder_right", "spriteborder_top", "spriteborder_bottom",
    };

    private static readonly char[] ItemSeparators = { ' ', '\t', '{', '}' };

    /// <summary>Das gezeichnete Sprite des gewählten Elements in der Vorschau (Jomini) oder <see langword="null"/>.</summary>
    private JominiSpriteVisual? _jominiSprite;

    /// <summary>Die Felder der gerade gezeigten Eingabe (Paar: zwei), aus denen die Helfer live lesen; <see langword="null"/> bei Eingaben ohne Textfeld.</summary>
    private IReadOnlyList<Control>? _liveInputs;

    /// <summary>Bildleiste bzw. Randskizze unter <paramref name="input"/>, wenn die Zeile dazu passt und eine Textur gezeichnet wird; sonst die Eingabe allein.</summary>
    private FrameworkElement WithJominiHelpers(string name, FrameworkElement input)
    {
        if (GuiSettings.Layout.IsClassic || _element is null || _selected is null
            || _jominiSprite is not { Placeholder: JominiSpritePlaceholder.None, TextureWidth: > 0, TextureHeight: > 0 } sprite)
        {
            return input;
        }

        FrameworkElement helper;
        Action update;
        if (FrameRows.Contains(name))
        {
            var strip = new JominiFrameStrip { ClicksEnabled = _editable };
            strip.FrameClicked += (_, frame) =>
            {
                // Zweite Sperre neben der Leiste: eine Bindung wird nie durch einen Klick ersetzt.
                if (!strip.IsBound)
                {
                    WriteFrame(frame);
                }
            };
            update = () =>
            {
                var (current, label) = CurrentFrame(name);
                strip.Show(sprite.TextureFullPath, FrameLayout(name, sprite), current, label, bound: label?.Contains('[') == true);
            };
            helper = new ScrollViewer
            {
                Content = strip,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 4, 0, 0),
            };
        }
        else if (BorderRows.Contains(name))
        {
            var sketch = new JominiBorderSketch();
            var element = _element;
            update = () => sketch.Show(
                sprite.TextureFullPath, sprite.Source, sprite.Density,
                JominiBorderSketchGeometry.BorderOf(element, LiveValues(name) is null ? null : name, LiveValues(name)));
            helper = sketch;
        }
        else
        {
            return input;
        }

        HookLive(update);
        update();
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(input);
        stack.Children.Add(helper);
        return stack;
    }

    /// <summary>Raster: in der Zeile <c>framesize</c>/<c>frame_grid</c> deren getippte Werte, die andere Eigenschaft aus dem Element (framesize gewinnt).</summary>
    private JominiFrameLayout? FrameLayout(string name, JominiSpriteVisual sprite)
    {
        IReadOnlyList<string>? ItemsOf(string key) => key == name && LiveValues(key) is { } live ? live : _element!.Property(key)?.Items;
        return JominiFrameGrid.ForItems(sprite.TextureWidth, sprite.TextureHeight, ItemsOf("framesize"), ItemsOf("frame_grid"));
    }

    /// <summary>
    /// Das aktuelle Bild und sein Text für den Hinweis: in der Zeile <c>frame</c> der getippte Wert, sonst die Eigenschaft (fehlend ist 1). Enthält der Text
    /// <c>[</c>, ist es eine Datenbindung (die Leiste hebt nichts hervor und nimmt keine Klicks an).
    /// </summary>
    private (long? Current, string? Label) CurrentFrame(string name)
    {
        if (name == "frame" && LiveValues(name) is [var text, ..])
        {
            return (JominiFrameGrid.FrameNumberOf(text), text.Trim());
        }

        var frame = _element!.Property("frame");
        return (JominiFrameGrid.FrameNumber(frame), frame?.Value.Trim());
    }

    /// <summary>
    /// Die getippten Werte: bei zwei Feldern je eins, bei einem Feld einer Paar-Zeile die Wörter (<c>20 20</c>), sonst der Text. <see langword="null"/> ohne
    /// Textfeld oder wenn alle Felder leer sind (z. B. eine eben hinzugefügte Zeile): dann gilt der Wert aus dem Element.
    /// </summary>
    private IReadOnlyList<string>? LiveValues(string name)
    {
        if (_liveInputs is null)
        {
            return null;
        }

        var texts = _liveInputs.Select(TextOf).ToList();
        if (texts.All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        return texts.Count == 1 && PairRows.Contains(name)
            ? texts[0].Split(ItemSeparators, StringSplitOptions.RemoveEmptyEntries)
            : texts;
    }

    private static string TextOf(Control input) => input switch
    {
        TextBox box => box.Text,
        ComboBox combo => combo.Text,
        _ => string.Empty,
    };

    /// <summary><paramref name="update"/> bei jeder Änderung eines Eingabefelds (auch dem Textfeld einer editierbaren Auswahl).</summary>
    private void HookLive(Action update)
    {
        foreach (var input in _liveInputs ?? Array.Empty<Control>())
        {
            if (input is TextBox box)
            {
                box.TextChanged += (_, _) => update();
            }
            else
            {
                input.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => update()));
            }
        }
    }

    /// <summary>
    /// Klick in die Bildleiste: <c>frame = n</c> sofort, an der Zeile des Elements (<c>SetValue</c>) oder als neue Zeile (<c>SetProperty</c>), wie <see cref="Apply"/>.
    /// In der Zeile <c>frame</c> ist das Ziel die gezeigte Zeile (wie bei Übernehmen), sonst die wirksame <c>frame</c>-Eigenschaft des Elements.
    /// </summary>
    private void WriteFrame(long frame)
    {
        if (!_editable || _selected is null || _element is null)
        {
            return;
        }

        var value = frame.ToString(CultureInfo.InvariantCulture);
        var row = _propertyName == "frame" && _property is not null ? _property : _element.Property("frame");
        var target = row is not null ? GuiPropertyInput.TargetFor(row, _selected) : new GuiPropertyTarget(_selected, false);
        ActionRequested?.Invoke(this, target.InPlace
            ? new GuiDesignerAction(GuiDesignerActionKind.SetValue, target.Node) { Value = value }
            : new GuiDesignerAction(GuiDesignerActionKind.SetProperty, _selected) { Name = "frame", Value = value });

        // Das Feld der Zeile frame zeigt den geschriebenen Wert (die Leiste folgt über TextChanged).
        if (_propertyName == "frame" && _liveInputs is [TextBox box])
        {
            box.Text = value;
        }
    }
}
