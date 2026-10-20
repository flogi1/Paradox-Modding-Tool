using System.Diagnostics;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;

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
        TimeSpan buildDuration,
        IReadOnlyList<GuiSourceFile> classicGfxFiles,
        ClassicAssets? classicAssets,
        Task<ClassicExampleIndex>? classicExamples,
        JominiTextService? text)
    {
        Text = text;
        Options = options;
        Index = index;
        Catalog = catalog;
        Textures = textures;
        ReusedDocuments = reusedDocuments;
        CatalogFromCache = catalogFromCache;
        BuildDuration = buildDuration;
        ClassicGfxFiles = classicGfxFiles;
        Classic = classicAssets;
        ClassicExamples = classicExamples;
    }

    public GuiWorkspaceOptions Options { get; }

    public GuiIndex Index { get; }

    public GuiRules Rules => Options.Rules;

    public GuiPropertyCatalog Catalog { get; }

    public GuiTextureLocator Textures { get; }

    public int ReusedDocuments { get; }

    public bool CatalogFromCache { get; }

    public TimeSpan BuildDuration { get; }

    /// <summary>
    /// Nur klassisch: alle <c>.gfx</c>-Dateien unter <c>interface</c> in Ladereihenfolge (Vanilla, Abhängigkeiten, Mod), die Grundlage der Sprite-Tabelle.
    /// Bei Jomini leer.
    /// </summary>
    public IReadOnlyList<GuiSourceFile> ClassicGfxFiles { get; }

    /// <summary>Nur klassisch: Sprites, Schriften und Texturen aus den <c>.gfx</c>-Dateien. Bei Jomini <see langword="null"/>.</summary>
    public ClassicAssets? Classic { get; }

    /// <summary>
    /// Nur klassisch: Vanilla-Beispiele für den Erklärkasten, im Hintergrund gebaut (nicht auf dem UI-Thread abwarten). Die Aufgabe kann scheitern; wer sie
    /// auswertet, fängt das ab. Beispiele kommen nur aus Vanilla, daher übernehmen <see cref="WithModFile"/> und das Speichern einer .gfx-Datei sie unverändert.
    /// Bei Jomini <see langword="null"/>.
    /// </summary>
    public Task<ClassicExampleIndex>? ClassicExamples { get; }

    /// <summary>
    /// Nur Jomini: Texte mit Spielschriften, Lokalisierung und Formaten für die Layout-Engine. Lädt träge; <see cref="Create"/> lädt Schriften und Englisch
    /// im Hintergrund vor. Klassisch <see langword="null"/>.
    /// </summary>
    public JominiTextService? Text { get; }

    public static GuiWorkspace Create(GuiWorkspaceOptions options, GuiWorkspace? previous = null, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var watch = Stopwatch.StartNew();
        var classic = options.Rules.Layout.IsClassic;
        IReadOnlyList<GuiSourceFile> files;
        IReadOnlyList<GuiSourceFile> gfxFiles = Array.Empty<GuiSourceFile>();
        IReadOnlyList<GuiSourceRoot> classicRoots = Array.Empty<GuiSourceRoot>();
        if (classic)
        {
            // Klassisch: nur interface/ jeder Wurzel (Installation, DLC-Ordner, Mods); die .gfx-Dateien gehen in den Katalog-Fingerabdruck und später in die Sprite-Tabelle.
            classicRoots = GuiSourceSet.ClassicRootsFor(options.InstallDirectory, options.Rules.Layout.VanillaRoots ?? new[] { string.Empty }, options.DependencyRoots, options.ModRoot);
            files = GuiSourceSet.Collect(classicRoots, ".gui", "interface");
            gfxFiles = GuiSourceSet.Collect(classicRoots, ".gfx", "interface");
        }
        else
        {
            files = GuiSourceSet.Collect(GuiSourceSet.RootsFor(options.InstallDirectory, options.DependencyRoots, options.ModRoot));
        }

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
                documents[i] = GuiParser.Parse(classic ? GuiTextReader.Read(files[i].FullPath).Text : File.ReadAllText(files[i].FullPath));
            }

            var finished = Interlocked.Increment(ref done);
            if (finished == files.Count || finished % 25 == 0)
            {
                progress?.Report((finished, files.Count));
            }
        });
        var index = GuiIndex.FromDocuments(files, documents, options.Rules.Layout);

        // Klassisch zählen die Vanilla-.gfx-Dateien mit: Ändert sich eine, lernt der Katalog neu. Bei Jomini bleibt der Fingerabdruck wie bisher.
        var fingerprint = GuiSourceSet.ComputeFingerprint(
            files.Concat(gfxFiles).Where(f => f.Layer == GuiLayer.Vanilla));
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
        cancellation.ThrowIfCancellationRequested();
        var classicAssets = classic
            ? ClassicAssets.Build(classicRoots, gfxFiles, options.Rules.Layout.SpriteFirstWins, options.Rules.Layout.CaseSensitive, previous: previous?.Classic)
            : null;
        // Beispiele nur aus Vanilla: im Hintergrund suchen, die Oberfläche wartet nie darauf. Ein Fehler lässt nur die Aufgabe scheitern.
        var examples = classic ? Task.Run(() => ClassicExampleIndex.Build(index)) : null;
        // Jomini-Texte: Schriften, Formate und Englisch im Hintergrund vorladen, damit die erste Vorschau nicht wartet (Warm wirft nie; scheitert es, lädt
        // der erste Text). Die Spielsprache kennt erst der Editor (Benutzerordner); er lädt sie mit Warm(Sprache) nach.
        var text = classic ? null : JominiTextService.Build(options, index, textures);
        if (text is not null)
        {
            _ = Task.Run(() => text.Warm(JominiLocalisation.English));
        }

        return new GuiWorkspace(options, index, catalog, textures, reused, fromCache, watch.Elapsed, gfxFiles, classicAssets, examples, text);
    }

    /// <summary>
    /// Übernimmt eine gespeicherte .gui-Datei aus Mod oder Abhängigkeits-Mod; klassisch auch eine gespeicherte <c>.gfx</c>-Datei unter <c>interface/</c>
    /// (neue Sprite-Tabelle); Jomini auch eine .yml-Datei (neue Lokalisierung des Textdienstes). Andere Pfade und Vanilla ändern nichts.
    /// </summary>
    public GuiWorkspace WithModFile(string fullPath, string text)
    {
        if (fullPath.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase))
        {
            return WithGfxFile(fullPath, text);
        }

        if (fullPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return WithLocalisationFile(fullPath);
        }

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

            // Klassisch liest der Index nur interface/; eine .gui-Datei anderswo in der Mod gehört nicht hinein.
            if (Options.Rules.Layout.IsClassic && !relative.Replace('\\', '/').StartsWith("interface/", StringComparison.OrdinalIgnoreCase))
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
            var index = Index.WithFile(file, text);
            return new GuiWorkspace(Options, index, Catalog, Textures, 0, true, TimeSpan.Zero, ClassicGfxFiles, Classic, ClassicExamples, Text?.WithIndex(index));
        }

        return this;
    }

    /// <summary>
    /// Jomini: eine gespeicherte .yml-Datei unter der Mod oder einer Abhängigkeit gibt dem Textdienst eine neue, träge Lokalisierung, damit offene
    /// Vorschauen den neuen Text zeigen. Index und Formate bleiben; klassisch (ohne Textdienst), Vanilla und andere Pfade ändern nichts.
    /// </summary>
    private GuiWorkspace WithLocalisationFile(string fullPath)
    {
        if (Text is null)
        {
            return this;
        }

        var roots = new[] { Options.ModRoot }.Concat(Options.DependencyRoots).Where(root => !string.IsNullOrWhiteSpace(root));
        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root!, fullPath);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                continue;
            }

            return new GuiWorkspace(Options, Index, Catalog, Textures, 0, true, TimeSpan.Zero, ClassicGfxFiles, Classic, ClassicExamples, Text.WithFreshLocalisation());
        }

        return this;
    }

    /// <summary>Klassisch: eine gespeicherte .gfx-Datei der Mod oder einer Abhängigkeit ersetzt ihren Eintrag in Dateiliste und Sprite-Tabelle.</summary>
    private GuiWorkspace WithGfxFile(string fullPath, string text)
    {
        if (Classic is null)
        {
            return this;
        }

        var roots = GuiSourceSet
            .ClassicRootsFor(Options.InstallDirectory, Options.Rules.Layout.VanillaRoots ?? new[] { string.Empty }, Options.DependencyRoots, Options.ModRoot)
            .Where(r => r.Layer != GuiLayer.Vanilla);
        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root.Directory, fullPath).Replace('\\', '/');
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
                || !relative.StartsWith("interface/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new FileInfo(fullPath);
            var file = new GuiSourceFile(
                fullPath,
                relative,
                root.Layer,
                root.Label,
                info.Exists ? info.Length : text.Length,
                info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
            var files = GuiSourceSet.ReplaceOrInsert(ClassicGfxFiles, file, f => f);
            return new GuiWorkspace(Options, Index, Catalog, Textures, 0, true, TimeSpan.Zero, files, Classic.WithGfxFile(file, text), ClassicExamples, Text);
        }

        return this;
    }

    public GuiResolver CreateResolver(GuiDocument? overlay = null, string? overlayPath = null) => new(Index, Rules, overlay, overlayPath);

    /// <summary>
    /// Neue Engine je Lauf (die Engine hält Zustand während eines Laufs): klassisch die <see cref="ClassicLayoutEngine"/>, sonst die Jomini-Engine mit
    /// dem Textdienst <see cref="Text"/>. Jede Jomini-Runde beginnt einen neuen Texturlauf (die klassische Engine ruft ihren selbst auf): Fehlschläge
    /// werden neu gesucht, Köpfe neu bestätigt, sodass eine neu angelegte oder geänderte Textur in der nächsten Vorschau erscheint.
    /// </summary>
    public IGuiLayoutEngine CreateLayoutEngine()
    {
        if (Classic is { } classic)
        {
            return new ClassicLayoutEngine(classic);
        }

        Textures.NewRun();
        // Fehlt die Installation, bleiben es schematische Kästen wie vor den Texturen (Spezifikation): kein Texturfinder, also keine Sprites und keine
        // Hinweise "nicht gefunden" an jedem Kasten. Die Größen ungesetzter Icons kommen weiter aus SizeOf.
        var sprites = Directory.Exists(Options.InstallDirectory) ? Textures : null;
        return new GuiLayoutEngine(Rules, Textures.SizeOf, Text, sprites);
    }
}
