using ParadoxModTool.Localization;

namespace ParadoxModTool.BugTracking;

/// <summary>
/// Texte der Discord-Anbindung, die in der Oberfläche ankommen. Die Keys stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche
/// Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class DiscordTexts
{
    public static string ResponseUnreadable => UiText.Get("discord.responseUnreadable", "Die Antwort von Discord war nicht lesbar.");
}
