using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>
/// Fenster und Sprites der geöffneten Mod-Datei, die im Spiel nicht gelten, weil eine andere Definition gewinnt (Ladereihenfolge des Spiels:
/// HOI IV zuletzt, EU IV bei Fenstern zuerst geladen, bei Sprites zuletzt). Verglichen wird der Pfad der Datei des Gewinners mit dem der geöffneten
/// Datei. Steht die Datei (noch) nicht im Index, lässt sich nichts sagen, und es gibt keinen Befund.
/// </summary>
public sealed class LosesToOtherCheck : IClassicCheck
{
    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        var layout = context.Workspace.Rules.Layout;
        foreach (var window in ClassicWindows.Of(context.Document))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = ClassicWindows.NameOf(window);
            var all = ClassicWindows.FindAll(context.Workspace.Index, name);
            if (!all.Any(d => SamePath(d.File.FullPath, context.DocumentPath)))
            {
                continue;
            }

            var winner = ClassicWindows.Winner(context.Workspace.Index, name);
            if (winner is not null && !SamePath(winner.File.FullPath, context.DocumentPath))
            {
                var text = GuiTexts.FindingWindowLoses(name, winner.File.RelativePath, winner.File.Label, layout.FirstWins);
                yield return new GuiFinding(GuiFindingKind.LosesToOther, GuiFindingSeverity.Warning, text, window, context.DocumentPath, window.Line);
            }
        }

        if (context.Workspace.Classic is not { } classic)
        {
            yield break;
        }

        foreach (var container in context.Document.Root.Children.Where(IsSpriteContainer))
        {
            foreach (var sprite in container.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock))
            {
                cancellation.ThrowIfCancellationRequested();
                var name = ClassicWindows.NameOf(sprite);
                if (name.Length == 0)
                {
                    continue;
                }

                var all = classic.Sprites.FindAll(name);
                if (!all.Any(s => SamePath(s.File.FullPath, context.DocumentPath)))
                {
                    continue;
                }

                var winner = classic.Sprites.Find(name).Sprite;
                if (winner is not null && !SamePath(winner.File.FullPath, context.DocumentPath))
                {
                    var text = GuiTexts.FindingSpriteLoses(name, winner.File.RelativePath, winner.File.Label, layout.SpriteFirstWins);
                    yield return new GuiFinding(GuiFindingKind.LosesToOther, GuiFindingSeverity.Warning, text, sprite, context.DocumentPath, sprite.Line);
                }
            }
        }
    }

    private static bool IsSpriteContainer(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && string.Equals(node.Name, "spriteTypes", StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Texte und Tooltips der geöffneten Mod-Datei, die wie ein Lokalisierungsschlüssel aussehen (kein Leerraum, nicht leer, kein <c>[…]</c>-Ausdruck, kein
/// <c>@</c>-Wert), aber in keiner englischen Lokalisierungsdatei stehen (Vanilla, Abhängigkeiten, Mod).
/// </summary>
public sealed class LocalisationCheck : IClassicCheck
{
    private static readonly HashSet<string> TextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "buttonText", "pdx_tooltip", "tooltip", "tooltipText", "delayedTooltipText",
    };

    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        if (context.Workspace.Classic is not { } classic)
        {
            yield break;
        }

        foreach (var node in context.Document.Root.Descendants())
        {
            cancellation.ThrowIfCancellationRequested();
            if (node.Kind != GuiNodeKind.Property || !TextKeys.Contains(node.Name))
            {
                continue;
            }

            var value = (node.Value ?? string.Empty).Trim().Trim('"');
            if (!LooksLikeKey(value)
                || classic.Localisation.Text(value) is not null
                || classic.Localisation.HasKeyIgnoreCase(value)
                || EngineKeys(context.Workspace).Contains(value))
            {
                continue;
            }

            yield return new GuiFinding(
                GuiFindingKind.LocalisationMissing, GuiFindingSeverity.Warning, GuiTexts.FindingLocalisationMissing(node.Name, value), node, context.DocumentPath, node.Line);
        }
    }

    /// <summary>Ein Schlüssel hat mindestens einen Buchstaben und einen <c>_</c>, keinen Leerraum, keinen Ausdruck (<c>[</c>, <c>$</c>, <c>@</c>) und keine Formatzeichen (<c>§</c>, <c>£</c>).</summary>
    private static bool LooksLikeKey(string value) =>
        value.Length > 0
        && value[0] != '@'
        && value.Contains('_')
        && value.Any(char.IsLetter)
        && !value.Any(char.IsWhiteSpace)
        && value.IndexOfAny(new[] { '[', '$', '§', '£' }) < 0;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GuiWorkspaceOptions, Lazy<HashSet<string>>> EngineKeyCache = new();

    /// <summary>
    /// Schlüssel, die in den Textfeldern der Vanilla-Dateien stehen (Engine-Schlüssel wie <c>CLOSE_TITLE</c> oder <c>FE_…</c> liegen in keiner yml). Einmal je
    /// Arbeitsbereich, träge beim ersten Zugriff; die Prüfung läuft im Hintergrund. Nutzt die schon geparsten Dokumente des Index.
    /// </summary>
    private static HashSet<string> EngineKeys(GuiWorkspace workspace) =>
        EngineKeyCache.GetValue(workspace.Options, _ => new Lazy<HashSet<string>>(() => CollectEngineKeys(workspace), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static HashSet<string> CollectEngineKeys(GuiWorkspace workspace)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in workspace.Index.Files.Where(f => f.Layer == GuiLayer.Vanilla))
        {
            foreach (var node in workspace.Index.DocumentOf(file).Root.Descendants())
            {
                if (node.Kind == GuiNodeKind.Property && TextKeys.Contains(node.Name) && node.Value is { } raw)
                {
                    keys.Add(raw.Trim().Trim('"'));
                }
            }
        }

        return keys;
    }
}

/// <summary>
/// (EU IV) Eine <c>.gui</c>-Datei der Mod in einem <c>interface/</c>-Unterordner, den ein <c>replace_path</c> der Mod nennt, lässt das Spiel beim Start
/// abstürzen (gemessen 2026-09-30). Die Ordner stehen im Profil (<c>replacePathGuiCrashRoots</c>), dieselbe Regel wie bei der Mod-Kompatibilität.
/// </summary>
public sealed class ReplacePathCheck : IClassicCheck
{
    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        var layout = context.Workspace.Rules.Layout;
        var modRoot = context.Workspace.Options.ModRoot;
        if (layout.ReplacePathGuiCrashRoots is not { Count: > 0 } crashRoots
            || string.IsNullOrWhiteSpace(modRoot)
            || !context.DocumentPath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var relative = global::ParadoxModTool.Mods.ReplacePaths.Normalize(Path.GetRelativePath(modRoot, context.DocumentPath));
        var slash = relative.LastIndexOf('/');
        if (slash <= 0 || relative.StartsWith("../", StringComparison.Ordinal))
        {
            yield break;
        }

        var folder = relative[..slash];
        var firstSlash = folder.IndexOf('/');
        if (firstSlash <= 0 || !crashRoots.Any(root => string.Equals(root, folder[..firstSlash], StringComparison.OrdinalIgnoreCase)))
        {
            yield break;
        }

        var entries = global::ParadoxModTool.Mods.ModMetadata.Read(modRoot, layout.MetadataFile ?? string.Empty)?.ReplacePaths;
        var entry = entries?.Select(global::ParadoxModTool.Mods.ReplacePaths.Normalize).FirstOrDefault(e => string.Equals(e, folder, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
        {
            yield return new GuiFinding(
                GuiFindingKind.ReplacePathSubfolder, GuiFindingSeverity.Error, GuiTexts.FindingReplacePathSubfolder(entry), null, context.DocumentPath, 1);
        }
    }
}
