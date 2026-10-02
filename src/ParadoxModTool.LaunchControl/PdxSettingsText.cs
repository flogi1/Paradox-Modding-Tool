using System.Text;
using System.Text.Json;

namespace ParadoxModTool.LaunchControl;

public enum PdxSettingsProblem
{
    None,
    Missing,
    UnknownFormat,
    Locked,
}

/// <summary>Eingetragener Editor (<c>System.editor</c>); <c>Editor</c> null = keiner eingetragen.</summary>
public sealed record PdxEditorRead(PdxSettingsProblem Problem, string? Editor);

/// <summary>
/// <c>System.editor</c> in <c>pdx_settings.json</c> lesen und minimal ändern (Plan 4 Ruling 5): Nur der Wert wird ersetzt, eingefügt oder
/// entfernt, alles andere bleibt byte-gleich. Escaping nur für <c>\</c>, <c>"</c> und Steuerzeichen.
/// </summary>
public static class PdxSettingsText
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private static readonly JsonReaderOptions ReaderOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static PdxEditorRead ReadEditor(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, DocumentOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new PdxEditorRead(PdxSettingsProblem.UnknownFormat, null);
            }

            if (!root.TryGetProperty("System", out var system))
            {
                return new PdxEditorRead(PdxSettingsProblem.None, null);
            }

            if (system.ValueKind != JsonValueKind.Object)
            {
                return new PdxEditorRead(PdxSettingsProblem.UnknownFormat, null);
            }

            if (!system.TryGetProperty("editor", out var editor))
            {
                return new PdxEditorRead(PdxSettingsProblem.None, null);
            }

            return editor.ValueKind == JsonValueKind.String
                ? new PdxEditorRead(PdxSettingsProblem.None, editor.GetString())
                : new PdxEditorRead(PdxSettingsProblem.UnknownFormat, null);
        }
        catch (JsonException)
        {
            return new PdxEditorRead(PdxSettingsProblem.UnknownFormat, null);
        }
    }

    /// <summary>
    /// Text mit <c>System.editor</c> = <paramref name="value"/>; null entfernt den Eintrag (ein leerer System-Block bleibt stehen). Neue Einträge im
    /// Stil der Datei (Tab-Einrückung). Null bei unbekanntem Format.
    /// </summary>
    public static string? WithEditor(string text, string? value)
    {
        if (ReadEditor(text).Problem != PdxSettingsProblem.None)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        string? rootProperty = null;
        int rootStart = -1, rootEnd = -1, lastRootValueEnd = -1, systemStart = -1, systemEnd = -1;
        int editorNameStart = -1, editorValueStart = -1, editorValueEnd = -1;
        while (reader.Read())
        {
            var depth = reader.CurrentDepth;
            var start = (int)reader.TokenStartIndex;
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject when depth == 0:
                    rootStart = start;
                    break;
                case JsonTokenType.EndObject when depth == 0:
                    rootEnd = start;
                    break;
                case JsonTokenType.PropertyName when depth == 1:
                    rootProperty = reader.GetString();
                    break;
                case JsonTokenType.StartObject when depth == 1 && rootProperty == "System" && systemStart < 0:
                    systemStart = start;
                    break;
                case JsonTokenType.PropertyName when depth == 2 && systemStart >= 0 && systemEnd < 0 && editorNameStart < 0 && reader.GetString() == "editor":
                    editorNameStart = start;
                    reader.Read();
                    editorValueStart = (int)reader.TokenStartIndex;
                    editorValueEnd = (int)reader.BytesConsumed;
                    break;
            }

            // Ende eines Werts auf oberster Ebene (für ein neues System am Ende) und Ende des System-Blocks.
            if (depth == 1 && reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray or JsonTokenType.String or JsonTokenType.Number
                    or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null)
            {
                lastRootValueEnd = (int)reader.BytesConsumed;
                if (reader.TokenType == JsonTokenType.EndObject && rootProperty == "System" && systemStart >= 0 && systemEnd < 0)
                {
                    systemEnd = start;
                }
            }
        }

        var result = new List<byte>(bytes);
        if (editorNameStart >= 0)
        {
            if (value is not null)
            {
                Replace(result, editorValueStart, editorValueEnd, Quote(value));
                return Encoding.UTF8.GetString(result.ToArray());
            }

            var previous = PreviousNonSpace(bytes, editorNameStart - 1);
            var next = NextNonSpace(bytes, editorValueEnd);
            if (bytes[previous] == (byte)',')
            {
                Replace(result, previous, editorValueEnd, string.Empty);
            }
            else if (bytes[next] == (byte)',')
            {
                Replace(result, previous + 1, next + 1, string.Empty);
            }
            else
            {
                Replace(result, previous + 1, editorValueEnd, string.Empty);
            }

            return Encoding.UTF8.GetString(result.ToArray());
        }

        if (value is null)
        {
            return text;
        }

        var entry = "\n\t\t\"editor\":\t" + Quote(value);
        if (systemStart >= 0)
        {
            var hasMembers = bytes[NextNonSpace(bytes, systemStart + 1)] != (byte)'}';
            Replace(result, systemStart + 1, systemStart + 1, hasMembers ? entry + "," : entry);
            return Encoding.UTF8.GetString(result.ToArray());
        }

        var block = "\t\"System\":\t{" + entry + "\n}";
        if (lastRootValueEnd >= 0)
        {
            Replace(result, lastRootValueEnd, lastRootValueEnd, ",\n" + block);
        }
        else
        {
            Replace(result, rootStart + 1, rootEnd, "\n" + block + "\n");
        }

        return Encoding.UTF8.GetString(result.ToArray());
    }

    /// <summary>JSON-Zeichenkette mit Escaping nur für <c>\</c>, <c>"</c> und Steuerzeichen (kein <c>\u002B</c> für "+").</summary>
    internal static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    private static void Replace(List<byte> bytes, int start, int end, string text)
    {
        bytes.RemoveRange(start, end - start);
        bytes.InsertRange(start, Encoding.UTF8.GetBytes(text));
    }

    private static int PreviousNonSpace(byte[] bytes, int index)
    {
        while (index > 0 && bytes[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index--;
        }

        return index;
    }

    private static int NextNonSpace(byte[] bytes, int index)
    {
        while (index < bytes.Length - 1 && bytes[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }

        return index;
    }
}
