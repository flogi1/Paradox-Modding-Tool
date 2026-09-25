using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Zeichnet die schematische Vorschau: je Element ein Kasten in berechneter Lage und Größe, Farbe nach Art, gestrichelt bei Annäherung.
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

    /// <summary>Beschriftungen nur bis zu dieser Zahl Kästen (große Vanilla-Fenster bleiben so beim Zoomen flüssig).</summary>
    private const int MaxLabelledBoxes = 400;

    private static readonly Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)> Styles = CreateStyles();

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

    public void SetScene(GuiSchematicScene? scene, bool keepView)
    {
        _scene = scene;
        if (!keepView || !_userMoved)
        {
            FitToView();
        }

        InvalidateVisual();
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
        var background = TryFindResource("Theme.EditorBackground") as Brush ?? Brushes.White;
        dc.DrawRectangle(background, null, new Rect(RenderSize));
        if (_scene is null)
        {
            return;
        }

        var textBrush = TryFindResource("Theme.Text") as Brush ?? Brushes.Black;
        var accent = TryFindResource("Theme.Accent") as Brush ?? Brushes.DodgerBlue;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushOpacity(_dimmed ? 0.45 : 1);
        var labels = _scene.Boxes.Count <= MaxLabelledBoxes;
        foreach (var item in _scene.Boxes)
        {
            var rect = ToScreen(item.Box);
            var (fill, pen) = Styles[(item.Category, item.Box.Uncertain)];
            dc.DrawRectangle(fill, pen, rect);
            if (labels && rect.Width > 40 && rect.Height > 14)
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

        if (_selected is not null)
        {
            var highlight = new Pen(accent, 2.5);
            foreach (var box in _scene.BoxesFor(_selected))
            {
                dc.DrawRectangle(null, highlight, ToScreen(box));
            }
        }

        dc.Pop();
        dc.Pop();
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

    private static Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)> CreateStyles()
    {
        var styles = new Dictionary<(GuiWidgetCategory Category, bool Dashed), (Brush Fill, Pen Pen)>();
        foreach (var category in Enum.GetValues<GuiWidgetCategory>())
        {
            var color = ColorOf(category);
            var fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
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

    private Rect ToScreen(LayoutBox box) =>
        new(box.X * _zoom + _pan.X, box.Y * _zoom + _pan.Y, Math.Max(1, box.Width * _zoom), Math.Max(1, box.Height * _zoom));
}
