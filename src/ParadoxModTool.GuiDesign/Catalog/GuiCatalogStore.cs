using System.Text.Json;

namespace ParadoxModTool.GuiDesign.Catalog;

/// <summary>Zwischenspeicher des Katalogs unter <c>%APPDATA%\ParadoxModTool\games\&lt;id&gt;\gui\catalog.json</c>.</summary>
public static class GuiCatalogStore
{
    public static string DefaultPath(string gameId) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "games", gameId, "gui", "catalog.json");

    /// <summary>Gespeicherter Katalog, wenn Version und Fingerabdruck passen; sonst null (neu lernen).</summary>
    public static GuiPropertyCatalog? Load(string path, string fingerprint)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var catalog = JsonSerializer.Deserialize<GuiPropertyCatalog>(File.ReadAllText(path));
            return catalog is not null && catalog.Version == GuiPropertyCatalog.CurrentVersion && catalog.Fingerprint == fingerprint ? catalog : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void Save(string path, GuiPropertyCatalog catalog)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(catalog));
        File.Move(temp, path, overwrite: true);
    }
}
