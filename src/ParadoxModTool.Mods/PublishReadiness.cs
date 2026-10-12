namespace ParadoxModTool.Mods;

/// <summary>Ein Hinweis im Veröffentlichen-Fenster. <see cref="Blocking"/> sperrt den Upload.</summary>
public sealed record PublishHint(string Text, bool Blocking);

/// <summary>Der Zustand, aus dem <see cref="PublishReadiness.Evaluate"/> die Hinweise ableitet. Die Fensterlogik füllt ihn aus Dateien, Einstellungen und Eingabefeldern.</summary>
/// <param name="Metadata">Die gespeicherte Metadaten-Datei; <see langword="null"/>, wenn sie fehlt oder nicht lesbar ist.</param>
/// <param name="UseSteamClient">Der Upload läuft über den Steam-Client; SteamCMD und Benutzername sind dann ohne Bedeutung.</param>
/// <param name="ClientProblem">Warum der Steam-Client nicht benutzbar ist (Text für den Nutzer); <see langword="null"/>, wenn er bereit ist.</param>
/// <param name="WorkshopIdOwner">Der Name einer anderen Mod der Verwaltung, der dieselbe Workshop-ID schon gehört; <see langword="null"/>, wenn keine andere Mod sie führt.</param>
/// <param name="VersionForChangelog">Die Version, für die der Changelog-Abschnitt gesucht wurde (die aktuell im Feld stehende).</param>
/// <param name="ClassicMetadata">Das Spiel nutzt die klassische <c>.mod</c>-Datei (EU IV, CK III); die Hinweise nennen dann sie statt der <c>metadata.json</c>.</param>
/// <param name="ClassicDescriptorMissing">Beim klassischen Format fehlt die <c>descriptor.mod</c> im Mod-Ordner. Hochgeladen wird nur der Ordner, ohne sie erkennt der Launcher die Mod nicht.</param>
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
    string? WorkshopIdOwner = null,
    bool ClassicMetadata = false,
    bool ClassicDescriptorMissing = false);

/// <summary>Die Hinweise vor dem Veröffentlichen und ob der Upload erlaubt ist.</summary>
public sealed record PublishReadiness(IReadOnlyList<PublishHint> Hints)
{
    public bool CanPublish => Hints.All(h => !h.Blocking);

    public static PublishReadiness Evaluate(PublishReadinessInput input)
    {
        var hints = new List<PublishHint>();

        if (string.IsNullOrWhiteSpace(input.AppId))
        {
            hints.Add(new PublishHint(ModsTexts.PublishNoAppId, true));
        }

        var versionValid = false;
        if (input.Metadata is null)
        {
            hints.Add(new PublishHint(ModsTexts.NoReadableMetadataFor(input.ClassicMetadata), true));
        }
        else if (!VersionNumber.TryParse(input.Metadata.Version, out _))
        {
            hints.Add(new PublishHint(ModsTexts.NoValidVersionFor(input.ClassicMetadata), true));
        }
        else
        {
            versionValid = true;
        }

        // Ohne lesbare Metadaten steht schon der Hinweis oben; der Ordner ist dann ohnehin unklar.
        if (input.ClassicMetadata && input.ClassicDescriptorMissing && input.Metadata is not null)
        {
            hints.Add(new PublishHint(ModsTexts.PublishDescriptorMissing, true));
        }

        if (input.MetadataUnsaved)
        {
            hints.Add(new PublishHint(input.ClassicMetadata ? ModsTexts.PublishUnsavedClassic : ModsTexts.PublishUnsaved, true));
        }

        var idText = input.WorkshopIdText?.Trim();
        if (!string.IsNullOrEmpty(idText) && !idText.All(char.IsAsciiDigit))
        {
            hints.Add(new PublishHint(ModsTexts.PublishWorkshopIdNumber, true));
        }

        if (!string.IsNullOrWhiteSpace(input.WorkshopIdOwner))
        {
            hints.Add(new PublishHint(ModsTexts.PublishWorkshopIdOwned(input.WorkshopIdOwner), true));
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
                hints.Add(new PublishHint(ModsTexts.PublishSteamCmdMissing, true));
            }

            if (string.IsNullOrWhiteSpace(input.UserName))
            {
                hints.Add(new PublishHint(ModsTexts.PublishSteamUserMissing, true));
            }
        }

        if (input.ChangelogExists && !input.ChangelogHasVersion && !string.IsNullOrWhiteSpace(input.VersionForChangelog))
        {
            hints.Add(new PublishHint(ModsTexts.PublishNoChangelogSection(input.VersionForChangelog), false));
        }

        if (input.IsFirstPublish && !input.PreviewExists)
        {
            hints.Add(new PublishHint(input.ClassicMetadata ? ModsTexts.PublishNoPreviewClassic : ModsTexts.PublishNoPreview, true));
        }

        if (versionValid && input.PublishedOnSteam)
        {
            hints.Add(new PublishHint(ModsTexts.PublishAlreadyPublished(input.Metadata!.Version), false));
        }

        if (input.NothingToUpload)
        {
            hints.Add(new PublishHint(ModsTexts.PublishNoFiles, true));
        }

        return new PublishReadiness(hints);
    }
}
