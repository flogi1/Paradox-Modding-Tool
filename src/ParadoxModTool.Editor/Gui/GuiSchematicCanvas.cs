using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Zeichnet die schematische Vorschau: je Element ein Kasten in berechneter Lage und Größe, Farbe nach Art, gestrichelt bei Annäherung.
/// Klassische Kästen mit Bild (HOI IV, EU IV) zeigen Textur bzw. Text im Spiel-Look, darüber Rahmen und rot den Überstand.
/// Bedingt sichtbare Elemente tragen oben rechts ein kleines Dreieck in der Warnfarbe des Themes.
/// Mausrad zoomt um den Mauszeiger, rechte oder mittlere Maustaste verschiebt, Linksklick wählt aus.
/// </summary>
public sealed class GuiSchematicCanvas : FrameworkElement
{
    private GuiSchematicScene? _scene;
    private GuiNode? _selected;
    private double _zoom = 1;
    private Vector _pan;
    private bool _userMoved;
    private bool _dimmed;
    private Point? _dragStart;
    private Vector _panAtDragStart;
    private bool _showBounds;
    private bool _markAllOverflow;
    private HashSet<LayoutBox> _buttonLabels = new();
    private ClassicFontLibrary? _fonts;

    /// <summary>Kästen, deren Textur im laufenden Zeichnen als Bild stand (nicht als schematischer Kasten); die Umrissrunde zeichnet ihren Rahmen.</summary>
    private readonly HashSet<LayoutBox> _spriteDrawn = new();

    /// <summary>1, solange ein Neuzeichnen wegen eines fertigen Bildes aussteht (mehrere Meldungen des Puffers werden zu einem Aufruf).</summary>
    private int _repaintPending;

    /// <summary>Beschriftungen nur bis zu dieser Zahl Kästen (große Vanilla-Fenster bleiben so beim Zoomen flüssig).</summary>
    private const int MaxLabelledBoxes = 400;

    private static readonly Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)> Styles = CreateStyles(40);

    /// <summary>Farbige Kästen auf dem dunklen Hintergrund: dieselben Farben, die Füllung kräftiger, damit auch die dunklen Kategorien (Fenster) sichtbar bleiben.</summary>
    private static readonly Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)> DarkStyles = CreateStyles(90);

    /// <summary>Fester dunkler Hintergrund der Jomini-Vorschau (#1B1B1F), unabhängig vom Theme.</summary>
    internal static readonly Color DarkBackgroundColor = Color.FromRgb(0x1B, 0x1B, 0x1F);

    private static readonly Brush DarkBackgroundBrush = CreateDarkBackgroundBrush();
    private static readonly Brush DarkLabelBrush = CreateDarkLabelBrush();

    private bool _darkBackground;

    /// <summary>Kasten ragt über Elternkasten oder Bildschirm hinaus.</summary>
    private static readonly Pen OverflowPen = CreateOverflowPen();

    public GuiSchematicCanvas()
    {
        // Der Puffer meldet fertige Bilder von einem Hintergrund-Thread; solange die Fläche im Fenster steht, zeichnet sie dann neu.
        Loaded += (_, _) =>
        {
            GuiImageCache.Shared.ImageReady -= OnImageReady;
            GuiImageCache.Shared.ImageReady += OnImageReady;

            // Das erste Zeichnen läuft vor Loaded, und nach einem Entladen (Registerwechsel) kann ein Bild fertig geworden sein, ohne dass jemand zuhörte:
            // jetzt einmal neu zeichnen, damit nichts schematisch stehen bleibt.
            InvalidateVisual();
        };
        Unloaded += (_, _) => GuiImageCache.Shared.ImageReady -= OnImageReady;
    }

    public event EventHandler<GuiNode>? NodeClicked;

    /// <summary>Linksklick auf einen Kasten, mit dem Kasten selbst (Inspektor: Auswahl im aufgelösten Baum). Kommt vor <see cref="NodeClicked"/>.</summary>
    public event EventHandler<LayoutBox>? BoxClicked;

    /// <summary>Gedämpft zeichnen (Code hat Fehler, Vorschau zeigt den letzten gültigen Stand).</summary>
    public bool Dimmed
    {
        get => _dimmed;
        set
        {
            _dimmed = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Fester dunkler Hintergrund (#1B1B1F) statt des Theme-Hintergrunds, für Jomini-Vorschauen (Designer und Inspektor setzen ihn, wenn der Arbeitsbereich
    /// nicht klassisch ist). Beschriftungen der farbigen Kästen sind dann hell; Überstand-Umriss, Auswahl und Bedingt-Marke behalten ihre Farben.
    /// </summary>
    public bool DarkBackground
    {
        get => _darkBackground;
        set
        {
            if (_darkBackground == value)
            {
                return;
            }

            _darkBackground = value;
            InvalidateVisual();
        }
    }

    /// <summary>Pinsel für die Kastenbeschriftung: auf dem dunklen Hintergrund immer hell, sonst die Textfarbe des Themes.</summary>
    internal static Brush LabelBrush(bool dark, Brush themeText) => dark ? DarkLabelBrush : themeText;

    private static readonly Brush DarkMutedBrush = CreateDarkMutedBrush();

    /// <summary>
    /// Vordergrund der Hinweistexte, die über der Fläche stehen (leere Vorschau, Fehlermeldung): auf dem dunklen Hintergrund ein helles Grau,
    /// sonst wieder <c>Theme.TextMuted</c> des Themes.
    /// </summary>
    internal static void ApplyMessageForeground(TextBlock message, bool dark)
    {
        if (dark)
        {
            message.Foreground = DarkMutedBrush;
        }
        else
        {
            message.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        }
    }

    private static Brush CreateDarkMutedBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xB0));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateDarkBackgroundBrush()
    {
        var brush = new SolidColorBrush(DarkBackgroundColor);
        brush.Freeze();
        return brush;
    }

    private static Brush CreateDarkLabelBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEC));
        brush.Freeze();
        return brush;
    }

    /// <summary>"Rahmen zeigen": jeden Kasten umranden (wie <c>guibounds</c> im Spiel), auch über Texturen.</summary>
    public bool ShowBounds
    {
        get => _showBounds;
        set
        {
            _showBounds = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// "Überstand markieren": jeden Kasten rot umranden, der über Elternrahmen oder Bildschirm hinausragt. Aus (Standard): nur beim gewählten Element,
    /// damit die Vorschau wie im Spiel aussieht.
    /// </summary>
    public bool MarkAllOverflow
    {
        get => _markAllOverflow;
        set
        {
            _markAllOverflow = value;
            InvalidateVisual();
        }
    }

    /// <summary>Wird der Überstand dieses Kastens rot umrandet? Alle bei <paramref name="markAll"/>, sonst nur die Kästen des gewählten Elements.</summary>
    internal static bool OverflowShown(LayoutBox box, bool markAll, IReadOnlyCollection<LayoutBox> selected) =>
        (box.OutsideParent || box.OutsideScreen) && (markAll || selected.Contains(box));

    /// <param name="fonts">Bitmap-Schriften des Arbeitsbereichs, der die Szene gelegt hat (klassisch; sonst <see langword="null"/>).</param>
    public void SetScene(GuiSchematicScene? scene, bool keepView, ClassicFontLibrary? fonts = null)
    {
        // Neue Szene = neuer Vorschaulauf: geänderte oder neu angelegte Texturen werden einmal gegen die Datei geprüft (nicht bei jedem Neuzeichnen).
        GuiImageCache.Shared.NewRun();
        _scene = scene;
        _fonts = fonts;
        _buttonLabels = scene is null ? new HashSet<LayoutBox>() : ButtonLabels(scene);
        if (scene is not null)
        {
            // Texturen im Hintergrund laden, in Zeichenreihenfolge (was zuerst gezeichnet wird, kommt zuerst); gezeichnet wird mit dem, was schon da ist.
            GuiImageCache.Shared.Prefetch(TexturesOf(scene));
        }

        if (!keepView || !_userMoved)
        {
            FitToView();
        }

        InvalidateVisual();
    }

    /// <summary>Alle Texturen, die diese Szene braucht: je Kasten erst das Sprite (mit seiner Färbung), dann die Text-Icons (weiß).</summary>
    private static IEnumerable<(string Path, GuiTint Tint)> TexturesOf(GuiSchematicScene scene)
    {
        foreach (var item in scene.Boxes)
        {
            if (item.Box.Sprite is { Placeholder: JominiSpritePlaceholder.None, TextureFullPath: { } path } sprite)
            {
                yield return (path, JominiSpriteRenderer.TintOf(sprite));
            }

            if (item.Box.Visual is not JominiTextVisual text)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.Kind == JominiTextRunKind.Icon && run.IconTexture is { } icon)
                    {
                        yield return (icon, GuiTint.White);
                    }
                }
            }
        }
    }

    /// <summary>Ein Bild ist fertig (oder gescheitert): einmal neu zeichnen, auch wenn der Puffer mehrere Bilder kurz nacheinander meldet.</summary>
    private void OnImageReady(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _repaintPending, 1) != 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                Volatile.Write(ref _repaintPending, 0);
                InvalidateVisual();
            }),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    public void SetSelection(GuiNode? node)
    {
        _selected = node;
        InvalidateVisual();
    }

    public void FitToView()
    {
        _userMoved = false;
        if (_scene is null || _scene.Width <= 0 || _scene.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _zoom = 1;
            _pan = new Vector();
            return;
        }

        const double padding = 12;
        _zoom = Math.Clamp(Math.Min((ActualWidth - 2 * padding) / _scene.Width, (ActualHeight - 2 * padding) / _scene.Height), 0.02, 8);
        _pan = new Vector(
            padding - _scene.Left * _zoom + (ActualWidth - 2 * padding - _scene.Width * _zoom) / 2,
            padding - _scene.Top * _zoom);
    }

    internal static Color ColorOf(GuiWidgetCategory category) => category switch
    {
        GuiWidgetCategory.Window => Color.FromRgb(0x55, 0x6B, 0x8D),
        GuiWidgetCategory.Container => Color.FromRgb(0x80, 0x80, 0x80),
        GuiWidgetCategory.Box => Color.FromRgb(0x4A, 0x90, 0xD9),
        GuiWidgetCategory.List => Color.FromRgb(0x7A, 0x4A, 0xD9),
        GuiWidgetCategory.Text => Color.FromRgb(0x2E, 0x9E, 0x5B),
        GuiWidgetCategory.Icon => Color.FromRgb(0xD9, 0x8E, 0x4A),
        GuiWidgetCategory.Button => Color.FromRgb(0xD9, 0x4A, 0x6A),
        _ => Color.FromRgb(0xA0, 0xA0, 0x40),
    };

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_userMoved)
        {
            FitToView();
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var background = _darkBackground ? DarkBackgroundBrush : TryFindResource("Theme.EditorBackground") as Brush ?? Brushes.White;
        dc.DrawRectangle(background, null, new Rect(RenderSize));
        if (_scene is null)
        {
            return;
        }

        var textBrush = LabelBrush(_darkBackground, TryFindResource("Theme.Text") as Brush ?? Brushes.Black);
        var accent = TryFindResource("Theme.Accent") as Brush ?? Brushes.DodgerBlue;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushOpacity(_dimmed ? 0.45 : 1);
        var labels = _scene.Boxes.Count <= MaxLabelledBoxes;
        var fonts = _fonts;
        _spriteDrawn.Clear();

        // Zeichenreihenfolge = Reihenfolge der Definition (Eltern vor Kindern). Klassische Kästen mit Bild zeigen Textur bzw. Text, Jomini-Texte
        // den farbigen Kasten ohne Beschriftung mit dem Text darüber, reine Container den farbigen Kasten mit Beschriftung.
        // Kästen in einem Scrollbereich bzw. unter scissor = yes (Jomini) werden auf dessen Fläche beschnitten, ebenso ihr Überstand- und Auswahlumriss
        // (DrawOutline), auch die Rahmen über den Bildern (klassisch gibt es keinen Beschnitt, dort ändert das nichts).
        foreach (var item in _scene.Boxes)
        {
            if (item.Box.Clip is { } clip)
            {
                dc.PushClip(new RectangleGeometry(ToScreen(clip)));
            }

            DrawBox(dc, item, labels, textBrush, accent, fonts, dpi);
            if (item.Box.Clip is not null)
            {
                dc.Pop();
            }
        }

        // Rahmen über den Bildern: Annäherung (gestrichelt, wie bei den farbigen Kästen) immer, alle Rahmen bei "Rahmen zeigen", Überstand rot (alle bei
        // "Überstand markieren", sonst nur beim gewählten Element). Jomini-Texte nicht: ihren Rahmen zeichnet DrawBox schon (innerhalb des Beschnitts);
        // ein Beschnitt gilt auch hier (DrawOutline), sonst ragte der Rahmen aus dem Scrollbereich. Jomini-Kästen mit gezeichnetem Sprite ja: dort
        // zeichnet DrawBox keinen schematischen Kasten, also auch keinen Rahmen.
        foreach (var item in _scene.Boxes)
        {
            if ((item.Box.Visual is not null and not JominiTextVisual || _spriteDrawn.Contains(item.Box)) && (item.Box.Uncertain || _showBounds))
            {
                DrawOutline(dc, item.Box, Styles[(item.Category, item.Box.Uncertain)].Pen);
            }
        }

        var selectedBoxes = _selected is null ? (IReadOnlyCollection<LayoutBox>)Array.Empty<LayoutBox>() : _scene.BoxesFor(_selected);
        foreach (var item in _scene.Boxes)
        {
            if (OverflowShown(item.Box, _markAllOverflow, selectedBoxes))
            {
                DrawOutline(dc, item.Box, OverflowPen);
            }
        }

        if (_selected is not null)
        {
            // Ein Knopf und sein Textkind teilen das Element: das Textkind nie umranden (beim vergrößerten 9-Slice ist seine Fläche kleiner als der
            // gezeichnete Knopf), und jede Fläche nur einmal.
            var highlight = new Pen(accent, 2.5);
            var drawn = new HashSet<Rect>();
            foreach (var box in selectedBoxes)
            {
                if (!_buttonLabels.Contains(box) && drawn.Add(ToScreen(box)))
                {
                    DrawOutline(dc, box, highlight);
                }
            }
        }

        // Bedingt sichtbare Elemente (visible = "[…]"): kleines Dreieck in der Ecke oben rechts, zuletzt, damit Kinder und Rahmen es nicht verdecken.
        var marker = TryFindResource("Theme.Warning") as Brush ?? Brushes.OrangeRed;
        foreach (var item in _scene.Boxes)
        {
            if (!item.Box.Conditional)
            {
                continue;
            }

            if (item.Box.Clip is { } clip)
            {
                dc.PushClip(new RectangleGeometry(ToScreen(clip)));
            }

            dc.DrawGeometry(marker, null, ConditionalMarker(ToScreen(item.Box)));
            if (item.Box.Clip is not null)
            {
                dc.Pop();
            }
        }

        dc.Pop();
        dc.Pop();
    }

    /// <summary>Umriss eines Kastens (Überstand, Auswahl); in einem Scrollbereich bzw. unter <c>scissor = yes</c> nur innerhalb dessen Fläche.</summary>
    private void DrawOutline(DrawingContext dc, LayoutBox box, Pen pen)
    {
        if (box.Clip is { } clip)
        {
            dc.PushClip(new RectangleGeometry(ToScreen(clip)));
        }

        dc.DrawRectangle(null, pen, ToScreen(box));
        if (box.Clip is not null)
        {
            dc.Pop();
        }
    }

    /// <summary>Seitenlänge der Marke bedingter Elemente in Bildschirmpixeln (unabhängig vom Zoom).</summary>
    private const double ConditionalMarkerSize = 6;

    /// <summary>Dreieck in der Ecke oben rechts des Kastens (rechter Winkel in der Ecke).</summary>
    private static Geometry ConditionalMarker(Rect rect)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(rect.Right - ConditionalMarkerSize, rect.Top), isFilled: true, isClosed: true);
            context.LineTo(new Point(rect.Right, rect.Top), isStroked: false, isSmoothJoin: false);
            context.LineTo(new Point(rect.Right, rect.Top + ConditionalMarkerSize), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Ein Kasten: klassisch mit Bild Textur bzw. Text; ein Jomini-Kasten mit Textur zuerst das Bild (steht es noch aus, der farbige Kasten wie ohne Textur),
    /// ein Jomini-Text darüber der Text; sonst (reine Container) der farbige Kasten mit Beschriftung. Wo ein Bild steht, gibt es keinen farbigen Kasten
    /// und keine Beschriftung.
    /// </summary>
    private void DrawBox(DrawingContext dc, GuiSceneBox item, bool labels, Brush textBrush, Brush accent, ClassicFontLibrary? fonts, double dpi)
    {
        var rect = ToScreen(item.Box);
        if (item.Box.Visual is { } visual and not JominiTextVisual)
        {
            ClassicVisualRenderer.Draw(dc, visual, rect, _zoom, GuiImageCache.Shared, fonts, dpi, _buttonLabels.Contains(item.Box));
            return;
        }

        var spriteDrawn = item.Box.Sprite is { } sprite && JominiSpriteRenderer.Draw(dc, sprite, rect, _zoom, GuiImageCache.Shared, dpi, item.Box.RenderScale);
        if (spriteDrawn)
        {
            _spriteDrawn.Add(item.Box);
        }
        else
        {
            var (fill, pen) = (_darkBackground ? DarkStyles : Styles)[(item.Category, item.Box.Uncertain)];
            dc.DrawRectangle(fill, pen, rect);
        }

        if (item.Box.Visual is JominiTextVisual jomini)
        {
            // Der Text wird nicht am eigenen Kasten beschnitten (7f); nur LayoutBox.Clip gilt, den OnRender schon um den Aufruf gelegt hat.
            JominiTextRenderer.Draw(dc, jomini, rect, _zoom, dpi, accent, item.Box.RenderScale);
            return;
        }

        if (!spriteDrawn && labels && rect.Width > 40 && rect.Height > 14)
        {
            var label = new FormattedText(item.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, textBrush, dpi)
            {
                MaxTextWidth = Math.Max(1, rect.Width - 4),
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(label, new Point(rect.X + 2, rect.Y + 1));
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var position = e.GetPosition(this);
        var world = new Point((position.X - _pan.X) / _zoom, (position.Y - _pan.Y) / _zoom);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.2 : 1 / 1.2), 0.02, 16);
        _pan = new Vector(position.X - world.X * _zoom, position.Y - world.Y * _zoom);
        _userMoved = true;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton is MouseButton.Right or MouseButton.Middle)
        {
            _dragStart = e.GetPosition(this);
            _panAtDragStart = _pan;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && _scene is not null)
        {
            var position = e.GetPosition(this);
            if (_scene.HitTest((position.X - _pan.X) / _zoom, (position.Y - _pan.Y) / _zoom) is { } box)
            {
                BoxClicked?.Invoke(this, box);
                if (_scene.SelectableNode(box) is { } node)
                {
                    NodeClicked?.Invoke(this, node);
                }
            }

            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragStart is { } start)
        {
            _pan = _panAtDragStart + (e.GetPosition(this) - start);
            _userMoved = true;
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_dragStart is not null)
        {
            _dragStart = null;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private static Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)> CreateStyles(byte fillAlpha)
    {
        var styles = new Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)>();
        foreach (var category in Enum.GetValues<GuiWidgetCategory>())
        {
            var color = ColorOf(category);
            var fill = new SolidColorBrush(Color.FromArgb(fillAlpha, color.R, color.G, color.B));
            fill.Freeze();
            var stroke = new SolidColorBrush(color);
            stroke.Freeze();
            foreach (var dashed in new[] { false, true })
            {
                var pen = new Pen(stroke, 1);
                if (dashed)
                {
                    pen.DashStyle = DashStyles.Dash;
                }

                pen.Freeze();
                styles[(category, dashed)] = (fill, pen);
            }
        }

        return styles;
    }

    /// <summary>Knopftexte: Textkinder mit demselben Element wie ihr Elternkasten (die klassische Engine legt den Knopftext so an); sie stehen senkrecht mittig.</summary>
    private static HashSet<LayoutBox> ButtonLabels(GuiSchematicScene scene)
    {
        var labels = new HashSet<LayoutBox>();
        foreach (var item in scene.Boxes)
        {
            foreach (var child in item.Box.Children)
            {
                if (child.Visual is GuiTextVisual && ReferenceEquals(child.Element, item.Box.Element))
                {
                    labels.Add(child);
                }
            }
        }

        return labels;
    }

    private static Pen CreateOverflowPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x20, 0x20)), 1.5);
        pen.Freeze();
        return pen;
    }

    private Rect ToScreen(LayoutBox box) =>
        new(box.X * _zoom + _pan.X, box.Y * _zoom + _pan.Y, Math.Max(1, box.Width * _zoom), Math.Max(1, box.Height * _zoom));

    /// <summary>Beschnittfläche auf der Zeichenfläche; leer bleibt leer (nichts sichtbar).</summary>
    private Rect ToScreen(LayoutRect rect) =>
        new(rect.X * _zoom + _pan.X, rect.Y * _zoom + _pan.Y, Math.Max(0, rect.Width * _zoom), Math.Max(0, rect.Height * _zoom));
}
