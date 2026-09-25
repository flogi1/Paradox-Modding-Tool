using System.Globalization;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Berechnet Lage und Größe aufgelöster Elemente: <c>size</c> (Pixel, Prozent), <c>position</c>, <c>parentanchor</c>/<c>widgetanchor</c>,
/// <c>margin</c>, <c>hbox</c>/<c>vbox</c>/<c>flowcontainer</c> mit <c>spacing</c>, <c>expand</c> und <c>layoutpolicy_*</c>, Raster und
/// Listen mit simulierten Einträgen, <c>minimumsize</c>/<c>maximumsize</c>. Icons ohne Größe nehmen die Texturgröße. Annäherungen stehen
/// als Hinweis am Kasten.
/// </summary>
public sealed class GuiLayoutEngine
{
    private static readonly HashSet<string> LinearBoxes = new(StringComparer.Ordinal) { "hbox", "vbox", "flowcontainer", "dynamicgridbox", "overlappingitembox" };

    private readonly GuiRules _rules;
    private readonly Func<string, (int Width, int Height)?> _textureSize;
    private readonly Dictionary<(ResolvedElement Element, double ParentWidth, double ParentHeight), (double W, double H, List<string> Notes)> _measureCache = new();
    private LayoutOptions _options = new();

    public GuiLayoutEngine(GuiRules rules, Func<string, (int Width, int Height)?> textureSize)
    {
        _rules = rules;
        _textureSize = textureSize;
    }

    public LayoutBox Layout(ResolvedElement root, LayoutOptions? options = null)
    {
        _options = options ?? new LayoutOptions();
        _measureCache.Clear();
        var screen = new Rect(0, 0, _options.ScreenWidth, _options.ScreenHeight);
        return Place(root, screen, -1, null, null) ?? new LayoutBox { Element = root, Notes = { GuiTexts.LayoutHidden } };
    }

    private LayoutBox? Place(ResolvedElement element, Rect parent, int repeat, Rect? forced, List<string>? measuredNotes)
    {
        if (!IsVisible(element))
        {
            return null;
        }

        var box = new LayoutBox { Element = element, RepeatIndex = repeat };
        Rect rect;
        if (forced is { } given)
        {
            rect = given;
            if (measuredNotes is not null)
            {
                box.Notes.AddRange(measuredNotes);
            }
        }
        else
        {
            var (width, height, notes) = Measure(element, parent.W, parent.H);
            box.Notes.AddRange(notes);
            var (x, y) = Anchor(element, parent, width, height, box.Notes);
            rect = new Rect(x, y, width, height);
        }

        box.X = rect.X;
        box.Y = rect.Y;
        box.Width = rect.W;
        box.Height = rect.H;
        Arrange(element, box, rect);
        return box;
    }

    private (double W, double H, List<string> Notes) Measure(ResolvedElement element, double parentWidth, double parentHeight)
    {
        var key = (element, parentWidth, parentHeight);
        if (_measureCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var notes = new List<string>();
        var (width, height) = ExplicitSize(element, parentWidth, parentHeight, notes);
        if (double.IsNaN(width) || double.IsNaN(height))
        {
            var (naturalWidth, naturalHeight) = NaturalSize(element, parentWidth, parentHeight, notes);
            if (double.IsNaN(width))
            {
                width = naturalWidth;
            }

            if (double.IsNaN(height))
            {
                height = naturalHeight;
            }
        }

        (width, height) = Clamp(element, width, height, parentWidth, parentHeight);
        var result = (Finite(width), Finite(height), notes);
        _measureCache[key] = result;
        return result;
    }

    private static (double W, double H) ExplicitSize(ResolvedElement element, double parentWidth, double parentHeight, List<string> notes)
    {
        var size = element.Property("size");
        if (size is null)
        {
            return (double.NaN, double.NaN);
        }

        var width = size.Items.Count >= 1 && TryLength(size.Items[0], parentWidth, out var w) ? w : double.NaN;
        var height = size.Items.Count >= 2 && TryLength(size.Items[1], parentHeight, out var h) ? h : double.NaN;
        if (double.IsNaN(width) || double.IsNaN(height))
        {
            notes.Add(GuiTexts.LayoutSizeUnresolved(size.Value));
        }

        return (width, height);
    }

    private (double W, double H) NaturalSize(ResolvedElement element, double parentWidth, double parentHeight, List<string> notes)
    {
        var kind = Kind(element);
        if (kind == "expand")
        {
            return (0, 0);
        }

        if (LinearBoxes.Contains(kind) || kind == "fixedgridbox")
        {
            return MeasureBox(element, parentWidth, parentHeight);
        }

        var texture = element.Property("texture")?.Value;
        if (!string.IsNullOrEmpty(texture) && !texture.Contains('['))
        {
            if (element.Property("framesize") is { Items.Count: >= 2 } frame
                && TryLength(frame.Items[0], parentWidth, out var frameWidth)
                && TryLength(frame.Items[1], parentHeight, out var frameHeight))
            {
                return (frameWidth, frameHeight);
            }

            if (_textureSize(texture) is { } textureSize)
            {
                return (textureSize.Width, textureSize.Height);
            }

            notes.Add(GuiTexts.LayoutTextureMissing(texture));
        }

        if (kind.StartsWith("text", StringComparison.Ordinal))
        {
            var text = element.Property("text")?.Value ?? element.Property("raw_text")?.Value ?? string.Empty;
            notes.Add(GuiTexts.LayoutTextEstimated);
            return (Math.Max(20, text.Length * 7), 20);
        }

        var children = LayoutChildren(element).ToList();
        if (children.Count == 0)
        {
            notes.Add(GuiTexts.LayoutSizeUnknown);
            return (0, 0);
        }

        double right = 0;
        double bottom = 0;
        foreach (var child in children)
        {
            var (childWidth, childHeight, _) = Measure(child, parentWidth, parentHeight);
            var (x, y) = Position(child, parentWidth, parentHeight, null);
            right = Math.Max(right, x + childWidth);
            bottom = Math.Max(bottom, y + childHeight);
        }

        var (left, top, marginRight, marginBottom) = Margins(element);
        return (right + left + marginRight, bottom + top + marginBottom);
    }

    private (double W, double H) MeasureBox(ResolvedElement element, double parentWidth, double parentHeight)
    {
        var (left, top, right, bottom) = Margins(element);
        var items = BoxItems(element);
        if (Kind(element) == "fixedgridbox")
        {
            var columns = GridColumns(element);
            var rows = (int)Math.Ceiling(items.Count / (double)columns);
            var usedColumns = Math.Min(columns, Math.Max(1, items.Count));
            return (usedColumns * Number(element, "addcolumn") + left + right, rows * Number(element, "addrow") + top + bottom);
        }

        var horizontal = IsHorizontal(element);
        var spacing = Number(element, "spacing");
        double main = 0;
        double cross = 0;
        foreach (var (child, _) in items)
        {
            var (width, height, _) = Measure(child, parentWidth, parentHeight);
            main += horizontal ? width : height;
            cross = Math.Max(cross, horizontal ? height : width);
        }

        main += spacing * Math.Max(0, items.Count - 1);
        return horizontal ? (main + left + right, cross + top + bottom) : (cross + left + right, main + top + bottom);
    }

    private void Arrange(ResolvedElement element, LayoutBox box, Rect rect)
    {
        var (left, top, right, bottom) = Margins(element);
        var content = new Rect(rect.X + left, rect.Y + top, Math.Max(0, rect.W - left - right), Math.Max(0, rect.H - top - bottom));
        if (Kind(element) == "fixedgridbox")
        {
            ArrangeGrid(element, box, content);
            return;
        }

        if (LinearBoxes.Contains(Kind(element)))
        {
            ArrangeLinear(element, box, content);
            return;
        }

        foreach (var child in LayoutChildren(element))
        {
            if (Place(child, content, -1, null, null) is { } placed)
            {
                box.Children.Add(placed);
            }
        }
    }

    private void ArrangeLinear(ResolvedElement element, LayoutBox box, Rect content)
    {
        var horizontal = IsHorizontal(element);
        var spacing = Number(element, "spacing");
        var measured = BoxItems(element).Select(item =>
        {
            var (width, height, notes) = Measure(item.Element, content.W, content.H);
            return (item.Element, item.Repeat, W: width, H: height, Notes: notes, Flexible: IsFlexible(item.Element, horizontal));
        }).ToList();

        var used = measured.Sum(m => horizontal ? m.W : m.H) + spacing * Math.Max(0, measured.Count - 1);
        var free = (horizontal ? content.W : content.H) - used;
        var flexible = measured.Count(m => m.Flexible);
        var extra = free > 0 && flexible > 0 && Kind(element) is "hbox" or "vbox" ? free / flexible : 0;

        var cursor = horizontal ? content.X : content.Y;
        foreach (var m in measured)
        {
            var width = m.W + (horizontal && m.Flexible ? extra : 0);
            var height = m.H + (!horizontal && m.Flexible ? extra : 0);
            if (horizontal && Expands(m.Element, "layoutpolicy_vertical"))
            {
                height = content.H;
            }

            if (!horizontal && Expands(m.Element, "layoutpolicy_horizontal"))
            {
                width = content.W;
            }

            var x = horizontal ? cursor : content.X + (content.W - width) / 2;
            var y = horizontal ? content.Y + (content.H - height) / 2 : cursor;
            if (Place(m.Element, content, m.Repeat, new Rect(x, y, width, height), m.Notes) is { } placed)
            {
                box.Children.Add(placed);
            }

            cursor += (horizontal ? width : height) + spacing;
        }
    }

    private void ArrangeGrid(ResolvedElement element, LayoutBox box, Rect content)
    {
        var items = BoxItems(element);
        var columns = GridColumns(element);
        var cellWidth = Number(element, "addcolumn");
        var cellHeight = Number(element, "addrow");
        box.Notes.Add(GuiTexts.LayoutGridApproximated);
        for (var i = 0; i < items.Count; i++)
        {
            var cell = new Rect(content.X + (i % columns) * cellWidth, content.Y + (i / columns) * cellHeight, cellWidth, cellHeight);
            if (Place(items[i].Element, cell, items[i].Repeat, null, null) is { } placed)
            {
                box.Children.Add(placed);
            }
        }
    }

    /// <summary>Einträge eines Kastens: bei Rastern und Listen der erste sichtbare Widget im <c>item</c>-Halter, so oft wiederholt wie eingestellt.</summary>
    private List<(ResolvedElement Element, int Repeat)> BoxItems(ResolvedElement element)
    {
        if (Kind(element) is "fixedgridbox" or "dynamicgridbox" or "overlappingitembox")
        {
            var template = element.Children.FirstOrDefault(c => c.Key == "item")?.Children.FirstOrDefault(IsVisible);
            return template is null
                ? new List<(ResolvedElement, int)>()
                : Enumerable.Range(0, Math.Max(0, _options.ListSampleCount)).Select(i => (template, i)).ToList();
        }

        return LayoutChildren(element).Select(child => (child, -1)).ToList();
    }

    /// <summary>Sichtbare Kinder im Layout. Layout-Halter (z.B. scrollwidget) sind durchlässig, andere Halter (Tooltips) und <c>item</c> fallen weg.</summary>
    private IEnumerable<ResolvedElement> LayoutChildren(ResolvedElement element)
    {
        foreach (var child in element.Children)
        {
            if (child.Role == GuiRole.WidgetHolder)
            {
                if (child.Key == "item" || !_rules.LayoutHolders.Contains(child.Key))
                {
                    continue;
                }

                foreach (var inner in LayoutChildren(child))
                {
                    yield return inner;
                }

                continue;
            }

            if (IsVisible(child))
            {
                yield return child;
            }
        }
    }

    private bool IsVisible(ResolvedElement element)
    {
        var visible = element.Property("visible")?.Value;
        if (visible is null or "yes")
        {
            return true;
        }

        return visible == "no" ? _options.ShowHidden : _options.ShowConditional;
    }

    private static (double X, double Y) Anchor(ResolvedElement element, Rect parent, double width, double height, List<string> notes)
    {
        var (positionX, positionY) = Position(element, parent.W, parent.H, notes);
        var parentAnchor = Anchors(element.Property("parentanchor")?.Value);
        var widgetAnchor = element.Property("widgetanchor") is { } anchor ? Anchors(anchor.Value) : parentAnchor;
        var x = parent.X + parentAnchor.H * parent.W + positionX - widgetAnchor.H * width;
        var y = parent.Y + parentAnchor.V * parent.H + positionY - widgetAnchor.V * height;
        return (x, y);
    }

    private static (double H, double V) Anchors(string? value)
    {
        double horizontal = 0;
        double vertical = 0;
        foreach (var flag in (value ?? string.Empty).Split('|'))
        {
            switch (flag.Trim())
            {
                case "right":
                    horizontal = 1;
                    break;
                case "hcenter":
                    horizontal = 0.5;
                    break;
                case "bottom":
                    vertical = 1;
                    break;
                case "vcenter":
                    vertical = 0.5;
                    break;
                case "center":
                    horizontal = 0.5;
                    vertical = 0.5;
                    break;
            }
        }

        return (horizontal, vertical);
    }

    private static (double X, double Y) Position(ResolvedElement element, double parentWidth, double parentHeight, List<string>? notes)
    {
        var position = element.Property("position");
        if (position is null)
        {
            return (0, 0);
        }

        if (position.Items.Count >= 2 && TryLength(position.Items[0], parentWidth, out var x) && TryLength(position.Items[1], parentHeight, out var y))
        {
            return (x, y);
        }

        notes?.Add(GuiTexts.LayoutPositionUnresolved(position.Value));
        return (0, 0);
    }

    private static (double W, double H) Clamp(ResolvedElement element, double width, double height, double parentWidth, double parentHeight)
    {
        if (element.Property("minimumsize") is { Items.Count: >= 2 } minimum)
        {
            if (TryLength(minimum.Items[0], parentWidth, out var minWidth) && minWidth > 0)
            {
                width = Math.Max(width, minWidth);
            }

            if (TryLength(minimum.Items[1], parentHeight, out var minHeight) && minHeight > 0)
            {
                height = Math.Max(height, minHeight);
            }
        }

        if (element.Property("maximumsize") is { Items.Count: >= 2 } maximum)
        {
            if (TryLength(maximum.Items[0], parentWidth, out var maxWidth) && maxWidth > 0)
            {
                width = Math.Min(width, maxWidth);
            }

            if (TryLength(maximum.Items[1], parentHeight, out var maxHeight) && maxHeight > 0)
            {
                height = Math.Min(height, maxHeight);
            }
        }

        return (width, height);
    }

    private static (double Left, double Top, double Right, double Bottom) Margins(ResolvedElement element)
    {
        double left = 0;
        double top = 0;
        double right = 0;
        double bottom = 0;
        if (element.Property("margin") is { Items.Count: >= 2 } margin
            && TryLength(margin.Items[0], 0, out var marginX)
            && TryLength(margin.Items[1], 0, out var marginY))
        {
            left = right = marginX;
            top = bottom = marginY;
        }

        left = Scalar(element, "margin_left", left);
        right = Scalar(element, "margin_right", right);
        top = Scalar(element, "margin_top", top);
        bottom = Scalar(element, "margin_bottom", bottom);
        return (left, top, right, bottom);
    }

    private static bool IsHorizontal(ResolvedElement element) => Kind(element) switch
    {
        "vbox" => false,
        "flowcontainer" => element.Property("direction")?.Value != "vertical",
        "dynamicgridbox" => element.Property("flipdirection")?.Value == "yes",
        _ => true,
    };

    private static bool Expands(ResolvedElement element, string policy) =>
        element.Property(policy)?.Value is "expanding" or "growing";

    private static bool IsFlexible(ResolvedElement element, bool horizontal) =>
        Kind(element) == "expand" || Expands(element, horizontal ? "layoutpolicy_horizontal" : "layoutpolicy_vertical");

    private static int GridColumns(ResolvedElement element) => Math.Max(1, (int)Scalar(element, "maxhorizontalslots", 1));

    private static double Number(ResolvedElement element, string name) => Scalar(element, name, 0);

    private static double Scalar(ResolvedElement element, string name, double fallback) =>
        element.Property(name) is { } property && TryLength(property.Value, 0, out var value) ? value : fallback;

    private static bool TryLength(string text, double parentLength, out double value)
    {
        value = 0;
        if (text.EndsWith('%'))
        {
            if (!double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                return false;
            }

            value = parentLength * percent / 100;
            return double.IsFinite(value);
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    /// <summary>Engine-Widget ohne das EU-V-Präfix "uber" (Vanilla: type hbox = uberhbox usw.), damit Boxen und Texte erkannt werden.</summary>
    private static string Kind(ResolvedElement element) => GuiWidgetKinds.Normalize(element.BaseWidget);

    private static double Finite(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

    private readonly record struct Rect(double X, double Y, double W, double H);
}
