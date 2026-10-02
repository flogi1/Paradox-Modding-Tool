using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

namespace ParadoxModTool.BugTracking;

/// <summary>Eine Art von Bug-Quelle (GitHub, ab Plan 3 Discord). Fehler kommen als <see cref="BugSourceException"/> mit lesbarem Grund.</summary>
public interface IBugSource
{
    /// <summary>Art wie in <see cref="BugSourceKinds"/>.</summary>
    string Kind { get; }

    BugSource Source { get; }

    Task<IReadOnlyList<RemoteBug>> FetchAsync(BugSourceEntry entry, CancellationToken cancellation);

    Task SetStatusAsync(BugSourceEntry entry, BugReport report, BugStatus status, CancellationToken cancellation);

    Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugSourceEntry entry, BugReport report, CancellationToken cancellation);
}

/// <summary>Fehler einer Quelle; <see cref="Exception.Message"/> ist für den Nutzer lesbar.</summary>
public sealed class BugSourceException : Exception
{
    public BugSourceException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
