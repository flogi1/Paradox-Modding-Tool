namespace ParadoxModTool.Performance;

/// <summary>Der Dump hat nicht das erwartete Format (eine Pflichtspalte fehlt); <see cref="Header"/> ist die gelesene Kopfzeile.</summary>
public sealed class ProfilingFormatException : Exception
{
    public ProfilingFormatException(string message, string header) : base(message)
    {
        Header = header;
    }

    /// <summary>Die Kopfzeile des Dumps, wie gelesen.</summary>
    public string Header { get; }
}
