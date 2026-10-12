using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Grund, warum eine Kennung nicht passt: ungültige Schreibweise oder schon vergeben.</summary>
public enum IdIssue
{
    Invalid,
    Duplicate,
}

/// <summary>Sicht auf ein Feld (Objekt in <c>head</c> oder <c>fields</c>); jede Änderung schreibt direkt ins JSON.</summary>
public sealed class FieldNode
{
    private readonly PackageDocument _document;

    internal FieldNode(PackageDocument document, JsonObject raw)
    {
        _document = document;
        Raw = raw;
    }

    public JsonObject Raw { get; }

    public string Id
    {
        get => PackageDocument.Str(Raw, "id") ?? string.Empty;
        set => _document.SetStr(Raw, "id", value);
    }

    public LocalizedText Label
    {
        get => LocalizedText.From(Raw["label"]);
        set => _document.SetNode(Raw, "label", value.ToNode());
    }

    /// <summary>Feldart; beim Wechsel fallen Angaben weg, die die neue Art nicht kennt (Auswahl, Asset, mehrsprachig).</summary>
    public FieldType Type
    {
        get => Enum.TryParse<FieldType>(PackageDocument.Str(Raw, "type"), ignoreCase: true, out var type) ? type : FieldType.Text;
        set
        {
            Raw["type"] = PackageDocument.EnumName(value);
            if (value != FieldType.Choice)
            {
                Raw.Remove("choices");
            }

            if (value != FieldType.Image)
            {
                Raw.Remove("asset");
            }

            if (value != FieldType.Number)
            {
                Raw.Remove("autoNumber");
            }

            if (value is not (FieldType.Text or FieldType.MultilineText))
            {
                Raw.Remove("multilingual");
                Raw.Remove("lookup");
            }

            _document.Touch();
        }
    }

    public bool Required
    {
        get => PackageDocument.Bool(Raw, "required");
        set => _document.SetNode(Raw, "required", value ? JsonValue.Create(true) : null);
    }

    /// <summary><c>autoNumber</c>: neuer Arbeitsstand bekommt die nächste freie Nummer; nur am letzten Schlüsselfeld vom Typ Zahl (der Lader prüft das).</summary>
    public bool AutoNumber
    {
        get => PackageDocument.Bool(Raw, "autoNumber");
        set => _document.SetNode(Raw, "autoNumber", value ? JsonValue.Create(true) : null);
    }

    public string? Pattern
    {
        get => PackageDocument.Str(Raw, "pattern");
        set => _document.SetStr(Raw, "pattern", value);
    }

    public string? Default
    {
        get => PackageDocument.Str(Raw, "default");
        set => _document.SetStr(Raw, "default", value);
    }

    public bool Multilingual
    {
        get => PackageDocument.Bool(Raw, "multilingual");
        set => _document.SetNode(Raw, "multilingual", value ? JsonValue.Create(true) : null);
    }

    public IReadOnlyList<string> Choices
    {
        get => Raw["choices"] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : Array.Empty<string>();
        set => _document.SetNode(Raw, "choices", value.Count == 0 ? null : new JsonArray(value.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()));
    }

    public string? Asset
    {
        get => PackageDocument.Str(Raw, "asset");
        set => _document.SetStr(Raw, "asset", value);
    }

    /// <summary>Feld hat einen Nachschlage-Verweis.</summary>
    public bool HasLookup => Raw["lookup"] is JsonObject;

    /// <summary>Feld-Angabe "Aus Datenbank" (Spec Teil 5b, Abschnitt 3); <see langword="null"/> ohne Angabe.</summary>
    public FieldLookup? Lookup => Raw["lookup"] is JsonObject lookup ? new FieldLookup(_document, lookup) : null;

    /// <summary>Legt <c>lookup</c> mit leeren Quellen und <c>fills</c> an; ein vorhandenes bleibt.</summary>
    public FieldLookup EnableLookup()
    {
        if (Lookup is { } existing)
        {
            return existing;
        }

        var lookup = new JsonObject { ["sources"] = new JsonArray(), ["fills"] = new JsonObject() };
        _document.SetNode(Raw, "lookup", lookup);
        return new FieldLookup(_document, lookup);
    }

    public void DisableLookup() => _document.SetNode(Raw, "lookup", null);
}

/// <summary>Liste von Feldern (<c>head</c> des Pakets oder <c>fields</c> einer Ebene).</summary>
public sealed class FieldList
{
    private readonly PackageDocument _document;
    private readonly JsonObject _owner;
    private readonly string _key;

    internal FieldList(PackageDocument document, JsonObject owner, string key)
    {
        _document = document;
        _owner = owner;
        _key = key;
    }

    /// <summary>Kennung der Ebene, zu der die Liste gehört; <see langword="null"/> beim Kopf.</summary>
    public string? LevelId => _key == "head" ? null : PackageDocument.Str(_owner, "id");

    public IReadOnlyList<FieldNode> Items =>
        _owner[_key] is JsonArray array ? array.OfType<JsonObject>().Select(o => new FieldNode(_document, o)).ToList() : Array.Empty<FieldNode>();

    /// <summary>Hängt ein Feld an; eine schon vergebene Kennung ist ein <see cref="ArgumentException"/>.</summary>
    public FieldNode Add(string id, FieldType type)
    {
        if (Items.Any(f => f.Id == id))
        {
            throw new ArgumentException(id, nameof(id));
        }

        if (_owner[_key] is not JsonArray array)
        {
            array = new JsonArray();
            _owner[_key] = array;
        }

        var raw = new JsonObject { ["id"] = id, ["type"] = PackageDocument.EnumName(type) };
        array.Add(raw);
        _document.Touch();
        return new FieldNode(_document, raw);
    }

    public void Remove(string id)
    {
        if (_owner[_key] is JsonArray array && array.OfType<JsonObject>().FirstOrDefault(o => PackageDocument.Str(o, "id") == id) is { } raw)
        {
            array.Remove(raw);
            _document.Touch();
        }
    }

    /// <summary>Verschiebt ein Feld um <paramref name="delta"/> Plätze (am Rand bleibt es stehen).</summary>
    public void Move(string id, int delta) => PackageDocument.MoveIn(_owner[_key] as JsonArray, o => PackageDocument.Str(o, "id") == id, delta, _document);
}

/// <summary>Sicht auf eine Formular-Ebene (<c>root</c> oder ein <c>child</c>).</summary>
public sealed class LevelNode
{
    private readonly PackageDocument _document;

    internal LevelNode(PackageDocument document, JsonObject raw, LevelNode? parent)
    {
        _document = document;
        Raw = raw;
        Parent = parent;
        Fields = new FieldList(document, raw, "fields");
    }

    public JsonObject Raw { get; }

    public LevelNode? Parent { get; }

    public FieldList Fields { get; }

    public LevelNode? Child => Raw["child"] is JsonObject child ? _document.LevelFor(child, this) : null;

    public string Id => PackageDocument.Str(Raw, "id") ?? string.Empty;

    public LocalizedText Label
    {
        get => LocalizedText.From(Raw["label"]);
        set => _document.SetNode(Raw, "label", value.ToNode());
    }

    /// <summary>Feste Gruppe aus einer Konstanten-Tabelle; <see langword="null"/> = zählbare Ebene.</summary>
    public string? ConstantsTable
    {
        get => PackageDocument.Str(Raw, "constantsTable");
        set => _document.SetStr(Raw, "constantsTable", value);
    }

    public int Min
    {
        get => PackageDocument.Int(Raw, "min") ?? 0;
        set => _document.SetNode(Raw, "min", JsonValue.Create(value));
    }

    /// <summary>Höchstzahl; ohne Angabe unbegrenzt wie im Loader.</summary>
    public int Max
    {
        get => PackageDocument.Int(Raw, "max") ?? int.MaxValue;
        set => _document.SetNode(Raw, "max", value == int.MaxValue ? null : JsonValue.Create(value));
    }

    public int? Initial
    {
        get => PackageDocument.Int(Raw, "initial");
        set => _document.SetNode(Raw, "initial", value is { } v ? JsonValue.Create(v) : null);
    }

    public string? NumberBaseField
    {
        get => PackageDocument.Str(Raw, "numberBaseField");
        set => _document.SetStr(Raw, "numberBaseField", value);
    }
}

public sealed partial class PackageDocument
{
    private static readonly Regex IdPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    private readonly Dictionary<JsonObject, LevelNode> _levels = new(ReferenceEqualityComparer.Instance);

    /// <summary>Kopf-Felder (<c>head</c>).</summary>
    public FieldList Head => new(this, Json, "head");

    /// <summary>Wurzel-Ebene oder <see langword="null"/>.</summary>
    public LevelNode? Root => Json["root"] is JsonObject root ? LevelFor(root, null) : null;

    /// <summary>Alle Ebenen von der Wurzel abwärts.</summary>
    public IReadOnlyList<LevelNode> Levels
    {
        get
        {
            var result = new List<LevelNode>();
            for (var level = Root; level is not null; level = level.Child)
            {
                result.Add(level);
            }

            return result;
        }
    }

    /// <summary>Kennung von Feld, Ebene oder Ausgabe (ein Scriban-Name).</summary>
    public static bool IsValidId(string id) => IdPattern.IsMatch(id);

    /// <summary>Problem einer Kennung <paramref name="candidate"/> an Stelle <paramref name="index"/> einer Liste (z. B. Assets); <see langword="null"/>, wenn sie passt.</summary>
    public static IdIssue? IdIssueOf(IReadOnlyList<string> ids, int index, string candidate) =>
        !IsValidId(candidate) ? IdIssue.Invalid
        : ids.Where((id, i) => i != index).Contains(candidate, StringComparer.Ordinal) ? IdIssue.Duplicate
        : null;

    /// <summary>Legt unter <paramref name="parent"/> (ohne: als Wurzel) eine neue Ebene an; gibt es dort schon eine, ist das ein Fehler.</summary>
    public LevelNode AddChildLevel(LevelNode? parent, string id)
    {
        var owner = parent?.Raw ?? Json;
        var key = parent is null ? "root" : "child";
        if (owner[key] is not null)
        {
            throw new InvalidOperationException(key);
        }

        var raw = new JsonObject { ["id"] = id };
        owner[key] = raw;
        Touch();
        return LevelFor(raw, parent);
    }

    /// <summary>Entfernt die Ebene samt allen tieferen.</summary>
    public void RemoveLevel(LevelNode level)
    {
        if (level.Parent is null)
        {
            Json.Remove("root");
        }
        else
        {
            level.Parent.Raw.Remove("child");
        }

        Touch();
    }

    /// <summary>Benennt eine Ebene um und zieht <c>forEach</c> der Ausgaben sowie Ebenen-Verweise im Einleser (<c>read</c>) mit.</summary>
    public void RenameLevel(LevelNode level, string newId)
    {
        var old = level.Id;
        level.Raw["id"] = newId;
        foreach (var output in (Json["outputs"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            if (Str(output, "forEach") == old)
            {
                output["forEach"] = newId;
            }
        }

        if (Json["read"] is JsonObject read)
        {
            if (read["elements"] is JsonObject elements && Str(elements, "level") == old)
            {
                elements["level"] = newId;
            }

            foreach (var entry in new[] { "localization", "flags" }.SelectMany(k => (read[k] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>()))
            {
                if (Str(entry, "field") is { } field && field.StartsWith(old + ".", StringComparison.Ordinal))
                {
                    entry["field"] = newId + field[old.Length..];
                }
            }
        }

        Touch();
    }

    public IReadOnlyList<string> ConstantTables => Json["constants"] is JsonObject map ? map.Select(p => p.Key).ToList() : Array.Empty<string>();

    /// <summary>Zeilen einer Tabelle als Kopien (Spalte → Wert, mehrsprachige Werte als Objekt).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, JsonNode?>> ConstantRows(string table) =>
        Json["constants"]?[table] is JsonArray rows
            ? rows.OfType<JsonObject>().Select(r => (IReadOnlyDictionary<string, JsonNode?>)r.ToDictionary(p => p.Key, p => p.Value?.DeepClone())).ToList()
            : Array.Empty<IReadOnlyDictionary<string, JsonNode?>>();

    public void SetConstantRows(string table, IReadOnlyList<IReadOnlyDictionary<string, JsonNode?>> rows)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            var raw = new JsonObject();
            foreach (var (column, value) in row)
            {
                raw[column] = value?.DeepClone();
            }

            array.Add(raw);
        }

        Constants()[table] = array;
        Touch();
    }

    public void AddConstantTable(string name)
    {
        Constants()[name] = new JsonArray();
        Touch();
    }

    public void RemoveConstantTable(string name)
    {
        if (Json["constants"] is JsonObject map)
        {
            map.Remove(name);
            Touch();
        }
    }

    /// <summary>Benennt eine Tabelle um (Reihenfolge bleibt) und zieht <c>constantsTable</c> der Ebenen mit.</summary>
    public void RenameConstantTable(string oldName, string newName)
    {
        if (Json["constants"] is not JsonObject map || !map.ContainsKey(oldName))
        {
            return;
        }

        var entries = map.ToList();
        map.Clear();
        foreach (var (key, value) in entries)
        {
            map[key == oldName ? newName : key] = value;
        }

        foreach (var level in Levels.Where(l => l.ConstantsTable == oldName))
        {
            level.Raw["constantsTable"] = newName;
        }

        Touch();
    }

    public IReadOnlyList<string> LevelsUsingTable(string name) => Levels.Where(l => l.ConstantsTable == name).Select(l => l.Id).ToList();

    internal LevelNode LevelFor(JsonObject raw, LevelNode? parent)
    {
        if (!_levels.TryGetValue(raw, out var level) || level.Parent != parent)
        {
            level = new LevelNode(this, raw, parent);
            _levels[raw] = level;
        }

        return level;
    }

    internal static int? Int(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    /// <summary>camelCase-Name eines Enum-Werts wie in package.json (<c>multilineText</c>, <c>ownFile</c>).</summary>
    internal static string EnumName<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    internal static void MoveIn(JsonArray? array, Func<JsonObject, bool> match, int delta, PackageDocument document)
    {
        if (array is null || array.OfType<JsonObject>().FirstOrDefault(match) is not { } item)
        {
            return;
        }

        var index = array.IndexOf(item);
        var target = Math.Clamp(index + delta, 0, array.Count - 1);
        if (target == index)
        {
            return;
        }

        array.RemoveAt(index);
        array.Insert(target, item);
        document.Touch();
    }

    private JsonObject Constants()
    {
        if (Json["constants"] is not JsonObject map)
        {
            map = new JsonObject();
            Json["constants"] = map;
        }

        return map;
    }
}
