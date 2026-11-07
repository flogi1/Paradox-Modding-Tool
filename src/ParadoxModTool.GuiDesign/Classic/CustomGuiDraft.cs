using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Was "Custom GUI anlegen" bzw. "In Vanilla-Fenster einbauen" schreiben würde: die Dateien mit altem und neuem Text, die Skriptdatei
/// (<c>common/custom_gui/&lt;Basis&gt;_custom_gui.txt</c>) und die Zeile (ab 1) des ersten neuen Eintrags zum Öffnen danach. Mit
/// <paramref name="Problems"/> bleibt <see cref="Files"/> leer. <see cref="Moves"/>: Fenster, die die Übernahme des Gastfensters aus einer verlierenden
/// Mod-Datei umziehen lässt (<see cref="ClassicOverridePlanner.Moves"/>); die App fragt vorher nach.
/// </summary>
public sealed record CustomGuiPlan(IReadOnlyList<GuiPlanFile> Files, string FocusPath, int FocusLine, IReadOnlyList<string> Problems)
{
    public bool CanApply => Problems.Count == 0 && Files.Count > 0;

    public IReadOnlyList<(string Window, string FromFile, string ToFile, string? Dependency)> Moves { get; init; } =
        Array.Empty<(string, string, string, string?)>();

    /// <summary>Hinweise der Übernahme des Gastfensters (<see cref="GuiOverridePlan.Warnings"/>, z. B. eine Abhängigkeit gewinnt), als Text; sperren nicht.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Plant Custom GUI für EU IV (Plan 3 Task 7). Schreibt nichts: Der Aufrufer schreibt die Dateien als einen Rückgängig-Schritt. Vorhandene Dateien werden
/// nur ergänzt (Einträge und Lokalisierung ans Ende, <c>scripted = yes</c> als eine Zeile), alles andere bleibt. Die Form folgt der Probe im Spiel
/// (script-probe-notes.md, Runde 2, 2026-10-30): ein <c>guiButtonType</c> mit <c>scripted = yes</c> plus <c>custom_button</c> und ein <c>windowType</c>-Kind
/// mit <c>scripted = yes</c> plus <c>custom_window</c> funktionieren; <c>backGround = &lt;Sprite&gt;</c> ließ das Spiel abstürzen und wird nie geschrieben.
/// </summary>
public static class CustomGuiDraft
{
    /// <summary>Die Arten, die PMT anlegt, je mit dem passenden Elementtyp (<see cref="CustomGuiWindows.Accepts"/>); <c>custom_shield</c> legt PMT nie an.</summary>
    private static readonly CustomGuiKind[] CreatableKinds = { CustomGuiKind.Button, CustomGuiKind.TextBox, CustomGuiKind.Icon, CustomGuiKind.Window };

    /// <summary>Die Skriptdatei der Mod: <c>common/custom_gui/&lt;Basis&gt;_custom_gui.txt</c>.</summary>
    public static string ScriptPath(string modRoot) =>
        Path.Combine(modRoot, "common", "custom_gui", ScriptedGuiDraft.BaseNameOf(modRoot) + "_custom_gui.txt");

    /// <summary>Die Art, die PMT für diesen Elementtyp anlegt (<c>guiButtonType</c> → <c>custom_button</c> usw.), sonst <see langword="null"/>.</summary>
    public static CustomGuiKind? KindFor(string elementType) =>
        CreatableKinds.Cast<CustomGuiKind?>().FirstOrDefault(k => CustomGuiWindows.Accepts(k!.Value, elementType));

    /// <summary>
    /// "Custom GUI anlegen" für ein Element einer Mod-Datei (<paramref name="guiPath"/>, Text <paramref name="document"/>): setzt <c>scripted = yes</c>,
    /// wenn es fehlt, und schreibt den passenden Eintrag (jeder mit <c>potential = { always = yes }</c> wie in der Probe, Knöpfe mit <c>trigger</c> und <c>effect</c>, alle außer Fenstern
    /// mit <c>tooltip = &lt;Name&gt;_tt</c> und dem leeren Text dazu). Ein Fenster bekommt <c>custom_window</c> und einen Eintrag für jedes Element darin
    /// mit <c>scripted = yes</c>, das noch keinen hat. <paramref name="readFile"/> liefert den Text einer vorhandenen Datei (ohne BOM) oder
    /// <see langword="null"/>; <paramref name="gameLanguage"/> wie bei <see cref="ScriptedGuiDraft.Plan"/>.
    /// </summary>
    public static CustomGuiPlan ForElement(
        GuiNode element, GuiDocument document, string guiPath, string modRoot, CustomGuiIndex index, Func<string, string?> readFile, string? gameLanguage = null)
    {
        var scriptPath = ScriptPath(modRoot);
        var problems = new List<string>();
        if (!IsUnder(modRoot, guiPath))
        {
            problems.Add(GuiTexts.CustomGuiFileNotMod(Path.GetFileName(guiPath)));
        }

        var name = ClassicWindows.NameOf(element).Trim();
        CheckName(name, problems);
        var kind = KindFor(element.Name);
        if (kind is null)
        {
            problems.Add(GuiTexts.CustomGuiTypeUnsupported(name.Length > 0 ? name : element.Name, element.Name));
        }
        else if (CustomGuiWindows.Enclosing(index.Supported, element) is null)
        {
            problems.Add(GuiTexts.CustomGuiNotInSupportedWindow(name.Length > 0 ? name : element.Name));
        }

        var existing = readFile(scriptPath);
        var taken = TakenNames(index, existing, scriptPath);
        if (ScriptedGuiDraft.IsIdentifier(name) && taken.TryGetValue(name, out var takenIn))
        {
            problems.Add(GuiTexts.CustomGuiNameTaken(name, takenIn));
        }

        if (problems.Count > 0)
        {
            return Empty(scriptPath, problems);
        }

        var files = new List<GuiPlanFile>();
        if (!CustomGuiChecks.IsScripted(element))
        {
            var edits = new GuiTextEdits(document).SetProperty(element, "scripted", "yes", ignoreNameCase: true);
            files.Add(new GuiPlanFile(guiPath, document.Text, TextEdit.Apply(document.Text, edits)));
        }

        var entries = EntriesFor(element, kind!.Value, taken);
        var (entryFiles, line) = EntryFiles(entries, modRoot, scriptPath, existing, readFile, gameLanguage);
        files.AddRange(entryFiles);
        return new CustomGuiPlan(files, scriptPath, line, problems);
    }

    /// <summary>
    /// "In Vanilla-Fenster einbauen": setzt das oberste Mod-Fenster <paramref name="modWindow"/> (ein <c>windowType</c> in <paramref name="modDocument"/>,
    /// Datei <paramref name="modWindowPath"/>) als letztes Kind mit <c>scripted = yes</c> in das Fenster <see cref="SupportedWindow.WindowName"/> von
    /// <paramref name="host"/>. Gewinnt schon eine Mod-Fassung des Gastfensters, wird nur dort eingefügt; sonst übernimmt der klassische Planer es wie
    /// "In Mod übernehmen" (EU IV: <c>00_&lt;Basis&gt;_overrides.gui</c>). Mit <paramref name="removeOriginal"/> (der Nutzer hat zugestimmt) verschwindet das
    /// oberste Fenster aus seiner Datei. Dazu die Einträge wie bei <see cref="ForElement"/> für ein Fenster. <paramref name="readEncoding"/> liefert die
    /// Kodierung einer Datei wie <see cref="GuiTextReader.Read"/> (für die Übernahme aus einer Windows-1252-Datei).
    /// </summary>
    public static CustomGuiPlan BuildInto(
        GuiNode modWindow, SupportedWindow host, GuiDocument modDocument, string modWindowPath, bool removeOriginal, GuiIndex guiIndex, GuiModTargets targets,
        CustomGuiIndex index, Func<string, string?> readFile, string? gameLanguage = null, Func<string, string?>? readEncoding = null)
    {
        var modRoot = targets.ModRoot;
        var scriptPath = ScriptPath(modRoot);
        var problems = new List<string>();
        if (!IsUnder(modRoot, modWindowPath))
        {
            problems.Add(GuiTexts.CustomGuiFileNotMod(Path.GetFileName(modWindowPath)));
        }

        var name = ClassicWindows.NameOf(modWindow).Trim();
        CheckName(name, problems);
        if (!ClassicWindows.IsWindow(modWindow))
        {
            problems.Add(GuiTexts.CustomGuiNotTopLevel(name.Length > 0 ? name : modWindow.Name));
        }

        if (!CustomGuiWindows.Accepts(CustomGuiKind.Window, modWindow.Name))
        {
            problems.Add(GuiTexts.CustomGuiNotWindowType(name.Length > 0 ? name : modWindow.Name, modWindow.Name));
        }

        problems.AddRange(BackgroundProblems(modWindow, name));
        var existing = readFile(scriptPath);
        var taken = TakenNames(index, existing, scriptPath);
        if (ScriptedGuiDraft.IsIdentifier(name) && taken.TryGetValue(name, out var takenIn))
        {
            problems.Add(GuiTexts.CustomGuiNameTaken(name, takenIn));
        }

        if (problems.Count > 0)
        {
            return Empty(scriptPath, problems);
        }

        var snippet = ScriptedSnippet(modDocument, modWindow);
        var files = new List<GuiPlanFile>();
        IReadOnlyList<(string, string, string, string?)> moves = Array.Empty<(string, string, string, string?)>();
        IReadOnlyList<string> warnings = Array.Empty<string>();
        var winner = ClassicWindows.Winner(guiIndex, host.TopLevelWindow);
        if (winner is null)
        {
            return Empty(scriptPath, new[] { GuiTexts.CustomGuiHostMissing(host.WindowName) });
        }

        if (winner.File.Layer == GuiLayer.Mod)
        {
            // Die Mod hat das Gastfenster schon (gewinnend): nur einfügen, der übrige Text der Datei bleibt Byte für Byte.
            var path = winner.File.FullPath;
            var old = readFile(path);
            var inserted = old is null ? null : InsertInto(old, host, name, snippet, problems);
            if (problems.Count > 0 || inserted is null)
            {
                return Empty(scriptPath, problems.Count > 0 ? problems : new[] { GuiTexts.CustomGuiHostMissing(host.WindowName) });
            }

            files.Add(new GuiPlanFile(path, old, inserted));
        }
        else
        {
            // Übernahme wie "In Mod übernehmen": Entwurf = Start des Entwurfs der Vanilla-Datei plus das neue Kind; der Planer wählt Zieldatei und prüft.
            var relative = "interface/" + host.File;
            var source = guiIndex.Files.FirstOrDefault(f => f.Layer == GuiLayer.Vanilla && string.Equals(f.RelativePath, relative, StringComparison.OrdinalIgnoreCase))
                ?? (winner.File.Layer == GuiLayer.Vanilla ? winner.File : null);
            if (source is null)
            {
                return Empty(scriptPath, new[] { GuiTexts.CustomGuiHostMissing(host.WindowName) });
            }

            var vanillaText = readFile(source.FullPath) ?? guiIndex.DocumentOf(source).Text;
            var start = GuiDraft.Start(source, vanillaText, guiIndex).Text;
            var draft = InsertInto(start, host, name, snippet, problems);
            if (problems.Count > 0 || draft is null)
            {
                return Empty(scriptPath, problems.Count > 0 ? problems : new[] { GuiTexts.CustomGuiHostMissing(host.WindowName) });
            }

            var takeOver = GuiOverridePlanner.Plan(new GuiOverrideRequest(source, vanillaText, draft, guiIndex, targets, readFile, start)
            {
                // Wie bei "In Mod übernehmen": Eine neue Datei mit Nicht-ASCII-Text aus einer Windows-1252-Quelle bleibt Windows-1252 (sonst zeigt EU IV "Â¤").
                SourceEncoding = readEncoding?.Invoke(source.FullPath),
            });
            if (takeOver.Blockers.Count > 0)
            {
                return Empty(scriptPath, takeOver.Blockers.Select(b => GuiTexts.TakeOverBlocker(b, classic: true)).ToList());
            }

            files.AddRange(takeOver.Files);
            moves = ClassicOverridePlanner.Moves(takeOver, modRoot);
            warnings = takeOver.Warnings.Select(GuiTexts.TakeOverWarning).ToList();
        }

        if (removeOriginal)
        {
            RemoveOriginal(files, modWindowPath, name, readFile(modWindowPath) ?? modDocument.Text);
        }

        var entries = EntriesFor(modWindow, CustomGuiKind.Window, taken);
        var (entryFiles, line) = EntryFiles(entries, modRoot, scriptPath, existing, readFile, gameLanguage);
        files.AddRange(entryFiles);
        return new CustomGuiPlan(files, scriptPath, line, problems) { Moves = moves, Warnings = warnings };
    }

    private static CustomGuiPlan Empty(string scriptPath, IReadOnlyList<string> problems) => new(Array.Empty<GuiPlanFile>(), scriptPath, 0, problems);

    private static void CheckName(string name, List<string> problems)
    {
        if (name.Length == 0)
        {
            problems.Add(GuiTexts.CustomGuiNoName);
        }
        else if (!ScriptedGuiDraft.IsIdentifier(name))
        {
            problems.Add(GuiTexts.CustomGuiNameInvalid(name));
        }
    }

    /// <summary>
    /// Namen mit Eintrag (ohne Rücksicht auf Groß-/Kleinschreibung) und die Datei dazu: aus dem Index und aus dem Text der Zieldatei, wie er jetzt auf der
    /// Platte steht (der Index liest im Hintergrund neu; ein zweiter Klick davor darf keinen doppelten Eintrag schreiben).
    /// </summary>
    private static Dictionary<string, string> TakenNames(CustomGuiIndex index, string? scriptText, string scriptPath)
    {
        var taken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in index.Entries)
        {
            taken.TryAdd(entry.Name, Path.GetFileName(entry.FilePath));
        }

        if (scriptText is not null)
        {
            foreach (var block in GuiParser.Parse(scriptText).Root.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock))
            {
                var name = block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    taken.TryAdd(name, Path.GetFileName(scriptPath));
                }
            }
        }

        return taken;
    }

    /// <summary>Das Element selbst, bei einem Fenster dazu jedes Element darin mit <c>scripted = yes</c>, passender Art, Namen und ohne Eintrag (Textreihenfolge).</summary>
    private static List<(CustomGuiKind Kind, string Name)> EntriesFor(GuiNode element, CustomGuiKind kind, IReadOnlyDictionary<string, string> taken)
    {
        var entries = new List<(CustomGuiKind, string)> { (kind, ClassicWindows.NameOf(element).Trim()) };
        if (kind != CustomGuiKind.Window)
        {
            return entries;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entries[0].Item2 };
        foreach (var node in element.Descendants().Where(n => n.Kind == GuiNodeKind.KeyedBlock && CustomGuiChecks.IsScripted(n)))
        {
            var name = ClassicWindows.NameOf(node).Trim();
            if (KindFor(node.Name) is { } childKind && ScriptedGuiDraft.IsIdentifier(name) && !taken.ContainsKey(name) && seen.Add(name))
            {
                entries.Add((childKind, name));
            }
        }

        return entries;
    }

    /// <summary>Skriptdatei (neu oder am Ende ergänzt, nach einer Leerzeile) und Lokalisierung der Tooltips; dazu die Zeile des ersten neuen Eintrags.</summary>
    private static (List<GuiPlanFile> Files, int Line) EntryFiles(
        IReadOnlyList<(CustomGuiKind Kind, string Name)> entries, string modRoot, string scriptPath, string? existing, Func<string, string?> readFile,
        string? gameLanguage)
    {
        var addition = string.Join("\n", entries.Select(e => EntryBlock(e.Kind, e.Name)));
        var script = existing is null ? new GuiPlanFile(scriptPath, null, addition) : ScriptedGuiDraft.Append(scriptPath, existing, addition, blankLine: true);
        var newline = existing is not null && existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var start = script.NewText.Length - addition.Replace("\n", newline).Length;
        var line = 1 + script.NewText[..start].Count(c => c == '\n');
        var files = new List<GuiPlanFile> { script };

        var keys = entries.Where(e => e.Kind != CustomGuiKind.Window).Select(e => e.Name + "_tt").ToList();
        if (keys.Count == 0)
        {
            return (files, line);
        }

        var baseName = ScriptedGuiDraft.BaseNameOf(modRoot);
        foreach (var language in ScriptedGuiDraft.Languages(gameLanguage))
        {
            // EU IV liest Lokalisierung flach aus localisation/ (vanilla 1.37 hat dort keine Sprachordner).
            var path = Path.Combine(modRoot, "localisation", baseName + "_l_" + language + ".yml");
            var old = readFile(path);
            var missing = keys.Where(k => old is null || !Regex.IsMatch(old, @"^\s*" + Regex.Escape(k) + @"\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase)).ToList();
            if (missing.Count == 0)
            {
                continue;
            }

            var lines = string.Concat(missing.Select(k => ScriptedGuiDraft.LocLine(k, string.Empty) + "\n"));
            files.Add(old is null || old.Trim().Length == 0
                ? new GuiPlanFile(path, old, "l_" + language + ":\n" + lines)
                : ScriptedGuiDraft.Append(path, old, lines, blankLine: false));
        }

        return (files, line);
    }

    /// <summary>Ein Eintrag mit Tabs und <c>\n</c>, mit Zeilenende am Schluss.</summary>
    private static string EntryBlock(CustomGuiKind kind, string name)
    {
        var builder = new StringBuilder();
        builder.Append(CustomGuiIndex.BlockName(kind)).Append(" = {\n");
        builder.Append("\tname = ").Append(name).Append('\n');
        builder.Append("\tpotential = { always = yes }\n");
        if (kind == CustomGuiKind.Button)
        {
            builder.Append("\ttrigger = { }\n");
            builder.Append("\teffect = { }\n");
        }

        // custom_window kennt laut example.txt keinen tooltip.
        if (kind != CustomGuiKind.Window)
        {
            builder.Append("\ttooltip = ").Append(name).Append("_tt\n");
        }

        return builder.Append("}\n").ToString();
    }

    /// <summary>
    /// Regel R3 (Probe Runde 1): <c>backGround</c> nennt in vanilla ein Kindelement; ein anderer Wert (ein Sprite) ließ EU IV abstürzen. Geprüft werden das
    /// Fenster und alle Elemente darin; PMT ändert den Text nicht, es meldet nur.
    /// </summary>
    private static IEnumerable<string> BackgroundProblems(GuiNode window, string windowName)
    {
        foreach (var node in new[] { window }.Concat(window.Descendants()).Where(n => n.Kind == GuiNodeKind.KeyedBlock))
        {
            foreach (var background in node.Children.Where(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "backGround", StringComparison.OrdinalIgnoreCase)))
            {
                var value = background.Value?.Trim() ?? string.Empty;
                if (value.Length > 0 && !node.Children.Any(c => c.Kind == GuiNodeKind.KeyedBlock && string.Equals(ClassicWindows.NameOf(c), value, StringComparison.OrdinalIgnoreCase)))
                {
                    var name = ClassicWindows.NameOf(node);
                    yield return GuiTexts.CustomGuiBackgroundSprite(name.Length > 0 ? name : windowName, value);
                }
            }
        }
    }

    /// <summary>Das Mod-Fenster als Schnipsel (<see cref="GuiTextEdits.Dedent"/>), mit <c>scripted = yes</c>, wenn es fehlt.</summary>
    private static string ScriptedSnippet(GuiDocument document, GuiNode window)
    {
        var snippet = new GuiTextEdits(document).Dedent(window);
        if (CustomGuiChecks.IsScripted(window))
        {
            return snippet;
        }

        var parsed = GuiParser.Parse(snippet);
        var node = parsed.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock);
        return TextEdit.Apply(snippet, new GuiTextEdits(parsed).SetProperty(node, "scripted", "yes", ignoreNameCase: true));
    }

    /// <summary>
    /// Fügt <paramref name="snippet"/> als letztes Kind in das Fenster <see cref="SupportedWindow.WindowName"/> (im obersten Fenster
    /// <see cref="SupportedWindow.TopLevelWindow"/>) von <paramref name="text"/> ein; <see langword="null"/> mit Problem, wenn es fehlt oder schon ein Element
    /// dieses Namens hat.
    /// </summary>
    private static string? InsertInto(string text, SupportedWindow host, string name, string snippet, List<string> problems)
    {
        var document = GuiParser.Parse(text);
        var top = ClassicWindows.Of(document).FirstOrDefault(w => string.Equals(ClassicWindows.NameOf(w), host.TopLevelWindow, StringComparison.OrdinalIgnoreCase));
        var target = top is null ? null
            : string.Equals(ClassicWindows.NameOf(top), host.WindowName, StringComparison.OrdinalIgnoreCase) ? top
            : top.Descendants().FirstOrDefault(n => n.Kind == GuiNodeKind.KeyedBlock && n.HasBody
                && string.Equals(ClassicWindows.NameOf(n), host.WindowName, StringComparison.OrdinalIgnoreCase));
        if (target is null || !target.HasBody)
        {
            problems.Add(GuiTexts.CustomGuiHostMissing(host.WindowName));
            return null;
        }

        if (target.Children.Any(c => c.Kind == GuiNodeKind.KeyedBlock && string.Equals(ClassicWindows.NameOf(c), name, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(GuiTexts.CustomGuiHostHasChild(host.WindowName, name));
            return null;
        }

        return TextEdit.Apply(text, new GuiTextEdits(document).InsertChild(target, snippet, InsertPosition.LastChild));
    }

    /// <summary>
    /// Entfernt das oberste Fenster <paramref name="name"/> aus <paramref name="path"/>. Plant der Plan dieselbe Datei schon (Gastfenster und Mod-Fenster in
    /// einer Datei), wird ihr neuer Text weiterbearbeitet: zwei Einträge für eine Datei überschrieben sich beim Schreiben.
    /// </summary>
    private static void RemoveOriginal(List<GuiPlanFile> files, string path, string name, string diskText)
    {
        var index = files.FindIndex(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
        var text = index >= 0 ? files[index].NewText : diskText;
        var document = GuiParser.Parse(text);
        var window = ClassicWindows.Of(document).FirstOrDefault(w => string.Equals(ClassicWindows.NameOf(w), name, StringComparison.OrdinalIgnoreCase));
        if (window is null)
        {
            return;
        }

        var newText = TextEdit.Apply(text, new GuiTextEdits(document).RemoveNode(window));
        if (index >= 0)
        {
            files[index] = files[index] with { NewText = newText };
        }
        else
        {
            files.Add(new GuiPlanFile(path, diskText, newText));
        }
    }

    private static bool IsUnder(string root, string path)
    {
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
            return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
