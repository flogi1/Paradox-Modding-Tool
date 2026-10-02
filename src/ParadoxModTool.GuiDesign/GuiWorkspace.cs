using System.Diagnostics;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign;

/// <param name="InstallDirectory">Installationsordner des Spiels mit <c>game</c>, <c>jomini</c>, <c>clausewitz</c> (nicht <c>…\game</c>).</param>
public sealed record GuiWorkspaceOptions(
    string InstallDirectory,
    string? ModRoot,
    IReadOnlyList<string> DependencyRoots,
    GuiRules Rules,
    string? CatalogPath,
    bool RelearnCatalog = false);

/// <summary>
/// Alles, was Designer und Inspektor über Spiel und Mod wissen müssen: Index aller .gui-Dateien, gelernter Katalog und Texturgrößen.
/// Unveränderlich. <see cref="Create"/> übernimmt unveränderte, schon geparste Dateien aus einem früheren Stand (z.B. beim Mod-Wechsel).
/// </summary>
public sealed class GuiWorkspace
{
    private GuiWorkspace(
        GuiWorkspaceOptions options,
        GuiIndex index,
        GuiPropertyCatalog catalog,
        GuiTextureLocator textures,
        int reusedDocuments,
        bool catalogFromCache,
        TimeSpan buildDuration)
    {
        Options = options;
        Index = index;
        Catalog = catalog;
        Textures = textures;
        ReusedDocuments = reusedDocuments;
        CatalogFromCache = catalogFromCache;
        BuildDuration = buildDuration;
    }

    public GuiWorkspaceOptions Options { get; }

    public GuiIndex Index { get; }

    public GuiRules Rules => Options.Rules;

    public GuiPropertyCatalog Catalog { get; }

    public GuiTextureLocator Textures { get; }

    public int ReusedDocuments { get; }

    public bool CatalogFromCache { get; }

    public TimeSpan BuildDuration { get; }

    public static GuiWorkspace Create(GuiWorkspaceOptions options, GuiWorkspace? previous = null, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var watch = Stopwatch.StartNew();
        var files = GuiSourceSet.Collect(GuiSourceSet.RootsFor(options.InstallDirectory, options.DependencyRoots, options.ModRoot));
        var known = new Dictionary<GuiSourceFile, GuiDocument>();
        if (previous is not null)
        {
            foreach (var file in previous.Index.Files)
            {
                known[file] = previous.Index.DocumentOf(file);
            }
        }

        var documents = new GuiDocument[files.Count];
        var reused = 0;
        var done = 0;
        cancellation.ThrowIfCancellationRequested();
        Parallel.For(0, files.Count, new ParallelOptions { CancellationToken = cancellation }, i =>
        {
            if (known.TryGetValue(files[i], out var document))
            {
                documents[i] = document;
                Interlocked.Increment(ref reused);
            }
            else
            {
                documents[i] = GuiParser.Parse(File.ReadAllText(files[i].FullPath));
            }

            var finished = Interlocked.Increment(ref done);
            if (finished == files.Count || finished % 25 == 0)
            {
                progress?.Report((finished, files.Count));
            }
        });
        var index = GuiIndex.FromDocuments(files, documents, options.Rules.Layout);

        var fingerprint = GuiSourceSet.ComputeFingerprint(files.Where(f => f.Layer == GuiLayer.Vanilla));
        GuiPropertyCatalog? catalog = null;
        var fromCache = false;
        if (!options.RelearnCatalog)
        {
            if (previous is not null && previous.Catalog.Fingerprint == fingerprint)
            {
                catalog = previous.Catalog;
                fromCache = true;
            }
            else if (options.CatalogPath is not null && GuiCatalogStore.Load(options.CatalogPath, fingerprint) is { } stored)
            {
                catalog = stored;
                fromCache = true;
            }
        }

        if (catalog is null)
        {
            cancellation.ThrowIfCancellationRequested();
            catalog = GuiCatalogLearner.Learn(index, options.Rules, fingerprint);
            if (options.CatalogPath is not null)
            {
                try
                {
                    GuiCatalogStore.Save(options.CatalogPath, catalog);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Der Katalog gilt für diese Sitzung; der nächste Start lernt neu.
                }
            }
        }

        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(options.InstallDirectory, options.DependencyRoots, options.ModRoot, options.Rules.Layout.Contexts));
        return new GuiWorkspace(options, index, catalog, textures, reused, fromCache, watch.Elapsed);
    }

    /// <summary>Übernimmt eine gespeicherte .gui-Datei aus Mod oder Abhängigkeits-Mod. Andere Pfade und Vanilla ändern nichts.</summary>
    public GuiWorkspace WithModFile(string fullPath, string text)
    {
        if (!fullPath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            return this;
        }

        var roots = GuiSourceSet.RootsFor(Options.InstallDirectory, Options.DependencyRoots, Options.ModRoot).Where(r => r.Layer != GuiLayer.Vanilla);
        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root.Directory, fullPath);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                continue;
            }

            var info = new FileInfo(fullPath);
            var file = new GuiSourceFile(
                fullPath,
                relative.Replace('\\', '/'),
                root.Layer,
                root.Label,
                info.Exists ? info.Length : text.Length,
                info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
            return new GuiWorkspace(Options, Index.WithFile(file, text), Catalog, Textures, 0, true, TimeSpan.Zero);
        }

        return this;
    }

    public GuiResolver CreateResolver(GuiDocument? overlay = null, string? overlayPath = null) => new(Index, Rules, overlay, overlayPath);

    /// <summary>Neue Engine je Lauf (die Engine hält Zustand während eines Laufs).</summary>
    public GuiLayoutEngine CreateLayoutEngine() => new(Rules, Textures.SizeOf);
}
