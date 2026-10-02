namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>Temporärer Ordnerbaum für Tests; relative Pfade mit "/".</summary>
internal sealed class TempTree : IDisposable
{
    public TempTree(params (string RelativePath, string Content)[] files)
    {
        Root = Path.Combine(Path.GetTempPath(), "pmt-errorlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        foreach (var (relative, content) in files)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    public string Root { get; }

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
