using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Patches;

/// <summary>
/// Liest und schreibt Rezepte (<c>&lt;id&gt;.json</c>) und ihr Gedächtnis (<c>&lt;id&gt;.memory.json</c>) in einem Rezeptordner. Der Ordner liegt
/// normalerweise außerhalb der Mod (siehe StorageLocations.ResolvePatchRecipesDirectory), damit Rezepte nie mit hochgeladen werden;
/// <see cref="Folder"/> ist der frühere Ort im Mod-Ordner.
/// </summary>
public static class PatchRecipeStore
{
    public const string FolderName = ".pmt-patches";

    private const string MemorySuffix = ".memory.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Optionen zum Lesen: wie <see cref="Options"/>, dazu Kommentare, Endkommas und Namen in beliebiger Schreibweise.</summary>
    private static readonly JsonSerializerOptions ReadOptions = new(Options)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Der frühere (und mit "im Mod-Ordner speichern" weiter genutzte) Rezeptordner <c>&lt;mod&gt;/.pmt-patches</c>.</summary>
    public static string Folder(string modRoot) => Path.Combine(modRoot, FolderName);

    public static string RecipePath(string folder, string id) => Path.Combine(folder, id + ".json");

    public static string MemoryPath(string folder, string id) => Path.Combine(folder, id + MemorySuffix);

    /// <summary>Alle Rezepte der Mod, nach Id sortiert; unlesbare Dateien landen mit Dateiname in <paramref name="errors"/>.</summary>
    public static IReadOnlyList<PatchRecipe> LoadAll(string folder, List<string> errors)
    {
        var result = new List<PatchRecipe>();
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (file.EndsWith(MemorySuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                result.Add(Deserialize(File.ReadAllText(file)));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                errors.Add(Path.GetFileName(file) + ": " + ex.Message);
            }
        }

        return result;
    }

    /// <summary>Hat die Mod mindestens eine Rezeptdatei (Gedächtnis-Dateien zählen nicht)?</summary>
    public static bool HasRecipes(string folder)
    {
        return Directory.Exists(folder)
            && Directory.EnumerateFiles(folder, "*.json").Any(f => !f.EndsWith(MemorySuffix, StringComparison.OrdinalIgnoreCase));
    }

    public static void Save(string folder, PatchRecipe recipe) =>
        WriteAtomic(RecipePath(folder, recipe.Id), JsonSerializer.Serialize(recipe, Options));

    public static void Delete(string folder, string id)
    {
        File.Delete(RecipePath(folder, id));
        File.Delete(MemoryPath(folder, id));
    }

    /// <summary>Das Gedächtnis des Rezepts; leer, wenn die Datei fehlt oder unlesbar ist.</summary>
    public static PatchMemory LoadMemory(string folder, string id)
    {
        var path = MemoryPath(folder, id);
        if (!File.Exists(path))
        {
            return new PatchMemory();
        }

        try
        {
            var memory = JsonSerializer.Deserialize<PatchMemory>(File.ReadAllText(path), Options) ?? new PatchMemory();
            return new PatchMemory
            {
                Units = new Dictionary<string, string>(memory.Units, StringComparer.Ordinal),
                ListEntries = new Dictionary<string, List<ListEntryRecord>>(memory.ListEntries, StringComparer.Ordinal),
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new PatchMemory();
        }
    }

    /// <summary>Text einer Gedächtnis-Datei, die es gibt, die sich aber nicht lesen lässt; sonst null. <see cref="LoadMemory"/> liefert dann ein leeres Gedächtnis.</summary>
    public static string? BrokenMemoryText(string folder, string id)
    {
        var path = MemoryPath(folder, id);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path);
            try
            {
                JsonSerializer.Deserialize<PatchMemory>(text, Options);
                return null;
            }
            catch (JsonException)
            {
                return text;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Zieht Rezepte und Gedächtnis aus dem früheren Ort <c>&lt;mod&gt;/.pmt-patches</c> nach <paramref name="folder"/>. Eine Datei, die es am
    /// Ziel schon gibt, bleibt am alten Ort liegen und wird nie überschrieben; der alte Ordner wird nur gelöscht, wenn er danach leer ist.
    /// </summary>
    public static RecipeMove MoveFromModFolder(string modRoot, string folder)
    {
        var source = Folder(modRoot);
        if (!Directory.Exists(source)
            || string.Equals(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return new RecipeMove(0, []);
        }

        var moved = 0;
        var kept = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList())
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(folder, name);
            if (File.Exists(target))
            {
                kept.Add(name);
                continue;
            }

            Directory.CreateDirectory(folder);
            File.Move(file, target);
            moved++;
        }

        if (!Directory.EnumerateFileSystemEntries(source).Any())
        {
            Directory.Delete(source);
        }

        return new RecipeMove(moved, kept);
    }

    public static string SerializeMemory(PatchMemory memory) => JsonSerializer.Serialize(memory, Options);

    /// <summary>Das Rezept im Speicherformat (z. B. als Fingerabdruck für den Wächter).</summary>
    public static string Serialize(PatchRecipe recipe) => JsonSerializer.Serialize(recipe, Options);

    /// <summary>
    /// Liest ein Rezept toleranter als <see cref="Save"/> schreibt (Kommentare, Endkommas, <c>kind</c> an beliebiger Stelle und Schreibweise,
    /// Enum-Werte in beliebiger Schreibweise, Null-Listen = leer). Eigenschaften, die das Modell nicht kennt, werden als
    /// <c>&lt;JSON-Pfad&gt;: &lt;Name&gt;</c> in <paramref name="unknownProperties"/> gesammelt und nicht übernommen.
    /// Wirft <see cref="JsonException"/>, wenn das Rezept nicht lesbar ist (auch bei unbekannter oder fehlender Regelart).
    /// </summary>
    public static PatchRecipe Deserialize(string json, List<string>? unknownProperties = null) =>
        PatchRecipeReader.Read(json, ReadOptions, unknownProperties);

    /// <summary>Tiefe Kopie über dieselbe JSON-Form wie beim Speichern.</summary>
    public static PatchRecipe Clone(PatchRecipe recipe)
    {
        var copy = JsonSerializer.Deserialize<PatchRecipe>(JsonSerializer.Serialize(recipe, Options), Options)!;
        copy.OnlyRowKey = recipe.OnlyRowKey;
        return copy;
    }

    private static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>Ergebnis von <see cref="PatchRecipeStore.MoveFromModFolder"/>: verschobene Dateien und am alten Ort belassene Dateinamen.</summary>
public sealed record RecipeMove(int Moved, IReadOnlyList<string> Kept);
