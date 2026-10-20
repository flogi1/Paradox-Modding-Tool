using System.Globalization;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Wert von <c>layoutpolicy_horizontal</c>/<c>layoutpolicy_vertical</c> in einer Achse.</summary>
internal enum LayoutPolicy
{
    Fixed,
    Preferred,
    Growing,
    Expanding,
    Shrinking,
}

/// <summary>
/// Berechnet Lage und Größe aufgelöster Elemente: <c>size</c> (Pixel, Prozent), <c>position</c>, <c>parentanchor</c>/<c>widgetanchor</c>,
/// <c>margin</c>, <c>hbox</c>/<c>vbox</c>/<c>flowcontainer</c> mit <c>spacing</c>, <c>expand</c> und <c>layoutpolicy_*</c>, Raster und
/// Listen mit simulierten Einträgen (auch hbox/vbox/flowcontainer mit <c>datamodel</c>), <c>minimumsize</c>/<c>maximumsize</c>,
/// <c>background</c> (füllt das Elternelement, ohne Fach) und Beschnitt durch <c>scrollarea</c>/<c>scissor</c>. Ohne <c>size</c> ist
/// ein <c>widget</c> 0×0, ein <c>container</c> die Hülle seiner Kinder und ein Icon so groß wie Textur bzw. Frame geteilt durch
/// <c>texture_density</c>; <c>-1</c> in <c>size</c> heißt "nicht gesetzt". Annäherungen stehen als Hinweis am Kasten.
/// </summary>
public sealed class GuiLayoutEngine : IGuiLayoutEngine
{
    private static readonly HashSet<string> LinearBoxes = new(StringComparer.Ordinal) { "hbox", "vbox", "flowcontainer", "dynamicgridbox", "overlappingitembox" };

    private readonly GuiRules _rules;
    private readonly Func<string, (int Width, int Height)?> _textureSize;
    private readonly JominiTextService? _text;
    private readonly TextureLocator? _textures;
    private readonly Dictionary<(ResolvedElement Element, string? ParentKind, double ParentWidth, double ParentHeight), (double W, double H, List<string> Notes)> _measureCache = new();
    private LayoutOptions _options = new();
    private IReadOnlyDictionary<ResolvedElement, string> _conditionalKeys = new Dictionary<ResolvedElement, string>();

    /// <param name="text">Texte mit den Spielschriften messen und setzen; ohne Dienst bleibt die grobe Schätzung (<see cref="GuiTexts.LayoutTextEstimated"/>).</param>
    /// <param name="textures">Texturfinder für die Texturbilder der Kästen (<see cref="LayoutBox.Sprite"/>); gelesen werden nur Köpfe. Ohne ihn gibt es keine Sprites.</param>
    public GuiLayoutEngine(GuiRules rules, Func<string, (int Width, int Height)?> textureSize, JominiTextService? text = null, TextureLocator? textures = null)
    {
        _rules = rules;
        _textureSize = textureSize;
        _text = text;
        _textures = textures;
    }

    public LayoutBox Layout(ResolvedElement root, LayoutOptions? options = null)
    {
        _options = options ?? new LayoutOptions();
        _measureCache.Clear();
        // Schlüssel der bedingten Elemente beim Absteigen durch den ganzen aufgelösten Baum (auch unsichtbare Geschwister zählen mit).
        _conditionalKeys = GuiConditionalKeys.Collect(root, _options.ViewedFile, _options.PreviewRootNode);
        // Regel 14: Die UI-Skalierung verkleinert den virtuellen Bildschirm (Auflösung / Skalierung); alles darauf ist linear.
        var scale = _options.UiScale > 0 ? _options.UiScale : 1;
        var screen = new Rect(0, 0, _options.ScreenWidth / scale, _options.ScreenHeight / scale);
        if (Place(root, screen, null, -1, null, null, (screen.W, screen.H)) is not { } box)
        {
            return new LayoutBox { Element = root, Notes = { GuiTexts.LayoutHidden }, ConditionalKeys = _conditionalKeys };
        }

        // scale nach dem Layout, vor Beschnitt und Überstand: beide sehen die skalierten Kästen.
        ApplyScale(box, false);
        NoteScaledVisuals(box);
        ApplyClips(box, null);
        ApplyAlpha(box, 1);

        MarkOverflow(box, null, screen);
        box.ConditionalKeys = _conditionalKeys;
        return box;
    }

    /// <summary>
    /// Kästen, die über den Kasten des Elternelements oder den Bildschirm hinausragen (mehr als 1 Pixel, wie klassisch). Ein
    /// <c>widget</c> ohne <c>size</c> (0×0) zählt nicht als Elternkasten: seine Kinder werden gegen den nächsten Vorfahren mit Größe
    /// geprüft. <c>background</c>-Kästen werden nie markiert (ihr margin vergrößert sie nach außen); ihre Kinder schon. Nachkommen eines
    /// beschneidenden Kastens (<c>scrollarea</c>, <c>scissor = yes</c>) ragen gewollt über den Ausschnitt hinaus: gegen ihn nie markiert,
    /// über den Bildschirm hinaus schon.
    /// </summary>
    private static void MarkOverflow(LayoutBox box, Rect? sizedParent, Rect screen)
    {
        var own = new Rect(box.X, box.Y, box.Width, box.Height);
        if (!box.IsBackground)
        {
            if (sizedParent is { } parent && Exceeds(own, parent))
            {
                box.OutsideParent = true;
            }

            if (Exceeds(own, screen))
            {
                box.OutsideScreen = true;
                box.OffScreen = !Intersects(own, screen);
            }
        }

        // Unter einem beschneidenden Kasten fehlt der Elternkasten (auch durch widgets ohne size hindurch).
        var next = Clips(box) ? null : box.Width <= 0 && box.Height <= 0 ? sizedParent : own;
        foreach (var child in box.Children)
        {
            MarkOverflow(child, next, screen);
        }
    }

    private static bool Exceeds(Rect box, Rect outer) =>
        box.X < outer.X - 1 || box.Y < outer.Y - 1 || box.X + box.W > outer.X + outer.W + 1 || box.Y + box.H > outer.Y + outer.H + 1;

    private static bool Intersects(Rect box, Rect outer) =>
        box.X < outer.X + outer.W && box.X + box.W > outer.X && box.Y < outer.Y + outer.H && box.Y + box.H > outer.Y;

    /// <summary>Unter dieser Deckkraft (eigenes × geerbtes <c>alpha</c>) gilt ein Kasten als unsichtbar und bekommt den Hinweis <see cref="GuiTexts.LayoutAlphaInvisible"/>.</summary>
    private const double InvisibleAlpha = 0.05;

    /// <summary>
    /// Probe 13x (alle drei Spiele): <c>alpha</c> eines Elements gilt auch für seine Nachkommen (auch einen <c>background</c>). Die Deckkraft des Sprites
    /// enthält schon das eigene <c>alpha</c>; hier kommen die Vorfahren dazu (<see cref="JominiSpriteRules.AlphaInherited"/>).
    /// Nie still: Der oberste Kasten, an dem die Deckkraft unter <see cref="InvisibleAlpha"/> fällt, bekommt einen Hinweis (die Vorschau zeigt die Endwerte von
    /// <c>_show</c> und seiner <c>next</c>-Kette; andere Zustände wie <c>_mouse_enter</c> oder eigene Namen und Animationen blenden im Spiel oft erst ein). Text ist für <c>alpha</c> nicht gemessen und bleibt voll deckend, ebenfalls mit Hinweis. Ein
    /// <c>alpha</c>, das keine Zahl ist (Bindung, Prozent), gilt als 1, mit Hinweis am Kasten.
    /// </summary>
    private static void ApplyAlpha(LayoutBox box, double inherited)
    {
        if (JominiSpriteRules.AlphaInherited && inherited < 1 && box.Sprite is { } sprite)
        {
            box.Sprite = sprite with { Opacity = Math.Clamp(sprite.Opacity * inherited, 0, 1) };
        }

        double own = 1;
        if (JominiSprites.OwnAlpha(box.Element) is { } alpha)
        {
            own = Math.Clamp(alpha, 0, 1);
        }
        else
        {
            AddNoteOnce(box, GuiTexts.LayoutTextureApproximated("alpha"));
        }

        var effective = inherited * own;
        if (effective < InvisibleAlpha && inherited >= InvisibleAlpha)
        {
            AddNoteOnce(box, GuiTexts.LayoutAlphaInvisible);
            if (!HasArea(box))
            {
                // Ohne Fläche (etwa widget ohne size, die übliche Einblend-Vorlage) wäre der gestrichelte Rahmen unsichtbar: den Hinweis zusätzlich an
                // die nächsten Nachkommen mit Fläche hängen (wie in MarkOverflow durch Kästen ohne Fläche hindurch).
                NoteInvisibleOnFirstWithArea(box);
            }
        }

        if (effective < 1 && box.Visual is JominiTextVisual)
        {
            AddNoteOnce(box, GuiTexts.LayoutAlphaTextUnmeasured);
        }

        var forChildren = JominiSpriteRules.AlphaInherited ? effective : 1;
        foreach (var child in box.Children)
        {
            ApplyAlpha(child, forChildren);
        }
    }

    private static bool HasArea(LayoutBox box) => box.Width > 0 && box.Height > 0;

    /// <summary>
    /// Jomini <c>scale</c> (Lauf 6, alle drei Spiele): Nach dem normalen Layout werden der Kasten und alle Nachkommen um die Ecke oben links des Elements
    /// skaliert (x′ = ox + (x − ox) × s, b′ = b × s); <c>position</c> und das Fach im Elternelement bleiben (15a, 15b). Von innen nach außen, so multiplizieren
    /// sich verschachtelte Faktoren in <see cref="LayoutBox.RenderScale"/>; verschachtelt ist nicht gemessen (Hinweis am inneren Kasten).
    /// <paramref name="scaledAbove"/>: ein Vorfahr hat ein eigenes <c>scale</c> ≠ 1.
    /// </summary>
    private static void ApplyScale(LayoutBox box, bool scaledAbove)
    {
        var own = OwnScale(box.Element, out var note);
        if (note is not null)
        {
            AddNoteOnce(box, note);
        }

        var scaled = own != 1;
        if (scaled && scaledAbove)
        {
            AddNoteOnce(box, GuiTexts.LayoutScaleApproximated("scale × scale"));
        }

        if (scaled && HasNonTopLeftAnchor(box.Element))
        {
            // 15a/15b messen den Drehpunkt (Ecke oben links) nur an Elementen, die oben links sitzen; mit anderem Anker nicht gemessen.
            AddNoteOnce(box, GuiTexts.LayoutScaleApproximated("anchor"));
        }

        foreach (var child in box.Children)
        {
            ApplyScale(child, scaledAbove || scaled);
        }

        if (scaled)
        {
            ScaleSubtree(box, box.X, box.Y, own);
        }
    }

    /// <summary>Hat das Element einen <c>parentanchor</c> oder <c>widgetanchor</c> außer oben links (right, hcenter, bottom, vcenter, center)?</summary>
    private static bool HasNonTopLeftAnchor(ResolvedElement element) =>
        Anchors(element.Property("parentanchor")?.Value) != (0, 0) || Anchors(element.Property("widgetanchor")?.Value) != (0, 0);

    private static void ScaleSubtree(LayoutBox box, double originX, double originY, double factor)
    {
        box.X = originX + (box.X - originX) * factor;
        box.Y = originY + (box.Y - originY) * factor;
        box.Width *= factor;
        box.Height *= factor;
        box.RenderScale *= factor;
        foreach (var child in box.Children)
        {
            ScaleSubtree(child, originX, originY, factor);
        }
    }

    /// <summary>
    /// Eigenes <c>scale</c> (mit <c>_show</c> und seiner <c>next</c>-Kette); fehlend = 1. Gilt als 1 mit <paramref name="note"/>: eine Bindung oder unbekannte
    /// Konstante (<see cref="GuiTexts.LayoutScaleUnresolved"/>), jede andere Angabe, die keine positive Zahl ist (0, negativ, Prozent, Text;
    /// <see cref="GuiTexts.LayoutScaleInvalid"/>). Eine Bindung in <c>_show</c> lässt den Wert davor stehen (Hinweis aus NoteShowBindings).
    /// </summary>
    private static double OwnScale(ResolvedElement element, out string? note)
    {
        note = null;
        if (JominiShowState.Property(element, "scale") is not { } property)
        {
            return 1;
        }

        if (!property.Value.EndsWith('%') && TryLength(property.Value, 0, out var value) && value > 0)
        {
            return value;
        }

        note = property.Unresolved || property.Value.Contains('[')
            ? GuiTexts.LayoutScaleUnresolved(property.Value)
            : GuiTexts.LayoutScaleInvalid(property.Value);
        return 1;
    }

    /// <summary>
    /// Hinweise an skaliert gezeichneten Kästen, wo <c>scale</c> nicht gemessen ist: Text unter einem Faktor ≠ 1 (gemessen nur 15 × 0,5 am Textfeld selbst),
    /// Ränder und Kacheln eines Sprites (mitskaliert angenommen).
    /// </summary>
    private static void NoteScaledVisuals(LayoutBox box)
    {
        if (box.RenderScale != 1)
        {
            if (box.Visual is JominiTextVisual)
            {
                AddNoteOnce(box, GuiTexts.LayoutScaleApproximated("text"));
            }

            if (box.Sprite is { Placeholder: JominiSpritePlaceholder.None } sprite)
            {
                var border = sprite.Border;
                if (sprite.Kind is JominiSpriteKind.CorneredStretched or JominiSpriteKind.CorneredTiled
                    && (border.Left != 0 || border.Top != 0 || border.Right != 0 || border.Bottom != 0))
                {
                    AddNoteOnce(box, GuiTexts.LayoutScaleApproximated("spriteborder"));
                }

                if (sprite.Kind is JominiSpriteKind.Tiled or JominiSpriteKind.CorneredTiled)
                {
                    AddNoteOnce(box, GuiTexts.LayoutScaleApproximated("tiling"));
                }
            }
        }

        foreach (var child in box.Children)
        {
            NoteScaledVisuals(child);
        }
    }

    /// <summary>Hängt <see cref="GuiTexts.LayoutAlphaInvisible"/> an jeden ersten Nachkommen mit Fläche; Kästen ohne Fläche werden durchlaufen.</summary>
    private static void NoteInvisibleOnFirstWithArea(LayoutBox box)
    {
        foreach (var child in box.Children)
        {
            if (HasArea(child))
            {
                AddNoteOnce(child, GuiTexts.LayoutAlphaInvisible);
            }
            else
            {
                NoteInvisibleOnFirstWithArea(child);
            }
        }
    }

    private static void AddNoteOnce(LayoutBox box, string note)
    {
        if (!box.Notes.Contains(note))
        {
            box.Notes.Add(note);
        }
    }

    /// <summary>
    /// <c>scrollarea</c> und Elemente mit <c>scissor = yes</c> beschneiden alle Nachkommen auf ihren eigenen Kasten, geschnitten mit
    /// dem Beschnitt ihrer Vorfahren; der beschneidende Kasten selbst trägt nur den Beschnitt der Vorfahren.
    /// </summary>
    private static void ApplyClips(LayoutBox box, LayoutRect? clip)
    {
        box.Clip = clip;
        var inner = clip;
        if (Clips(box))
        {
            var own = new LayoutRect(box.X, box.Y, box.Width, box.Height);
            inner = clip is { } outer ? Intersect(outer, own) : own;
        }

        foreach (var child in box.Children)
        {
            // Ein background (mit margin auch außerhalb des Kastens, 8c) gilt nur für äußere Beschnitte, nicht für den seines
            // Elternelements. Nicht gemessen: so bleibt der nach außen vergrößerte Rand sichtbar.
            ApplyClips(child, child.IsBackground ? clip : inner);
        }
    }

    private static bool Clips(LayoutBox box) => Kind(box.Element) == "scrollarea" || box.Element.Property("scissor")?.Value == "yes";

    private static LayoutRect Intersect(LayoutRect a, LayoutRect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new LayoutRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// Legt ein Element in den Inhalt seines Elternelements. <paramref name="parentKind"/> ist die Art des Elternelements
    /// (<c>null</c> für den Bildschirm); davon hängt ab, ob eine hbox/vbox ohne <c>size</c> das Elternelement füllt.
    /// <paramref name="measuredAgainst"/> ist die Länge des Elternelements, gegen die dieses Element gemessen wurde (Bezug für alle
    /// Prozentwerte beim Legen). Sie weicht vom Inhalt <paramref name="parent"/> ab, wenn eine Box ohne size ihre Kinder gegen die Länge
    /// ihres eigenen Elternelements gemessen hat.
    /// </summary>
    private LayoutBox? Place(ResolvedElement element, Rect parent, string? parentKind, int repeat, Rect? forced, List<string>? measuredNotes, (double W, double H) measuredAgainst)
    {
        if (!IsVisible(element))
        {
            return null;
        }

        var conditionalKey = _conditionalKeys.GetValueOrDefault(element);
        var box = new LayoutBox { Element = element, RepeatIndex = repeat, Conditional = conditionalKey is not null, ConditionalKey = conditionalKey };
        if (Kind(element) is "hbox" or "vbox" && parentKind is "flowcontainer" or "container")
        {
            // Regel 15: Das Spiel stürzt ab; gelegt wird die Box wie in einem widget.
            box.Notes.Add(GuiTexts.LayoutBoxInContainer(parentKind));
        }

        Rect rect;
        var filled = false;
        if (forced is { } given)
        {
            rect = given;
            if (measuredNotes is not null)
            {
                box.Notes.AddRange(measuredNotes);
            }
        }
        else if (FillsParent(element, parentKind))
        {
            // Regel 1: hbox/vbox ohne size füllt den Inhalt eines Elternelements, das keine Box ist; Anker gelten nicht.
            if (HasAnchorOrPosition(element))
            {
                box.Notes.Add(GuiTexts.LayoutFillIgnoresAnchor);
            }

            if (parent.W <= 0 && parent.H <= 0)
            {
                // Elternelement 0×0 (widget ohne size): nicht gemessen, daher Inhaltsgröße am Ursprung des Elternelements.
                var (width, height, notes) = Measure(element, parentKind, parent.W, parent.H);
                box.Notes.AddRange(notes);
                box.Notes.Add(GuiTexts.LayoutBoxInZeroParent);
                rect = new Rect(parent.X, parent.Y, width, height);
            }
            else
            {
                // minimum- und maximumsize begrenzen auch die füllende Box; sie bleibt am Ursprung des Elternelements.
                var (width, height) = Clamp(element, parent.W, parent.H, parent.W, parent.H);
                rect = new Rect(parent.X, parent.Y, width, height);
                filled = true;
            }
        }
        else
        {
            if (SizelessBoxInPlainParent(element, parentKind) && HasAnchorOrPosition(element))
            {
                // set_parent_size_to_minimum (füllt nicht, Probe 10b): gemessen nur ohne position und Anker; hier wie in einem widget gelegt.
                box.Notes.Add(GuiTexts.LayoutMinSizeBoxAnchorUnmeasured);
            }

            var (width, height, notes) = Measure(element, parentKind, parent.W, parent.H);
            box.Notes.AddRange(notes);
            var (x, y) = Anchor(element, parent, width, height, box.Notes);
            rect = new Rect(x, y, width, height);
        }

        box.X = rect.X;
        box.Y = rect.Y;
        box.Width = rect.W;
        box.Height = rect.H;
        SetTextVisual(element, box, rect);
        SetSprite(element, box);
        NoteShowBindings(element, box);
        Arrange(element, box, rect, measuredAgainst, filled);
        NoteIgnoredResizeParent(element, box);
        return box;
    }

    /// <summary>
    /// Eine Bindung in <c>_show</c> oder seiner <c>next</c>-Kette (<c>size</c>, <c>position</c>, <c>alpha</c>, <c>scale</c>) ist zur Laufzeit offen und wird nicht angewandt:
    /// der Wert davor bleibt, der Kasten trägt den üblichen Hinweis der Eigenschaft. Eine Kette, die sich wiederholt, bekommt
    /// <see cref="GuiTexts.LayoutShowNextApproximated"/>.
    /// </summary>
    private static void NoteShowBindings(ResolvedElement element, LayoutBox box)
    {
        var overlay = JominiShowState.Overlay(element);
        foreach (var bound in overlay.Ignored)
        {
            AddNoteOnce(box, bound.Name switch
            {
                "size" => GuiTexts.LayoutSizeUnresolved(bound.Value),
                "position" => GuiTexts.LayoutPositionUnresolved(bound.Value),
                "scale" => GuiTexts.LayoutScaleUnresolved(bound.Value),
                _ => GuiTexts.LayoutTextureApproximated(bound.Name),
            });
        }

        if (overlay.ChainApproximated)
        {
            AddNoteOnce(box, GuiTexts.LayoutShowNextApproximated);
        }
    }

    /// <summary>
    /// Textbild für Textfelder und jedes Element mit <c>text</c>/<c>raw_text</c> (Buttons: nur das Bild, die Größe bleibt), gesetzt in den fertigen Kasten
    /// (Kürzen an seiner Breite). Hinweise, die schon die Messung brachte, kommen nicht doppelt.
    /// </summary>
    private void SetTextVisual(ResolvedElement element, LayoutBox box, Rect rect)
    {
        if (_text is null || !(MeasuresText(element) || element.Property("text") is not null || element.Property("raw_text") is not null))
        {
            return;
        }

        var notes = new List<string>();
        // Beschriftungen (Buttons): die Größe hängt nicht am Text, Annahmen zu Schriftgröße und Schrift bleiben ohne Hinweis (Ruling Fix-Runde 1).
        box.Visual = _text.Visual(element, rect.W, _options.Language, notes, labelOnly: !MeasuresText(element), scale: OwnScale(element, out _));
        foreach (var note in notes.Where(note => !box.Notes.Contains(note)))
        {
            box.Notes.Add(note);
        }
    }

    /// <summary>
    /// Texturbild des Kastens (neben dem Text: ein Button behält Textur und Beschriftung). Hinweise, die schon die Messung brachte (z. B. fehlende
    /// Textur aus <see cref="IconSize"/>), kommen nicht doppelt.
    /// </summary>
    private void SetSprite(ResolvedElement element, LayoutBox box)
    {
        if (_textures is null)
        {
            return;
        }

        var notes = new List<string>();
        box.Sprite = JominiSprites.Build(element, _textures, notes, IsBackground(element));
        foreach (var note in notes.Where(note => !box.Notes.Contains(note)))
        {
            box.Notes.Add(note);
        }
    }

    /// <summary>
    /// resizeparent = yes wirkt nur auf ein widget/container ohne size (Probe 10a); ein Elternelement mit Größe behält sie (nicht gemessen, Hinweis am
    /// Textfeld).
    /// </summary>
    private void NoteIgnoredResizeParent(ResolvedElement element, LayoutBox box)
    {
        if (_text is null || (Kind(element) is "widget" or "container" && JominiShowState.Property(element, "size") is null))
        {
            return;
        }

        foreach (var child in box.Children.Where(child => MeasuresText(child.Element) && ResizesParent(child.Element)))
        {
            child.Notes.Add(GuiTexts.LayoutTextApproximated("resizeparent"));
        }
    }

    /// <summary>Textfeld, das der Textdienst misst (ohne Dienst bleibt die Schätzung).</summary>
    private bool MeasuresText(ResolvedElement element) => _text is not null && IsTextField(Kind(element));

    /// <summary>
    /// Textfeld-Art (textbox, text_single, text_multi, ...): beginnt mit "text", aber nicht die Definitionsblöcke <c>texticon</c> und
    /// <c>textformatting</c> (als Wurzel ausgelegt sonst je eine Schrift-Notiz).
    /// </summary>
    private static bool IsTextField(string kind) =>
        kind.StartsWith("text", StringComparison.Ordinal) && kind is not ("texticon" or "textformatting");

    private static bool AutoResizes(ResolvedElement element) => element.Property("autoresize")?.Value == "yes";

    private static bool ResizesParent(ResolvedElement element) => element.Property("resizeparent")?.Value == "yes";

    /// <summary>Erstes sichtbares Textfeld mit <c>resizeparent = yes</c> unter den Kindern.</summary>
    private ResolvedElement? ResizingTextChild(ResolvedElement element) =>
        LayoutChildren(element).FirstOrDefault(child => MeasuresText(child) && ResizesParent(child));

    /// <summary>
    /// Größe eines Elements im Inhalt seines Elternelements. Die Hinweise hängen von der Art des Elternelements ab
    /// (<paramref name="parentKind"/>), daher gehört sie zum Cache-Schlüssel.
    /// </summary>
    private (double W, double H, List<string> Notes) Measure(ResolvedElement element, string? parentKind, double parentWidth, double parentHeight)
    {
        var key = (element, parentKind, parentWidth, parentHeight);
        if (_measureCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var notes = new List<string>();
        if (parentKind is "hbox" or "vbox" && element.Property("ignore_layout")?.Value != "yes")
        {
            // Regel 5: Kinder von hbox/vbox legt die Box; Anker und position wirken nicht, das Spiel meldet einen Fehler.
            // Mit ignore_layout = yes (Vanilla, z.B. EU-V-Cooltip-Vorlagen) sind Anker gewollt: kein Fehlerhinweis.
            if (element.Property("parentanchor") is not null)
            {
                notes.Add(GuiTexts.LayoutGameError("parentanchor in hbox/vbox"));
            }
            else if (element.Property("widgetanchor") is not null)
            {
                notes.Add(GuiTexts.LayoutGameError("widgetanchor in hbox/vbox"));
            }

            if (JominiShowState.Property(element, "position") is not null)
            {
                notes.Add(GuiTexts.LayoutGameError("position in hbox/vbox"));
            }
        }

        // Regel 13: autoresize = yes setzt die Größe auf den Text und schlägt size (7e); minimum-/maximumsize klemmen danach wie sonst.
        var (width, height) = MeasuresText(element) && AutoResizes(element)
            ? (double.NaN, double.NaN)
            : ExplicitSize(element, parentWidth, parentHeight, notes);
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
        var size = JominiShowState.Property(element, "size");
        if (size is null)
        {
            return (double.NaN, double.NaN);
        }

        var width = SizeItem(size, 0, parentWidth, out var widthResolved);
        var height = SizeItem(size, 1, parentHeight, out var heightResolved);
        if (!widthResolved || !heightResolved)
        {
            notes.Add(GuiTexts.LayoutSizeUnresolved(size.Value));
        }

        return (width, height);
    }

    /// <summary>
    /// Eine Achse von <c>size</c>. <c>-1</c> heißt "nicht gesetzt" (Regel 10): NaN, die natürliche Größe gilt, ohne Hinweis.
    /// Nicht auswertbare Werte geben ebenfalls NaN, aber <paramref name="resolved"/> = <c>false</c>.
    /// </summary>
    private static double SizeItem(ResolvedProperty size, int index, double parentLength, out bool resolved)
    {
        resolved = true;
        if (size.Items.Count > index && IsUnset(size.Items[index]))
        {
            return double.NaN;
        }

        if (size.Items.Count > index && TryLength(size.Items[index], parentLength, out var value))
        {
            return value;
        }

        resolved = false;
        return double.NaN;
    }

    /// <summary><c>-1</c> in einer Achse von <c>size</c>: "nicht gesetzt" (Probe 6d/6e).</summary>
    private static bool IsUnset(string item) => !item.EndsWith('%') && TryLength(item, 0, out var value) && value == -1;

    private (double W, double H) NaturalSize(ResolvedElement element, double parentWidth, double parentHeight, List<string> notes)
    {
        var kind = Kind(element);
        if (kind == "expand")
        {
            return (0, 0);
        }

        if (LinearBoxes.Contains(kind) || kind == "fixedgridbox")
        {
            return MeasureBox(element, parentWidth, parentHeight, notes);
        }

        // Eine Achse mit size -1 nimmt weder die Texturgröße noch die Hülle der Kinder: frei steht das Element dort mit 0 da
        // (Probe 6d), in einer Box füllt es nach seiner Policy (6e). Nur der container nimmt auch dann die Hülle (Regel 9).
        // Gemessen ist -1 nur an Icons, Boxen und containern; an anderen Arten (button, scrollarea, window, widget …) mit Hinweis.
        var unset = JominiShowState.Property(element, "size")?.Items.Any(IsUnset) == true;
        if (_text is not null && kind is "widget" or "container" && ResizingTextChild(element) is { } sizer)
        {
            // Probe 10a: resizeparent = yes macht ein widget/container ohne size so groß wie das erste solche Textfeld.
            var (sizerWidth, sizerHeight, _) = Measure(sizer, kind, parentWidth, parentHeight);
            return (sizerWidth, sizerHeight);
        }

        if (kind == "widget")
        {
            // Regel 9 (Probe 6b): widget ohne size ist 0×0 und wächst nicht mit den Kindern; die Kinder ragen hinaus.
            if (unset)
            {
                notes.Add(GuiTexts.LayoutMinusOneUnmeasured(kind));
            }

            return (0, 0);
        }

        var texture = element.Property("texture")?.Value;
        if (!unset && !string.IsNullOrEmpty(texture) && !texture.Contains('['))
        {
            if (IconSize(element, texture, notes) is { } iconSize)
            {
                return iconSize;
            }
        }

        if (IsTextField(kind))
        {
            if (_text is not null)
            {
                // Regel 13: mit autoresize die Textgröße (mit multiline + max_width umbrochen); ohne size und ohne autoresize 0×0, der Text läuft
                // trotzdem ab dem Ursprung hinaus (Probe 11g, alle drei Spiele, ohne Hinweis).
                // Eigenes scale (Lauf 6, 15c): Größe aus dem in floor(fontsize × scale) gesetzten Text, durch scale geteilt.
                return AutoResizes(element) ? _text.Measure(element, null, _options.Language, notes, OwnScale(element, out _)) : (0, 0);
            }

            var text = element.Property("text")?.Value ?? element.Property("raw_text")?.Value ?? string.Empty;
            notes.Add(GuiTexts.LayoutTextEstimated);
            return (Math.Max(20, text.Length * 7), 20);
        }

        if (unset && kind != "container")
        {
            if (kind != "icon")
            {
                notes.Add(GuiTexts.LayoutMinusOneUnmeasured(kind));
            }

            return (0, 0);
        }

        // Regel 12: ein background trägt nicht zur Hülle bei (Probe 9j).
        var children = LayoutChildren(element).Where(child => !IsBackground(child)).ToList();
        if (children.Count == 0)
        {
            notes.Add(GuiTexts.LayoutSizeUnknown);
            return (0, 0);
        }

        double right = 0;
        double bottom = 0;
        foreach (var child in children)
        {
            var (childWidth, childHeight, _) = Measure(child, kind, parentWidth, parentHeight);
            var (x, y) = Position(child, parentWidth, parentHeight, null);
            right = Math.Max(right, x + childWidth);
            bottom = Math.Max(bottom, y + childHeight);
        }

        var (left, top, marginRight, marginBottom) = Margins(element);
        return (right + left + marginRight, bottom + top + marginBottom);
    }

    /// <summary>
    /// Natürliche Größe eines Icons (Regel 11): <c>framesize</c>, sonst mit <c>frame_grid = { c r }</c> Textur / (c, r), sonst die
    /// Texturgröße; alles geteilt durch <c>texture_density</c> (fehlend oder ≤ 0 = 1). Ein Bild ist ganze Pixel groß (14d: 6 × 33 = 198 bei 100 / 3 und bei
    /// <c>framesize = { 33.3333 20 }</c>), die Teilung durch die Dichte bleibt gebrochen (14c: 6 × 50 / 3 = 100). <c>null</c>, wenn die Textur fehlt (mit Hinweis).
    /// </summary>
    private (double W, double H)? IconSize(ResolvedElement element, string texture, List<string> notes)
    {
        var density = Scalar(element, "texture_density", 1);
        if (density <= 0)
        {
            density = 1;
        }

        if (element.Property("framesize") is { } frame)
        {
            if (frame.Items.Count >= 2
                && !frame.Items[0].EndsWith('%') && !frame.Items[1].EndsWith('%')
                && TryLength(frame.Items[0], 0, out var frameWidth)
                && TryLength(frame.Items[1], 0, out var frameHeight))
            {
                return (JominiSprites.WholePixels(frameWidth, notes) / density, JominiSprites.WholePixels(frameHeight, notes) / density);
            }

            // Datenbindung oder Prozent: nicht auswertbar, die ganze Textur (gleich wie beim Sprite).
            var note = GuiTexts.LayoutTextureApproximated("framesize");
            if (!notes.Contains(note))
            {
                notes.Add(note);
            }
        }

        if (_textureSize(texture) is not { } textureSize)
        {
            // "Nicht gefunden" nur, wenn die Datei fehlt (oder kein Texturfinder da ist). Eine vorhandene, aber leere oder unlesbare Datei nennt das
            // Sprite mit seinem Formathinweis.
            if (_textures?.Locate(texture) is null)
            {
                notes.Add(GuiTexts.LayoutTextureMissing(texture));
            }

            return null;
        }

        if (element.Property("framesize") is null && element.Property("frame_grid") is { Items.Count: >= 2 } grid)
        {
            // Spalten und Zeilen ganzzahlig wie beim Bildausschnitt (JominiSprites.FrameSource).
            var columns = TryLength(grid.Items[0], 0, out var c) && c >= 1 ? Math.Floor(c) : 1;
            var rows = TryLength(grid.Items[1], 0, out var r) && r >= 1 ? Math.Floor(r) : 1;
            return (JominiSprites.WholePixels(textureSize.Width / columns, notes) / density, JominiSprites.WholePixels(textureSize.Height / rows, notes) / density);
        }

        return (textureSize.Width / density, textureSize.Height / density);
    }

    private (double W, double H) MeasureBox(ResolvedElement element, double parentWidth, double parentHeight, List<string> notes)
    {
        var (left, top, right, bottom) = Margins(element);
        var items = BoxItems(element);
        if (Kind(element) == "fixedgridbox")
        {
            // Prozent in addcolumn/addrow ohne size in dieser Achse: Bezug ist die Länge des Elternelements, nicht gemessen.
            if ((IsPercent(element, "addcolumn") && OwnLength(element, 0, parentWidth) is null)
                || (IsPercent(element, "addrow") && OwnLength(element, 1, parentHeight) is null))
            {
                notes.Add(GuiTexts.LayoutPercentInSizelessBox);
            }

            var columns = GridColumns(element);
            var rows = (int)Math.Ceiling(items.Count / (double)columns);
            var usedColumns = Math.Min(columns, Math.Max(1, items.Count));
            var (cellWidth, cellHeight) = GridCell(element, parentWidth, parentHeight);
            return (usedColumns * cellWidth + left + right, rows * cellHeight + top + bottom);
        }

        var horizontal = IsHorizontal(element);
        var spacing = Number(element, "spacing");
        // Prozent der Kinder: vom eigenen Inhalt, wenn die Achse eine size hat; sonst (Größe folgt erst aus den Kindern) von der Länge des
        // Elternelements, nicht gemessen und daher mit Hinweis. ArrangeLinear nimmt denselben Bezug.
        var ownWidth = OwnLength(element, 0, parentWidth);
        var ownHeight = OwnLength(element, 1, parentHeight);
        var referenceWidth = ownWidth is { } knownWidth ? Math.Max(0, knownWidth - left - right) : parentWidth;
        var referenceHeight = ownHeight is { } knownHeight ? Math.Max(0, knownHeight - top - bottom) : parentHeight;
        if (items.Any(item => (ownWidth is null && UsesPercent(item.Element, 0)) || (ownHeight is null && UsesPercent(item.Element, 1))))
        {
            notes.Add(GuiTexts.LayoutPercentInSizelessBox);
        }

        double main = 0;
        double cross = 0;
        foreach (var (child, _) in items)
        {
            var (width, height, _) = Measure(child, Kind(element), referenceWidth, referenceHeight);
            main += horizontal ? width : height;
            cross = Math.Max(cross, horizontal ? height : width);
        }

        main += spacing * Math.Max(0, items.Count - 1);
        return horizontal ? (main + left + right, cross + top + bottom) : (cross + left + right, main + top + bottom);
    }

    /// <summary>
    /// Zellgröße eines fixedgridbox aus <c>addcolumn</c>/<c>addrow</c>. Prozent beziehen sich auf den Inhalt des Rasters: die Achse
    /// seiner <c>size</c>, ohne size (oder mit <c>-1</c>) die Länge des Elternelements, jeweils ohne margin. Messen und Legen nutzen
    /// denselben Bezug, damit die gemessene Größe und die gelegten Zellen übereinstimmen.
    /// </summary>
    private static (double W, double H) GridCell(ResolvedElement grid, double parentWidth, double parentHeight)
    {
        var (left, top, right, bottom) = Margins(grid);
        var width = OwnLength(grid, 0, parentWidth) ?? parentWidth;
        var height = OwnLength(grid, 1, parentHeight) ?? parentHeight;
        return (Length(grid, "addcolumn", width - left - right), Length(grid, "addrow", height - top - bottom));
    }

    /// <summary>Eine Achse von <c>size</c>, wenn gesetzt und auswertbar (nicht <c>-1</c>), Prozent von <paramref name="parentLength"/>; sonst <c>null</c>.</summary>
    private static double? OwnLength(ResolvedElement element, int index, double parentLength) =>
        JominiShowState.Property(element, "size") is { } size && size.Items.Count > index && !IsUnset(size.Items[index]) && TryLength(size.Items[index], parentLength, out var value)
            ? value
            : null;

    /// <summary>Ist die Eigenschaft <paramref name="name"/> ein Prozentwert?</summary>
    private static bool IsPercent(ResolvedElement element, string name) => element.Property(name)?.Value.EndsWith('%') == true;

    /// <summary>Hat <c>size</c>, <c>minimumsize</c> oder <c>maximumsize</c> in dieser Achse einen Prozentwert?</summary>
    private static bool UsesPercent(ResolvedElement element, int index) =>
        new[] { "size", "minimumsize", "maximumsize" }.Any(name =>
            (name == "size" ? JominiShowState.Property(element, name) : element.Property(name)) is { } property && property.Items.Count > index && property.Items[index].EndsWith('%'));

    /// <summary>
    /// Legt die Kinder in den Kasten <paramref name="rect"/>; <paramref name="measuredAgainst"/> ist die Länge des Elternelements, gegen
    /// die das Element gemessen wurde (Bezug für Prozent). <paramref name="filled"/>: die Box füllt ihr Elternelement (Größe vom Elternelement, nicht von den Kindern).
    /// </summary>
    private void Arrange(ResolvedElement element, LayoutBox box, Rect rect, (double W, double H) measuredAgainst, bool filled)
    {
        if (Kind(element) == "widget" && MarginNames.Any(name => element.Property(name) is not null))
        {
            // Regel 9: margin wirkt nur an Boxen und background; an einem widget meldet das Spiel einen Fehler.
            box.Notes.Add(GuiTexts.LayoutGameError("margin an widget"));
        }

        if (Kind(element) is "hbox" or "vbox" && element.Property("align") is not null)
        {
            // align gilt in Boxen nicht (Probe 3b–3e).
            box.Notes.Add(GuiTexts.LayoutGameError("align an hbox/vbox"));
        }

        // Regel 12: background füllt den ganzen Kasten (um seinen margin verändert), belegt kein Fach und wird zuerst gezeichnet.
        foreach (var background in LayoutChildren(element).Where(IsBackground))
        {
            var backgroundRect = BackgroundRect(background, rect);
            if (Place(background, rect, Kind(element), -1, backgroundRect, null, (backgroundRect.W, backgroundRect.H)) is { } placed)
            {
                placed.IsBackground = true;
                box.Children.Add(placed);
            }
        }

        var (left, top, right, bottom) = Margins(element);
        var content = new Rect(rect.X + left, rect.Y + top, Math.Max(0, rect.W - left - right), Math.Max(0, rect.H - top - bottom));
        if (Kind(element) == "fixedgridbox")
        {
            ArrangeGrid(element, box, content, GridCell(element, measuredAgainst.W, measuredAgainst.H));
            return;
        }

        if (LinearBoxes.Contains(Kind(element)))
        {
            ArrangeLinear(element, box, content, measuredAgainst, filled);
            if (Kind(element) is "hbox" or "vbox" or "flowcontainer")
            {
                PlaceIgnoringLayout(element, box, rect);
            }

            return;
        }

        foreach (var child in LayoutChildren(element).Where(child => !IsBackground(child)))
        {
            if (Place(child, content, Kind(element), -1, null, null, (content.W, content.H)) is { } placed)
            {
                box.Children.Add(placed);
            }
        }
    }

    /// <summary>
    /// Kasten eines <c>background</c> (Regel 12, Probe 8b/8c): Kasten des Elternelements, um <c>margin</c> verändert;
    /// positiv nach außen, negativ nach innen.
    /// </summary>
    private static Rect BackgroundRect(ResolvedElement background, Rect parent)
    {
        var (left, top, right, bottom) = MarginValues(background);
        return new Rect(parent.X - left, parent.Y - top, Math.Max(0, parent.W + left + right), Math.Max(0, parent.H + top + bottom));
    }

    /// <summary>
    /// Kinder einer hbox/vbox/flowcontainer mit <c>ignore_layout = yes</c> (Vanilla EU V, Cooltips): ohne Fach, nach ihren eigenen
    /// Ankern und ihrer <c>position</c> im Kasten der Box gelegt, wie Kinder eines einfachen widget (eine hbox ohne size füllt dann
    /// den Kasten). Nicht gemessen, daher mit Hinweis; gezeichnet nach den Kindern mit Fach.
    /// </summary>
    private void PlaceIgnoringLayout(ResolvedElement element, LayoutBox box, Rect rect)
    {
        foreach (var child in LayoutChildren(element).Where(child => !IsBackground(child) && IgnoresLayout(child)))
        {
            if (Place(child, rect, "widget", -1, null, null, (rect.W, rect.H)) is { } placed)
            {
                placed.Notes.Add(GuiTexts.LayoutIgnoreLayoutApproximated);
                box.Children.Add(placed);
            }
        }
    }

    private void ArrangeLinear(ResolvedElement element, LayoutBox box, Rect content, (double W, double H) measuredAgainst, bool filled)
    {
        var horizontal = IsHorizontal(element);
        var spacing = Number(element, "spacing");
        // Bezug für Prozent wie in MeasureBox: eigener Inhalt, wenn die Größe der Achse feststeht (size oder füllend), sonst die Länge des
        // Elternelements, gegen die die Box gemessen wurde.
        var referenceWidth = filled || OwnLength(element, 0, measuredAgainst.W) is not null ? content.W : measuredAgainst.W;
        var referenceHeight = filled || OwnLength(element, 1, measuredAgainst.H) is not null ? content.H : measuredAgainst.H;
        var measured = BoxItems(element).Select(item =>
        {
            var (width, height, notes) = Measure(item.Element, Kind(element), referenceWidth, referenceHeight);
            return (item.Element, item.Repeat, W: width, H: height, Notes: notes);
        }).ToList();
        var flow = Kind(element) == "flowcontainer";

        // Hauptachse: Basisgröße = gemessene Größe; nur hbox/vbox verteilen freien Platz (flowcontainer und Listen bleiben gepackt).
        var sizes = measured.Select(m => horizontal ? m.W : m.H).ToArray();
        var length = horizontal ? content.W : content.H;
        var free = length - sizes.Sum() - spacing * Math.Max(0, measured.Count - 1);
        // Prozent in minimum-/maximumsize der Kinder mit demselben Bezug wie beim Messen (sonst läge die Grenze hinter der Basisgröße).
        var padding = Kind(element) is "hbox" or "vbox"
            ? Distribute(measured.Select(m => m.Element).ToList(), sizes, free, horizontal, horizontal ? referenceWidth : referenceHeight, box.Notes)
            : 0;

        // Polster für space-around: je zur Hälfte vor und nach jedem Kind.
        var cursor = (horizontal ? content.X : content.Y) + padding / 2;
        for (var i = 0; i < measured.Count; i++)
        {
            var m = measured[i];
            LayoutBox? placed;
            if (flow && Kind(m.Element) is "hbox" or "vbox")
            {
                // Regel 15 (Absturz im Spiel): wie in einem widget gelegt, der Hinweis kommt aus Place.
                placed = Place(m.Element, content, Kind(element), m.Repeat, null, null, (content.W, content.H));
            }
            else
            {
                // Eigene Liste für flowcontainer-Hinweise, damit die Messung im Cache unverändert bleibt.
                var notes = flow ? new List<string>(m.Notes) : m.Notes;
                var slot = flow
                    ? FlowSlot(m.Element, content, horizontal, cursor, m.W, m.H, notes)
                    : BoxSlot(m.Element, content, horizontal, cursor, horizontal ? sizes[i] : m.W, horizontal ? m.H : sizes[i]);
                placed = Place(m.Element, content, Kind(element), m.Repeat, slot, notes, (referenceWidth, referenceHeight));
            }

            // Ein unsichtbares Kind (nur bei ignoreinvisible = no in der Liste) behält sein Fach, gezeichnet wird es nicht.
            if (placed is not null)
            {
                box.Children.Add(placed);
            }

            cursor += sizes[i] + spacing + padding;
        }
    }

    /// <summary>
    /// Fach eines hbox/vbox- oder Listen-Kindes (Regel 5): quer mittig; <c>expanding</c>/<c>growing</c> in der Querachse strecken
    /// auf die volle Breite bzw. Höhe (auch das Engine-<c>expand</c>, das ohne Policy wie der Vanilla-Typ growing/growing ist).
    /// </summary>
    private static Rect BoxSlot(ResolvedElement child, Rect content, bool horizontal, double cursor, double width, double height)
    {
        if (PolicyOf(child, !horizontal) is LayoutPolicy.Expanding or LayoutPolicy.Growing)
        {
            if (horizontal)
            {
                height = content.H;
            }
            else
            {
                width = content.W;
            }
        }

        return horizontal
            ? new Rect(cursor, content.Y + (content.H - height) / 2, width, height)
            : new Rect(content.X + (content.W - width) / 2, cursor, width, height);
    }

    /// <summary>
    /// Fach eines flowcontainer-Kindes (Regel 8): gepackt, ohne Dehnen (auch quer, ohne Hinweis), quer am Anfang. Der
    /// Querachsen-Anteil von <c>parentanchor</c>/<c>widgetanchor</c> bezieht sich auf die Quergröße des flowcontainers (Probe 3j).
    /// <c>position</c> und der Hauptachsen-Anteil der Anker sind nicht gemessen: ignoriert, mit Hinweis in <paramref name="notes"/>.
    /// </summary>
    private static Rect FlowSlot(ResolvedElement child, Rect content, bool horizontal, double cursor, double width, double height, List<string> notes)
    {
        var parentAnchor = Anchors(child.Property("parentanchor")?.Value);
        var widgetAnchor = child.Property("widgetanchor") is { } anchor ? Anchors(anchor.Value) : parentAnchor;
        if (JominiShowState.Property(child, "position") is not null)
        {
            notes.Add(GuiTexts.LayoutFlowChildUnmeasured("position"));
        }

        if ((horizontal ? parentAnchor.H : parentAnchor.V) != 0)
        {
            notes.Add(GuiTexts.LayoutFlowChildUnmeasured("parentanchor"));
        }
        else if ((horizontal ? widgetAnchor.H : widgetAnchor.V) != 0)
        {
            notes.Add(GuiTexts.LayoutFlowChildUnmeasured("widgetanchor"));
        }

        return horizontal
            ? new Rect(cursor, content.Y + parentAnchor.V * content.H - widgetAnchor.V * height, width, height)
            : new Rect(content.X + parentAnchor.H * content.W - widgetAnchor.H * width, cursor, width, height);
    }

    /// <summary>
    /// Verteilt den freien Platz der Hauptachse nach den gemessenen Regeln 2–4 und ändert <paramref name="sizes"/>.
    /// Gibt das Polster je Kind für space-around zurück (0, wenn ein Kind den Platz nimmt oder kein Platz frei ist).
    /// Bleibt Platz übrig, den kein Kind nehmen kann (nicht gemessen), steht ein Hinweis in <paramref name="notes"/>.
    /// </summary>
    private static double Distribute(IReadOnlyList<ResolvedElement> children, double[] sizes, double free, bool horizontal, double length, List<string> notes)
    {
        if (children.Count == 0 || free == 0)
        {
            return 0;
        }

        var policies = children.Select(c => PolicyOf(c, horizontal)).ToArray();
        var all = Enumerable.Range(0, children.Count).ToList();
        if (free < 0)
        {
            // Überlauf: preferred und shrinking schrumpfen gleich bis minimumsize; der Rest ragt hinten heraus.
            var shrinking = all.Where(i => policies[i] is LayoutPolicy.Preferred or LayoutPolicy.Shrinking).ToList();
            Share(shrinking, sizes, free, i => MainLimit(children[i], "minimumsize", horizontal, length, 0));
            return 0;
        }

        // Freier Platz geht an expanding-Kinder, nur ohne solche an growing/preferred.
        var sharing = all.Where(i => policies[i] == LayoutPolicy.Expanding).ToList();
        if (sharing.Count == 0)
        {
            sharing = all.Where(i => policies[i] is LayoutPolicy.Growing or LayoutPolicy.Preferred).ToList();
        }

        if (sharing.Count == 0)
        {
            return free / children.Count;
        }

        double Maximum(int i) => MainLimit(children[i], "maximumsize", horizontal, length, double.PositiveInfinity);
        var factor = horizontal ? "layoutstretchfactor_horizontal" : "layoutstretchfactor_vertical";
        var leftover = sharing.Any(i => children[i].Property(factor) is not null)
            ? Stretch(sharing, sizes, free, i => Math.Max(0, Scalar(children[i], factor, 1)), Maximum)
            : Share(sharing, sizes, free, Maximum);

        // Platz, der nach allen Deckeln übrig bleibt, ist nicht gemessen: die Kinder bleiben dann ab dem Anfang gepackt.
        if (leftover > 0.001)
        {
            notes.Add(GuiTexts.LayoutFreeSpaceUnmeasured);
        }

        return 0;
    }

    /// <summary>
    /// Gleicher Zuschlag (oder Abzug bei negativem <paramref name="amount"/>) je Kind des Pools. Ein Kind, das dabei seine
    /// Grenze überschreitet, bleibt an der Grenze und fällt aus dem Pool; der Rest geht an die übrigen, bis sich nichts mehr ändert.
    /// Gibt zurück, was nicht verteilt werden konnte (0, wenn alles verteilt ist).
    /// </summary>
    private static double Share(List<int> pool, double[] sizes, double amount, Func<int, double> limit)
    {
        pool = new List<int>(pool);
        var growing = amount > 0;
        while (pool.Count > 0 && amount != 0)
        {
            var share = amount / pool.Count;
            var clamped = pool.Where(i => growing ? sizes[i] + share > limit(i) : sizes[i] + share < limit(i)).ToList();
            if (clamped.Count == 0)
            {
                foreach (var i in pool)
                {
                    sizes[i] += share;
                }

                return 0;
            }

            foreach (var i in clamped)
            {
                // Die Grenze liegt nie hinter der Basisgröße (Measure klemmt schon auf minimum- und maximumsize).
                var bound = growing ? Math.Max(sizes[i], limit(i)) : Math.Min(sizes[i], limit(i));
                amount -= bound - sizes[i];
                sizes[i] = bound;
                pool.Remove(i);
            }
        }

        return amount;
    }

    /// <summary>
    /// Mit <c>layoutstretchfactor_*</c>: Die Endgrößen des Pools verhalten sich wie die Faktoren (fehlend = 1), nie unter der
    /// Basisgröße und nie über <c>maximumsize</c>. Begrenzte Kinder fallen aus dem Pool, der Rest wird neu aufgeteilt.
    /// Gibt den Platz zurück, den kein Kind nehmen konnte (0, wenn alles verteilt ist).
    /// </summary>
    private static double Stretch(List<int> pool, double[] sizes, double free, Func<int, double> factor, Func<int, double> maximum)
    {
        pool = new List<int>(pool);
        var total = pool.Sum(i => sizes[i]) + free;
        while (pool.Count > 0)
        {
            var factorSum = pool.Sum(factor);
            if (factorSum <= 0)
            {
                // Alle Faktoren 0: die übrigen Kinder bleiben bei ihrer Basisgröße.
                return total - pool.Sum(i => sizes[i]);
            }

            var targets = pool.ToDictionary(i => i, i => total * factor(i) / factorSum);
            var clamped = pool.Where(i => targets[i] < sizes[i] || targets[i] > maximum(i)).ToList();
            if (clamped.Count == 0)
            {
                foreach (var i in pool)
                {
                    sizes[i] = targets[i];
                }

                return 0;
            }

            foreach (var i in clamped)
            {
                sizes[i] = targets[i] < sizes[i] ? sizes[i] : Math.Max(sizes[i], maximum(i));
                total -= sizes[i];
                pool.Remove(i);
            }
        }

        return total;
    }

    /// <summary>Wert der Hauptachse aus <c>minimumsize</c>/<c>maximumsize</c>; fehlend oder ≤ 0 (<c>-1</c>) gilt als nicht gesetzt.</summary>
    private static double MainLimit(ResolvedElement element, string name, bool horizontal, double length, double fallback) =>
        element.Property(name) is { Items.Count: >= 2 } limit
        && TryLength(limit.Items[horizontal ? 0 : 1], length, out var value)
        && value > 0
            ? value
            : fallback;

    private void ArrangeGrid(ResolvedElement element, LayoutBox box, Rect content, (double W, double H) cellSize)
    {
        var items = BoxItems(element);
        var columns = GridColumns(element);
        var (cellWidth, cellHeight) = cellSize;
        box.Notes.Add(GuiTexts.LayoutGridApproximated);
        for (var i = 0; i < items.Count; i++)
        {
            var cell = new Rect(content.X + (i % columns) * cellWidth, content.Y + (i / columns) * cellHeight, cellWidth, cellHeight);
            if (Place(items[i].Element, cell, Kind(element), items[i].Repeat, null, null, (cell.W, cell.H)) is { } placed)
            {
                box.Children.Add(placed);
            }
        }
    }

    /// <summary>
    /// Einträge eines Kastens mit Fach. Raster und Listen: der erste sichtbare Widget im <c>item</c>-Halter, so oft wiederholt wie
    /// eingestellt. hbox/vbox/flowcontainer: die Kinder ohne <c>background</c> und ohne <c>ignore_layout = yes</c>, mit
    /// <c>datamodel</c> danach die simulierten Einträge.
    /// </summary>
    private List<(ResolvedElement Element, int Repeat)> BoxItems(ResolvedElement element)
    {
        if (Kind(element) is "fixedgridbox" or "dynamicgridbox" or "overlappingitembox")
        {
            return ListSamples(element);
        }

        // Regel 7: Unsichtbare Kinder nehmen keinen Platz (Engine-Standard ignoreinvisible = yes); mit ignoreinvisible = no
        // (z.B. Vanilla-Typ flowcontainer in CK III und EU V) behalten sie ihr Fach.
        var keepInvisible = element.Property("ignoreinvisible")?.Value == "no";
        var items = LayoutChildren(element, keepInvisible)
            .Where(child => !IsBackground(child) && !IgnoresLayout(child))
            .Select(child => (child, -1))
            .ToList();
        if (element.Property("datamodel") is not null)
        {
            items.AddRange(ListSamples(element));
        }

        return items;
    }

    /// <summary>Simulierte Einträge einer Liste: der erste sichtbare Widget im <c>item</c>-Halter, <c>ListSampleCount</c>-mal.</summary>
    private List<(ResolvedElement Element, int Repeat)> ListSamples(ResolvedElement element)
    {
        var template = element.Children.FirstOrDefault(c => c.Key == "item")?.Children.FirstOrDefault(IsVisible);
        return template is null
            ? new List<(ResolvedElement, int)>()
            : Enumerable.Range(0, Math.Max(0, _options.ListSampleCount)).Select(i => (template, i)).ToList();
    }

    /// <summary>Regel 12: ein <c>background</c>-Kind (Engine-Widget in allen drei Jomini-Profilen) belegt kein Fach.</summary>
    private static bool IsBackground(ResolvedElement element) =>
        element.Role != GuiRole.WidgetHolder && element.Key == "background";

    private static bool IgnoresLayout(ResolvedElement element) => element.Property("ignore_layout")?.Value == "yes";

    /// <summary>
    /// Kinder im Layout, ohne unsichtbare (außer mit <paramref name="includeInvisible"/>). Layout-Halter (z.B. scrollwidget) sind
    /// durchlässig, andere Halter (Tooltips) und <c>item</c> fallen weg.
    /// </summary>
    private IEnumerable<ResolvedElement> LayoutChildren(ResolvedElement element, bool includeInvisible = false)
    {
        foreach (var child in element.Children)
        {
            if (child.Role == GuiRole.WidgetHolder)
            {
                if (child.Key == "item" || !_rules.LayoutHolders.Contains(child.Key))
                {
                    continue;
                }

                foreach (var inner in LayoutChildren(child, includeInvisible))
                {
                    yield return inner;
                }

                continue;
            }

            if (includeInvisible || IsVisible(child))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// <c>visible = no</c> nur mit "Unsichtbare zeigen"; bedingte (<see cref="GuiConditionalKeys.IsConditional"/>) mit "Bedingte zeigen", außer ihr
    /// Schlüssel ist ausgeblendet. Ausgeblendete verhalten sich wie unsichtbare Kinder (Regel 7, <c>ignoreinvisible</c>).
    /// </summary>
    private bool IsVisible(ResolvedElement element)
    {
        if (GuiConditionalKeys.IsConditional(element))
        {
            return _options.ShowConditional
                && !(_conditionalKeys.TryGetValue(element, out var key) && _options.HiddenConditionals.Contains(key));
        }

        return element.Property("visible")?.Value != "no" || _options.ShowHidden;
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
        var position = JominiShowState.Property(element, "position");
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

    private static readonly string[] MarginNames = { "margin", "margin_left", "margin_right", "margin_top", "margin_bottom" };

    /// <summary>
    /// Innenabstand des Inhalts; an einem einfachen <c>widget</c> wirkungslos (Probe 6a, Hinweis in <see cref="Arrange"/>). An einem
    /// <c>background</c> verändert <c>margin</c> dessen eigenen Kasten (<see cref="BackgroundRect"/>), nicht seinen Inhalt.
    /// </summary>
    private static (double Left, double Top, double Right, double Bottom) Margins(ResolvedElement element) =>
        Kind(element) == "widget" || IsBackground(element) ? (0, 0, 0, 0) : MarginValues(element);

    /// <summary>Werte aus <c>margin = { x y }</c> und <c>margin_left</c>/<c>_right</c>/<c>_top</c>/<c>_bottom</c> (Einzelwerte gewinnen).</summary>
    private static (double Left, double Top, double Right, double Bottom) MarginValues(ResolvedElement element)
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

    /// <summary>
    /// <c>layoutpolicy_*</c> einer Achse, Groß-/Kleinschreibung egal. Fehlend oder unbekannt = <c>fixed</c> (Engine-Standard, Lauf 3);
    /// das Engine-Widget <c>expand</c> ohne Policy wächst wie <c>growing</c>.
    /// </summary>
    internal static LayoutPolicy PolicyOf(ResolvedElement element, bool horizontal)
    {
        var value = element.Property(horizontal ? "layoutpolicy_horizontal" : "layoutpolicy_vertical")?.Value;
        if (value is null)
        {
            return Kind(element) == "expand" ? LayoutPolicy.Growing : LayoutPolicy.Fixed;
        }

        return value.ToLowerInvariant() switch
        {
            "preferred" => LayoutPolicy.Preferred,
            "growing" => LayoutPolicy.Growing,
            "expanding" => LayoutPolicy.Expanding,
            "shrinking" => LayoutPolicy.Shrinking,
            _ => LayoutPolicy.Fixed,
        };
    }

    /// <summary>
    /// hbox/vbox ohne <c>size</c>, deren Elternelement keine Box ist (widget, window, container): füllt es. Ohne Elternelement
    /// (Wurzel einer Einzelvorschau, <paramref name="parentKind"/> = <c>null</c>) behält sie ihre Inhaltsgröße. Mit
    /// <c>set_parent_size_to_minimum = yes</c> füllt sie nie (Probe 10b). In einem flowcontainer (Fehlerfall, Regel 15) wie in einem widget.
    /// </summary>
    private static bool FillsParent(ResolvedElement element, string? parentKind) =>
        SizelessBoxInPlainParent(element, parentKind) && element.Property("set_parent_size_to_minimum")?.Value != "yes";

    /// <summary>hbox/vbox ohne <c>size</c> in einem Elternelement, das keine Box ist (oder in einem flowcontainer): füllt es, außer mit <c>set_parent_size_to_minimum</c>.</summary>
    private static bool SizelessBoxInPlainParent(ResolvedElement element, string? parentKind) =>
        Kind(element) is "hbox" or "vbox"
        && JominiShowState.Property(element, "size") is null
        && parentKind is not null
        && (parentKind == "flowcontainer" || !LinearBoxes.Contains(parentKind));

    private static bool HasAnchorOrPosition(ResolvedElement element) =>
        JominiShowState.Property(element, "position") is not null || element.Property("parentanchor") is not null || element.Property("widgetanchor") is not null;

    private static int GridColumns(ResolvedElement element) => Math.Max(1, (int)Scalar(element, "maxhorizontalslots", 1));

    private static double Number(ResolvedElement element, string name) => Scalar(element, name, 0);

    private static double Scalar(ResolvedElement element, string name, double fallback) =>
        element.Property(name) is { } property && TryLength(property.Value, 0, out var value) ? value : fallback;

    /// <summary>Länge aus einer Eigenschaft, Prozent bezogen auf <paramref name="reference"/>; fehlend oder nicht auswertbar = 0.</summary>
    private static double Length(ResolvedElement element, string name, double reference) =>
        element.Property(name) is { } property && TryLength(property.Value, reference, out var value) ? value : 0;

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
