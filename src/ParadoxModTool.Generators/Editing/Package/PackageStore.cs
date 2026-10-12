using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Pakete anlegen, kopieren und löschen (Spec Teil 5a, Abschnitt 2). Ordnername = Paket-Kennung.</summary>
public static class PackageStore
{
    private static readonly Regex IdPattern = new("^[a-z0-9_-]{1,40}$", RegexOptions.CultureInvariant);

    public static bool IsValidPackageId(string id) => IdPattern.IsMatch(id);

    /// <summary>Kennung schon vergeben: als mitgeliefertes Paket (gewinnt immer) oder als eigenes Paket bzw. Datenordner.</summary>
    public static bool IdTaken(string id, string shippedRoot, string packagesRoot) =>
        Directory.Exists(Path.Combine(shippedRoot, id)) || Directory.Exists(Path.Combine(packagesRoot, id));

    /// <summary>Legt ein Mindestpaket an: ein Kopf-Feld <c>key</c> als Schlüsselfeld, beide Ziele, keine Ausgaben, leerer Vorlagenordner.</summary>
    public static string Create(string packagesRoot, string id, string name, string gameId)
    {
        var folder = Path.Combine(packagesRoot, id);
        if (Directory.Exists(folder))
        {
            throw new IOException(folder);
        }

        Directory.CreateDirectory(Path.Combine(folder, "templates"));
        var json = new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["gameId"] = gameId,
            ["keyField"] = "key",
            ["head"] = new JsonArray(new JsonObject { ["id"] = "key", ["label"] = "Key", ["type"] = "text", ["required"] = true }),
            ["targets"] = new JsonArray("mod", "output"),
            ["outputs"] = new JsonArray(),
        };
        File.WriteAllText(Path.Combine(folder, "package.json"), json.ToJsonString(new() { WriteIndented = true }).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
        return folder;
    }

    /// <summary>
    /// Kopiert Paketdateien aus <paramref name="sourceFolder"/>, auf Wunsch Arbeitsstände und Assets aus <paramref name="sourceDataFolder"/>
    /// (bei mitgelieferten Paketen der Datenordner unter der alten Kennung), und setzt Kennung und Namen.
    /// </summary>
    public static string Copy(string sourceFolder, string sourceDataFolder, string packagesRoot, string newId, string newName, bool withStates, bool withAssets)
    {
        var target = Path.Combine(packagesRoot, newId);
        if (Directory.Exists(target))
        {
            throw new IOException(target);
        }

        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(sourceFolder, "package.json"), Path.Combine(target, "package.json"));
        CopyFileIfExists(Path.Combine(sourceFolder, "functions.sbn"), Path.Combine(target, "functions.sbn"));
        CopyTree(Path.Combine(sourceFolder, "templates"), Path.Combine(target, "templates"));
        Directory.CreateDirectory(Path.Combine(target, "templates"));
        if (withStates)
        {
            CopyTree(Path.Combine(sourceDataFolder, "states"), Path.Combine(target, "states"));
        }

        if (withAssets)
        {
            CopyTree(Path.Combine(sourceDataFolder, "assets"), Path.Combine(target, "assets"));
        }

        var document = PackageDocument.Load(target);
        document.ChangeIdKeepingMarkers(newId);
        document.Name = LocalizedText.Plain(newName);
        document.Save();
        return target;
    }

    /// <summary>Zahl der Arbeitsstände und Asset-Dateien im Datenordner (für die Rückfrage beim Löschen).</summary>
    public static (int States, int Assets) Contents(string dataFolder)
    {
        var states = Path.Combine(dataFolder, "states");
        var assets = Path.Combine(dataFolder, "assets");
        return (
            Directory.Exists(states) ? Directory.GetFiles(states, "*.json").Length : 0,
            Directory.Exists(assets) ? Directory.GetFiles(assets, "*", SearchOption.AllDirectories).Length : 0);
    }

    /// <summary>Löscht den Paketordner über <paramref name="deleteDirectory"/> (die App gibt das Löschen in den Papierkorb hinein).</summary>
    public static void Delete(string folder, Action<string> deleteDirectory) => deleteDirectory(folder);

    internal static void CopyTree(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
        }
    }

    private static void CopyFileIfExists(string source, string target)
    {
        if (File.Exists(source))
        {
            File.Copy(source, target);
        }
    }
}
