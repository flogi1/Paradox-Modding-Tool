using System.Text;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Wo ein neues Scripted GUI (HOI IV) erscheint: über einem Spielfenster (<c>player_context</c> mit <c>parent_window_token</c>), als Entscheidungskategorie
/// (<c>decision_category</c> plus Kategorie und Lokalisierung) oder mit einem anderen <c>context_type</c> ohne weitere Schlüssel.
/// </summary>
public enum ScriptedGuiPlacement
{
    ParentToken,
    DecisionCategory,
    OtherContext,
}

/// <summary>
/// Wunsch für ein neues Scripted GUI. <paramref name="ParentToken"/> gilt nur für <see cref="ScriptedGuiPlacement.ParentToken"/>,
/// <paramref name="ContextType"/> nur für <see cref="ScriptedGuiPlacement.OtherContext"/>, <paramref name="CategoryId"/> und <paramref name="CategoryName"/>
/// nur für <see cref="ScriptedGuiPlacement.DecisionCategory"/> (ein leerer Name wird die ID). <paramref name="ButtonNames"/>: je Knopf ein leerer
/// <c>&lt;Knopf&gt;_click</c>-Effekt.
/// </summary>
public sealed record ScriptedGuiRequest(
    string WindowName, string EntryName, ScriptedGuiPlacement Placement, string? ParentToken, string? ContextType, string? CategoryId, string? CategoryName,
    IReadOnlyList<string> ButtonNames);

/// <summary>
/// Was "Scripted GUI anlegen" bzw. "Effekt anlegen" schreiben würde: die Dateien mit altem und neuem Text, die Skriptdatei und die Zeile (ab 1) des neuen
/// Blocks zum Öffnen danach. Mit <paramref name="Problems"/> bleibt <see cref="Files"/> leer.
/// </summary>
public sealed record ScriptedGuiPlan(IReadOnlyList<GuiPlanFile> Files, string ScriptPath, int FocusLine, IReadOnlyList<string> Problems)
{
    public bool CanApply => Problems.Count == 0 && Files.Count > 0;
}

/// <summary>
/// Plant neue Scripted GUIs und Effekte für HOI IV (Plan 3 Task 6). Schreibt nichts: Der Aufrufer schreibt die Dateien als einen Rückgängig-Schritt.
/// Vorhandene Dateien werden nur an einer Stelle ergänzt, alles andere (Kommentare, Einrückung, Zeilenende) bleibt. Die Vorgaben folgen der Probe im Spiel
/// (script-probe-notes.md, 2026-10-30): <c>top_bar</c> zeigt das Fenster, eine leere Kategorie braucht <c>visible_when_empty = yes</c>.
/// </summary>
public static class ScriptedGuiDraft
{
    /// <summary>Kategorie-Symbol der Probe (Runde 2 im Spiel getestet), in vanilla <c>interface/decisions.gfx</c>.</summary>
    public const string CategoryIcon = "GFX_decision_category_generic";

    /// <summary>Im Spiel getestet (Runde 1): <c>player_context</c> mit <c>top_bar</c> zeigt das Fenster, der <c>_click</c>-Effekt wirkt.</summary>
    public const string DefaultParentToken = "top_bar";

    public const string DefaultOtherContext = "selected_country_context";

    private const string PlayerContext = "player_context";

    public static ScriptedGuiPlacement DefaultPlacement => ScriptedGuiPlacement.ParentToken;

    /// <summary>Die Werte von <c>parent_window_token</c> aus <c>common/scripted_guis/_documentation.md</c> (HOI IV 1.19), <c>top_bar</c> zuerst.</summary>
    public static IReadOnlyList<string> ParentTokens { get; } = new[]
    {
        "top_bar", "decision_tab", "technology_tab", "trade_tab", "construction_tab", "production_tab", "deployment_tab", "logistics_tab", "diplomacy_tab",
        "national_focus", "politics_tab", "selected_country_view", "selected_state_view", "selected_country_view_info", "selected_country_view_diplomacy",
        "army_ledger", "navy_ledger", "civilian_ledger", "air_ledger", "tech_infantry_folder", "tech_support_folder", "tech_armor_folder",
        "tech_artillery_folder", "tech_land_doctrine_folder", "tech_naval_folder", "tech_naval_doctrine_folder", "tech_air_techs_folder",
        "tech_air_doctrine_folder", "tech_electronics_folder", "tech_industry_folder",
    };

    /// <summary>Die übrigen <c>context_type</c>-Werte der Doku (ohne <c>player_context</c> und <c>decision_category</c>, die eigene Anzeigearten haben).</summary>
    public static IReadOnlyList<string> OtherContextTypes { get; } = new[]
    {
        "selected_country_context", "selected_state_context", "diplomacy_target_context", "diplomatic_action", "national_focus_context", "country_mapicon",
        "state_mapicon",
    };

    /// <summary><c>&lt;Basis&gt;_&lt;Fenster&gt;</c>; Zeichen außerhalb von <c>[A-Za-z0-9_]</c> im Fensternamen werden <c>_</c>.</summary>
    public static string DefaultEntryName(string modRoot, string windowName) => BaseNameOf(modRoot) + "_" + Identifier(windowName);

    /// <summary>Vorschlag für die Kategorie-ID: Eintragsname plus <c>_category</c>.</summary>
    public static string DefaultCategoryId(string entryName) => entryName + "_category";

    /// <summary>Nur <c>[A-Za-z0-9_]</c>, nicht leer.</summary>
    public static bool IsIdentifier(string? name) => !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>
    /// Plant Skripteintrag (und bei <see cref="ScriptedGuiPlacement.DecisionCategory"/> Kategorie und Lokalisierung). <paramref name="readFile"/> liefert den
    /// Text einer vorhandenen Datei (voller Pfad, ohne BOM) oder <see langword="null"/>. <paramref name="gameLanguage"/> ist die Spielsprache aus
    /// <c>settings.txt</c> (<c>l_german</c>); ist sie nicht Englisch, kommen die Texte auch in <c>localisation/&lt;Sprache&gt;/&lt;Basis&gt;_l_&lt;Sprache&gt;.yml</c>.
    /// </summary>
    public static ScriptedGuiPlan Plan(
        ScriptedGuiRequest request, string modRoot, ScriptedGuiIndex index, Func<string, string?> readFile, string? gameLanguage = null)
    {
        var baseName = BaseNameOf(modRoot);
        var scriptPath = Path.Combine(modRoot, "common", "scripted_guis", baseName + "_scripted_guis.txt");
        var categoryPath = Path.Combine(modRoot, "common", "decisions", "categories", baseName + "_categories.txt");
        var existing = readFile(scriptPath);
        var problems = Check(request, index, existing, Path.GetFileName(scriptPath),
            request.Placement == ScriptedGuiPlacement.DecisionCategory ? readFile(categoryPath) : null);
        if (problems.Count > 0)
        {
            return new ScriptedGuiPlan(Array.Empty<GuiPlanFile>(), scriptPath, 0, problems);
        }

        var files = new List<GuiPlanFile>();
        var newText = existing is null ? NewScriptFile(EntryBlock(request)) : InsertEntry(existing, EntryBlock(request));
        files.Add(new GuiPlanFile(scriptPath, existing, newText));
        var focus = EntryLine(newText, request.EntryName);

        if (request.Placement == ScriptedGuiPlacement.DecisionCategory)
        {
            files.Add(Append(categoryPath, readFile(categoryPath), CategoryBlock(request.CategoryId!, request.EntryName), blankLine: true));

            var name = string.IsNullOrWhiteSpace(request.CategoryName) ? request.CategoryId! : request.CategoryName.Trim();
            foreach (var language in Languages(gameLanguage))
            {
                var locPath = Path.Combine(modRoot, "localisation", language, baseName + "_l_" + language + ".yml");
                var lines = LocLine(request.CategoryId!, name) + "\n" + LocLine(request.CategoryId + "_desc", string.Empty) + "\n";
                var old = readFile(locPath);
                files.Add(old is null || old.Trim().Length == 0
                    ? new GuiPlanFile(locPath, old, "l_" + language + ":\n" + lines)
                    : Append(locPath, old, lines, blankLine: false));
            }
        }

        return new ScriptedGuiPlan(files, scriptPath, focus, problems);
    }

    /// <summary>
    /// Plant <c>&lt;Knopf&gt;_click = { }</c> unter <c>effects</c> des Eintrags (fehlt <c>effects</c>, entsteht es vor <c>triggers</c> bzw. als letztes Kind).
    /// Nur für Einträge der Mod; ein schon vorhandener Schlüssel (ohne Rücksicht auf Groß-/Kleinschreibung) ist ein Problem.
    /// </summary>
    public static ScriptedGuiPlan AddEffect(ScriptedGuiEntry entry, string buttonName, Func<string, string?> readFile)
    {
        var key = buttonName + "_click";
        var problems = new List<string>();
        if (!entry.IsMod)
        {
            problems.Add(GuiTexts.ScriptedGuiEntryNotMod(entry.Name));
        }

        if (!IsIdentifier(buttonName))
        {
            problems.Add(GuiTexts.ScriptedGuiNameInvalid(buttonName));
        }

        var text = problems.Count == 0 ? readFile(entry.FilePath) : null;
        var parsed = text is null ? null : GuiParser.Parse(text);
        var node = parsed is null ? null : FindEntry(parsed, entry.Name, entry.Line);
        if (problems.Count == 0 && node is null)
        {
            problems.Add(GuiTexts.ScriptedGuiEntryNotFound(entry.Name, Path.GetFileName(entry.FilePath)));
        }

        var effects = node?.Children.LastOrDefault(c => IsBlock(c, "effects"));
        if (effects is not null && effects.Children.Any(c => c.Kind is GuiNodeKind.KeyedBlock or GuiNodeKind.Property
                && string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(GuiTexts.ScriptedGuiEffectExists(key, entry.Name));
        }

        if (problems.Count > 0 || text is null || parsed is null || node is null)
        {
            return new ScriptedGuiPlan(Array.Empty<GuiPlanFile>(), entry.FilePath, 0, problems);
        }

        var edits = new GuiTextEdits(parsed);
        string newText;
        if (effects is not null)
        {
            newText = TextEdit.Apply(text, edits.InsertChild(effects, key + " = { }", InsertPosition.LastChild));
        }
        else
        {
            var snippet = "effects = {\n\t" + key + " = { }\n}";
            var triggers = node.Children.FirstOrDefault(c => IsBlock(c, "triggers"));
            newText = TextEdit.Apply(text, triggers is null
                ? edits.InsertChild(node, snippet, InsertPosition.LastChild)
                : edits.InsertChild(node, snippet, InsertPosition.Before, triggers));
        }

        var newEntry = FindEntry(GuiParser.Parse(newText), entry.Name, null);
        var line = newEntry?.Children.LastOrDefault(c => IsBlock(c, "effects"))?.Children
            .LastOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase))?.Line ?? entry.Line;
        return new ScriptedGuiPlan(new[] { new GuiPlanFile(entry.FilePath, text, newText) }, entry.FilePath, line, problems);
    }

    /// <summary>
    /// Prüft den Wunsch gegen den Index und gegen den Text der Zieldateien, wie er jetzt auf der Platte steht (<paramref name="scriptText"/>,
    /// <paramref name="categoryText"/>): Der Index liest im Hintergrund neu, ein zweiter Klick davor darf keinen doppelten Eintrag und keine doppelte
    /// Kategorie schreiben (wie <c>CustomGuiDraft.TakenNames</c>).
    /// </summary>
    private static List<string> Check(ScriptedGuiRequest request, ScriptedGuiIndex index, string? scriptText, string scriptFile, string? categoryText)
    {
        var problems = new List<string>();
        var onDisk = scriptText is null
            ? new List<(string Name, string? Window)>()
            : GuiParser.Parse(scriptText).Root.Children.Where(ScriptedGuiIndex.IsContainerBlock)
                .SelectMany(c => c.Children.Where(e => e.Kind == GuiNodeKind.KeyedBlock && e.Name.Length > 0))
                .Select(e => (Name: e.Name, Window: e.Children.LastOrDefault(p => p.Kind == GuiNodeKind.Property
                    && string.Equals(p.Name, "window_name", StringComparison.OrdinalIgnoreCase))?.Value?.Trim()))
                .ToList();
        if (!IsIdentifier(request.EntryName))
        {
            problems.Add(GuiTexts.ScriptedGuiNameInvalid(request.EntryName));
        }
        else if (index.Entries.FirstOrDefault(e => string.Equals(e.Name, request.EntryName, StringComparison.OrdinalIgnoreCase)) is { } taken)
        {
            problems.Add(GuiTexts.ScriptedGuiNameTaken(taken.Name, Path.GetFileName(taken.FilePath)));
        }
        else if (onDisk.FirstOrDefault(e => string.Equals(e.Name, request.EntryName, StringComparison.OrdinalIgnoreCase)) is { Name: not null } written)
        {
            problems.Add(GuiTexts.ScriptedGuiNameTaken(written.Name, scriptFile));
        }

        if (index.Entries.FirstOrDefault(e => e.IsMod && string.Equals(e.WindowName, request.WindowName, StringComparison.OrdinalIgnoreCase)) is { } own)
        {
            problems.Add(GuiTexts.ScriptedGuiWindowHasEntry(request.WindowName, own.Name));
        }
        else if (onDisk.FirstOrDefault(e => string.Equals(e.Window, request.WindowName, StringComparison.OrdinalIgnoreCase)) is { Name: not null } ownOnDisk)
        {
            problems.Add(GuiTexts.ScriptedGuiWindowHasEntry(request.WindowName, ownOnDisk.Name));
        }

        var categoriesOnDisk = categoryText is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : GuiParser.Parse(categoryText).Root.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Length > 0)
                .Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var button in request.ButtonNames.Where(b => !IsIdentifier(b)))
        {
            problems.Add(GuiTexts.ScriptedGuiNameInvalid(button));
        }

        switch (request.Placement)
        {
            case ScriptedGuiPlacement.ParentToken when !IsIdentifier(request.ParentToken):
                problems.Add(GuiTexts.ScriptedGuiNameInvalid(request.ParentToken ?? string.Empty));
                break;
            case ScriptedGuiPlacement.OtherContext when !IsIdentifier(request.ContextType):
                problems.Add(GuiTexts.ScriptedGuiNameInvalid(request.ContextType ?? string.Empty));
                break;
            case ScriptedGuiPlacement.DecisionCategory when !IsIdentifier(request.CategoryId):
                problems.Add(GuiTexts.ScriptedGuiNameInvalid(request.CategoryId ?? string.Empty));
                break;
            case ScriptedGuiPlacement.DecisionCategory when index.CategoryIds.Contains(request.CategoryId!) || categoriesOnDisk.Contains(request.CategoryId!):
                problems.Add(GuiTexts.ScriptedGuiCategoryTaken(request.CategoryId!));
                break;
        }

        return problems;
    }

    /// <summary>Der Eintrag mit Tabs, Zeilen mit <c>\n</c>, ohne äußere Einrückung.</summary>
    private static string EntryBlock(ScriptedGuiRequest request)
    {
        var context = request.Placement switch
        {
            ScriptedGuiPlacement.ParentToken => PlayerContext,
            ScriptedGuiPlacement.DecisionCategory => ScriptedGuiIndex.DecisionCategory,
            _ => request.ContextType!,
        };
        var builder = new StringBuilder();
        builder.Append(request.EntryName).Append(" = {\n");
        builder.Append("\tcontext_type = ").Append(context).Append('\n');
        if (request.Placement == ScriptedGuiPlacement.ParentToken)
        {
            builder.Append("\tparent_window_token = ").Append(request.ParentToken).Append('\n');
        }

        builder.Append("\twindow_name = \"").Append(request.WindowName).Append("\"\n");
        builder.Append("\tvisible = { always = yes }\n");
        if (request.ButtonNames.Count == 0)
        {
            builder.Append("\teffects = { }\n");
        }
        else
        {
            builder.Append("\teffects = {\n");
            foreach (var button in request.ButtonNames)
            {
                builder.Append("\t\t").Append(button).Append("_click = { }\n");
            }

            builder.Append("\t}\n");
        }

        return builder.Append("\ttriggers = { }\n}").ToString();
    }

    private static string CategoryBlock(string categoryId, string entryName) =>
        categoryId + " = {\n" +
        "\ticon = " + CategoryIcon + "\n" +
        "\tallowed = { always = yes }\n" +
        "\tvisible_when_empty = yes\n" +
        "\tscripted_gui = " + entryName + "\n" +
        "}\n";

    /// <summary>Lokalisierungszeile wie in vanilla (ohne Versionsnummer): Leerzeichen, Schlüssel, Doppelpunkt, Text in Anführungszeichen.</summary>
    internal static string LocLine(string key, string text) =>
        " " + key + ": \"" + text.Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    /// <summary>Englisch immer; dazu die Spielsprache (ohne <c>l_</c>), wenn sie lesbar und nicht Englisch ist.</summary>
    internal static IEnumerable<string> Languages(string? gameLanguage)
    {
        yield return "english";
        var language = gameLanguage?.Trim().ToLowerInvariant() ?? string.Empty;
        language = language.StartsWith("l_", StringComparison.Ordinal) ? language[2..] : language;
        if (IsIdentifier(language) && language != "english")
        {
            yield return language;
        }
    }

    private static string NewScriptFile(string entry) =>
        "scripted_gui = {\n" + string.Join("\n", entry.Split('\n').Select(l => "\t" + l)) + "\n}\n";

    /// <summary>In den ersten obersten <c>scripted_gui</c>-Block als letztes Kind, sonst ein neuer Block am Ende.</summary>
    private static string InsertEntry(string existing, string entry)
    {
        var document = GuiParser.Parse(existing);
        var edits = new GuiTextEdits(document);
        var block = document.Root.Children.FirstOrDefault(c => ScriptedGuiIndex.IsContainerBlock(c) && c.CloseBrace >= 0);
        if (block is not null)
        {
            return TextEdit.Apply(existing, edits.InsertChild(block, entry, InsertPosition.LastChild));
        }

        var newline = edits.Newline;
        var text = new StringBuilder(existing);
        if (existing.Trim().Length > 0)
        {
            text.Append(existing.EndsWith('\n') ? string.Empty : newline).Append(newline);
        }

        return text.Append(NewScriptFile(entry).Replace("\n", newline)).ToString();
    }

    /// <summary>Hängt <paramref name="addition"/> (Zeilen mit <c>\n</c>) an das Dateiende, im Zeilenende der Datei; mit <paramref name="blankLine"/> nach einer Leerzeile.</summary>
    internal static GuiPlanFile Append(string path, string? existing, string addition, bool blankLine)
    {
        if (existing is null)
        {
            return new GuiPlanFile(path, null, addition);
        }

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var text = new StringBuilder(existing);
        if (existing.Trim().Length > 0)
        {
            text.Append(existing.EndsWith('\n') ? string.Empty : newline);
            if (blankLine)
            {
                text.Append(newline);
            }
        }

        return new GuiPlanFile(path, existing, text.Append(addition.Replace("\n", newline)).ToString());
    }

    private static int EntryLine(string text, string entryName) => FindEntry(GuiParser.Parse(text), entryName, null)?.Line ?? 1;

    /// <summary>Der Eintrag unter einem obersten <c>scripted_gui</c>; mit <paramref name="line"/> bevorzugt der an dieser Zeile.</summary>
    private static GuiNode? FindEntry(GuiDocument document, string entryName, int? line)
    {
        var candidates = document.Root.Children
            .Where(c => ScriptedGuiIndex.IsContainerBlock(c))
            .SelectMany(c => c.Children)
            .Where(c => c.Kind == GuiNodeKind.KeyedBlock && string.Equals(c.Name, entryName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return candidates.FirstOrDefault(c => c.Line == line) ?? candidates.LastOrDefault();
    }

    private static bool IsBlock(GuiNode node, string name) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.HasBody && string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase);

    private static string Identifier(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return builder.ToString();
    }

    internal static string BaseNameOf(string modRoot) => GuiModTargets.BaseName(Path.GetFileName(modRoot.TrimEnd('\\', '/')));
}
