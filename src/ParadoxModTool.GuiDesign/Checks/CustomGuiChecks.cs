using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>
/// (EU IV) Verbindungen der Custom GUI. Jeder Befund betrifft ein Element der geöffneten Mod-Datei oder einen Eintrag der Mod; Einträge aus Vanilla und
/// Abhängigkeiten melden selbst nie etwas:
/// <list type="bullet">
/// <item><see cref="GuiFindingKind.CustomEntryMissing"/> (Fehler): ein Element mit <c>scripted = yes</c>, das kein Eintrag nennt.</item>
/// <item><see cref="GuiFindingKind.CustomElementMissing"/> (Warnung): ein Eintrag der Mod, dessen Name in keiner <c>.gui</c>-Datei vorkommt. Er gehört zu
/// keinem Element; er erscheint in jeder geöffneten Mod-Datei, ohne Element, und der Klick öffnet den Eintrag.</item>
/// <item><see cref="GuiFindingKind.CustomNotScripted"/> (Warnung): ein Eintrag der Mod, dessen Element es gibt, aber kein Element dieses Namens hat
/// <c>scripted = yes</c> (laut example.txt wirkt der Eintrag dann nicht). Angezeigt wie <see cref="GuiFindingKind.CustomElementMissing"/>.</item>
/// <item><see cref="GuiFindingKind.CustomKindMismatch"/> (Warnung): kein Eintrag des Namens passt zum Elementtyp (<see cref="CustomGuiWindows.Accepts"/>);
/// bei <c>custom_shield</c> mit dem Zusatz "unsicher".</item>
/// <item><see cref="GuiFindingKind.CustomOutsideSupportedWindow"/> (Fehler): ein Element mit <c>scripted = yes</c> liegt in keinem unterstützten Fenster.
/// Nur, wenn die Fensterliste geladen und mindestens ein Fenster aufgelöst ist.</item>
/// </list>
/// </summary>
public sealed class CustomGuiChecks : IClassicCheck
{
    private readonly CustomGuiIndex? _index;

    /// <summary>Prüft gegen <see cref="ClassicAssets.CustomGuis"/> des Arbeitsbereichs.</summary>
    public CustomGuiChecks()
    {
    }

    /// <summary>Prüft gegen einen fest gegebenen Index (Tests: Vanilla als Mod).</summary>
    internal CustomGuiChecks(CustomGuiIndex index) => _index = index;

    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        var index = _index;
        if (index is null)
        {
            // Läuft im Hintergrund: Warten auf den Index ist hier erlaubt (ein Fehler beim Bauen wird zum Hinweis "Prüfung fehlgeschlagen").
            if (context.Workspace.Classic?.CustomGuis is not { } task)
            {
                yield break;
            }

            index = task.GetAwaiter().GetResult();
        }

        foreach (var entry in index.Entries.Where(e => e.IsMod))
        {
            cancellation.ThrowIfCancellationRequested();
            if (index.Windows.ElementTypes(entry.Name).Count == 0)
            {
                yield return new GuiFinding(
                    GuiFindingKind.CustomElementMissing, GuiFindingSeverity.Warning,
                    GuiTexts.FindingCustomElementMissing(CustomGuiIndex.BlockName(entry.Kind), entry.Name),
                    null, context.DocumentPath, 0, entry.FilePath, entry.Line);
            }
            else if (!index.Windows.HasScriptedElement(entry.Name))
            {
                yield return new GuiFinding(
                    GuiFindingKind.CustomNotScripted, GuiFindingSeverity.Warning, GuiTexts.FindingCustomNotScripted(entry.Name),
                    null, context.DocumentPath, 0, entry.FilePath, entry.Line);
            }
        }

        var checkWindows = index.Supported.Count > 0;
        foreach (var element in context.Document.Root.Descendants().Where(IsScripted))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = ClassicWindows.NameOf(element);
            if (name.Length == 0)
            {
                continue;
            }

            var entries = index.FindAll(name);
            if (entries.Count == 0)
            {
                yield return new GuiFinding(
                    GuiFindingKind.CustomEntryMissing, GuiFindingSeverity.Error, GuiTexts.FindingCustomEntryMissing(name, element.Name),
                    element, context.DocumentPath, element.Line);
            }
            else if (!entries.Any(e => CustomGuiWindows.Accepts(e.Kind, element.Name)))
            {
                var entry = entries[0];
                yield return new GuiFinding(
                    GuiFindingKind.CustomKindMismatch, GuiFindingSeverity.Warning,
                    GuiTexts.FindingCustomKindMismatch(
                        name, element.Name, CustomGuiIndex.BlockName(entry.Kind), CustomGuiWindows.AcceptedTypes(entry.Kind), entry.Kind == CustomGuiKind.Shield),
                    element, context.DocumentPath, element.Line, entry.FilePath, entry.Line);
            }

            if (checkWindows && CustomGuiWindows.Enclosing(index.Supported, element) is null)
            {
                var window = ClassicWindows.EnclosingOf(element) is { } top && !ReferenceEquals(top, element) ? ClassicWindows.NameOf(top) : name;
                yield return new GuiFinding(
                    GuiFindingKind.CustomOutsideSupportedWindow, GuiFindingSeverity.Error, GuiTexts.FindingCustomOutsideSupportedWindow(name, window),
                    element, context.DocumentPath, element.Line);
            }
        }
    }

    /// <summary>Ein benannter Block mit <c>scripted = yes</c> (Schlüssel und Wert ohne Rücksicht auf Groß-/Kleinschreibung).</summary>
    public static bool IsScripted(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock
        && node.Children.Any(c => c.Kind == GuiNodeKind.Property
            && string.Equals(c.Name, "scripted", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase));
}
