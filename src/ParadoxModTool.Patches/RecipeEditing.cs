using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Hilfen für das Bearbeiten von Rezepten im Fenster: Ids, neue Regeln, Reihenfolge, Warnungen, Spaltennamen, Einzel-Vorschau.</summary>
public static class RecipeEditing
{
    private static readonly Regex ColumnName = new(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant);

    /// <summary>Dateinamen-sichere Id aus dem Namen (Kleinbuchstaben, ASCII-Buchstaben und Ziffern, sonst <c>_</c>), eindeutig per Zähler.</summary>
    public static string NewId(string name, IEnumerable<string> existingIds)
    {
        var builder = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_');
        }

        var slug = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
        if (slug.Length == 0)
        {
            slug = "rezept";
        }

        var taken = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(slug))
        {
            return slug;
        }

        for (var i = 2; ; i++)
        {
            if (!taken.Contains(slug + "_" + i))
            {
                return slug + "_" + i;
            }
        }
    }

    public static PatchRecipe Create(string name, string gameId, IEnumerable<string> existingIds) =>
        new() { Id = NewId(name, existingIds), Name = name, GameId = gameId };

    /// <param name="kind"><c>lines</c>, <c>blockCopy</c>, <c>template</c> oder <c>listEntry</c>.</param>
    public static string NewRuleId(PatchRecipe recipe, string kind)
    {
        var ids = recipe.Rules.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            if (!ids.Contains(kind + i))
            {
                return kind + i;
            }
        }
    }

    public static PatchRule NewRule(PatchRecipe recipe, string kind, string name)
    {
        PatchRule rule = kind switch
        {
            "lines" => new LineRule(),
            "blockCopy" => new BlockCopyRule(),
            "template" => new TemplateRule(),
            "listEntry" => new ListEntryRule(),
            "blockEdit" => new BlockEditRule(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        rule.Id = NewRuleId(recipe, kind);
        rule.Name = name;
        return rule;
    }

    /// <summary>Wächter "Kopie anlegen + Regel": die Datei kommt (mit <c>;</c>) ins Dateimuster der Regel, die Kopie entsteht beim nächsten Anwenden.</summary>
    /// <remarks>
    /// Kopiert die Regel schon aus Vanilla oder hat ihr Dateimuster kein <c>*</c>, kommt die Datei mit <c>;</c> dazu. Sonst würde das Einschalten
    /// der Kopie jede passende Vanilla-Datei des Musters kopieren; dann entsteht direkt hinter der Regel eine gleiche Regel nur für diese Datei.
    /// Gibt die Regel zurück, die die Datei jetzt abdeckt.
    /// </remarks>
    public static LineRule AddFileToLineRule(PatchRecipe recipe, LineRule rule, string relativePath)
    {
        if (rule.CreateCopy || !rule.Files.Contains('*'))
        {
            AppendFile(rule, relativePath);
            return rule;
        }

        var path = WatchPath(relativePath);
        var sibling = new LineRule
        {
            Id = NewRuleId(recipe, "lines"),
            Name = (rule.Name.Length > 0 ? rule.Name : rule.Id) + ": " + path,
            Enabled = rule.Enabled,
            Scope = rule.Scope,
            Files = path,
            Match = rule.Match,
            MatchKind = rule.MatchKind,
            Action = rule.Action,
            Replacement = rule.Replacement,
            FollowNext = rule.FollowNext,
            CreateCopy = true,
        };
        var index = recipe.Rules.IndexOf(rule);
        recipe.Rules.Insert(index < 0 ? recipe.Rules.Count : index + 1, sibling);
        return sibling;
    }

    private static void AppendFile(LineRule rule, string relativePath)
    {
        var path = WatchPath(relativePath);
        if (rule.Files.Trim().Length == 0)
        {
            rule.Files = path;
        }
        else if (!GlobMatcher.IsMatch(rule.Files, path))
        {
            rule.Files = rule.Files.TrimEnd(' ', ';') + ";" + path;
        }

        rule.CreateCopy = true;
    }

    /// <summary>
    /// Neue Zeilen-Regel aus einer Fundstelle; sie startet ausgeschaltet, weil "Auskommentieren" bei einer Zeile mit <c>{</c> die Datei zerstören würde.
    /// </summary>
    public static LineRule NewLineRuleForHit(PatchRecipe recipe, string relativePath, WatchLine line)
    {
        var rule = (LineRule)NewRule(recipe, "lines", line.Pattern);
        rule.Files = WatchPath(relativePath);
        rule.Match = line.RowKey ? "{key}" : line.Pattern;
        rule.Scope = line.RowKey ? RuleScope.PerRow : RuleScope.Global;
        rule.Action = LineAction.Comment;
        rule.CreateCopy = true;
        rule.Enabled = false;
        recipe.Rules.Add(rule);
        return rule;
    }

    public static void Ignore(PatchRecipe recipe, string relativePath, string note)
    {
        recipe.Watch.Ignored.RemoveAll(entry => string.Equals(WatchPath(entry.Path), WatchPath(relativePath), StringComparison.OrdinalIgnoreCase));
        recipe.Watch.Ignored.Add(new IgnoredWatchFile { Path = WatchPath(relativePath), Note = note });
    }

    /// <summary>Ignoriert den Ordner der Datei samt Unterordnern (Muster <c>ordner/**/*</c>); null, wenn die Datei im Spielordner selbst liegt (kein Ordner).</summary>
    public static string? IgnoreFolder(PatchRecipe recipe, string relativePath, string note)
    {
        var path = WatchPath(relativePath);
        var slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        var pattern = path[..slash] + "/**/*";
        recipe.Watch.Ignored.RemoveAll(entry => string.Equals(WatchPath(entry.Path), pattern, StringComparison.OrdinalIgnoreCase));
        recipe.Watch.Ignored.Add(new IgnoredWatchFile { Path = pattern, Note = note });
        return pattern;
    }

    /// <summary>Hebt das Ignorieren der Datei auf: ihren Eintrag und jede Ordner-Regel (Muster mit <c>*</c>), die sie trifft.</summary>
    public static void Unignore(PatchRecipe recipe, string relativePath) =>
        recipe.Watch.Ignored.RemoveAll(entry => IsIgnoredBy(entry, relativePath));

    /// <summary>Trifft der Eintrag die Datei? Ohne <c>*</c> ist es der Pfad selbst, mit <c>*</c> ein Ordner-Muster.</summary>
    public static bool IsIgnoredBy(IgnoredWatchFile entry, string relativePath)
    {
        var pattern = WatchPath(entry.Path);
        var path = WatchPath(relativePath);
        return pattern.Contains('*') ? GlobMatcher.IsMatch(pattern, path) : string.Equals(pattern, path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Suchmuster des Wächters aus einem Text mit einem Muster je Zeile.</summary>
    public static void SetWatchPatterns(PatchRecipe recipe, IEnumerable<string> lines) =>
        recipe.Watch.Patterns = lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList();

    private static string WatchPath(string path) => path.Replace('\\', '/').Trim('/');

    public static void Move(PatchRecipe recipe, string ruleId, int delta)
    {
        var index = recipe.Rules.FindIndex(r => r.Id == ruleId);
        if (index < 0)
        {
            return;
        }

        var target = Math.Clamp(index + delta, 0, recipe.Rules.Count - 1);
        var rule = recipe.Rules[index];
        recipe.Rules.RemoveAt(index);
        recipe.Rules.Insert(target, rule);
    }

    /// <summary>Unbekannte Platzhalter in allen Vorlagen-Feldern der Regel plus die Prüfungen der Zeilen-Regel.</summary>
    public static IReadOnlyList<string> RuleWarnings(PatchRecipe recipe, PatchRule rule)
    {
        var columns = recipe.Columns.Select(c => c.Name).ToList();
        var fields = rule switch
        {
            LineRule line => new[] { line.Match, line.Replacement },
            BlockCopyRule copy => new[] { copy.BlockKey }.Concat(copy.Edits.Select(e => e.Text)).ToArray(),
            TemplateRule template => new[] { template.Text },
            ListEntryRule list => new[] { list.ListPath, list.Entry },
            BlockEditRule edit => new[] { edit.BlockKey }.Concat(edit.Edits.Select(e => e.Text)).ToArray(),
            _ => Array.Empty<string>(),
        };
        var warnings = fields
            .SelectMany(f => TemplateRenderer.UnknownPlaceholders(f ?? string.Empty, columns))
            .Distinct(StringComparer.Ordinal)
            .Select(name => PatchTexts.UnknownPlaceholder(rule.Name, name))
            .ToList();
        if (rule is LineRule lineRule)
        {
            warnings.AddRange(LineRuleValidation.Warnings(lineRule));
        }

        return warnings;
    }

    /// <summary>Null bei gültigem Namen; <paramref name="ownName"/> ist der bisherige Name beim Umbenennen.</summary>
    public static string? ColumnNameProblem(PatchRecipe recipe, string name, string? ownName)
    {
        if (!ColumnName.IsMatch(name))
        {
            return PatchTexts.ColumnNameInvalid;
        }

        if (name == "key")
        {
            return PatchTexts.ColumnNameReserved;
        }

        return name != ownName && recipe.Columns.Any(c => c.Name == name) ? PatchTexts.ColumnNameTaken : null;
    }

    /// <summary>Kopie mit nur dieser Regel (aktiv) und nur dieser Zeile, für „Ergebnis zeigen“; die Id bleibt, damit das Gedächtnis gefunden wird.</summary>
    public static PatchRecipe SingleRulePreview(PatchRecipe recipe, string ruleId, string rowKey)
    {
        var copy = PatchRecipeStore.Clone(recipe);
        copy.OnlyRowKey = rowKey;
        foreach (var rule in copy.Rules)
        {
            rule.Enabled = rule.Id == ruleId;
        }

        return copy;
    }
}

/// <summary>Filter der Tabelle: Text in Schlüssel oder Werten (ohne Groß-/Kleinschreibung), optional nur Zeilen mit Fehlern.</summary>
public static class TableFilter
{
    public static bool Matches(ResolvedRow row, string filterText, bool onlyErrors)
    {
        if (onlyErrors && !row.HasErrors)
        {
            return false;
        }

        var filter = filterText.Trim();
        return filter.Length == 0
            || row.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Values.Values.Any(v => v.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
