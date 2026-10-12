using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Sicht auf eine Ausgabe in <c>outputs</c>. Beim Wechsel der Art bleiben nur Angaben, die auch die neue Art kennt.</summary>
public sealed class OutputNode
{
    private static readonly string[] AllKeys =
        { "template", "block", "forEach", "skeleton", "legacyBegin", "legacyEnd", "line", "keyPattern", "source", "newLine", "legacyPatterns", "keep", "checkUnreferenced", "match", "keys", "message", "insertAfter", "ifLimit", "external", "jsonPath", "entryKey" };

    private readonly PackageDocument _document;

    internal OutputNode(PackageDocument document, JsonObject raw)
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

    public OutputKind Kind
    {
        get => Enum.TryParse<OutputKind>(PackageDocument.Str(Raw, "kind"), ignoreCase: true, out var kind) ? kind : OutputKind.OwnFile;
        set
        {
            Raw["kind"] = PackageDocument.EnumName(value);
            var keep = KeysOf(value);
            foreach (var key in AllKeys.Where(k => !keep.Contains(k)))
            {
                Raw.Remove(key);
            }

            if (value == OutputKind.JsonEntry)
            {
                // "jsonEntry" schreibt in eine externe Datei und braucht keinen Pfad; "path" bleibt als leerer Text stehen
                Raw["path"] = string.Empty;
            }

            _document.Touch();
        }
    }

    /// <summary>Pfad-Vorlage; bleibt auch leer im JSON (Pflichtangabe der Ausgabe).</summary>
    public string Path
    {
        get => PackageDocument.Str(Raw, "path") ?? string.Empty;
        set => _document.SetNode(Raw, "path", JsonValue.Create(value));
    }

    /// <summary>Meldung einer Prüfung ("check") je Oberflächensprache.</summary>
    public LocalizedText Message
    {
        get => LocalizedText.From(Raw["message"]);
        set => _document.SetNode(Raw, "message", value.ToNode());
    }

    public string? Get(string key) => PackageDocument.Str(Raw, key);

    public void Set(string key, string? value) => _document.SetStr(Raw, key, value);

    public IReadOnlyList<string> GetList(string key) =>
        Raw[key] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : Array.Empty<string>();

    public void SetList(string key, IReadOnlyList<string> values) =>
        _document.SetNode(Raw, key, values.Count == 0 ? null : new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()));

    public bool CheckUnreferenced
    {
        get => PackageDocument.Bool(Raw, "checkUnreferenced");
        set => _document.SetNode(Raw, "checkUnreferenced", value ? JsonValue.Create(true) : null);
    }

    /// <summary>Angaben, die eine Ausgabe-Art neben Kennung, Art und Pfad kennt (Spec Teil 5a, Abschnitt 3).</summary>
    public static IReadOnlyList<string> KeysOf(OutputKind kind) => kind switch
    {
        OutputKind.OwnFile => new[] { "template", "newLine" },
        OutputKind.Localization => new[] { "template", "keys", "insertAfter", "newLine" },
        OutputKind.InsertSection => new[] { "template", "block", "forEach", "skeleton", "legacyBegin", "legacyEnd", "legacyPatterns", "newLine" },
        OutputKind.EnsureLine => new[] { "block", "line", "skeleton", "newLine" },
        OutputKind.SyncBlocks => new[] { "template", "keyPattern", "keep", "checkUnreferenced", "newLine" },
        OutputKind.CopyBinary => new[] { "source", "newLine" },
        OutputKind.SyncMatching => new[] { "template", "block", "forEach", "match", "insertAfter", "ifLimit", "skeleton", "newLine" },
        OutputKind.Check => new[] { "template", "message" },
        OutputKind.JsonEntry => new[] { "external", "jsonPath", "entryKey", "template" },
        _ => new[] { "newLine" },
    };
}

public sealed partial class PackageDocument
{
    public IReadOnlyList<OutputNode> Outputs =>
        Json["outputs"] is JsonArray array ? array.OfType<JsonObject>().Select(o => new OutputNode(this, o)).ToList() : Array.Empty<OutputNode>();

    public OutputNode AddOutput(string id, OutputKind kind)
    {
        var raw = new JsonObject { ["id"] = id, ["kind"] = EnumName(kind), ["path"] = string.Empty };
        OutputArray().Add(raw);
        Touch();
        return new OutputNode(this, raw);
    }

    /// <summary>Kopie direkt hinter dem Original mit freier Kennung (<c>x_copy</c>, <c>x_copy2</c> ...).</summary>
    public OutputNode DuplicateOutput(string id)
    {
        var array = OutputArray();
        var original = array.OfType<JsonObject>().First(o => Str(o, "id") == id);
        var used = Outputs.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var newId = id + "_copy";
        for (var n = 2; used.Contains(newId); n++)
        {
            newId = id + "_copy" + n;
        }

        var copy = original.DeepClone().AsObject();
        copy["id"] = newId;
        array.Insert(array.IndexOf(original) + 1, copy);
        Touch();
        return new OutputNode(this, copy);
    }

    public void RemoveOutput(string id)
    {
        var array = OutputArray();
        if (array.OfType<JsonObject>().FirstOrDefault(o => Str(o, "id") == id) is { } raw)
        {
            array.Remove(raw);
            Touch();
        }
    }

    public void MoveOutput(string id, int delta) => MoveIn(Json["outputs"] as JsonArray, o => Str(o, "id") == id, delta, this);

    /// <summary>Zieht <c>template</c> und <c>skeleton</c> aller Ausgaben auf den neuen Vorlagennamen.</summary>
    public void RenameTemplateReferences(string oldName, string newName)
    {
        foreach (var output in Outputs)
        {
            foreach (var key in new[] { "template", "skeleton" }.Where(k => output.Get(k) == oldName))
            {
                output.Raw[key] = newName;
            }
        }

        Touch();
    }

    /// <summary>Zieht <c>external</c> aller Ausgaben auf die neue Kennung einer externen Datei.</summary>
    public void RenameExternalReferences(string oldId, string newId)
    {
        foreach (var output in Outputs.Where(o => o.Get("external") == oldId))
        {
            output.Raw["external"] = newId;
        }

        Touch();
    }

    public IReadOnlyList<string> OutputsUsingTemplate(string name) =>
        Outputs.Where(o => o.Get("template") == name || o.Get("skeleton") == name).Select(o => o.Id).ToList();

    private JsonArray OutputArray()
    {
        if (Json["outputs"] is not JsonArray array)
        {
            array = new JsonArray();
            Json["outputs"] = array;
        }

        return array;
    }
}
