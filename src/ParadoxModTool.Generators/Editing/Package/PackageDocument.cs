using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// package.json als JSON-Baum für den Paket-Editor (Spec Teil 5a, Abschnitt 1). Die Zugriffe lesen und schreiben direkt in <see cref="Json"/>;
/// alles andere (<c>read</c>, <c>lookups</c>, unbekannte Schlüssel) bleibt unberührt. Mehrsprachige Texte behalten alle Sprachen.
/// </summary>
public sealed partial class PackageDocument
{
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly JsonDocumentOptions Strict = new() { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = true };

    private static readonly JsonSerializerOptions Write = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private PackageDocument(string folder, JsonObject root, bool hadComments)
    {
        Folder = folder;
        Json = root;
        HadComments = hadComments;
    }

    /// <summary>Nach jeder Änderung über die Zugriffe.</summary>
    public event Action? Changed;

    public string Folder { get; }

    public JsonObject Json { get; }

    /// <summary>Die Datei enthielt Kommentare; beim Speichern gehen sie verloren.</summary>
    public bool HadComments { get; }

    public bool IsDirty { get; private set; }

    /// <summary>Liest package.json; ungültiges JSON ist ein <see cref="GeneratorPackageException"/>.</summary>
    public static PackageDocument Load(string folder)
    {
        var path = System.IO.Path.Combine(folder, "package.json");
        if (!File.Exists(path))
        {
            throw new GeneratorPackageException(GeneratorsTexts.PackageJsonMissing(folder));
        }

        var text = File.ReadAllText(path);
        JsonObject root;
        try
        {
            root = JsonNode.Parse(text, documentOptions: Lenient) as JsonObject ?? throw new GeneratorPackageException(GeneratorsTexts.PackageJsonInvalid("{ }"));
        }
        catch (JsonException ex)
        {
            throw new GeneratorPackageException(GeneratorsTexts.PackageJsonInvalid(ex.Message));
        }

        var hadComments = false;
        try
        {
            JsonNode.Parse(text, documentOptions: Strict);
        }
        catch (JsonException)
        {
            hadComments = true;
        }

        return new PackageDocument(folder, root, hadComments);
    }

    /// <summary>Schreibt package.json eingerückt (UTF-8 ohne BOM, LF) über eine temporäre Datei.</summary>
    public void Save()
    {
        var text = Json.ToJsonString(Write).Replace("\r\n", "\n") + "\n";
        var target = System.IO.Path.Combine(Folder, "package.json");
        var temp = System.IO.Path.Combine(Folder, System.IO.Path.GetRandomFileName() + ".tmp");
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        IsDirty = false;
    }

    /// <summary>Für die Knoten-Sichten: eine Änderung melden.</summary>
    internal void Touch()
    {
        IsDirty = true;
        Changed?.Invoke();
    }

    public string Id
    {
        get => Str(Json, "id") ?? string.Empty;
        set => SetStr(Json, "id", value);
    }

    /// <summary><c>markerId</c>: Kennung in den Markierungen der Mod; ohne Angabe (<see langword="null"/>) gilt <see cref="Id"/>.</summary>
    public string? MarkerId
    {
        get => Str(Json, "markerId");
        set => SetStr(Json, "markerId", value);
    }

    /// <summary>Vor dem Wechsel der Kennung (Kopie, Import unter neuer Kennung): die bisherige Markierungs-Kennung festhalten.</summary>
    public void ChangeIdKeepingMarkers(string newId)
    {
        MarkerId ??= Id;
        Id = newId;
    }

    public LocalizedText Name
    {
        get => LocalizedText.From(Json["name"]);
        set => SetNode(Json, "name", value.ToNode());
    }

    public string GameId
    {
        get => Str(Json, "gameId") ?? string.Empty;
        set => SetStr(Json, "gameId", value);
    }

    /// <summary>Das erste Schlüsselfeld; setzen macht daraus einen Schlüssel aus genau diesem Feld.</summary>
    public string KeyField
    {
        get => KeyFields.FirstOrDefault() ?? string.Empty;
        set => KeyFields = new[] { value };
    }

    /// <summary>Schlüsselfelder (Spec 2026-10-06, Abschnitt 3): gespeichert als <c>keyField</c> bei einem Feld, sonst als <c>keyFields</c>.</summary>
    public IReadOnlyList<string> KeyFields
    {
        get => Json["keyFields"] is JsonArray array && array.Count > 0
            ? array.Select(n => n?.ToString() ?? string.Empty).ToList()
            : Str(Json, "keyField") is { } single ? new[] { single } : Array.Empty<string>();
        set
        {
            if (value.Count == 1)
            {
                Json.Remove("keyFields");
                SetStr(Json, "keyField", value[0]);
                return;
            }

            Json.Remove("keyField");
            SetNode(Json, "keyFields", new JsonArray(value.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray()));
        }
    }

    /// <summary>Trennzeichen zusammengesetzter Schlüssel; die Vorgabe <c>_</c> wird nicht gespeichert.</summary>
    public string KeySeparator
    {
        get => Str(Json, "keySeparator") ?? "_";
        set => SetNode(Json, "keySeparator", string.IsNullOrEmpty(value) || value == "_" ? null : JsonValue.Create(value));
    }

    /// <summary>Erlaubte Ziele; ohne Eintrag wie im Loader beide.</summary>
    public IReadOnlyList<string> Targets
    {
        get => Json["targets"] is JsonArray array ? array.Select(n => n?.ToString() ?? string.Empty).ToList() : new[] { "mod", "output" };
        set => SetNode(Json, "targets", new JsonArray(value.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()));
    }

    public LanguageDefaults Languages
    {
        get => Json["languages"] is JsonObject l
            ? new LanguageDefaults(Bool(l, "extendEnglish"), Bool(l, "copyEnglish"), Bool(l, "englishForMissing"))
            : new LanguageDefaults();
        set => SetNode(Json, "languages", new JsonObject
        {
            ["extendEnglish"] = value.ExtendEnglish,
            ["copyEnglish"] = value.CopyEnglish,
            ["englishForMissing"] = value.EnglishForMissing,
        });
    }

    /// <summary>Assets in Dateireihenfolge; Text-Einträge gelten als aus dem Spiel. Schreiben immer in Objektform.</summary>
    public IReadOnlyList<(string Id, AssetDef Asset)> Assets
    {
        get => Json["assets"] is JsonObject map
            ? map.Select(p => (p.Key, p.Value is JsonObject o
                ? new AssetDef(Str(o, "path") ?? string.Empty, string.Equals(Str(o, "origin"), "mod", StringComparison.OrdinalIgnoreCase) ? AssetOrigin.Mod : AssetOrigin.Game)
                : new AssetDef(p.Value?.ToString() ?? string.Empty, AssetOrigin.Game))).ToList()
            : new List<(string, AssetDef)>();
        set
        {
            var map = new JsonObject();
            foreach (var (id, asset) in value)
            {
                map[id] = new JsonObject { ["path"] = asset.Path, ["origin"] = asset.Origin == AssetOrigin.Mod ? "mod" : "game" };
            }

            SetNode(Json, "assets", map.Count > 0 ? map : null);
        }
    }

    /// <summary>Externe Dateien (<c>externals</c>, Kennung → Beschriftung und Hinweis) in Dateireihenfolge; den Pfad wählt der Nutzer im Generator-Fenster, er steht nicht im Paket.</summary>
    public IReadOnlyList<(string Id, LocalizedText Label, LocalizedText Hint)> Externals
    {
        get => Json["externals"] is JsonObject map
            ? map.Select(p => (p.Key, LocalizedText.From((p.Value as JsonObject)?["label"]), LocalizedText.From((p.Value as JsonObject)?["hint"]))).ToList()
            : new List<(string, LocalizedText, LocalizedText)>();
        set
        {
            var existing = Json["externals"] as JsonObject;
            var map = new JsonObject();
            foreach (var (id, label, hint) in value)
            {
                // Unbekannte Angaben einer Datei bleiben erhalten
                var entry = existing?[id] is JsonObject old ? (JsonObject)old.DeepClone() : new JsonObject();
                SetNode(entry, "label", label.ToNode());
                SetNode(entry, "hint", hint.ToNode());
                map[id] = entry;
            }

            SetNode(Json, "externals", map.Count > 0 ? map : null);
        }
    }

    /// <summary><c>afterGenerate</c>: Hinweis, den das Generator-Fenster nach erfolgreichem Erzeugen unter der Meldung zeigt.</summary>
    public LocalizedText AfterGenerate
    {
        get => LocalizedText.From(Json["afterGenerate"]);
        set => SetNode(Json, "afterGenerate", value.ToNode());
    }

    /// <summary>
    /// Abschnitt <c>candidates</c> (Modifier ohne Idee); <see langword="null"/> = es gibt ihn nicht, setzen von <see langword="null"/> entfernt ihn.
    /// Unbekannte Schlüssel im Abschnitt, in <c>create</c> und in Gruppen und Verwendungen bleiben beim Setzen erhalten.
    /// </summary>
    public CandidatesDoc? Candidates
    {
        get
        {
            if (Json["candidates"] is not JsonObject c)
            {
                return null;
            }

            var create = c["create"] as JsonObject;
            return new CandidatesDoc(
                Str(c, "category") ?? string.Empty,
                Objects(c, "used").Select(u => (Str(u, "file") ?? string.Empty, Str(u, "block") ?? string.Empty, CommaList(u, "except"))).ToList(),
                Objects(c, "groups").Select(g => (Str(g, "id") ?? string.Empty, LocalizedText.From(g["label"]), Str(g, "pattern") ?? string.Empty, Bool(g, "hidden"))).ToList(),
                create is null ? string.Empty : CommaList(create, "ask"),
                create is null ? string.Empty : Str(create, "name") ?? string.Empty,
                create is null ? string.Empty : Str(create, "modifiers") ?? string.Empty);
        }

        set
        {
            if (value is null)
            {
                SetNode(Json, "candidates", null);
                return;
            }

            var old = Json["candidates"] as JsonObject;
            var section = old is null ? new JsonObject() : (JsonObject)old.DeepClone();
            SetStr(section, "category", value.Category);

            // Verwendungen: Ein Eintrag behält seine unbekannten Schlüssel, wenn er (Datei, Block) behält, sonst die Stelle in der Liste
            var oldUsed = Objects(section, "used").ToList();
            var used = new JsonArray();
            for (var i = 0; i < value.Used.Count; i++)
            {
                var (file, block, except) = value.Used[i];
                var entry = oldUsed.FirstOrDefault(u => Str(u, "file") == file && Str(u, "block") == block)
                    ?? (oldUsed.Count == value.Used.Count ? oldUsed[i] : null);
                var node = entry is null ? new JsonObject() : (JsonObject)entry.DeepClone();
                SetStr(node, "file", file);
                SetStr(node, "block", block);
                SetList(node, "except", except);
                used.Add(node);
            }

            SetNode(section, "used", used);

            // Gruppen: je Kennung; eine umbenannte Gruppe behält ihre unbekannten Schlüssel nur, wenn die Zahl der Gruppen gleich bleibt
            var oldGroups = Objects(section, "groups").ToList();
            var groups = new JsonArray();
            for (var i = 0; i < value.Groups.Count; i++)
            {
                var (id, label, pattern, hidden) = value.Groups[i];
                var entry = (id.Length > 0 ? oldGroups.FirstOrDefault(g => Str(g, "id") == id) : null)
                    ?? (oldGroups.Count == value.Groups.Count ? oldGroups[i] : null);
                var node = entry is null ? new JsonObject() : (JsonObject)entry.DeepClone();
                SetStr(node, "id", id);
                SetNode(node, "label", label.ToNode());
                SetStr(node, "pattern", pattern);

                // false ist die Vorgabe: nur schreiben, wenn es schon da stand oder wahr ist
                SetNode(node, "hidden", hidden || node["hidden"] is not null ? JsonValue.Create(hidden) : null);
                groups.Add(node);
            }

            SetNode(section, "groups", groups.Count > 0 || section["groups"] is not null ? groups : null);

            var create = section["create"] as JsonObject ?? new JsonObject();
            SetList(create, "ask", value.Ask);
            SetStr(create, "name", value.Name);
            SetStr(create, "modifiers", value.Modifiers);
            SetNode(section, "create", create);
            SetNode(Json, "candidates", section);
        }
    }

    private static IEnumerable<JsonObject> Objects(JsonObject o, string key) =>
        o[key] is JsonArray array ? array.OfType<JsonObject>() : Enumerable.Empty<JsonObject>();

    /// <summary>Ein Text-Array als "a, b".</summary>
    private static string CommaList(JsonObject o, string key) =>
        o[key] is JsonArray array ? string.Join(", ", array.Select(n => n?.ToString() ?? string.Empty)) : Str(o, key) ?? string.Empty;

    /// <summary>Schreibt "a, b" als Text-Array; leer lässt den Schlüssel weg (stand er schon da, bleibt ein leeres Array).</summary>
    private void SetList(JsonObject o, string key, string commaList)
    {
        var items = commaList.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        SetNode(o, key, items.Count > 0 || o[key] is not null ? new JsonArray(items.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()) : null);
    }

    public IReadOnlyList<(string When, LocalizedText Message)> Warnings
    {
        get => Messages("warnings");
        set => SetMessages("warnings", value);
    }

    public IReadOnlyList<(string When, LocalizedText Message)> Errors
    {
        get => Messages("errors");
        set => SetMessages("errors", value);
    }

    private IReadOnlyList<(string When, LocalizedText Message)> Messages(string key) =>
        Json[key] is JsonArray array
            ? array.OfType<JsonObject>().Select(o => (Str(o, "when") ?? string.Empty, LocalizedText.From(o["message"]))).ToList()
            : new List<(string, LocalizedText)>();

    private void SetMessages(string key, IReadOnlyList<(string When, LocalizedText Message)> value) =>
        SetNode(Json, key, value.Count == 0 ? null : new JsonArray(value.Select(m => (JsonNode?)new JsonObject { ["when"] = m.When, ["message"] = m.Message.ToNode() }).ToArray()));

    internal static string? Str(JsonObject o, string key) => o[key] is JsonValue v ? v.ToString() : null;

    internal static bool Bool(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>Setzt einen Text; leer oder <see langword="null"/> entfernt den Schlüssel.</summary>
    internal void SetStr(JsonObject o, string key, string? value) => SetNode(o, key, string.IsNullOrEmpty(value) ? null : JsonValue.Create(value));

    /// <summary>Setzt einen Knoten an seiner Stelle (Reihenfolge bleibt); <see langword="null"/> entfernt den Schlüssel.</summary>
    internal void SetNode(JsonObject o, string key, JsonNode? value)
    {
        if (value is null)
        {
            o.Remove(key);
        }
        else
        {
            o[key] = value;
        }

        Touch();
    }
}
