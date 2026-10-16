namespace ParadoxModTool.Patches;

internal enum RuleFieldKind
{
    /// <summary>Text, Schlüssel, Muster für Zeilen usw.</summary>
    Text,

    /// <summary>Dateimuster, aus dem die Regel liest (<c>files</c>, <c>sourceFiles</c>); mehrere Teile mit <c>;</c>.</summary>
    Glob,

    /// <summary>Die eine Mod-Datei, in die die Regel schreibt (<c>target</c>).</summary>
    Target,
}

/// <param name="Name">Name wie im Rezept-JSON, Bearbeitungen als <c>edits[i].key</c>.</param>
internal sealed record RuleField(string Name, string? Value, RuleFieldKind Kind, bool Required)
{
    public bool IsPath => Kind != RuleFieldKind.Text;
}

/// <summary>Die Felder jeder Regelart an einer Stelle: Grundlage für Pflichtfelder, Pfadprüfung und die Suche nach <c># PMT-</c>-Zeilen.</summary>
internal static class RuleFields
{
    /// <summary>Alle Felder der Regel in Rezept-Reihenfolge, samt den Feldern ihrer Bearbeitungen.</summary>
    public static IReadOnlyList<RuleField> Of(PatchRule rule)
    {
        var fields = new List<RuleField>();
        switch (rule)
        {
            case LineRule line:
                fields.Add(new("files", line.Files, RuleFieldKind.Glob, Required: true));
                fields.Add(new("match", line.Match, RuleFieldKind.Text, Required: true));
                fields.Add(new("replacement", line.Replacement, RuleFieldKind.Text, Required: false));
                break;
            case BlockCopyRule copy:
                fields.Add(new("sourceFiles", copy.SourceFiles, RuleFieldKind.Glob, Required: true));
                fields.Add(new("blockKey", copy.BlockKey, RuleFieldKind.Text, Required: true));
                fields.Add(new("target", copy.Target, RuleFieldKind.Target, Required: true));
                AddEdits(fields, copy.Edits);
                break;
            case TemplateRule template:
                fields.Add(new("target", template.Target, RuleFieldKind.Target, Required: true));
                fields.Add(new("inside", template.Inside, RuleFieldKind.Text, Required: false));
                fields.Add(new("text", template.Text, RuleFieldKind.Text, Required: true));
                break;
            case ListEntryRule list:
                fields.Add(new("target", list.Target, RuleFieldKind.Target, Required: true));
                fields.Add(new("listPath", list.ListPath, RuleFieldKind.Text, Required: true));
                fields.Add(new("entry", list.Entry, RuleFieldKind.Text, Required: true));
                break;
            case BlockEditRule edit:
                fields.Add(new("files", edit.Files, RuleFieldKind.Glob, Required: true));
                fields.Add(new("blockKey", edit.BlockKey, RuleFieldKind.Text, Required: true));
                AddEdits(fields, edit.Edits);
                break;
        }

        return fields;
    }

    private static void AddEdits(List<RuleField> fields, List<BlockEdit>? edits)
    {
        for (var i = 0; i < (edits?.Count ?? 0); i++)
        {
            if (edits![i] is not { } edit)
            {
                continue;
            }

            fields.Add(new($"edits[{i}].key", edit.Key, RuleFieldKind.Text, Required: false));
            fields.Add(new($"edits[{i}].value", edit.Value, RuleFieldKind.Text, Required: false));
            fields.Add(new($"edits[{i}].text", edit.Text, RuleFieldKind.Text, Required: false));
        }
    }
}
