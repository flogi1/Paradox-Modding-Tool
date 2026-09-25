namespace ParadoxModTool.SteamIntegration;

/// <summary>Die Steamworks-Bibliothek ist nicht benutzbar (fehlt, Steam läuft nicht, Funktion fehlt). Die Nachricht ist für den Nutzer gedacht.</summary>
public sealed class SteamworksUnavailableException : Exception
{
    public SteamworksUnavailableException(string message)
        : base(message)
    {
    }
}

/// <summary>Der Status eines laufenden Workshop-Updates (<c>EItemUpdateStatus</c>).</summary>
public enum ItemUpdateStatus
{
    Invalid = 0,
    PreparingConfig = 1,
    PreparingContent = 2,
    UploadingContent = 3,
    UploadingPreviewFile = 4,
    CommittingChanges = 5,
}

public readonly record struct ItemUpdateProgress(ItemUpdateStatus Status, ulong Processed, ulong Total);

/// <param name="Result">Der <c>EResult</c>-Code von Steam; <c>1</c> = OK.</param>
public sealed record CreateItemResult(int Result, ulong PublishedFileId, bool NeedsLegalAgreement);

/// <param name="Result">Der <c>EResult</c>-Code von Steam; <c>1</c> = OK.</param>
/// <param name="PublishedFileId"><c>0</c>, wenn die Bibliothek die ID nicht liefert.</param>
public sealed record SubmitItemResult(int Result, ulong PublishedFileId, bool NeedsLegalAgreement);

/// <summary>Die Steamworks-Funktionen, die der Workshop-Upload braucht. Die echte Umsetzung ist <see cref="SteamworksNative"/>; Tests benutzen eine Fake-Klasse.</summary>
public interface ISteamworksApi : IDisposable
{
    /// <summary>Lädt die Bibliothek und meldet sich beim laufenden Steam-Client an.</summary>
    /// <exception cref="SteamworksUnavailableException">Bibliothek fehlt oder unbrauchbar, Steam läuft nicht oder ist nicht angemeldet.</exception>
    void Init();

    /// <summary>Beendet die Verbindung. Darf mehrfach und auch ohne erfolgreiches <see cref="Init"/> aufgerufen werden.</summary>
    void Shutdown();

    void RunCallbacks();

    ulong CreateItem(uint appId);

    /// <summary>Liefert das Update-Handle; <c>0</c> = ungültig.</summary>
    ulong StartItemUpdate(uint appId, ulong publishedFileId);

    bool SetItemTitle(ulong handle, string title);

    bool SetItemDescription(ulong handle, string description);

    bool SetItemPreview(ulong handle, string previewFile);

    bool SetItemContent(ulong handle, string contentFolder);

    /// <param name="visibility"><c>0</c> öffentlich, <c>1</c> nur Freunde, <c>2</c> privat.</param>
    bool SetItemVisibility(ulong handle, int visibility);

    /// <summary>Liefert das Aufrufhandle für das Ergebnis; <c>0</c> = ungültig.</summary>
    ulong SubmitItemUpdate(ulong handle, string changeNote);

    /// <summary>Ist das Ergebnis des Aufrufs da (auch bei einem Fehler des Aufrufs)?</summary>
    bool IsCallCompleted(ulong call);

    /// <summary><see langword="null"/>, wenn das Ergebnis nicht lesbar ist.</summary>
    CreateItemResult? ReadCreateItemResult(ulong call);

    /// <summary><see langword="null"/>, wenn das Ergebnis nicht lesbar ist.</summary>
    SubmitItemResult? ReadSubmitItemResult(ulong call);

    ItemUpdateProgress GetItemUpdateProgress(ulong handle);
}
