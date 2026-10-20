using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Endwerte des Zustands <c>_show</c> (Entscheidung 2026-10-20): Nach dem Öffnen eines Fensters zeigt das Spiel den Endzustand von <c>_show</c>
/// (etwa <c>alpha = 1</c> nach dem Einblenden, eine andere <c>size</c>), nicht den Grundzustand. Die Vorschau legt deshalb <c>size</c>,
/// <c>position</c>, <c>alpha</c> und <c>scale</c> aus <c>_show</c> über die Werte des Elements, danach die der Zustände, die <c>next</c> ab <c>_show</c>
/// nennt (Endwert der Kette; eine Schleife endet vor dem ersten wiederholten Namen). Der Resolver und die Eigenschaftenzeilen bleiben
/// unberührt: der Inspektor zeigt die Werte dort, wo sie geschrieben stehen.
/// </summary>
internal static class JominiShowState
{
    /// <summary>Die Eigenschaften, die ein <c>_show</c>-Zustand für die Vorschau überlagert.</summary>
    internal static readonly IReadOnlyList<string> Names = new[] { "size", "position", "alpha", "scale" };

    private const int MaxTemplateDepth = 16;

    /// <summary>Die Eigenschaft wie die Vorschau sie liest: der Wert aus <c>_show</c>, sonst der des Elements (Zustandsblöcke zählen dort nicht).</summary>
    internal static ResolvedProperty? Property(ResolvedElement element, string name) =>
        Overlay(element).Applied.TryGetValue(name, out var overlaid) ? overlaid : element.Property(name);

    /// <summary>Überlagerte Werte (später gewinnt) und die nicht angewandten Bindungen des Elements, einmal je Element berechnet.</summary>
    internal static ShowOverlay Overlay(ResolvedElement element, Func<string, GuiNode?>? templates = null)
    {
        if (templates is null && element.ShowOverlayCache is { } cached)
        {
            return cached;
        }

        var result = Compute(element, templates ?? element.TemplateLookup);
        if (templates is null)
        {
            element.ShowOverlayCache = result;
        }

        return result;
    }

    private static ShowOverlay Compute(ResolvedElement element, Func<string, GuiNode?>? templates)
    {
        var applied = new Dictionary<string, ResolvedProperty>(StringComparer.Ordinal);
        var ignored = new List<ResolvedProperty>();
        var states = element.StructuredBlocks("state").Where(s => s.Origin.Node is not null).Select(s => (Node: s.Origin.Node!, s.Origin)).ToList();

        // Erst alle _show-Zustände (der spätere gewinnt), dann die Kette über next (Entscheidung 2026-10-20): jeder Zustand der Kette legt seine Werte
        // über die bisherigen, der letzte gewinnt; was er nicht setzt, bleibt. Gleichnamige Zustände gelten alle, in Reihenfolge.
        var next = ApplyStates(element, states, ShowName, templates, applied, ignored);
        var approximated = false;
        var visited = new HashSet<string>(StringComparer.Ordinal) { ShowName };
        while (next is not null)
        {
            if (!states.Any(s => HasName(s.Node, next)))
            {
                // Fehlendes Ziel (etwa EU V tooltip_delayed_showup_animation, next = fade_in): im Spiel endet die Kette hier; kein Hinweis
                // (Entscheidung 2026-10-20).
                break;
            }

            if (!visited.Add(next))
            {
                // Schleife (ein Name wiederholt sich, auch zurück zu _show): die Werte bis hierher bleiben, mit Hinweis.
                approximated = true;
                break;
            }

            next = ApplyStates(element, states, next, templates, applied, ignored);
        }

        return new ShowOverlay(applied, ignored, approximated);
    }

    private const string ShowName = "_show";

    /// <summary>Legt alle Zustände namens <paramref name="name"/> in Reihenfolge über die Werte; gibt das zuletzt gelesene <c>next</c> zurück (sonst null).</summary>
    private static string? ApplyStates(
        ResolvedElement element,
        IReadOnlyList<(GuiNode Node, GuiOrigin Origin)> states,
        string name,
        Func<string, GuiNode?>? templates,
        Dictionary<string, ResolvedProperty> applied,
        List<ResolvedProperty> ignored)
    {
        string? next = null;
        foreach (var (node, origin) in states)
        {
            if (HasName(node, name))
            {
                Collect(element, node.Children, origin, templates, applied, ignored, new HashSet<string>(StringComparer.Ordinal), 0, ref next);
            }
        }

        return next;
    }

    /// <summary>Der Zustand heißt <paramref name="name"/> (<c>name = x</c> oder <c>name = "x"</c>).</summary>
    private static bool HasName(GuiNode state, string name) =>
        state.Children.Any(c => c.Kind == GuiNodeKind.Property && c.Name == "name" && string.Equals(Unquote(c.Value), name, StringComparison.Ordinal));

    private static string? Unquote(string? value) => value?.Trim().Trim('"');

    private static void Collect(
        ResolvedElement element,
        IEnumerable<GuiNode> entries,
        GuiOrigin origin,
        Func<string, GuiNode?>? templates,
        Dictionary<string, ResolvedProperty> applied,
        List<ResolvedProperty> ignored,
        HashSet<string> stack,
        int depth,
        ref string? next)
    {
        foreach (var entry in entries)
        {
            if (entry.Kind == GuiNodeKind.Using)
            {
                // Vorlagen wie beim Element: Zyklus- und Tiefengrenze, der spätere Eintrag überschreibt den früheren.
                var name = entry.Value ?? string.Empty;
                if (depth < MaxTemplateDepth && templates?.Invoke(name) is { } template && stack.Add(name))
                {
                    Collect(element, template.Children, new GuiOrigin(GuiOriginKind.Template, name, element.FilePathLookup?.Invoke(template) ?? origin.FilePath, template.Line, template), templates, applied, ignored, stack, depth + 1, ref next);
                    stack.Remove(name);
                }

                continue;
            }

            if (entry.Kind == GuiNodeKind.Property && entry.Name == "next")
            {
                // Nächster Zustand der Kette; der zuletzt geschriebene gilt (auch aus einer Vorlage).
                var target = Unquote(entry.Value);
                next = string.IsNullOrEmpty(target) ? next : target;
                continue;
            }

            if (!Names.Contains(entry.Name) || Read(entry, origin) is not { } property)
            {
                continue;
            }

            if (property.Unresolved || IsBinding(property))
            {
                // Eine Bindung ist zur Laufzeit offen: nicht angewandt (der Wert davor bleibt), mit dem üblichen Hinweis der Eigenschaft.
                ignored.RemoveAll(p => p.Name == property.Name);
                ignored.Add(property);
                continue;
            }

            ignored.RemoveAll(p => p.Name == property.Name);
            applied[property.Name] = property;
        }
    }

    private static bool IsBinding(ResolvedProperty property) =>
        property.Value.Contains('[') || property.Items.Any(i => i.Contains('['));

    private static ResolvedProperty? Read(GuiNode entry, GuiOrigin origin)
    {
        if (entry.Kind == GuiNodeKind.Property)
        {
            var (value, unresolved) = Substitute(entry.Value ?? string.Empty, entry, entry.IsQuoted);
            return new ResolvedProperty { Name = entry.Name, Role = GuiRole.Property, Value = value, IsQuoted = entry.IsQuoted, Origin = origin with { Node = entry, Line = entry.Line }, Unresolved = unresolved };
        }

        if (entry.Kind == GuiNodeKind.KeyedBlock && entry.Children.Any(c => c.Kind == GuiNodeKind.ListItem))
        {
            var unresolved = false;
            var items = new List<string>();
            foreach (var item in entry.Children.Where(c => c.Kind == GuiNodeKind.ListItem))
            {
                var (value, itemUnresolved) = Substitute(item.Value ?? string.Empty, item, item.IsQuoted);
                unresolved |= itemUnresolved;
                items.Add(value);
            }

            return new ResolvedProperty { Name = entry.Name, Role = GuiRole.ValueBlock, Items = items, Value = string.Join(" ", items), Origin = origin with { Node = entry, Line = entry.Line }, Unresolved = unresolved };
        }

        return null;
    }

    /// <summary>Konstanten ersetzen wie der Resolver; ohne Wert bleibt der Text und die Eigenschaft gilt als unaufgelöst. Text in Anführungszeichen bleibt, wie er ist.</summary>
    private static (string Value, bool Unresolved) Substitute(string value, GuiNode at, bool quoted)
    {
        if (quoted)
        {
            return (value, false);
        }

        var substituted = GuiResolver.SubstituteValue(value, at);
        return substituted is null ? (value, true) : (substituted, false);
    }
}

/// <summary>
/// Ergebnis von <see cref="JominiShowState.Overlay"/>: angewandte Werte je Name, die Bindungen, die nicht angewandt wurden, und ob die Kette über
/// <c>next</c> sich wiederholt (<paramref name="ChainApproximated"/>, Hinweis <see cref="GuiTexts.LayoutShowNextApproximated"/>).
/// </summary>
internal sealed record ShowOverlay(IReadOnlyDictionary<string, ResolvedProperty> Applied, IReadOnlyList<ResolvedProperty> Ignored, bool ChainApproximated = false);
