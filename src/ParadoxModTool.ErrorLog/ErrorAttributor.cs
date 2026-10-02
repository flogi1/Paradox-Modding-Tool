namespace ParadoxModTool.ErrorLog;

public enum ErrorBucket
{
    OwnMod,
    OtherMod,
    Vanilla,
    Engine,
    Unassigned,
    Unmatched,
}

/// <summary>Zuordnung eines Fehlers. <c>Source</c> ist gesetzt, wenn der Fehler eine Datei nennt und sie gefunden wurde.</summary>
public sealed record ErrorAttribution(ErrorBucket Bucket, ModSource? Mod, ResolvedSource? Source);

/// <summary>
/// Ordnet einen Fehler einer Mod oder Vanilla zu: über die genannte Datei (SourceResolver), sonst über den Namen (Mod des Playsets, in
/// der der Name gesetzt oder benutzt wird; eigene Mods zuerst), sonst Vanilla-Definition. Ohne Datei und Namen: Engine-Meldung.
/// </summary>
public static class ErrorAttributor
{
    public static ErrorAttribution Attribute(ErrorEntry entry, SourceResolver resolver, SymbolIndex index)
    {
        if (entry.Kind is null)
        {
            return new ErrorAttribution(ErrorBucket.Unmatched, null, null);
        }

        if (entry.FilePath is not null)
        {
            var source = resolver.Resolve(entry.FilePath);
            if (source is null)
            {
                return new ErrorAttribution(ErrorBucket.Unassigned, null, null);
            }

            var bucket = source.IsVanilla ? ErrorBucket.Vanilla : source.IsOwn ? ErrorBucket.OwnMod : ErrorBucket.OtherMod;
            return new ErrorAttribution(bucket, source.Mod, source);
        }

        if (entry.Name is not null)
        {
            var occurrences = index.Find(entry.Name).Where(o => o.Role != SymbolRole.Localization).ToList();
            var mod = occurrences.Select(o => o.Mod).FirstOrDefault(m => m is { IsOwn: true })
                ?? occurrences.Select(o => o.Mod).FirstOrDefault(m => m is { InPlayset: true });
            if (mod is not null)
            {
                return new ErrorAttribution(mod.IsOwn ? ErrorBucket.OwnMod : ErrorBucket.OtherMod, mod, null);
            }

            return occurrences.Any(o => o.Mod is null)
                ? new ErrorAttribution(ErrorBucket.Vanilla, null, null)
                : new ErrorAttribution(ErrorBucket.Unassigned, null, null);
        }

        return new ErrorAttribution(ErrorBucket.Engine, null, null);
    }
}
