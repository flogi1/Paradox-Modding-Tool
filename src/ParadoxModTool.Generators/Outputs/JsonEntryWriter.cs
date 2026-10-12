using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators;

/// <summary>
/// Setzt einen Eintrag in einer JSON-Datei und schreibt sie im Stil von Pythons <c>json.dumps(ensure_ascii=False, indent=N)</c> zurück, damit der Rest
/// der Datei (etwa die KI-Tag-Datei des Tag-Editors) bytegleich bleibt: Trenner <c>,</c> + Zeilenumbruch und <c>": "</c>, <c>[]</c>/<c>{}</c> für leere
/// Behälter, Nicht-ASCII-Zeichen roh. Die Einrückung <c>N</c> ist die der ersten eingerückten Zeile (sonst 1), das Zeilenende das der Datei.
/// </summary>
public static class JsonEntryWriter
{
    /// <summary>
    /// Ersetzt den Wert von <paramref name="key"/> im Objekt unter <paramref name="jsonPath"/> (Punkt-Pfad aus Objekten, leer = Wurzel) oder hängt den
    /// Schlüssel am Ende an. <paramref name="valueJson"/> ist der neue Wert als JSON. Ungültiges JSON ist ein <see cref="GeneratorOutputException"/>.
    /// </summary>
    public static string Set(string json, string jsonPath, string key, string valueJson)
    {
        var root = Parse(json, GeneratorsTexts.JsonEntryFileInvalid);
        var value = Parse(valueJson, GeneratorsTexts.JsonEntryValueInvalid);
        var target = root;
        foreach (var segment in jsonPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            target = (target as JsonObject)?[segment] ?? throw new GeneratorOutputException(GeneratorsTexts.JsonEntryPathMissing(jsonPath));
        }

        if (target is not JsonObject entries)
        {
            throw new GeneratorOutputException(GeneratorsTexts.JsonEntryPathMissing(jsonPath));
        }

        entries[key] = value;

        var newLine = json.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var builder = new StringBuilder();
        new Writer(builder, IndentOf(json), newLine).Write(root, 0);
        if (json.EndsWith('\n'))
        {
            builder.Append(newLine);
        }

        return builder.ToString();
    }

    private static JsonNode? Parse(string json, Func<string, string> error)
    {
        try
        {
            var node = JsonNode.Parse(json);
            // JsonNode baut die Eigenschaften von Objekten erst beim ersten Zugriff auf; doppelte Namen fielen sonst später als ArgumentException auf
            Touch(node);
            return node;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new GeneratorOutputException(error(ex.Message));
        }
    }

    private static void Touch(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    Touch(property.Value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Touch(item);
                }

                break;
        }
    }

    /// <summary>Führende Leerzeichen der ersten eingerückten Zeile; ohne eine solche 1 (Vorgabe des Tag-Editors).</summary>
    private static int IndentOf(string json)
    {
        foreach (var line in json.Split('\n'))
        {
            var content = line.TrimEnd('\r');
            var spaces = content.Length - content.TrimStart(' ').Length;
            if (spaces > 0 && spaces < content.Length)
            {
                return spaces;
            }
        }

        return 1;
    }

    private sealed class Writer(StringBuilder sb, int indent, string newLine)
    {
        public void Write(JsonNode? node, int depth)
        {
            switch (node)
            {
                case JsonObject obj:
                    WriteContainer(obj.Count, depth, '{', '}', obj.Select(p => ((string?)p.Key, p.Value)));
                    break;
                case JsonArray array:
                    WriteContainer(array.Count, depth, '[', ']', array.Select(v => ((string?)null, v)));
                    break;
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    WriteString(value.GetValue<string>());
                    break;
                case JsonValue value:
                    // Zahlen, true und false stehen so da, wie sie gelesen wurden
                    sb.Append(value.ToJsonString());
                    break;
                default:
                    sb.Append("null");
                    break;
            }
        }

        private void WriteContainer(int count, int depth, char open, char close, IEnumerable<(string? Key, JsonNode? Value)> items)
        {
            if (count == 0)
            {
                sb.Append(open).Append(close);
                return;
            }

            sb.Append(open);
            var first = true;
            foreach (var (key, value) in items)
            {
                sb.Append(first ? string.Empty : ",").Append(newLine).Append(' ', indent * (depth + 1));
                first = false;
                if (key is not null)
                {
                    WriteString(key);
                    sb.Append(": ");
                }

                Write(value, depth + 1);
            }

            sb.Append(newLine).Append(' ', indent * depth).Append(close);
        }

        /// <summary>Wie Python: <c>"</c>, <c>\</c> und die Steuerzeichen werden maskiert, alles andere (auch Nicht-ASCII) bleibt roh.</summary>
        private void WriteString(string text)
        {
            sb.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case < ' ': sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); break;
                    default: sb.Append(c); break;
                }
            }

            sb.Append('"');
        }
    }
}
