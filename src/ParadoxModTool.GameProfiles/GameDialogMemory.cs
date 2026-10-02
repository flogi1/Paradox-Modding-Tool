using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Kennung (<c>ClientGuid</c>) für Datei- und Ordnerdialoge. Windows merkt sich den zuletzt benutzten Ordner pro Kennung; ohne
/// eigene Kennung teilen sich alle Spiele denselben Ordner, weil jedes Spielfenster dieselbe Programmdatei ist. Die Kennung ergibt
/// sich fest aus Spiel und Zweck des Dialogs, damit jedes Spiel und jeder Dialog seinen eigenen letzten Ordner behält.
/// </summary>
public static class GameDialogMemory
{
    /// <summary>Spiel dieses Programmfensters; wird beim Start gesetzt.</summary>
    public static string? CurrentGameId { get; set; }

    /// <summary>Kennung für den Dialog <paramref name="purpose"/> im Spiel dieses Fensters.</summary>
    public static Guid For(string purpose) => For(CurrentGameId, purpose);

    /// <summary>Kennung für den Dialog <paramref name="purpose"/> im Spiel <paramref name="gameId"/> (Groß-/Kleinschreibung egal).</summary>
    public static Guid For(string? gameId, string purpose)
    {
        var text = $"ParadoxModTool|{(gameId ?? string.Empty).ToLowerInvariant()}|{purpose}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return new Guid(hash);
    }
}
