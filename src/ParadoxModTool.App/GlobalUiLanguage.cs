using System.IO;
using System.Text.Json;
using ParadoxModTool.Localization;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Oberflächensprache aus den globalen Einstellungen (settings.json, nicht je Spiel), bevor ein Spiel geöffnet ist: für Startmeldungen,
/// den Aufruf als externer Editor und den Hilfsprozess für den Steam-Upload.
/// </summary>
internal static class GlobalUiLanguage
{
    /// <summary>Lädt die Oberflächentexte (locales/&lt;sprache&gt;.json) in der global eingestellten Sprache; ohne lesbare Einstellung Englisch.</summary>
    public static void Load(SessionRepository repository) =>
        UiText.Load(LocaleSources.Default(), Read(repository));

    /// <summary>Die Sprache aus settings.json; <see langword="null"/> (= Englisch), wenn die Datei fehlt oder nicht lesbar ist.</summary>
    public static string? Read(SessionRepository repository)
    {
        try
        {
            using var stream = new FileStream(repository.SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("UiLanguage", out var language) && language.ValueKind == JsonValueKind.String
                    ? language.GetString()
                    : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
