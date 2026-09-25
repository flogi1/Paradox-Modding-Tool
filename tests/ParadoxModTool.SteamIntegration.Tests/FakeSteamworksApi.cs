namespace ParadoxModTool.SteamIntegration.Tests;

/// <summary>Ersetzt Steamworks im Test: hält jeden Aufruf in <see cref="Calls"/> fest und liefert einstellbare Ergebnisse.</summary>
internal sealed class FakeSteamworksApi : ISteamworksApi
{
    public List<string> Calls { get; } = new();

    public Exception? InitError { get; set; }

    public CreateItemResult? CreateResult { get; set; } = new(1, 3456789012UL, false);

    public SubmitItemResult? SubmitResult { get; set; } = new(1, 0, false);

    /// <summary>Name eines <c>Set*</c>-Aufrufs (<c>Title</c>, <c>Description</c>, <c>Preview</c>, <c>Visibility</c>, <c>Content</c>), der <see langword="false"/> liefert.</summary>
    public string? FailSet { get; set; }

    public ulong Handle { get; set; } = 77;

    /// <summary>So oft liefert <see cref="IsCallCompleted"/> zuerst <see langword="false"/>.</summary>
    public int PendingPolls { get; set; }

    public Queue<ItemUpdateProgress> ProgressQueue { get; } = new();

    public Action? OnRunCallbacks { get; set; }

    public bool Disposed { get; private set; }

    public void Init()
    {
        Calls.Add("Init");
        if (InitError is not null)
        {
            throw InitError;
        }
    }

    public void Shutdown() => Calls.Add("Shutdown");

    public void RunCallbacks() => OnRunCallbacks?.Invoke();

    public ulong CreateItem(uint appId)
    {
        Calls.Add($"CreateItem {appId}");
        return 100;
    }

    public ulong StartItemUpdate(uint appId, ulong publishedFileId)
    {
        Calls.Add($"Start {appId} {publishedFileId}");
        return Handle;
    }

    public bool SetItemTitle(ulong handle, string title) => Set("Title", title);

    public bool SetItemDescription(ulong handle, string description) => Set("Description", description);

    public bool SetItemPreview(ulong handle, string previewFile) => Set("Preview", previewFile);

    public bool SetItemContent(ulong handle, string contentFolder) => Set("Content", contentFolder);

    public bool SetItemVisibility(ulong handle, int visibility) => Set("Visibility", visibility.ToString());

    public ulong SubmitItemUpdate(ulong handle, string changeNote)
    {
        Calls.Add($"Submit {handle} {changeNote}");
        return 200;
    }

    public bool IsCallCompleted(ulong call)
    {
        if (PendingPolls > 0)
        {
            PendingPolls--;
            return false;
        }

        return true;
    }

    public CreateItemResult? ReadCreateItemResult(ulong call) => CreateResult;

    public SubmitItemResult? ReadSubmitItemResult(ulong call) => SubmitResult;

    public ItemUpdateProgress GetItemUpdateProgress(ulong handle) =>
        ProgressQueue.Count > 0 ? ProgressQueue.Dequeue() : default;

    public void Dispose() => Disposed = true;

    private bool Set(string name, string value)
    {
        Calls.Add($"{name} {value}");
        return FailSet != name;
    }
}

/// <summary>Sammelt Fortschrittszeilen synchron (<see cref="Progress{T}"/> meldet asynchron).</summary>
internal sealed class ListProgress : IProgress<string>
{
    public List<string> Lines { get; } = new();

    public void Report(string value) => Lines.Add(value);
}
