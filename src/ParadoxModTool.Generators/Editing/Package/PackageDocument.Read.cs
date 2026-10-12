using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Sicht auf den Abschnitt <c>read</c> (Spec Teil 5b, Abschnitt 1). Muster und Pfade bleiben auch leer im JSON (Pflichtangaben); unbekannte Schlüssel
/// bleiben erhalten.
/// </summary>
public sealed class ReadNode
{
    private readonly PackageDocument _document;

    internal ReadNode(PackageDocument document, JsonObject raw)
    {
        _document = document;
        Raw = raw;
    }

    public JsonObject Raw { get; }

    /// <summary>Regex auf die Pfade der Mod; bei Schlüsseln aus Blöcken der Teil <c>file</c> von <c>keys</c>.</summary>
    public string Keys
    {
        get => Raw["keys"] is JsonObject keys ? PackageDocument.Str(keys, "file") ?? string.Empty : PackageDocument.Str(Raw, "keys") ?? string.Empty;
        set => WriteKeys(value, KeysBlock);
    }

    /// <summary>Regex auf oberste Blockschlüssel (Spec 2026-10-06, Abschnitt 4); leer = Schlüssel aus dem Pfad, dann ist <c>keys</c> wieder ein Text.</summary>
    public string KeysBlock
    {
        get => Raw["keys"] is JsonObject keys ? PackageDocument.Str(keys, "block") ?? string.Empty : string.Empty;
        set => WriteKeys(Keys, value);
    }

    /// <summary>Zeilen <c>file</c>, <c>block</c>, <c>pattern</c>, <c>field</c>, <c>join</c> (gesammelte Werte).</summary>
    public ReadRowList Values => new(_document, Raw, "values", new[] { "file", "block", "pattern", "body", "field", "join" });

    private void WriteKeys(string file, string block) =>
        _document.SetNode(Raw, "keys", string.IsNullOrEmpty(block) ? JsonValue.Create(file) : new JsonObject { ["file"] = file, ["block"] = block });

    /// <summary>Kennung der Lokalisierungs-Ausgabe, aus der gelesen wird; leer = die erste (Schlüssel fällt weg).</summary>
    public string LocalizationOutput
    {
        get => PackageDocument.Str(Raw, "localizationOutput") ?? string.Empty;
        set => _document.SetNode(Raw, "localizationOutput", string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value.Trim()));
    }

    public string ElementsFile
    {
        get => PackageDocument.Str(Elements, "file") ?? string.Empty;
        set => _document.SetNode(Elements, "file", JsonValue.Create(value));
    }

    public string ElementsBlock
    {
        get => PackageDocument.Str(Elements, "block") ?? string.Empty;
        set => _document.SetNode(Elements, "block", JsonValue.Create(value));
    }

    public string ElementsLevel
    {
        get => PackageDocument.Str(Elements, "level") ?? string.Empty;
        set => _document.SetNode(Elements, "level", JsonValue.Create(value));
    }

    /// <summary>Felder der Elemente-Ebene, die den Blockinhalt übernehmen (<c>body</c>), mit ausgenommenen Schlüsseln.</summary>
    public IReadOnlyDictionary<string, (bool Body, IReadOnlyList<string> Except)> ElementFields =>
        Elements["fields"] is JsonObject fields
            ? fields.Where(p => p.Value is JsonObject).ToDictionary(
                p => p.Key,
                p => (PackageDocument.Bool((JsonObject)p.Value!, "body"),
                    (IReadOnlyList<string>)(p.Value!["except"] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : new List<string>())),
                StringComparer.Ordinal)
            : new Dictionary<string, (bool, IReadOnlyList<string>)>(StringComparer.Ordinal);

    /// <summary>Setzt die Angabe eines Felds; ohne Blockinhalt und ohne Ausnahmen fällt der Eintrag weg.</summary>
    public void SetElementField(string field, bool body, IReadOnlyList<string> except)
    {
        var fields = Elements["fields"] as JsonObject;
        if (!body && except.Count == 0)
        {
            fields?.Remove(field);
            if (fields is { Count: 0 })
            {
                Elements.Remove("fields");
            }

            _document.Touch();
            return;
        }

        if (fields is null)
        {
            fields = new JsonObject();
            Elements["fields"] = fields;
        }

        if (fields[field] is not JsonObject entry)
        {
            entry = new JsonObject();
            fields[field] = entry;
        }

        entry["body"] = body;
        if (except.Count > 0)
        {
            entry["except"] = new JsonArray(except.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray());
        }
        else
        {
            entry.Remove("except");
        }

        _document.Touch();
    }

    /// <summary>Benennt einen Eintrag in <c>elements.fields</c> um; die Reihenfolge bleibt.</summary>
    internal void RenameElementField(string old, string newId)
    {
        if (Elements["fields"] is not JsonObject fields || !fields.ContainsKey(old))
        {
            return;
        }

        var renamed = new JsonObject();
        foreach (var (key, value) in fields.ToList())
        {
            fields.Remove(key);
            renamed[key == old ? newId : key] = value;
        }

        Elements["fields"] = renamed;
        _document.Touch();
    }

    /// <summary>Zeilen <c>key</c>, <c>field</c>.</summary>
    public ReadRowList Localization => new(_document, Raw, "localization", new[] { "key", "field" });

    /// <summary>Zeilen <c>file</c>, <c>block</c>, <c>contains</c>, <c>field</c>.</summary>
    public ReadRowList Flags => new(_document, Raw, "flags", new[] { "file", "block", "contains", "pattern", "field" });

    /// <summary>Zeilen <c>field</c>, <c>file</c>.</summary>
    public ReadRowList Assets => new(_document, Raw, "assets", new[] { "field", "file" });

    private JsonObject Elements
    {
        get
        {
            if (Raw["elements"] is not JsonObject elements)
            {
                elements = new JsonObject();
                Raw["elements"] = elements;
            }

            return elements;
        }
    }
}

/// <summary>Liste gleich gebauter Zeilen in <c>read</c> (Lokalisierung, Merkmale, Bilder); die letzte entfernte Zeile entfernt die Liste.</summary>
public sealed class ReadRowList
{
    private readonly PackageDocument _document;
    private readonly JsonObject _owner;
    private readonly string _key;
    private readonly IReadOnlyList<string> _columns;

    internal ReadRowList(PackageDocument document, JsonObject owner, string key, IReadOnlyList<string> columns)
    {
        _document = document;
        _owner = owner;
        _key = key;
        _columns = columns;
    }

    public IReadOnlyList<ReadRow> Items =>
        _owner[_key] is JsonArray array ? array.OfType<JsonObject>().Select(o => new ReadRow(_document, o)).ToList() : Array.Empty<ReadRow>();

    /// <summary>Neue Zeile am Ende, alle Spalten leer.</summary>
    public ReadRow Add()
    {
        if (_owner[_key] is not JsonArray array)
        {
            array = new JsonArray();
            _owner[_key] = array;
        }

        var row = new JsonObject();
        foreach (var column in _columns)
        {
            row[column] = string.Empty;
        }

        array.Add(row);
        _document.Touch();
        return new ReadRow(_document, row);
    }

    public void Remove(int index)
    {
        if (_owner[_key] is not JsonArray array || index < 0 || index >= array.Count)
        {
            return;
        }

        array.RemoveAt(index);
        if (array.Count == 0)
        {
            _owner.Remove(_key);
        }

        _document.Touch();
    }

    public void Move(int index, int delta)
    {
        if (_owner[_key] is not JsonArray array || index < 0 || index >= array.Count || index + delta < 0 || index + delta >= array.Count)
        {
            return;
        }

        var node = array[index];
        array.RemoveAt(index);
        array.Insert(index + delta, node);
        _document.Touch();
    }
}

/// <summary>Eine Zeile in <c>read</c>; leere Werte bleiben als <c>""</c> stehen.</summary>
public sealed class ReadRow
{
    private readonly PackageDocument _document;

    internal ReadRow(PackageDocument document, JsonObject raw)
    {
        _document = document;
        Raw = raw;
    }

    public JsonObject Raw { get; }

    public string Get(string column) => PackageDocument.Str(Raw, column) ?? string.Empty;

    public void Set(string column, string value) => _document.SetNode(Raw, column, JsonValue.Create(value));

    /// <summary>Optionale Spalte (z. B. <c>output</c>) ganz entfernen statt sie leer stehen zu lassen.</summary>
    public void Remove(string column) => _document.SetNode(Raw, column, null);
}

public sealed partial class PackageDocument
{
    public ReadNode? Read => Json["read"] is JsonObject read ? new ReadNode(this, read) : null;

    /// <summary>Legt einen leeren Abschnitt <c>read</c> an (Elemente auf der tiefsten Ebene); ein vorhandener bleibt.</summary>
    public ReadNode EnableRead()
    {
        if (Read is { } existing)
        {
            return existing;
        }

        var read = new JsonObject
        {
            ["keys"] = string.Empty,
            ["elements"] = new JsonObject
            {
                ["file"] = string.Empty,
                ["block"] = string.Empty,
                ["level"] = Levels.LastOrDefault()?.Id ?? string.Empty,
            },
        };
        Json["read"] = read;
        Touch();
        return new ReadNode(this, read);
    }

    public void DisableRead()
    {
        Json.Remove("read");
        Touch();
    }

    /// <summary>
    /// Benennt ein Feld um und zieht die Verweise mit (Spec Teil 5b, Abschnitt 1 "References"): Schlüsselfeld, <c>read</c> (Elemente-Felder,
    /// <c>Ebene.Feld</c> in Lokalisierung und Merkmalen, Bildfelder des Kopfs) und <c>fills</c> der Felder derselben Liste.
    /// </summary>
    public void RenameField(FieldList list, FieldNode field, string newId)
    {
        var old = field.Id;
        field.Id = newId;
        var level = list.LevelId;
        if (level is null && KeyFields.Contains(old))
        {
            KeyFields = KeyFields.Select(f => f == old ? newId : f).ToList();
        }

        if (Read is { } read)
        {
            if (level is not null && read.ElementsLevel == level)
            {
                read.RenameElementField(old, newId);
            }

            foreach (var row in read.Localization.Items.Concat(read.Flags.Items).Where(r => level is not null && r.Get("field") == level + "." + old))
            {
                row.Set("field", level + "." + newId);
            }

            foreach (var row in read.Assets.Items.Where(r => level is null && r.Get("field") == old))
            {
                row.Set("field", newId);
            }
        }

        foreach (var lookup in list.Items.Select(f => f.Lookup).OfType<FieldLookup>())
        {
            foreach (var entry in new[] { "name", "desc" }.Where(e => lookup.Fill(e) == old))
            {
                lookup.SetFill(entry, newId);
            }
        }
    }
}
