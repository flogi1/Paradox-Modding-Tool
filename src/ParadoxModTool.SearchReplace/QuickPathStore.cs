using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.SearchReplace;

/// <summary>Ein eigener Schnellzugriff-Knopf (Suchen in Dateien, Compare-Reiter): Name, Ordner und die Spiele, in denen er erscheint.</summary>
public sealed class QuickPathButtonState
{
    public string Label { get; set; } = string.Empty;
    public string? Path { get; set; }

    /// <summary>
    /// Kennungen der Spiele (Spielprofile), in denen der Knopf angezeigt wird. Leer = in allen Spielen, auch in später hinzukommenden;
    /// so laden auch Dateien aus der Zeit vor dieser Einstellung unverändert.
    /// </summary>
    public List<string> Games { get; set; } = new();

    /// <summary>Ob der Knopf im Spiel <paramref name="gameId"/> angezeigt wird; ohne bekanntes Spiel (<see langword="null"/>) immer.</summary>
    public bool AppliesTo(string? gameId) => Games.Count == 0 || gameId is null || Games.Contains(gameId, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Einstellungen der Schnellzugriff-Knöpfe: Sichtbarkeit und die eigenen Knöpfe, beliebig viele (Nutzerwunsch 2026-09-30; vorher
/// genau 4 Plätze "Frei 1–4"). Die festen, spielprofil-abhängigen Knöpfe (Mod-Ordner, in_game, main_menu, loading_screen) kommen
/// aus dem aktiven Spielprofil und werden hier nicht gespeichert.
/// </summary>
public sealed class QuickPathSettings
{
    public bool Visible { get; set; } = true;

    public List<QuickPathButtonState> CustomButtons { get; set; } = new();

    /// <summary>Verschiebt einen eigenen Knopf um <paramref name="delta"/> Plätze; an den Enden bleibt er stehen.</summary>
    public void Move(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= CustomButtons.Count || target < 0 || target >= CustomButtons.Count)
        {
            return;
        }

        var button = CustomButtons[index];
        CustomButtons.RemoveAt(index);
        CustomButtons.Insert(target, button);
    }
}

/// <summary>
/// Speichert/lädt <see cref="QuickPathSettings"/> als JSON unter %APPDATA% (wie <see cref="SearchFavoritesStore"/>): eine fehlende
/// oder beschädigte Datei liefert Standardeinstellungen statt eines Fehlers. Beim Laden fallen Knöpfe ohne Ordner weg; das sind die
/// leeren Plätze "Frei 1–4" aus der Zeit mit fester Anzahl, neue Knöpfe entstehen über "+" immer mit Ordner.
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
            settings.CustomButtons = (settings.CustomButtons ?? new List<QuickPathButtonState>())
                .Where(b => b is not null && !string.IsNullOrWhiteSpace(b.Path))
                .ToList();
            foreach (var button in settings.CustomButtons)
            {
                button.Games = (button.Games ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

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

    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "search-quickpaths.json");
    }
}
