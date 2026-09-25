namespace ParadoxModTool.Mods;

/// <summary>Ein Hinweis im Veröffentlichen-Fenster. <see cref="Blocking"/> sperrt den Upload.</summary>
public sealed record PublishHint(string Text, bool Blocking);

/// <summary>Der Zustand, aus dem <see cref="PublishReadiness.Evaluate"/> die Hinweise ableitet. Die Fensterlogik füllt ihn aus Dateien, Einstellungen und Eingabefeldern.</summary>
/// <param name="Metadata">Die gespeicherte Metadaten-Datei; <see langword="null"/>, wenn sie fehlt oder nicht lesbar ist.</param>
/// <param name="UseSteamClient">Der Upload läuft über den Steam-Client; SteamCMD und Benutzername sind dann ohne Bedeutung.</param>
/// <param name="ClientProblem">Warum der Steam-Client nicht benutzbar ist (Text für den Nutzer); <see langword="null"/>, wenn er bereit ist.</param>
/// <param name="WorkshopIdOwner">Der Name einer anderen Mod der Verwaltung, der dieselbe Workshop-ID schon gehört; <see langword="null"/>, wenn keine andere Mod sie führt.</param>
/// <param name="VersionForChangelog">Die Version, für die der Changelog-Abschnitt gesucht wurde (die aktuell im Feld stehende).</param>
public sealed record PublishReadinessInput(
    string? AppId,
    ModMetadata? Metadata,
    bool MetadataUnsaved,
    string? WorkshopIdText,
    bool SteamCmdReady,
    string? UserName,
    string? VersionForChangelog,
    bool ChangelogHasVersion,
    bool ChangelogExists,
    bool IsFirstPublish,
    bool PreviewExists,
    bool PublishedOnSteam,
    bool NothingToUpload,
    bool UseSteamClient = false,
    string? ClientProblem = null,
    string? WorkshopIdOwner = null);

/// <summary>Die Hinweise vor dem Veröffentlichen und ob der Upload erlaubt ist.</summary>
public sealed record PublishReadiness(IReadOnlyList<PublishHint> Hints)
{
    public bool CanPublish => Hints.All(h => !h.Blocking);

    public static PublishReadiness Evaluate(PublishReadinessInput input)
    {
        var hints = new List<PublishHint>();

        if (string.IsNullOrWhiteSpace(input.AppId))
        {
            hints.Add(new PublishHint("Das Spielprofil enthält keine Steam-AppID.", true));
        }

        var versionValid = false;
        if (input.Metadata is null)
        {
            hints.Add(new PublishHint("Keine lesbare metadata.json gefunden.", true));
        }
        else if (!VersionNumber.TryParse(input.Metadata.Version, out _))
        {
            hints.Add(new PublishHint("metadata.json enthält keine gültige Version.", true));
        }
        else
        {
            versionValid = true;
        }

        if (input.MetadataUnsaved)
        {
            hints.Add(new PublishHint("metadata.json hat ungespeicherte Änderungen. Speichere sie vor dem Veröffentlichen.", true));
        }

        var idText = input.WorkshopIdText?.Trim();
        if (!string.IsNullOrEmpty(idText) && !idText.All(char.IsAsciiDigit))
        {
            hints.Add(new PublishHint("Die Workshop-ID muss eine Zahl sein.", true));
        }

        if (!string.IsNullOrWhiteSpace(input.WorkshopIdOwner))
        {
            hints.Add(new PublishHint($"Diese Workshop-ID gehört schon zur Mod „{input.WorkshopIdOwner}“. Ein Upload würde deren Workshop-Seite überschreiben.", true));
        }

        if (input.UseSteamClient)
        {
            if (!string.IsNullOrWhiteSpace(input.ClientProblem))
            {
                hints.Add(new PublishHint(input.ClientProblem, true));
            }
        }
        else
        {
            if (!input.SteamCmdReady)
            {
                hints.Add(new PublishHint("SteamCMD ist nicht eingerichtet.", true));
            }

            if (string.IsNullOrWhiteSpace(input.UserName))
            {
                hints.Add(new PublishHint("Der Steam-Benutzername fehlt.", true));
            }
        }

        if (input.ChangelogExists && !input.ChangelogHasVersion && !string.IsNullOrWhiteSpace(input.VersionForChangelog))
        {
            hints.Add(new PublishHint($"Im Changelog gibt es keinen Abschnitt für Version {input.VersionForChangelog}. Die Changenote bleibt sonst leer.", false));
        }

        if (input.IsFirstPublish && !input.PreviewExists)
        {
            hints.Add(new PublishHint("Das Vorschaubild fehlt. Beim ersten Veröffentlichen braucht die Workshop-Seite eines (Feld picture in der metadata.json).", true));
        }

        if (versionValid && input.PublishedOnSteam)
        {
            hints.Add(new PublishHint($"Version {input.Metadata!.Version} ist schon auf Steam veröffentlicht. Ein erneuter Upload überschreibt den Inhalt.", false));
        }

        if (input.NothingToUpload)
        {
            hints.Add(new PublishHint("Der Mod-Ordner enthält nach dem Ausschluss keine Dateien.", true));
        }

        return new PublishReadiness(hints);
    }
}
