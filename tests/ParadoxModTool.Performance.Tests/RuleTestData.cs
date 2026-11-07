using System.Globalization;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance.Tests;

/// <summary>Handgemachte Dumps, Klassifizierer über eine feste Dateimenge und error.log-Dateien für die Regeltests.</summary>
internal static class RuleTestData
{
    public const string ModRoot = @"C:\mod";
    public const string VanillaRoot = @"C:\van";

    private static readonly ErrorCatalog Catalog = ErrorCatalog.Load(RepoFiles.PathOf("profiles/errorlog/eu5.json"));

    public static string Full(string root, string relative) =>
        Path.Combine(root, "in_game", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Klassifizierer: <paramref name="modFiles"/> liegen in der Mod, <paramref name="vanillaFiles"/> in Vanilla (beides = Both).</summary>
    public static OriginClassifier Classifier(IEnumerable<string> modFiles, IEnumerable<string> vanillaFiles)
    {
        var set = new HashSet<string>(
            modFiles.Select(f => Full(ModRoot, f)).Concat(vanillaFiles.Select(f => Full(VanillaRoot, f))),
            StringComparer.OrdinalIgnoreCase);
        return new OriginClassifier(ModRoot, Array.Empty<string>(), new[] { VanillaRoot }, set.Contains);
    }

    /// <summary>Dump aus Zeilen <c>Schlüssel;Calls;Self;Total</c>.</summary>
    public static ProfilingDump Dump(params string[] rows) =>
        ProfilingDumpReader.Read(new StringReader("File Location;Call Count;Self Time;Total Time\n" + string.Join("\n", rows) + "\n"));

    /// <summary>error.log mit <c>count</c> gleichen Meldungen je Stelle (jede Zeile mit eigener Uhrzeit), über <see cref="ErrorHotspots.Load"/> gelesen.</summary>
    public static ErrorHotspots Errors(params (string Path, int Line, int Count)[] places) =>
        ErrorsWith(places.Select(p => (p.Path, p.Line, p.Count, Message(p.Path, p.Line))).ToArray());

    /// <summary>Wie <see cref="Errors"/>, aber mit eigener Meldung je Stelle; sie muss auf <c>at Pfad:Zeile</c> enden, damit die Stelle erkannt wird.</summary>
    public static ErrorHotspots ErrorsWith(params (string Path, int Line, int Count, string Message)[] places)
    {
        var lines = new List<string>();
        var second = 0;
        foreach (var (_, _, count, message) in places)
        {
            for (var i = 0; i < count; i++)
            {
                var time = TimeSpan.FromSeconds(36_000 + second++).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                lines.Add($"[{time}][jomini_effect.cpp:556]: {message}");
            }
        }

        using var tree = new TempTree(("error.log", string.Join("\n", lines) + "\n"));
        return ErrorHotspots.Load(tree.PathOf("error.log"), Catalog);
    }

    public static string Message(string path, int line) => $"Unknown effect spam_effect at {path}:{line}";
}
