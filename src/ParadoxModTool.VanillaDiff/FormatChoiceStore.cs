using System.Text.Json;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Pro-Datei-Einstellung, ob beim Merge die Mod-Formatierung beibehalten wird (Abschnitt 7:
/// "Grundeinstellung sollte immer sein, dass die Vanilla Grund-Formatierung übernommen wird
/// und die Mod Formatierung nur, wenn es gute Gründe dafür gibt"). Schlüssel = Dateipfad
/// relativ zum Mod-Ordner, "/"-getrennt; fehlt ein Eintrag, ist der Standard Vanilla-Formatierung
/// (also <see langword="false"/>).
/// </summary>
public sealed class FormatChoiceSettings
{
    public Dictionary<string, bool> PreferModFormatting { get; set; } = new();
}

/// <summary>
/// Speichert/lädt <see cref="FormatChoiceSettings"/> als JSON-Datei im Mod-Ordner unter
/// ".pmt-format-choice.json" - nach demselben Muster wie
/// <see cref="ParadoxModTool.SearchReplace.QuickPathStore"/>: eine fehlende oder beschädigte
/// Datei liefert bewusst STANDARDEINSTELLUNGEN (leere Zuordnung, also überall Vanilla-Formatierung)
/// statt eines Fehlers zurück.
/// </summary>
public static class FormatChoiceStore
{
    private const string FileName = ".pmt-format-choice.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(FormatChoiceSettings settings, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(path, json);
    }

    public static FormatChoiceSettings TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new FormatChoiceSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FormatChoiceSettings>(json, Options) ?? new FormatChoiceSettings();
        }
        catch (JsonException)
        {
            return new FormatChoiceSettings();
        }
        catch (IOException)
        {
            return new FormatChoiceSettings();
        }
    }

    public static string GetDefaultFilePath(string modRoot) => Path.Combine(modRoot, FileName);
}
