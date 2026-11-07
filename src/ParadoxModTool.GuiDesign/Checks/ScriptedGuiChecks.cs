using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>
/// (HOI IV) Verbindungen der Scripted GUIs. Jeder Befund betrifft einen Eintrag der Mod oder ein Fenster der geöffneten Mod-Datei; Einträge aus Vanilla und
/// Abhängigkeiten melden nie etwas:
/// <list type="bullet">
/// <item><see cref="GuiFindingKind.ScriptWindowMissing"/> (Fehler): <c>window_name</c> eines Mod-Eintrags ist kein oberstes Fenster. Ein solcher Eintrag
/// gehört zu keinem Fenster; er erscheint in jeder geöffneten Mod-Datei, ohne Element.</item>
/// <item><see cref="GuiFindingKind.ScriptElementUnknown"/> (Warnung): ein Schlüssel eines Mod-Eintrags meint kein bekanntes Element. Nicht, wenn ein
/// Einstiegsfenster erst im Spiel feststeht (<c>[…]</c>) oder das Fenster fehlt.</item>
/// <item><see cref="GuiFindingKind.CategoryMissing"/> (Warnung): ein <c>decision_category</c>-Eintrag der Mod, den keine Kategorie einbindet.</item>
/// <item><see cref="GuiFindingKind.ScriptButtonWithoutEffect"/> (Hinweis): ein Knopf in einem Fenster mit Scripted GUI ohne <c>_click</c>-Effekt.</item>
/// </list>
/// Die Befunde zu Elementen und Kategorien stehen nur in der Datei, die das Fenster oder eines seiner Einstiegsfenster enthält; der Klick öffnet die
/// Skriptdatei an der Zeile.
/// </summary>
public sealed class ScriptedGuiChecks : IClassicCheck
{
    private static readonly HashSet<string> ButtonTypes = new(StringComparer.OrdinalIgnoreCase) { "buttonType", "guiButtonType" };

    private readonly ScriptedGuiIndex? _index;

    /// <summary>Prüft gegen <see cref="ClassicAssets.ScriptedGuis"/> des Arbeitsbereichs.</summary>
    public ScriptedGuiChecks()
    {
    }

    /// <summary>Prüft gegen einen fest gegebenen Index (Tests: Vanilla als Mod).</summary>
    internal ScriptedGuiChecks(ScriptedGuiIndex index) => _index = index;

    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        var index = _index;
        if (index is null)
        {
            // Läuft im Hintergrund: Warten auf den Index ist hier erlaubt (ein Fehler beim Bauen wird zum Hinweis "Prüfung fehlgeschlagen").
            if (context.Workspace.Classic?.ScriptedGuis is not { } task)
            {
                yield break;
            }

            index = task.GetAwaiter().GetResult();
        }

        var windows = new Dictionary<string, GuiNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in ClassicWindows.Of(context.Document))
        {
            windows.TryAdd(ClassicWindows.NameOf(window), window);
        }

        foreach (var entry in index.Entries.Where(e => e.IsMod))
        {
            cancellation.ThrowIfCancellationRequested();
            if (entry.WindowName is { } windowName && !ScriptedGuiIndex.IsDynamic(windowName) && !index.Windows.HasWindow(windowName))
            {
                yield return new GuiFinding(
                    GuiFindingKind.ScriptWindowMissing, GuiFindingSeverity.Error, GuiTexts.FindingScriptWindowMissing(entry.Name, windowName),
                    null, context.DocumentPath, 0, entry.FilePath, entry.Line);
                continue;
            }

            if (RelatedWindow(entry, windows) is not { } related)
            {
                continue;
            }

            if (!entry.HasDynamicContainer)
            {
                foreach (var binding in entry.Bindings.Where(b => b.ElementName is null))
                {
                    yield return new GuiFinding(
                        GuiFindingKind.ScriptElementUnknown, GuiFindingSeverity.Warning,
                        GuiTexts.FindingScriptElementUnknown(entry.Name, binding.Key, binding.Section),
                        related, context.DocumentPath, related.Line, entry.FilePath, binding.Line);
                }
            }

            if (string.Equals(entry.ContextType, ScriptedGuiIndex.DecisionCategory, StringComparison.OrdinalIgnoreCase) && !index.Categories.ContainsKey(entry.Name))
            {
                yield return new GuiFinding(
                    GuiFindingKind.CategoryMissing, GuiFindingSeverity.Warning, GuiTexts.FindingCategoryMissing(entry.Name),
                    related, context.DocumentPath, related.Line, entry.FilePath, entry.Line);
            }
        }

        foreach (var (name, window) in windows)
        {
            cancellation.ThrowIfCancellationRequested();
            var entries = index.ForWindow(name);
            if (entries.Count == 0)
            {
                continue;
            }

            foreach (var button in window.Descendants().Where(n => n.Kind == GuiNodeKind.KeyedBlock && ButtonTypes.Contains(n.Name)))
            {
                var buttonName = ClassicWindows.NameOf(button);
                if (buttonName.Length == 0 || entries.Any(e => e.Bindings.Any(b => IsClick(b, buttonName))))
                {
                    continue;
                }

                var entry = entries[0];
                yield return new GuiFinding(
                    GuiFindingKind.ScriptButtonWithoutEffect, GuiFindingSeverity.Hint, GuiTexts.FindingScriptButtonWithoutEffect(buttonName, entry.Name),
                    button, context.DocumentPath, button.Line, entry.FilePath, entry.Line);
            }
        }
    }

    /// <summary>Das Fenster des Eintrags in der geöffneten Datei: zuerst <c>window_name</c>, sonst ein Einstiegsfenster.</summary>
    private static GuiNode? RelatedWindow(ScriptedGuiEntry entry, IReadOnlyDictionary<string, GuiNode> windows)
    {
        if (entry.WindowName is { } name && windows.TryGetValue(name, out var window))
        {
            return window;
        }

        return entry.EntryContainers.Select(c => windows.TryGetValue(c, out var w) ? w : null).FirstOrDefault(w => w is not null);
    }

    /// <summary>Ein Effekt unter <c>effects</c>, der auf einen Klick des Knopfs läuft (jede Kette aus <c>_right</c>/<c>_alt</c>/… vor <c>_click</c>).</summary>
    private static bool IsClick(ScriptedGuiBinding binding, string buttonName) =>
        binding.Section == "effects"
        && string.Equals(binding.ElementName, buttonName, StringComparison.OrdinalIgnoreCase)
        && binding.Key.EndsWith("_click", StringComparison.OrdinalIgnoreCase);
}
