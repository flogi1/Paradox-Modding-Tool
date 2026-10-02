using System.Text;

namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Liest den Datentyp-Dump des Spiels (Konsolenbefehl <c>DumpDataTypes</c>, Ordner <c>logs/data_types</c>). Einträge sind Blöcke aus Kopfzeile
/// (<c>Country.GetCapital</c>, <c>Not( Arg0 )</c>) und den Feldern <c>Description:</c>, <c>Definition type:</c>, <c>Return type:</c>, getrennt durch
/// Zeilen aus Bindestrichen.
/// </summary>
public static class DataTypeLogParser
{
    public const string FilePattern = "data_types_*.txt";

    public static IReadOnlyList<DataTypeEntry> ParseFolder(string folder)
    {
        var result = new List<DataTypeEntry>();
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (var path in Directory.GetFiles(folder, FilePattern).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                result.AddRange(Parse(File.ReadAllText(path, Encoding.UTF8), Path.GetFileName(path)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nicht lesbare Datei: übersprungen (der Aufrufer meldet sie über DataTypeSource).
            }
        }

        return result;
    }

    public static IReadOnlyList<DataTypeEntry> Parse(string text, string source)
    {
        var result = new List<DataTypeEntry>();
        var block = new List<string>();

        void Flush()
        {
            if (Build(block, source) is { } entry)
            {
                result.Add(entry);
            }

            block.Clear();
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length >= 5 && line.All(c => c == '-'))
            {
                Flush();
            }
            else if (line.Length > 0)
            {
                block.Add(line);
            }
        }

        Flush();
        return result;
    }

    private static DataTypeEntry? Build(List<string> block, string source)
    {
        if (block.Count == 0)
        {
            return null;
        }

        string description = string.Empty, definition = string.Empty, returnType = string.Empty;
        foreach (var line in block.Skip(1))
        {
            if (TryField(line, "Description:", out var value))
            {
                description = value;
            }
            else if (TryField(line, "Definition type:", out value))
            {
                definition = value;
            }
            else if (TryField(line, "Return type:", out value))
            {
                returnType = value;
            }
        }

        DataTypeKind? kind = definition switch
        {
            "Type" => DataTypeKind.Type,
            "Promote" => DataTypeKind.Promote,
            "Function" => DataTypeKind.Function,
            "Global promote" => DataTypeKind.GlobalPromote,
            "Global function" => DataTypeKind.GlobalFunction,
            "Global macro" => DataTypeKind.GlobalMacro,
            _ => null,
        };
        if (kind is null)
        {
            return null;
        }

        var head = block[0];
        var arguments = Array.Empty<string>();
        var open = head.IndexOf('(');
        if (open >= 0)
        {
            var close = head.LastIndexOf(')');
            var inner = close > open ? head[(open + 1)..close] : head[(open + 1)..];
            arguments = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            head = head[..open].Trim();
        }

        var owner = string.Empty;
        var name = head;
        if (kind is DataTypeKind.Promote or DataTypeKind.Function)
        {
            var dot = head.LastIndexOf('.');
            if (dot > 0)
            {
                owner = head[..dot];
                name = head[(dot + 1)..];
            }
        }

        return new DataTypeEntry(name, owner, kind.Value, arguments, returnType, description, source);
    }

    private static bool TryField(string line, string label, out string value)
    {
        if (line.StartsWith(label, StringComparison.Ordinal))
        {
            value = line[label.Length..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
