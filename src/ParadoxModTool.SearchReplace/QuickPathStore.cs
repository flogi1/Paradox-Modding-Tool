using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.SearchReplace;

/// <summary>
/// EIN frei konfigurierbarer Schnellzugriff-Button im "Ordner (Dateien)"-Modus von
/// SearchReplaceWindow (Nutzer-Feedback: "Darüber hinaus sollte es 4 weitere Buttons geben, wo
/// man den Pfad frei konfigurieren kann, den man speichern will."). <see cref="Path"/> ist
/// <see langword="null"/>, solange der Nutzer für diesen Button noch keinen Pfad festgelegt hat -
/// der Button zeigt dann seinen (ggf. noch unveränderten Standard-)Namen, ist aber deaktiviert,
/// bis über den kleinen "✎"-Button ein Pfad zugewiesen wurde.
/// </summary>
public sealed class QuickPathButtonState
{
    public string Label { get; set; } = string.Empty;
    public string? Path { get; set; }
}

/// <summary>
/// Gesamteinstellungen für die Schnellzugriff-Buttons (Nutzer-Feedback: "es sollten für das Spiel
/// 4 Default Buttons angezeigt werden ... Darüber hinaus sollte es 4 weitere Buttons geben ...
/// Man sollte auch die Möglichkeit haben die Buttons auszublenden."). Die 4 FESTEN,
/// spielprofil-abhängigen Buttons (Mod-Ordner/in_game/main_menu/loading_screen) werden NICHT hier
/// gespeichert - die kommen direkt aus dem aktiven GameProfile (siehe SearchReplaceWindow.
/// BuildFixedQuickPathButtons) und brauchen daher keine eigene Persistenz.
/// </summary>
public sealed class QuickPathSettings
{
    public bool Visible { get; set; } = true;

    public List<QuickPathButtonState> CustomButtons { get; set; } = CreateDefaultCustomButtons();

    public static List<QuickPathButtonState> CreateDefaultCustomButtons() => new()
    {
        new QuickPathButtonState { Label = "Frei 1" },
        new QuickPathButtonState { Label = "Frei 2" },
        new QuickPathButtonState { Label = "Frei 3" },
        new QuickPathButtonState { Label = "Frei 4" },
    };
}

/// <summary>
/// Speichert/lädt <see cref="QuickPathSettings"/> als JSON-Datei außerhalb des Mod-Ordners unter
/// %APPDATA% - nach demselben Muster wie <see cref="SearchFavoritesStore"/>: eine fehlende oder
/// beschädigte Datei liefert bewusst STANDARDEINSTELLUNGEN (4 leere, umbenennbare Buttons,
/// sichtbar) statt eines Fehlers zurück. Eine geladene, aber unvollständige Liste (z. B. aus einer
/// von Hand bearbeiteten Datei mit weniger als 4 Einträgen) wird defensiv auf genau 4 Einträge
/// aufgefüllt, damit die Anzeige (4 feste UI-Plätze) nie durch eine zu kurze Liste durcheinandergerät.
/// </summary>
public static class QuickPathStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(QuickPathSettings settings, string path)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static QuickPathSettings TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new QuickPathSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<QuickPathSettings>(json, Options) ?? new QuickPathSettings();
            NormalizeCustomButtonCount(settings);
            return settings;
        }
        catch (JsonException)
        {
            return new QuickPathSettings();
        }
        catch (IOException)
        {
            return new QuickPathSettings();
        }
    }

    private static void NormalizeCustomButtonCount(QuickPathSettings settings)
    {
        var defaults = QuickPathSettings.CreateDefaultCustomButtons();
        while (settings.CustomButtons.Count < 4)
        {
            settings.CustomButtons.Add(defaults[settings.CustomButtons.Count]);
        }

        if (settings.CustomButtons.Count > 4)
        {
            settings.CustomButtons = settings.CustomButtons.Take(4).ToList();
        }
    }

    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "search-quickpaths.json");
    }
}
