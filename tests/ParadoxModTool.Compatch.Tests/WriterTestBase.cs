namespace ParadoxModTool.Compatch.Tests;

/// <summary>Zusätzlich zu Vanilla/A/B ein Zielordner "patch" und Hilfen für Schreibtests.</summary>
public abstract class WriterTestBase : CompatchTestBase
{
    protected string PatchDir => Path.Combine(Dir, "patch");

    protected void PutPatch(string relative, string content)
    {
        var path = Path.Combine(PatchDir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    protected string ReadPatch(string relative) =>
        File.ReadAllText(Path.Combine(PatchDir, relative.Replace('/', Path.DirectorySeparatorChar)));

    protected WriteRequest Request(CompatchInput input, CompatchRun run, PatchTarget target = PatchTarget.Compatch, string? folder = null, IReadOnlyCollection<string>? locked = null, IReadOnlyList<WrittenEntry>? previous = null, IReadOnlyDictionary<string, string>? previousFiles = null) => new()
    {
        Run = run,
        Input = input,
        Target = target,
        TargetFolder = folder ?? PatchDir,
        LockedPaths = locked ?? Array.Empty<string>(),
        PreviousForeignEntries = previous ?? Array.Empty<WrittenEntry>(),
        PreviousWholeFiles = previousFiles ?? new Dictionary<string, string>(),
    };

    protected static string Preview(PreparedWrite prepared, string relative) =>
        prepared.Changes.Single(c => string.Equals(c.RelativePath, relative, StringComparison.OrdinalIgnoreCase)).PreviewText;
}
