using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Patches;

/// <summary>
/// Bereitet Rezept-JSON vor dem Einlesen auf, damit auch von KIs geschriebene Rezepte laden: Kommentare und Endkommas sind erlaubt,
/// <c>kind</c> darf an beliebiger Stelle und in beliebiger Schreibweise stehen (System.Text.Json verlangt es als erstes Feld und exakt),
/// Nullwerte werden zu den Standardwerten (leere Listen), Zahlen und Wahrheitswerte in Textfeldern zu Text, bei doppelten Namen gilt der letzte Wert,
/// und Eigenschaften, die das Modell nicht kennt, werden gesammelt.
/// </summary>
internal static class PatchRecipeReader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Dictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> PropertyCache = new();

    /// <summary>Wert von <c>kind</c> (case-insensitiv) zum Regeltyp, abgeleitet aus den <see cref="JsonDerivedTypeAttribute"/>en von <see cref="PatchRule"/>.</summary>
    private static readonly Dictionary<string, (string Kind, Type Type)> RuleKinds = typeof(PatchRule)
        .GetCustomAttributes<JsonDerivedTypeAttribute>()
        .ToDictionary(a => (string)a.TypeDiscriminator!, a => ((string)a.TypeDiscriminator!, a.DerivedType), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Liest das Rezept; <paramref name="options"/> sind die Rezept-Optionen des Stores (mit Kommentar-Toleranz und ohne Beachtung der Groß-/Kleinschreibung von Namen).
    /// Doppelte Namen sind erlaubt, der letzte Wert gilt (wie früher beim direkten Einlesen). Wirft bei unlesbarem Inhalt nur <see cref="JsonException"/>.
    /// </summary>
    public static PatchRecipe Read(string json, JsonSerializerOptions options, List<string>? unknownProperties)
    {
        try
        {
            JsonNode? root;
            using (var document = JsonDocument.Parse(json, DocumentOptions))
            {
                root = ToNode(document.RootElement);
            }

            if (root is null)
            {
                throw new JsonException("The recipe file is empty (null).");
            }

            if (root is JsonObject obj)
            {
                WalkObject(obj, typeof(PatchRecipe), "$", unknownProperties);
            }

            return root.Deserialize<PatchRecipe>(options)!;
        }
        catch (ArgumentException ex)
        {
            // Rückfallnetz: JsonObject meldet doppelte Namen als ArgumentException; eine handbearbeitete Datei darf PMT nicht abstürzen lassen.
            throw new JsonException("The recipe could not be read: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Baut den Knotenbaum selbst auf: <see cref="JsonDocument"/> lässt doppelte Namen zu, <see cref="JsonObject"/> nicht. Der letzte Wert gilt und steht
    /// an der Stelle des ersten. Einfache Werte werden geklont, damit nichts auf das freigegebene Dokument zeigt.
    /// </summary>
    private static JsonNode? ToNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    obj[property.Name] = ToNode(property.Value);
                }

                return obj;
            case JsonValueKind.Array:
                var array = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(ToNode(item));
                }

                return array;
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;
            default:
                return JsonValue.Create(element.Clone());
        }
    }

    /// <summary>
    /// Eine Zahl oder ein Wahrheitswert, wo Text erwartet wird, als Text (Zahlen so, wie sie im JSON stehen: <c>1.50</c> bleibt <c>1.50</c>); sonst null.
    /// KIs schreiben z. B. <c>"values": { "income": 5 }</c>.
    /// </summary>
    private static JsonNode? AsText(JsonNode node)
    {
        if (node is not JsonValue value || value.GetValueKind() is not (JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return JsonValue.Create(value.TryGetValue<JsonElement>(out var element) ? element.GetRawText() : value.ToJsonString());
    }

    private static void WalkObject(JsonObject obj, Type type, string path, List<string>? unknown, bool isRule = false)
    {
        var known = KnownProperties(type);

        // Mehrere Schreibweisen derselben Eigenschaft ("name", "Name"): wie bei doppelten Namen gilt die letzte.
        var keys = obj.Select(p => p.Key).ToList();
        foreach (var group in keys.Where(known.ContainsKey).GroupBy(k => known[k].Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            foreach (var key in group.SkipLast(1))
            {
                obj.Remove(key);
            }
        }

        foreach (var name in obj.Select(p => p.Key).ToList())
        {
            if (!known.TryGetValue(name, out var property))
            {
                if (!(isRule && string.Equals(name, "kind", StringComparison.OrdinalIgnoreCase)))
                {
                    unknown?.Add(path + ": " + name);
                }

                continue;
            }

            var value = obj[name];
            if (value is null)
            {
                // Null wird zum Standardwert des Modells (leere Liste, leeres Wächter-Objekt usw.).
                obj.Remove(name);
                continue;
            }

            if ((Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == typeof(string))
            {
                if (AsText(value) is { } text)
                {
                    obj[name] = text;
                }

                continue;
            }

            WalkValue(value, property.PropertyType, path + "." + JsonNamingPolicy.CamelCase.ConvertName(property.Name), unknown);
        }
    }

    private static void WalkValue(JsonNode value, Type type, string path, List<string>? unknown)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (value is JsonArray array)
            {
                WalkArray(array, type.GetGenericArguments()[0], path, unknown);
            }
        }
        else if (type == typeof(Dictionary<string, string>))
        {
            if (value is JsonObject map)
            {
                WalkTextMap(map);
            }
        }
        else if (IsModelClass(type) && value is JsonObject child)
        {
            WalkObject(child, type, path, unknown);
        }
    }

    /// <summary>Werte einer Zuordnung Name → Text (z. B. <c>values</c> einer Zeile): Zahlen und Wahrheitswerte als Text, Nullwerte fehlen (dann gilt der Standardwert).</summary>
    private static void WalkTextMap(JsonObject map)
    {
        foreach (var key in map.Select(p => p.Key).ToList())
        {
            if (map[key] is not { } value)
            {
                map.Remove(key);
            }
            else if (AsText(value) is { } text)
            {
                map[key] = text;
            }
        }
    }

    /// <summary>Eine Textliste (z. B. <c>watch.patterns</c>): Nullwerte fallen weg, Zahlen und Wahrheitswerte werden Text.</summary>
    private static void WalkTextArray(JsonArray array)
    {
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is not { } value)
            {
                array.RemoveAt(i);
            }
            else if (AsText(value) is { } text)
            {
                array[i] = text;
            }
        }
    }

    private static void WalkArray(JsonArray array, Type elementType, string path, List<string>? unknown)
    {
        if (elementType == typeof(string))
        {
            WalkTextArray(array);
            return;
        }

        var isRule = elementType == typeof(PatchRule);
        if (!isRule && !IsModelClass(elementType))
        {
            return;
        }

        var nulls = new List<int>();
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject item)
            {
                if (array[i] is null)
                {
                    nulls.Add(i);
                }

                continue;
            }

            var itemPath = path + "[" + i + "]";
            if (isRule)
            {
                WalkRule(item, itemPath, unknown);
            }
            else
            {
                WalkObject(item, elementType, itemPath, unknown);
            }
        }

        for (var n = nulls.Count - 1; n >= 0; n--)
        {
            array.RemoveAt(nulls[n]);
        }
    }

    private static void WalkRule(JsonObject rule, string path, List<string>? unknown)
    {
        // Mehrere Schreibweisen ("KIND" und "kind", "Id" und "id"): die letzte gilt, wie bei doppelten Namen.
        var keys = rule.Select(p => p.Key).ToList();
        var kindNames = keys.Where(k => string.Equals(k, "kind", StringComparison.OrdinalIgnoreCase)).ToList();
        var id = keys.LastOrDefault(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)) is { } idKey
            && rule[idKey] is JsonValue idValue && idValue.TryGetValue<string>(out var idText) ? idText : null;
        var label = string.IsNullOrEmpty(id) ? "at " + path : "'" + id + "'";

        if (kindNames.Count == 0 || rule[kindNames[^1]] is not { } kindNode)
        {
            throw new JsonException("Rule " + label + " has no \"kind\" (expected one of: " + string.Join(", ", RuleKinds.Values.Select(v => v.Kind)) + ").");
        }

        var kindText = kindNode is JsonValue kindValue && kindValue.TryGetValue<string>(out var text) ? text : kindNode.ToJsonString();
        if (!RuleKinds.TryGetValue(kindText, out var kind))
        {
            throw new JsonException("Rule " + label + " has the unknown kind \"" + kindText + "\" (expected one of: " + string.Join(", ", RuleKinds.Values.Select(v => v.Kind)) + ").");
        }

        // kind als erstes Feld und in der Schreibweise des Modells: System.Text.Json liest den Unterscheidungswert nur so.
        var others = keys.Where(k => !kindNames.Contains(k)).ToList();
        var moved = others.Select(k => (Key: k, Node: rule[k])).ToList();
        foreach (var key in keys)
        {
            rule.Remove(key);
        }

        rule.Add("kind", kind.Kind);
        foreach (var (key, node) in moved)
        {
            rule.Add(key, node);
        }

        WalkObject(rule, kind.Type, path, unknown, isRule: true);
    }

    private static bool IsModelClass(Type type) =>
        type.IsClass && type != typeof(string) && type.Namespace == typeof(PatchRecipe).Namespace && !typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>Bekannte Eigenschaften eines Modelltyps, nach ihrem camelCase-Namen (ohne Beachtung der Schreibweise) nachschlagbar.</summary>
    private static IReadOnlyDictionary<string, PropertyInfo> KnownProperties(Type type)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var result = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.SetMethod is not { IsPublic: true }
                    || property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
                {
                    continue;
                }

                result[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property;
            }

            PropertyCache[type] = result;
            return result;
        }
    }
}
