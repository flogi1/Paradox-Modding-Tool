using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators;

public enum IssueSeverity
{
    Error,
    Warning,
}

/// <summary>Ein Befund der Formularprüfung; <see cref="Path"/> wie <c>categories[1].ideas[2].name</c> (Indizes ab 1).</summary>
public sealed record FormIssue(IssueSeverity Severity, string Message, string? Path);

/// <summary>
/// Prüft ein Formular: Pflichtfelder, Muster, Anzahl je Ebene und Paket-Fehler sind Fehler; Paket-Warnungen (nur ohne Fehler) fragen vor dem
/// Erzeugen nach.
/// </summary>
public static class FormValidator
{
    public static IReadOnlyList<FormIssue> Check(GeneratorPackage package, GeneratorState state, GeneratorRenderer renderer)
    {
        var issues = new List<FormIssue>();
        CheckFields(package.Head, state.Head, null, issues);
        if (package.Root is { } root)
        {
            CheckLevel(root, state.Head.Children.GetValueOrDefault(root.Id) ?? new List<FormNode>(), root.Id, issues);
        }

        var model = TemplateModel.Build(package, state, null);
        var fieldErrors = issues.Count;
        issues.AddRange(package.Errors.Where(e => renderer.Evaluate(e.When, model)).Select(e => new FormIssue(IssueSeverity.Error, e.Message, null)));
        if (fieldErrors == 0 && issues.Count == 0)
        {
            issues.AddRange(package.Warnings.Where(w => renderer.Evaluate(w.When, model)).Select(w => new FormIssue(IssueSeverity.Warning, w.Message, null)));
        }

        return issues;
    }

    private static void CheckLevel(LevelDef level, List<FormNode> nodes, string path, List<FormIssue> issues)
    {
        if (level.ConstantsTable is null && (nodes.Count < level.Min || nodes.Count > level.Max))
        {
            issues.Add(new FormIssue(IssueSeverity.Error, GeneratorsTexts.CountOutOfRange(level.Label, level.Min, level.Max), path));
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var itemPath = $"{path}[{i + 1}]";
            CheckFields(level.Fields, nodes[i], itemPath, issues);
            if (level.Child is { } child)
            {
                CheckLevel(child, nodes[i].Children.GetValueOrDefault(child.Id) ?? new List<FormNode>(), $"{itemPath}.{child.Id}", issues);
            }
        }
    }

    private static void CheckFields(IReadOnlyList<FieldDef> fields, FormNode node, string? path, List<FormIssue> issues)
    {
        foreach (var field in fields)
        {
            var value = node.Values.GetValueOrDefault(field.Id) ?? string.Empty;
            var fieldPath = path is null ? field.Id : $"{path}.{field.Id}";
            if (field.Required && value.Trim().Length == 0)
            {
                issues.Add(new FormIssue(IssueSeverity.Error, GeneratorsTexts.FieldRequired(field.Label), fieldPath));
            }
            else if (field.Pattern is { } pattern && value.Length > 0 && !Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
            {
                issues.Add(new FormIssue(IssueSeverity.Error, GeneratorsTexts.FieldPattern(field.Label, pattern), fieldPath));
            }

            if (field.Type == FieldType.Modifiers)
            {
                issues.AddRange(ModifiersWithoutValue(value).Select(key => new FormIssue(IssueSeverity.Error, GeneratorsTexts.ModifierWithoutValue(field.Label, key), fieldPath)));
            }
        }
    }

    /// <summary>Die Schlüssel der Zeilen <c>schlüssel =</c> ohne Wert in einem Modifikatorfeld; Kommentare und Leerzeilen zählen nicht.</summary>
    private static IEnumerable<string> ModifiersWithoutValue(string text)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var comment = line.IndexOf('#');
            var content = (comment >= 0 ? line[..comment] : line).Trim();
            if (content.EndsWith('=') && content[..^1].Trim() is { Length: > 0 } key)
            {
                yield return key;
            }
        }
    }
}
