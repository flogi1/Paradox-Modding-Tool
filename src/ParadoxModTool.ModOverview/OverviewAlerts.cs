using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.ModOverview;

/// <summary>Art eines Hinweises; die Reihenfolge ist auch die Anzeige-Reihenfolge innerhalb einer Mod.</summary>
public enum OverviewAlertKind
{
    MissingFolder,
    MissingDescriptor,
    OutdatedGameVersion,
    ReleaseStatus,
    SteamPage,
    SubmodWithoutDependency,
    DependencyNotInProject,

    /// <summary>Eine Bug-Quelle des Projekts war beim letzten Abruf nicht erreichbar (Fehler in <see cref="OverviewAlert.Detail"/>).</summary>
    BugSourceFailed,
}

/// <summary>Ein Hinweis im Alert-Bereich, einer Mod eines Spiels zugeordnet.</summary>
/// <param name="Detail">Zusätzlicher Text für den Tooltip (z. B. die Fehlermeldung einer Bug-Quelle); sonst <see langword="null"/>.</param>
public sealed record OverviewAlert(OverviewAlertKind Kind, string GameId, string ModId, string Text, string? Detail = null);

/// <summary>Fehler einer Bug-Quelle beim Abruf in der Übersicht, dem Projekt (Hauptmod) zugeordnet.</summary>
public sealed record OverviewBugSourceError(OverviewMod Root, string SourceKey, string Error);

/// <summary>Berechnet die Hinweise zu einem Stand der Übersicht; reine Funktion ohne Dateizugriff.</summary>
public static class OverviewAlerts
{
    /// <summary>Hinweise zu nicht erreichbaren Bug-Quellen; die Fehlermeldung steht in <see cref="OverviewAlert.Detail"/>.</summary>
    public static IReadOnlyList<OverviewAlert> ForSourceErrors(IEnumerable<OverviewBugSourceError> errors) =>
        errors.Select(e => new OverviewAlert(OverviewAlertKind.BugSourceFailed, e.Root.GameId, e.Root.Mod.Id, OverviewTexts.AlertBugSourceFailed(e.SourceKey), e.Error)).ToList();

    /// <summary>Sortiert nach Spiel (Reihenfolge in <see cref="OverviewSnapshot.Games"/>), dann Mod-Name, dann Art.</summary>
    public static IReadOnlyList<OverviewAlert> Compute(OverviewSnapshot snapshot)
    {
        var gameOrder = snapshot.Games.Select((g, i) => (g.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return snapshot.Mods
            .OrderBy(m => gameOrder.TryGetValue(m.GameId, out var index) ? index : int.MaxValue)
            .ThenBy(m => m.Mod.Name, StringComparer.CurrentCultureIgnoreCase)
            .SelectMany(m => For(m, snapshot).OrderBy(a => a.Kind))
            .ToList();
    }

    private static IEnumerable<OverviewAlert> For(OverviewMod mod, OverviewSnapshot snapshot)
    {
        OverviewAlert Alert(OverviewAlertKind kind, string text) => new(kind, mod.GameId, mod.Mod.Id, text);

        foreach (var path in mod.MissingPaths)
        {
            yield return Alert(OverviewAlertKind.MissingFolder, OverviewTexts.AlertMissingFolder(path));
        }

        if (mod.Metadata is null)
        {
            yield return Alert(OverviewAlertKind.MissingDescriptor, OverviewTexts.AlertMissingDescriptor);
        }

        if (mod.VersionSupport == VersionSupport.Outdated)
        {
            var installed = snapshot.Games.FirstOrDefault(g => g.Id == mod.GameId)?.InstalledVersion ?? string.Empty;
            yield return Alert(OverviewAlertKind.OutdatedGameVersion, OverviewTexts.AlertOutdated(mod.Metadata?.SupportedGameVersion ?? string.Empty, installed));
        }

        var hints = (mod.ReleaseStatusProblem is { } problem ? new[] { problem } : Array.Empty<string>())
            .Concat(mod.ReleaseStatus?.Hints ?? Array.Empty<string>())
            .ToList();
        if (hints.Count > 0)
        {
            yield return Alert(OverviewAlertKind.ReleaseStatus, OverviewTexts.AlertReleaseStatus(string.Join("; ", hints)));
        }

        if (mod.WorkshopId is { Length: > 0 } && mod.Steam?.State is WorkshopPageState.Hidden or WorkshopPageState.NotFound)
        {
            yield return Alert(OverviewAlertKind.SteamPage,
                mod.Steam.State == WorkshopPageState.Hidden ? OverviewTexts.AlertSteamHidden : OverviewTexts.AlertSteamNotFound);
        }

        var root = mod.ProjectRoot;
        if (mod.Mod.Role == ModRole.Submod && !ReferenceEquals(root, mod) && !mod.ResolvedDependencies.Any(d => ReferenceEquals(d.Target, root)))
        {
            yield return Alert(OverviewAlertKind.SubmodWithoutDependency, OverviewTexts.AlertSubmodWithoutDependency(root.Mod.Name));
        }

        foreach (var (dependency, target) in mod.ResolvedDependencies)
        {
            if (target is not null && !ReferenceEquals(target, root))
            {
                yield return Alert(OverviewAlertKind.DependencyNotInProject, OverviewTexts.AlertDependencyNotInProject(dependency.DisplayName));
            }
        }
    }
}
