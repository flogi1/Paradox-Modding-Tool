namespace ParadoxModTool.Mods;

/// <summary>Die drei Versionsstände einer Mod nebeneinander und Hinweise, wo sie nicht zusammenpassen.</summary>
/// <param name="MetadataVersion">Version aus der Metadaten-Datei; <see langword="null"/>, wenn es keine gültige gibt.</param>
/// <param name="ChangelogTop">Version des ersten Versions-Banners im Changelog; <see langword="null"/> ohne Changelog oder ohne Banner.</param>
/// <param name="LatestRelease">Höchste erfasste Release; <see langword="null"/>, wenn keine erfasst ist.</param>
public sealed record ReleaseStatus(string? MetadataVersion, string? ChangelogTop, string? LatestRelease, IReadOnlyList<string> Hints)
{
    /// <param name="changelog">Die Abschnitte des Changelogs; <see langword="null"/>, wenn die Mod keinen Changelog hat (dann gibt es dazu auch keinen Hinweis).</param>
    /// <param name="classic">Das Spiel nutzt die klassische <c>.mod</c>-Datei; die Hinweise nennen dann sie statt der <c>metadata.json</c>.</param>
    /// <param name="descriptorMissing">Beim klassischen Format fehlt die <c>descriptor.mod</c> im Mod-Ordner (siehe <see cref="PublishReadinessInput.ClassicDescriptorMissing"/>).</param>
    public static ReleaseStatus Evaluate(ModMetadata? metadata, IReadOnlyList<ChangelogEntry>? changelog, IReadOnlyList<ReleaseEntry> releases, bool classic = false, bool descriptorMissing = false)
    {
        var hints = new List<string>();
        var metadataValid = VersionNumber.TryParse(metadata?.Version, out var metadataVersion);
        if (metadata is null)
        {
            hints.Add(ModsTexts.NoReadableMetadataFor(classic));
        }
        else if (!metadataValid)
        {
            hints.Add(ModsTexts.NoValidVersionFor(classic));
        }

        if (classic && descriptorMissing && metadata is not null)
        {
            hints.Add(ModsTexts.PublishDescriptorMissing);
        }

        string? top = null;
        if (changelog is not null)
        {
            if (changelog.Count == 0)
            {
                hints.Add(ModsTexts.StatusNoBanners);
            }
            else
            {
                top = changelog[0].Version;
                if (metadataValid)
                {
                    var listed = changelog.Any(e => VersionNumber.TryParse(e.Version, out var v) && v.Equals(metadataVersion));
                    if (!listed)
                    {
                        hints.Add(ModsTexts.StatusMissingSection(metadataVersion.Text));
                    }
                    else if (!VersionNumber.TryParse(top, out var topVersion) || !topVersion.Equals(metadataVersion))
                    {
                        hints.Add(classic ? ModsTexts.StatusMismatchClassic(metadataVersion.Text, top) : ModsTexts.StatusMismatch(metadataVersion.Text, top));
                    }
                }
            }
        }

        if (metadataValid && !releases.Any(r => VersionNumber.TryParse(r.Version, out var v) && v.Equals(metadataVersion)))
        {
            hints.Add(ModsTexts.StatusNotReleased(metadataVersion.Text));
        }

        var latest = releases
            .Select(r => (r.Version, Ok: VersionNumber.TryParse(r.Version, out var v), Number: v))
            .Where(r => r.Ok)
            .OrderByDescending(r => r.Number)
            .Select(r => r.Version)
            .FirstOrDefault();

        return new ReleaseStatus(metadataValid ? metadataVersion.Text : null, top, latest, hints);
    }
}
