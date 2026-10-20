using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Kleine Skizze (etwa 160 × 100 Pixel) im Erklärungsfeld: <c>anchors</c> (die neun Ankerpunkte), <c>percent</c> (<c>%</c> gegen <c>%%</c>),
/// <c>frames</c> (der Frame-Streifen des Sprites) und <c>nineSlice</c> (Ränder eines Neun-Teile-Sprites). Farben kommen aus den Theme-Ressourcen;
/// ohne Theme (Tests) tragen feste Ersatzfarben. Das Bild des Frame-Streifens lädt im Hintergrund über den <see cref="GuiImageCache"/> der Vorschau.
/// </summary>
public sealed class ClassicExplainSketch : FrameworkElement
{
    private const double SketchWidth = 160;
    private const double SketchHeight = 100;

    private static readonly Regex PercentValue = new(@"(-?\d+(?:\.\d+)?)(%%?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Numbers = new(@"-?\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string? _kind;
    private string? _value;
    private GuiSpriteVisual? _sprite;
    private BitmapSource? _bitmap;
    private int _version;

    /// <summary><c>anchors</c>, <c>percent</c>, <c>frames</c> oder <c>nineSlice</c>; alles andere zeichnet nichts.</summary>
    public string? Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Refresh();
        }
    }

    /// <summary>Aktueller Wert der Eigenschaft (Anker, Prozentwert, Rand) oder <see langword="null"/>.</summary>
    public string? Value
    {
        get => _value;
        set
        {
            _value = value;
            InvalidateVisual();
        }
    }

    /// <summary>Das Sprite des gewählten Elements (Frame, Frames, Ränder, Textur) oder <see langword="null"/>.</summary>
    public GuiSpriteVisual? Sprite
    {
        get => _sprite;
        set
        {
            _sprite = value;
            Refresh();
        }
    }

    /// <summary>
    /// Nummer des Ankerpunkts 0…8 (zeilenweise von oben links; 4 ist <c>CENTER</c>) oder <see langword="null"/> bei einem ungültigen Wert.
    /// Die nur in Vanilla gesehenen Kurzformen (<c>left</c> …) zählen wie in der Vorschau als ihr Ziel.
    /// </summary>
    public static int? AnchorIndex(string? value)
    {
        if (!ClassicAnchors.TryParse(value, out var fx, out var fy, out _))
        {
            return null;
        }

        return (int)Math.Round(fy * 2) * 3 + (int)Math.Round(fx * 2);
    }

    /// <summary>Der erste Prozentwert im Text: Zahl und Art (<c>1</c> = <c>%</c>, <c>2</c> = <c>%%</c>); <c>(null, 0)</c>, wenn keiner dasteht.</summary>
    internal static (double? Number, int Kind) PercentOf(string? value)
    {
        if (value is not null && PercentValue.Match(value) is { Success: true } match
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return (number, match.Groups[2].Value.Length);
        }

        return (null, 0);
    }

    protected override Size MeasureOverride(Size availableSize) => new(SketchWidth, SketchHeight);

    protected override void OnRender(DrawingContext dc)
    {
        var palette = new Palette(
            Resource("Theme.Text", Brushes.Black),
            Resource("Theme.TextMuted", Brushes.Gray),
            Resource("Theme.Accent", Brushes.DodgerBlue),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        switch (_kind)
        {
            case "anchors":
                DrawAnchors(dc, palette);
                break;
            case "percent":
                DrawPercent(dc, palette);
                break;
            case "frames":
                DrawFrames(dc, palette);
                break;
            case "nineSlice":
                DrawNineSlice(dc, palette);
                break;
        }
    }

    private Brush Resource(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private void Refresh()
    {
        _bitmap = null;
        var mine = ++_version;
        if (_kind == "frames" && _sprite?.TextureFullPath is { Length: > 0 } path)
        {
            var dispatcher = Dispatcher;
            _ = Task.Run(() => GuiImageCache.Shared.Get(path)).ContinueWith(
                task => dispatcher.BeginInvoke(() =>
                {
                    if (mine != _version || !task.IsCompletedSuccessfully || task.Result is not { } bitmap)
                    {
                        return;
                    }

                    _bitmap = bitmap;
                    InvalidateVisual();
                }),
                TaskScheduler.Default);
        }

        InvalidateVisual();
    }

    // Das Elternrechteck mit den neun Ankerpunkten; der aktuelle Wert ist gefüllt und größer, ein ungültiger Wert zeigt keinen.
    private void DrawAnchors(DrawingContext dc, Palette p)
    {
        var parent = new Rect(16, 10, 128, 80);
        dc.DrawRectangle(null, new Pen(p.Muted, 1), parent);
        var current = AnchorIndex(_value);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var centre = new Point(parent.Left + column * parent.Width / 2, parent.Top + row * parent.Height / 2);
                if (current == row * 3 + column)
                {
                    dc.DrawEllipse(p.Accent, new Pen(p.Accent, 1), centre, 6, 6);
                }
                else
                {
                    dc.DrawEllipse(Brushes.Transparent, new Pen(p.Muted, 1), centre, 3, 3);
                }
            }
        }
    }

    // Eltern (100 Einheiten) mit einem Kind bei x = 60: N% ist ein Anteil der Elterngröße und ragt hinaus, N%% ein Anteil von Elterngröße − Position.
    private void DrawPercent(DrawingContext dc, Palette p)
    {
        const double scale = 0.9;
        var (given, kind) = PercentOf(_value);
        var number = given ?? 100;
        var parent = new Rect(6, 6, 100 * scale, 88);
        dc.DrawRectangle(null, new Pen(p.Muted, 1), parent);
        Text(dc, p, GuiTexts.ExplainSketchParent + " 100", new Point(parent.Left + 3, parent.Bottom - 14), p.Muted);
        var left = parent.Left + 60 * scale;
        dc.DrawLine(new Pen(p.Muted, 1) { DashStyle = DashStyles.Dot }, new Point(left, parent.Top), new Point(left, parent.Bottom - 16));
        Text(dc, p, "x = 60", new Point(left + 3, parent.Top + 1), p.Muted);

        Bar(dc, p, left, 28, number / 100 * 100 * scale, $"{Format(number)}% = {Format(number)}", kind == 1);
        Bar(dc, p, left, 56, number / 100 * (100 - 60) * scale, $"{Format(number)}%% = {Format(number * 0.4)}", kind == 2);
    }

    private static void Bar(DrawingContext dc, Palette p, double left, double top, double width, string label, bool highlight)
    {
        var fill = Fade(p.Accent, highlight ? 0.55 : 0.25);
        var bar = new Rect(left, top, Math.Max(1, Math.Min(width, SketchWidth - left - 1)), 16);
        dc.DrawRectangle(fill, new Pen(highlight ? p.Accent : p.Muted, highlight ? 2 : 1), bar);
        Text(dc, p, label, new Point(bar.Left + 3, bar.Bottom + 1), highlight ? p.Text : p.Muted);
    }

    // Der Frame-Streifen des Sprites (alle Frames nebeneinander), der aktuelle Frame umrandet; ohne Bild nummerierte Kästchen.
    private void DrawFrames(DrawingContext dc, Palette p)
    {
        if (_sprite is not { } sprite)
        {
            return;
        }

        var frames = Math.Max(1, sprite.Frames);
        var current = Math.Clamp(sprite.Frame, 1, frames);
        var area = new Rect(6, 14, 148, 58);
        Rect strip;
        if (_bitmap is { PixelWidth: > 0, PixelHeight: > 0 } bitmap)
        {
            var factor = Math.Min(area.Width / bitmap.PixelWidth, area.Height / bitmap.PixelHeight);
            var size = new Size(bitmap.PixelWidth * factor, bitmap.PixelHeight * factor);
            strip = new Rect(area.Left + (area.Width - size.Width) / 2, area.Top + (area.Height - size.Height) / 2, size.Width, size.Height);
            dc.DrawImage(bitmap, strip);
            dc.DrawRectangle(null, new Pen(p.Muted, 1), strip);
            var width = strip.Width / frames;
            for (var i = 1; i < frames; i++)
            {
                dc.DrawLine(new Pen(p.Muted, 1), new Point(strip.Left + i * width, strip.Top), new Point(strip.Left + i * width, strip.Bottom));
            }

            dc.DrawRectangle(null, new Pen(p.Accent, 2), new Rect(strip.Left + (current - 1) * width, strip.Top, width, strip.Height));
        }
        else
        {
            var shown = Math.Min(frames, 20);
            var width = area.Width / shown;
            for (var i = 0; i < shown; i++)
            {
                var cell = new Rect(area.Left + i * width + 1, area.Top + 8, Math.Max(1, width - 2), 40);
                var isCurrent = i + 1 == current;
                dc.DrawRectangle(isCurrent ? Fade(p.Accent, 0.3) : null, new Pen(isCurrent ? p.Accent : p.Muted, isCurrent ? 2 : 1), cell);
                if (width >= 14)
                {
                    Text(dc, p, (i + 1).ToString(CultureInfo.InvariantCulture), new Point(cell.Left + 3, cell.Top + 3), isCurrent ? p.Text : p.Muted);
                }
            }
        }

        Text(dc, p, $"{current} / {frames}", new Point(6, 78), p.Text);
    }

    // Ein schematischer Kasten mit Randlinien; die Zahlen sind die Randbreiten (borderSize, sonst die des Sprites).
    private void DrawNineSlice(DrawingContext dc, Palette p)
    {
        var (x, y) = BorderSize();
        var outer = new Rect(30, 8, 100, 84);
        const double insetX = 24;
        const double insetY = 20;
        dc.DrawRectangle(null, new Pen(p.Muted, 1), outer);
        var dashed = new Pen(p.Accent, 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(dashed, new Point(outer.Left + insetX, outer.Top), new Point(outer.Left + insetX, outer.Bottom));
        dc.DrawLine(dashed, new Point(outer.Right - insetX, outer.Top), new Point(outer.Right - insetX, outer.Bottom));
        dc.DrawLine(dashed, new Point(outer.Left, outer.Top + insetY), new Point(outer.Right, outer.Top + insetY));
        dc.DrawLine(dashed, new Point(outer.Left, outer.Bottom - insetY), new Point(outer.Right, outer.Bottom - insetY));
        if (x is not null)
        {
            Text(dc, p, x, new Point(outer.Left + 6, outer.Top + outer.Height / 2 - 6), p.Text);
        }

        if (y is not null)
        {
            Text(dc, p, y, new Point(outer.Left + outer.Width / 2 - 3, outer.Top + 3), p.Text);
        }
    }

    private (string? X, string? Y) BorderSize()
    {
        var numbers = _value is null ? null : Numbers.Matches(_value).Select(m => m.Value).ToList();
        if (numbers is { Count: >= 2 })
        {
            return (numbers[0], numbers[1]);
        }

        return _sprite is { } sprite && (sprite.BorderX > 0 || sprite.BorderY > 0)
            ? (sprite.BorderX.ToString(CultureInfo.InvariantCulture), sprite.BorderY.ToString(CultureInfo.InvariantCulture))
            : (null, null);
    }

    private static Brush Fade(Brush brush, double opacity)
    {
        var faded = brush.Clone();
        faded.Opacity = opacity;
        return faded;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Text(DrawingContext dc, Palette p, string text, Point origin, Brush brush) =>
        dc.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, p.PixelsPerDip), origin);

    private sealed record Palette(Brush Text, Brush Muted, Brush Accent, double PixelsPerDip);
}
