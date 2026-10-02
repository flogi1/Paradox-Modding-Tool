using System.Text;
using System.Text.Json;

namespace ParadoxModTool.Mods;

/// <summary>
/// Liest und ändert das Feld <c>version</c> einer Metadaten-Datei, ohne den übrigen Text anzufassen (Tabs, Kommentare, Reihenfolge bleiben). Gearbeitet wird direkt an den
/// Byte-Positionen des JSON-Lesers, nicht über ein erneutes Serialisieren. Nur das <c>version</c> auf oberster Ebene zählt; ein <c>version</c> in <c>relationships</c> bleibt unberührt.
/// Kommentare und ein Komma am Ende sind wie im Spiel erlaubt.
/// </summary>
public static class ModMetadataEditor
{
    private static readonly JsonReaderOptions ReaderOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private sealed record Scan(int OpenBrace, int CloseBrace, int FirstPropertyStart, bool ValueFound, int ValueStart, int ValueLength, string? ValueText);

    /// <summary>Prüft, ob der Text ein lesbares JSON-Objekt ist. Gibt eine Fehlermeldung mit Zeile und Spalte zurück, sonst <see langword="null"/>.</summary>
    public static string? Validate(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, DocumentOptions);
            return document.RootElement.ValueKind == JsonValueKind.Object ? null : ModsTexts.MetadataNotObject;
        }
        catch (JsonException ex)
        {
            return ex.LineNumber is long line && ex.BytePositionInLine is long column
                ? ModsTexts.MetadataInvalidJsonAt(line + 1, column + 1)
                : ModsTexts.MetadataInvalidJson;
        }
    }

    /// <summary>Der Wert von <c>version</c> auf oberster Ebene (Text oder Zahl als Text); <see langword="null"/>, wenn das Feld fehlt oder der Text kein JSON-Objekt ist.</summary>
    public static string? ReadVersion(string text)
    {
        try
        {
            return ScanRoot(Encoding.UTF8.GetBytes(text)).ValueText;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Setzt <c>version</c> auf oberster Ebene. Fehlt das Feld, wird es direkt nach der öffnenden Klammer eingefügt. Wirft <see cref="ArgumentException"/> bei leerer Version und
    /// <see cref="InvalidOperationException"/>, wenn der Text kein JSON-Objekt ist.
    /// </summary>
    public static string SetVersion(string text, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(ModsTexts.MetadataEmptyVersion, nameof(version));
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var scan = ScanRoot(bytes);
        var quoted = "\"" + JsonEncodedText.Encode(version.Trim()).ToString() + "\"";

        if (scan.ValueFound)
        {
            return Splice(bytes, scan.ValueStart, scan.ValueLength, quoted);
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (scan.FirstPropertyStart < 0)
        {
            return Splice(bytes, scan.OpenBrace, scan.CloseBrace + 1 - scan.OpenBrace, "{" + newline + "\t\"version\": " + quoted + newline + "}");
        }

        var multiLine = bytes.AsSpan(scan.OpenBrace + 1, scan.FirstPropertyStart - scan.OpenBrace - 1).Contains((byte)'\n');
        var insertion = multiLine
            ? newline + LineIndent(bytes, scan.FirstPropertyStart) + "\"version\": " + quoted + ","
            : "\"version\": " + quoted + ",";
        return Splice(bytes, scan.OpenBrace + 1, 0, insertion);
    }

    /// <summary>Zählt den letzten Teil einer Versionsnummer um eins hoch (<c>2.0.5</c> wird <c>2.0.6</c>). Eine ungültige Nummer bleibt unverändert.</summary>
    public static string NextPatch(string version)
    {
        if (!VersionNumber.TryParse(version, out var number))
        {
            return version;
        }

        var parts = number.Text.Split('.');
        parts[^1] = (int.Parse(parts[^1]) + 1).ToString();
        return string.Join(".", parts);
    }

    private static Scan ScanRoot(byte[] bytes)
    {
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        int depth = 0, open = -1, close = -1, firstProperty = -1, valueStart = -1, valueLength = 0;
        string? valueText = null;
        var found = false;

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        depth++;
                        if (depth == 1 && open < 0)
                        {
                            open = (int)reader.TokenStartIndex;
                        }

                        break;
                    case JsonTokenType.StartArray:
                        depth++;
                        break;
                    case JsonTokenType.EndArray:
                        depth--;
                        break;
                    case JsonTokenType.EndObject:
                        depth--;
                        if (depth == 0 && close < 0)
                        {
                            close = (int)reader.TokenStartIndex;
                        }

                        break;
                    case JsonTokenType.PropertyName when depth == 1:
                        if (firstProperty < 0)
                        {
                            firstProperty = (int)reader.TokenStartIndex;
                        }

                        if (!found && string.Equals(reader.GetString(), "version", StringComparison.OrdinalIgnoreCase))
                        {
                            reader.Read();
                            valueStart = (int)reader.TokenStartIndex;
                            switch (reader.TokenType)
                            {
                                case JsonTokenType.String:
                                    valueLength = reader.ValueSpan.Length + 2;
                                    valueText = reader.GetString();
                                    found = true;
                                    break;
                                case JsonTokenType.Number:
                                    valueLength = reader.ValueSpan.Length;
                                    valueText = Encoding.UTF8.GetString(reader.ValueSpan);
                                    found = true;
                                    break;
                                default:
                                    throw new InvalidOperationException(ModsTexts.MetadataVersionType);
                            }
                        }

                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(ModsTexts.MetadataFileInvalidJson, ex);
        }

        if (open < 0)
        {
            throw new InvalidOperationException(ModsTexts.MetadataFileNotObject);
        }

        return new Scan(open, close, firstProperty, found, valueStart, valueLength, valueText);
    }

    private static string Splice(byte[] bytes, int start, int length, string replacement)
    {
        var insert = Encoding.UTF8.GetBytes(replacement);
        var result = new byte[bytes.Length - length + insert.Length];
        Buffer.BlockCopy(bytes, 0, result, 0, start);
        Buffer.BlockCopy(insert, 0, result, start, insert.Length);
        Buffer.BlockCopy(bytes, start + length, result, start + insert.Length, bytes.Length - start - length);
        return Encoding.UTF8.GetString(result);
    }

    /// <summary>Die Leerzeichen und Tabs am Anfang der Zeile, in der <paramref name="position"/> steht (leer, wenn davor anderer Text steht).</summary>
    private static string LineIndent(byte[] bytes, int position)
    {
        var lineStart = position;
        while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        var indent = Encoding.UTF8.GetString(bytes, lineStart, position - lineStart);
        return indent.All(c => c is ' ' or '\t') ? indent : string.Empty;
    }
}
