using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Lookups;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Sicht auf eine Nachschlage-Quelle in <c>lookups</c> (Spec Teil 5b, Abschnitt 2). Beim Artwechsel fallen die Angaben der alten Art weg.</summary>
public sealed class LookupSourceNode
{
    private static readonly string[] GameKeysKeys = { "folder", "depth", "namedOnly", "tagFromFile", "tagInBlock" };
    private static readonly string[] JsonKeys = { "asset", "items", "fields" };

    private readonly PackageDocument _document;

    internal LookupSourceNode(PackageDocument document, JsonObject raw)
    {
        _document = document;
        Raw = raw;
    }

    public JsonObject Raw { get; }

    public string Id
    {
        get => PackageDocument.Str(Raw, "id") ?? string.Empty;
        internal set => _document.SetStr(Raw, "id", value);
    }

    public LookupKind Kind
    {
        get => Enum.TryParse<LookupKind>(PackageDocument.Str(Raw, "kind"), ignoreCase: true, out var kind) ? kind : LookupKind.GameKeys;
        set
        {
            Raw["kind"] = PackageDocument.EnumName(value);
            foreach (var key in value == LookupKind.GameKeys ? JsonKeys : GameKeysKeys)
            {
                Raw.Remove(key);
            }

            _document.Touch();
        }
    }

    public LocalizedText Label
    {
        get => LocalizedText.From(Raw["label"]);
        set => _document.SetNode(Raw, "label", value.ToNode());
    }

    public string? Get(string key) => PackageDocument.Str(Raw, key);

    public void Set(string key, string? value) => _document.SetStr(Raw, key, value);

    /// <summary>Pflichtangabe (<c>folder</c>, <c>asset</c>, <c>items</c>): bleibt auch leer als <c>""</c> im JSON.</summary>
    public void SetText(string key, string value) => _document.SetNode(Raw, key, JsonValue.Create(value));

    /// <summary>Blocktiefe (1 oder 2); 1 ohne Angabe bleibt ohne Angabe.</summary>
    public int Depth
    {
        get => Raw["depth"] is JsonValue v && v.TryGetValue<int>(out var depth) ? depth : 1;
        set => _document.SetNode(Raw, "depth", value == 1 && Raw["depth"] is null ? null : JsonValue.Create(value));
    }

    public bool NamedOnly
    {
        get => PackageDocument.Bool(Raw, "namedOnly");
        set => _document.SetNode(Raw, "namedOnly", value ? JsonValue.Create(true) : null);
    }

    public IReadOnlyList<string> TagInBlock
    {
        get => Raw["tagInBlock"] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : Array.Empty<string>();
        set => _document.SetNode(Raw, "tagInBlock", value.Count == 0 ? null : new JsonArray(value.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()));
    }

    /// <summary>Zuordnung eines Eintragsfelds (<c>key</c>, <c>name</c>, <c>desc</c>, <c>tag</c>, <c>nation</c>) zu einer Eigenschaft der JSON-Datei.</summary>
    public string? JsonField(string name) => Raw["fields"] is JsonObject fields ? PackageDocument.Str(fields, name) : null;

    public void SetJsonField(string name, string? property)
    {
        if (Raw["fields"] is not JsonObject fields)
        {
            if (string.IsNullOrEmpty(property))
            {
                return;
            }

            fields = new JsonObject();
            Raw["fields"] = fields;
        }

        _document.SetStr(fields, name, property);
        if (fields.Count == 0)
        {
            Raw.Remove("fields");
        }
    }
}

/// <summary>Feld-Angabe <c>lookup</c>: Quellen und <c>fills</c> (Eintragsfeld <c>name</c>/<c>desc</c> → Feld derselben Ebene).</summary>
public sealed class FieldLookup
{
    private readonly PackageDocument _document;

    internal FieldLookup(PackageDocument document, JsonObject raw)
    {
        _document = document;
        Raw = raw;
    }

    public JsonObject Raw { get; }

    public IReadOnlyList<string> Sources
    {
        get => Raw["sources"] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : Array.Empty<string>();
        set => _document.SetNode(Raw, "sources", new JsonArray(value.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()));
    }

    public string? Fill(string entryField) => Raw["fills"] is JsonObject fills ? PackageDocument.Str(fills, entryField) : null;

    public void SetFill(string entryField, string? fieldId)
    {
        if (Raw["fills"] is not JsonObject fills)
        {
            fills = new JsonObject();
            Raw["fills"] = fills;
        }

        _document.SetStr(fills, entryField, fieldId);
    }
}

public sealed partial class PackageDocument
{
    public IReadOnlyList<LookupSourceNode> LookupSources =>
        Json["lookups"] is JsonArray array ? array.OfType<JsonObject>().Select(o => new LookupSourceNode(this, o)).ToList() : Array.Empty<LookupSourceNode>();

    /// <summary>Alle Felder: Kopf, dann die Ebenen von oben nach unten.</summary>
    public IEnumerable<(FieldList List, FieldNode Field)> AllFields()
    {
        foreach (var list in new[] { Head }.Concat(Levels.Select(l => l.Fields)))
        {
            foreach (var field in list.Items)
            {
                yield return (list, field);
            }
        }
    }

    public IReadOnlyList<FieldNode> FieldsUsingSource(string id) =>
        AllFields().Select(p => p.Field).Where(f => f.Lookup?.Sources.Contains(id, StringComparer.Ordinal) == true).ToList();

    /// <summary>Neue Quelle am Ende; Spielquellen mit leerem Ordner, JSON-Quellen mit leerem Asset und Pfad.</summary>
    public LookupSourceNode AddLookupSource(string id, LookupKind kind)
    {
        var raw = new JsonObject { ["id"] = id, ["kind"] = EnumName(kind), ["label"] = id };
        if (kind == LookupKind.GameKeys)
        {
            raw["folder"] = string.Empty;
        }
        else
        {
            raw["asset"] = string.Empty;
            raw["items"] = string.Empty;
        }

        Sources().Add(raw);
        Touch();
        return new LookupSourceNode(this, raw);
    }

    /// <summary>Kopie direkt hinter dem Original mit freier Kennung <c>&lt;id&gt;_2</c>, <c>_3</c> …</summary>
    public LookupSourceNode DuplicateLookupSource(string id)
    {
        var array = Sources();
        var index = array.IndexOf(array.OfType<JsonObject>().First(o => Str(o, "id") == id));
        var taken = LookupSources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var number = 2;
        while (taken.Contains($"{id}_{number}"))
        {
            number++;
        }

        var copy = array[index]!.DeepClone().AsObject();
        copy["id"] = $"{id}_{number}";
        var at = index + 1;
        while (at < array.Count && Str((JsonObject)array[at]!, "id")?.StartsWith(id + "_", StringComparison.Ordinal) == true)
        {
            at++;
        }

        array.Insert(at, copy);
        Touch();
        return new LookupSourceNode(this, copy);
    }

    /// <summary>Entfernt die Quelle und nimmt sie aus allen Feldern; ein <c>lookup</c> ohne Quellen fällt weg. Gibt die Zahl geänderter Felder zurück.</summary>
    public int RemoveLookupSource(string id)
    {
        var changed = 0;
        foreach (var field in FieldsUsingSource(id))
        {
            var rest = field.Lookup!.Sources.Where(s => s != id).ToList();
            if (rest.Count == 0)
            {
                field.DisableLookup();
            }
            else
            {
                field.Lookup!.Sources = rest;
            }

            changed++;
        }

        var array = Sources();
        if (array.OfType<JsonObject>().FirstOrDefault(o => Str(o, "id") == id) is { } raw)
        {
            array.Remove(raw);
        }

        if (array.Count == 0)
        {
            Json.Remove("lookups");
        }

        Touch();
        return changed;
    }

    public void MoveLookupSource(string id, int delta)
    {
        var array = Sources();
        var raw = array.OfType<JsonObject>().FirstOrDefault(o => Str(o, "id") == id);
        var index = raw is null ? -1 : array.IndexOf(raw);
        var target = Math.Clamp(index + delta, 0, array.Count - 1);
        if (index < 0 || target == index)
        {
            return;
        }

        array.RemoveAt(index);
        array.Insert(target, raw);
        Touch();
    }

    /// <summary>Benennt die Quelle um; die Quellen-Listen aller Felder ziehen mit.</summary>
    public void RenameLookupSource(LookupSourceNode source, string newId)
    {
        var old = source.Id;
        foreach (var field in FieldsUsingSource(old))
        {
            field.Lookup!.Sources = field.Lookup.Sources.Select(s => s == old ? newId : s).ToList();
        }

        source.Id = newId;
    }

    private JsonArray Sources()
    {
        if (Json["lookups"] is not JsonArray array)
        {
            array = new JsonArray();
            Json["lookups"] = array;
        }

        return array;
    }
}
