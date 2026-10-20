using System.Diagnostics;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Eigene Sammlung ohne Parallelität: der Zähler von <see cref="GuiImageDecoder"/> darf nicht von anderen Testklassen hochgezählt werden.</summary>
[CollectionDefinition(nameof(RealClassicGuiTests), DisableParallelization = true)]
public sealed class RealClassicGuiCollection : ICollectionFixture<RealClassicWorkspaces>
{
}

/// <summary>Die Arbeitsstände der echten Installationen, je Spiel einmal gebaut; mit Bauzeit und den dabei gezählten Dekodier-Aufrufen.</summary>
public sealed class RealClassicWorkspaces
{
    private readonly Lazy<BuiltWorkspace?> _hoi4 = new(() => Build("hoi4"));
    private readonly Lazy<BuiltWorkspace?> _eu4 = new(() => Build("eu4"));

    internal BuiltWorkspace? Hoi4 => _hoi4.Value;

    internal BuiltWorkspace? Eu4 => _eu4.Value;

    private static BuiltWorkspace? Build(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return null;
        }

        var before = GuiImageDecoder.DecodeCalls;
        var watch = Stopwatch.StartNew();
        var workspace = GuiWorkspace.Create(
            new GuiWorkspaceOptions(install.InstallDirectory, null, Array.Empty<string>(), ClassicLayoutKit.Rules(game), CatalogPath: null));
        watch.Stop();
        return new BuiltWorkspace(workspace, watch.Elapsed, GuiImageDecoder.DecodeCalls - before);
    }
}

internal sealed record BuiltWorkspace(GuiWorkspace Workspace, TimeSpan Elapsed, long DecodeCalls)
{
    /// <summary>Alle Fenster direkt unter <c>guiTypes</c> (auch Kopien, die verlieren, und Positionen/Texte auf oberster Ebene).</summary>
    public IReadOnlyList<GuiDefinition> TopLevelWindows() =>
        Workspace.Index.Definitions.Where(d => d.Kind == GuiDefinitionKind.NamedWidget && d.IsTopLevel).ToList();

    public IReadOnlyList<GuiDefinition> WindowsIn(string relativePath) =>
        TopLevelWindows().Where(d => string.Equals(d.File.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Legt ein Fenster mit einer neuen Engine.</summary>
    public LayoutBox Layout(GuiDefinition window, LayoutOptions? options = null) =>
        Workspace.CreateLayoutEngine().Layout(Workspace.CreateResolver().Resolve(window.Node), options ?? new LayoutOptions());
}

/// <summary>
/// Prüfungen gegen die echten Installationen von HOI IV (1.19.3) und EU IV (1.37.5); fehlt ein Spiel, kehrt der Test sofort zurück.
/// Bekannte Vanilla-Zahlen stehen als Grenze im Test und in der Meldung.
/// </summary>
[Collection(nameof(RealClassicGuiTests))]
public sealed class RealClassicGuiTests
{
    private readonly RealClassicWorkspaces _workspaces;
    private readonly ITestOutputHelper _output;

    public RealClassicGuiTests(RealClassicWorkspaces workspaces, ITestOutputHelper output)
    {
        _workspaces = workspaces;
        _output = output;
    }

    [Fact]
    public void Hoi4_workspace_builds_without_decoding_images()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        var windows = hoi4.TopLevelWindows();
        var diagnostics = hoi4.Workspace.Index.Diagnostics;
        _output.WriteLine(
            $"HOI IV: {hoi4.Workspace.Index.Files.Count} .gui, {hoi4.Workspace.ClassicGfxFiles.Count} .gfx, {windows.Count} Fenster "
            + $"({windows.Select(w => w.Name).Distinct(StringComparer.Ordinal).Count()} Namen), {hoi4.Workspace.Classic!.Sprites.Sprites.Count} Sprites, "
            + $"{diagnostics.Count} Parse-Diagnosen, gebaut in {hoi4.Elapsed.TotalMilliseconds:F0} ms");
        foreach (var diagnostic in diagnostics)
        {
            _output.WriteLine($"  {diagnostic.File.RelativePath}:{diagnostic.Diagnostic.Line} {diagnostic.Diagnostic.Message}");
        }

        Assert.True(windows.Count > 1800, $"nur {windows.Count} Fenster");
        // Vanilla 1.19.3: vier Dateien, denen eine schließende Klammer fehlt (backend.gui und drei *_scripted_gui.gui).
        Assert.True(diagnostics.Count <= 4, $"Vanilla hat 4 Parse-Diagnosen, gefunden {diagnostics.Count}");
        Assert.Equal(0, hoi4.DecodeCalls);
    }

    [Fact]
    public void Hoi4_every_top_level_window_lays_out()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        var before = GuiImageDecoder.DecodeCalls;
        var failures = LayoutAll(hoi4, hoi4.TopLevelWindows(), new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152 });

        Assert.Empty(failures);
        Assert.Equal(before, GuiImageDecoder.DecodeCalls);
    }

    [Fact]
    public void Hoi4_main_menu_sprites_decode()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        var windows = hoi4.WindowsIn("interface/frontendmainview.gui");
        Assert.NotEmpty(windows);
        AssertSpritesDecode(hoi4, windows, new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152 });
    }

    [Fact]
    public void Eu4_workspace_builds_and_main_menu_lays_out()
    {
        if (_workspaces.Eu4 is not { } eu4)
        {
            return;
        }

        var windows = eu4.TopLevelWindows();
        var diagnostics = eu4.Workspace.Index.Diagnostics;
        _output.WriteLine(
            $"EU IV: {eu4.Workspace.Index.Files.Count} .gui, {eu4.Workspace.ClassicGfxFiles.Count} .gfx, {windows.Count} Fenster, "
            + $"{eu4.Workspace.Classic!.Sprites.Sprites.Count} Sprites, {diagnostics.Count} Parse-Diagnosen, gebaut in {eu4.Elapsed.TotalMilliseconds:F0} ms");
        foreach (var diagnostic in diagnostics)
        {
            _output.WriteLine($"  {diagnostic.File.RelativePath}:{diagnostic.Diagnostic.Line} {diagnostic.Diagnostic.Message}");
        }

        Assert.True(diagnostics.Count == 0, $"Vanilla 1.37.5 hat keine Parse-Diagnosen, gefunden {diagnostics.Count}");
        Assert.Equal(0, eu4.DecodeCalls);

        // Die einzige Windows-1252-Datei: im Index, ohne Ersatzzeichen gelesen, ihre Fenster lassen sich legen.
        var endOfCombat = eu4.Workspace.Index.Files.Single(f => string.Equals(f.RelativePath, "interface/end_of_combat.gui", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(GuiTextReader.Windows1252, GuiTextReader.Read(endOfCombat.FullPath).EncodingName);
        Assert.DoesNotContain('�', eu4.Workspace.Index.DocumentOf(endOfCombat).Text);
        var options = new LayoutOptions { ScreenWidth = 2560, ScreenHeight = 1440, UiScale = 1.3 };
        Assert.Empty(LayoutAll(eu4, eu4.WindowsIn("interface/end_of_combat.gui"), options));

        var frontend = eu4.WindowsIn("interface/frontend.gui");
        Assert.NotEmpty(frontend);
        Assert.Empty(LayoutAll(eu4, frontend, options));
        AssertSpritesDecode(eu4, frontend, options);
    }

    [Fact]
    public void Eu4_texture_tga_falls_back_to_dds()
    {
        if (_workspaces.Eu4 is not { } eu4)
        {
            return;
        }

        // Gezählt werden die wirksamen Sprites je Name, deren .tga es in keiner Wurzel gibt, wohl aber die .dds gleichen Namens.
        var classic = eu4.Workspace.Classic!;
        var fallback = classic.Sprites.Sprites
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .Select(name => classic.Sprites.Find(name).Sprite)
            .Where(s => s?.TexturePath is { } path && path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                && classic.Textures.Locate(path) is { } full && full.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            .Count();
        _output.WriteLine($"EU IV: {fallback} Sprites finden ihre .tga nur als .dds");

        Assert.True(fallback >= 500, $"nur {fallback} Sprites über den .dds-Ersatz");
    }

    [Fact]
    public void Eu4_main_menu_bottom_panel_against_the_probe_screenshot()
    {
        if (_workspaces.Eu4 is not { } eu4)
        {
            return;
        }

        // Spieleinstellung des Probe-Laufs (settings.txt): 2560×1440, game_ui_scale 1.3; eu4-probe-run-2026-10-17.jpg ist auf 2000 Pixel Breite
        // verkleinert, ein Bildpixel sind also 2560 / 2000 / 1.3 virtuelle Pixel.
        var panel = eu4.WindowsIn("interface/frontend.gui").Single(w => w.Name == "country_selection_panel");
        var root = eu4.Layout(panel, new LayoutOptions { ScreenWidth = 2560, ScreenHeight = 1440, UiScale = 1.3 });
        var bottom = ClassicLayoutKit.Find(root, "mainmenu_panel_bottom");
        const double toScreenshot = 2000.0 / 2560 * 1.3;
        foreach (var box in new[] { bottom }.Concat(bottom.Descendants()))
        {
            _output.WriteLine(
                $"{ClassicLayoutKit.NameOf(box.Element),-32} {box.X,8:F1} {box.Y,8:F1} {box.Width,7:F1} {box.Height,7:F1}"
                + $"   Bild: {box.X * toScreenshot,7:F1} {box.Y * toScreenshot,7:F1} {box.Width * toScreenshot,6:F1} {box.Height * toScreenshot,6:F1}");
        }

        // Lage des Panels selbst: fullscreen = yes von mainmenu legt es CENTER_DOWN an den ganzen virtuellen Bildschirm (1969 × 1108). Im Bild abgelesen
        // (Controller-Ruling fullscreen): linke obere Ecke bei etwa (432.6, 1023.7) virtuellen Pixeln, ±15.
        _output.WriteLine($"mainmenu_panel_bottom: {bottom.X:F1} {bottom.Y:F1}");
        Assert.InRange(bottom.X, 432.6 - 15, 432.6 + 15);
        Assert.InRange(bottom.Y, 1023.7 - 15, 1023.7 + 15);

        // Geprüft werden außerdem die Abstände der Texte zur Mitte von "Single Player" (im Bild abgelesen, Textmitten in Bildpixeln).
        var measured = new Dictionary<string, (double X, double Y)>
        {
            ["single_player_button_text"] = (903.5, 1033.5),
            ["multi_player_button_text"] = (1090, 1033.5),
            ["tutorial_button_text"] = (795, 1091.5),
            ["exit_button_text"] = (1203.5, 1091.5),
            ["version_label"] = (534, 1094.5),
        };
        var anchor = Centre(ClassicLayoutKit.Find(bottom, "single_player_button_text"));
        var (anchorX, anchorY) = measured["single_player_button_text"];
        foreach (var (name, (x, y)) in measured)
        {
            var centre = Centre(ClassicLayoutKit.Find(bottom, name));
            var expected = ((x - anchorX) / toScreenshot, (y - anchorY) / toScreenshot);
            var actual = (centre.X - anchor.X, centre.Y - anchor.Y);
            _output.WriteLine($"{name}: Bild {expected.Item1:F1} {expected.Item2:F1}, PMT {actual.Item1:F1} {actual.Item2:F1}");
            Assert.True(
                Math.Abs(expected.Item1 - actual.Item1) <= 15 && Math.Abs(expected.Item2 - actual.Item2) <= 15,
                $"{name}: Abstand im Bild {expected}, in PMT {actual}");
        }

        static (double X, double Y) Centre(LayoutBox box) => (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    /// <summary>Ein Mod-Ordner, den es nicht gibt: Die Planung liest nichts davon (<c>readFile</c> liefert null), nur sein Name zählt.</summary>
    private static readonly string DummyModRoot = Path.Combine(Path.GetTempPath(), "PmtRealCheckMod");

    [Fact]
    public void Hoi4_default_override_names_sort_last()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        // Für jeden Ordner unter interface/ mit Vanilla-Dateien (Installation und DLC-Ordner): Der Standardname der Überschreibdatei sortiert nach jeder
        // Vanilla-Datei dieses Ordners und deckt sich mit keiner Vanilla-Datei.
        var index = hoi4.Workspace.Index;
        var order = GameLoadOrder.For(index.Layout.CaseSensitive);
        var defaultName = ClassicOverrideTargets.DefaultFileName(DummyModRoot, index.Layout);
        var folders = VanillaInterfaceFolders(index);
        var grown = new List<string>();
        foreach (var (folder, files) in folders)
        {
            var target = ClassicOverrideTargets.For(files[0], index, DummyModRoot, null);
            var name = target.RelativePath[(folder.Length + 1)..];
            Assert.Equal(folder, target.RelativePath[..target.RelativePath.LastIndexOf('/')]);
            Assert.All(files, f => Assert.True(order.Compare(target.RelativePath, f.RelativePath) > 0, $"{target.RelativePath} sortiert nicht nach {f.RelativePath}"));
            Assert.Null(ClassicOverrideTargets.Colliding(index, target.RelativePath));
            if (name != defaultName)
            {
                grown.Add($"{folder}: {name}");
            }
        }

        _output.WriteLine($"HOI IV: {folders.Count} Ordner mit Vanilla-Dateien unter interface/, Standardname {defaultName}; Präfix verlängert in {grown.Count}:");
        foreach (var line in grown)
        {
            _output.WriteLine("  " + line);
        }

        Assert.True(folders.Count > 5, $"nur {folders.Count} Ordner gefunden");
    }

    [Fact]
    public void Eu4_default_override_name_sorts_first()
    {
        if (_workspaces.Eu4 is not { } eu4)
        {
            return;
        }

        // EU IV schreibt nie in einen Unterordner: Das Ziel liegt direkt in interface/ und sortiert vor jeder Vanilla-Datei dort, auch wenn die Quelle
        // aus einem Unterordner stammt.
        var index = eu4.Workspace.Index;
        var order = GameLoadOrder.For(index.Layout.CaseSensitive);
        var defaultName = ClassicOverrideTargets.DefaultFileName(DummyModRoot, index.Layout);
        var folders = VanillaInterfaceFolders(index);
        var rootFiles = index.Files.Where(f => f.Layer == GuiLayer.Vanilla && f.RelativePath.Count(c => c == '/') == 1).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, files) in folders)
        {
            var target = ClassicOverrideTargets.For(files[0], index, DummyModRoot, null);
            seen.Add(target.RelativePath);
            Assert.Equal(ClassicTargetRule.RootFirstWins, target.Rule);
            Assert.DoesNotContain('/', target.RelativePath["interface/".Length..]);
            Assert.All(rootFiles, f => Assert.True(order.Compare(target.RelativePath, f.RelativePath) < 0, $"{target.RelativePath} sortiert nicht vor {f.RelativePath}"));
            Assert.Null(ClassicOverrideTargets.Colliding(index, target.RelativePath));
        }

        _output.WriteLine($"EU IV: {folders.Count} Ordner, {rootFiles.Count} Vanilla-Dateien direkt in interface/, Standardname {defaultName}, Ziele: {string.Join(", ", seen)}");
        Assert.Single(seen);
    }

    [Fact]
    public void Hoi4_takeover_of_topbar_wins()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        var index = hoi4.Workspace.Index;
        var source = index.Files.Single(f => f.Layer == GuiLayer.Vanilla && string.Equals(f.RelativePath, "interface/topbar.gui", StringComparison.OrdinalIgnoreCase));
        var plan = PlanMovedIcon(index, source, "topbar", out var target);

        _output.WriteLine($"HOI IV topbar: Ziel {target.RelativePath} ({target.Rule}, unsicher: {target.Uncertain}), Blocker: {plan.Blockers.Count}, Warnungen: {plan.Warnings.Count}");
        AssertSingleWinningWindow(index, plan, "topbar", target);

        // Keine DLC-Datei definiert topbar: DLC-Dateien in interface/ allein machen die Reihenfolge nicht unsicher (kein Dauerhinweis bei jeder Übernahme).
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Eu4_takeover_of_a_subfolder_window_goes_to_root()
    {
        if (_workspaces.Eu4 is not { } eu4)
        {
            return;
        }

        // Ein Fenster aus einem Unterordner von interface/, das dort gilt (das zuerst geladene) und eine verschiebbare Position hat.
        var index = eu4.Workspace.Index;
        string? name = null;
        GuiSourceFile? source = null;
        foreach (var window in eu4.TopLevelWindows().Where(w => w.File.Layer == GuiLayer.Vanilla && w.File.RelativePath.Count(c => c == '/') > 1))
        {
            if (ClassicWindows.Winner(index, window.Name)?.File == window.File && MovableIcon(window.Node) is not null)
            {
                (name, source) = (window.Name, window.File);
                break;
            }
        }

        Assert.NotNull(source);
        var plan = PlanMovedIcon(index, source, name!, out var target);

        _output.WriteLine($"EU IV {name} aus {source.RelativePath}: Ziel {target.RelativePath} ({target.Rule}), Blocker: {plan.Blockers.Count}, Warnungen: {plan.Warnings.Count}");
        Assert.Equal(ClassicTargetRule.RootFirstWins, target.Rule);
        Assert.Equal(2, target.RelativePath.Count(c => c == '/') + 1);
        AssertSingleWinningWindow(index, plan, name!, target);
    }

    [Fact]
    public void Example_index_builds_quickly()
    {
        if (_workspaces.Hoi4 is not { } hoi4)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var examples = ClassicExampleIndex.Build(hoi4.Workspace.Index);
        watch.Stop();

        _output.WriteLine($"HOI IV: Beispiel-Index gebaut in {watch.ElapsedMilliseconds} ms ({examples.For("position", null).Count} Beispiele zu position)");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Beispiel-Index brauchte {watch.ElapsedMilliseconds} ms");
    }

    /// <summary>Die Ordner unter <c>interface/</c> mit Vanilla-Dateien (Installation und DLC-Ordner), jeweils mit diesen Dateien.</summary>
    private static List<(string Folder, List<GuiSourceFile> Files)> VanillaInterfaceFolders(GuiIndex index) =>
        index.Files
            .Where(f => f.Layer == GuiLayer.Vanilla && f.RelativePath.StartsWith("interface/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => f.RelativePath[..f.RelativePath.LastIndexOf('/')], StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.ToList()))
            .ToList();

    /// <summary>Plant die Übernahme von <paramref name="windowName"/> mit einem um 1 Pixel verschobenen Element (ohne Mod, ohne Baseline-Umweg: wie "In Mod übernehmen").</summary>
    private static GuiOverridePlan PlanMovedIcon(GuiIndex index, GuiSourceFile source, string windowName, out ClassicTarget target)
    {
        var (vanillaText, encoding) = GuiTextReader.Read(source.FullPath);
        var start = GuiDraft.Start(source, vanillaText, index).Text;
        var document = GuiParser.Parse(start);
        var window = ClassicWindows.Of(document).First(w => ClassicWindows.NameOf(w) == windowName);
        var position = MovableIcon(window) ?? throw new InvalidOperationException($"{windowName} hat keine verschiebbare Position");
        var (x, y) = PairOf(position)!.Value;
        var edit = ClassicEdits.SetPair(document, position, "position", new ClassicPairKeys("x", "y"), (x + 1).ToString(), y.ToString());
        var draft = TextEdit.Apply(start, edit.Edits);
        Assert.NotEqual(start, draft);

        var layout = index.Layout;
        target = ClassicOverrideTargets.For(source, index, DummyModRoot, null);
        var targets = GuiModTargets.For(DummyModRoot, null, null, layout.FirstWins, classic: true);
        return GuiOverridePlanner.Plan(new GuiOverrideRequest(source, vanillaText, draft, index, targets, _ => null, start) { SourceEncoding = encoding });
    }

    /// <summary>Der Plan schreibt genau eine neue Datei mit genau diesem einen Fenster, ohne Blocker; nach dem Schreiben gewinnt dieses Fenster im Index.</summary>
    private static void AssertSingleWinningWindow(GuiIndex index, GuiOverridePlan plan, string windowName, ClassicTarget target)
    {
        Assert.Empty(plan.Blockers);
        Assert.True(plan.CanApply);

        // Hinweise gibt es höchstens zur DLC-Reihenfolge (nicht gemessen), nie dazu, dass eine Abhängigkeit gewinnt.
        Assert.All(plan.Warnings, w => Assert.Equal(GuiPlanWarningKind.UncertainOrder, w.Kind));
        var file = Assert.Single(plan.Files);
        Assert.Equal(target.FullPath, file.FullPath);
        Assert.True(file.IsNew);
        var written = ClassicWindows.Of(GuiParser.Parse(file.NewText));
        Assert.Equal(windowName, ClassicWindows.NameOf(Assert.Single(written)));

        var label = Path.GetFileName(DummyModRoot);
        var after = index.WithFile(new GuiSourceFile(file.FullPath, target.RelativePath, GuiLayer.Mod, label, 0, 0), file.NewText);
        var winner = ClassicWindows.Winner(after, windowName);
        Assert.NotNull(winner);
        Assert.Equal(GuiLayer.Mod, winner.File.Layer);
        Assert.Equal(target.RelativePath, winner.File.RelativePath);
    }

    /// <summary>Ein Block <c>position</c> mit zwei ganzzahligen Werten in einem Element des Fensters (nie im Fenster selbst, nie in <c>if_resolution</c>).</summary>
    private static GuiNode? MovableIcon(GuiNode window) =>
        window.Descendants().FirstOrDefault(n =>
            n.Kind == GuiNodeKind.KeyedBlock && n.HasBody && string.Equals(n.Name, "position", StringComparison.OrdinalIgnoreCase)
            && n.Parent != window && n.Parent is { } parent && !string.Equals(parent.Name, "if_resolution", StringComparison.OrdinalIgnoreCase)
            && PairOf(n) is not null);

    private static (int X, int Y)? PairOf(GuiNode block)
    {
        int? x = null, y = null;
        foreach (var child in block.Children.Where(c => c.Kind == GuiNodeKind.Property))
        {
            var parsed = int.TryParse(child.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : (int?)null;
            if (string.Equals(child.Name, "x", StringComparison.OrdinalIgnoreCase))
            {
                x = parsed;
            }
            else if (string.Equals(child.Name, "y", StringComparison.OrdinalIgnoreCase))
            {
                y = parsed;
            }
        }

        return x is { } px && y is { } py ? (px, py) : null;
    }

    private List<string> LayoutAll(BuiltWorkspace built, IEnumerable<GuiDefinition> windows, LayoutOptions options)
    {
        var failures = new List<string>();
        var times = new List<(GuiDefinition Window, long Ms)>();
        var watch = Stopwatch.StartNew();
        foreach (var window in windows)
        {
            var single = Stopwatch.StartNew();
            try
            {
                built.Layout(window, options);
            }
            catch (Exception ex)
            {
                failures.Add($"{window.File.RelativePath} {window.Name}: {ex.GetType().Name} {ex.Message}");
            }

            times.Add((window, single.ElapsedMilliseconds));
        }

        _output.WriteLine($"{times.Count} Fenster gelegt in {watch.ElapsedMilliseconds} ms; am langsamsten:");
        foreach (var (window, ms) in times.OrderByDescending(t => t.Ms).Take(5))
        {
            _output.WriteLine($"  {ms} ms {window.File.RelativePath} {window.Name}");
        }

        return failures;
    }

    /// <summary>Jede Textur, die die Engine beim Legen der Fenster für ein Sprite findet, dekodiert (PNG dekodiert WPF, nicht dieser Dekoder).</summary>
    private void AssertSpritesDecode(BuiltWorkspace built, IEnumerable<GuiDefinition> windows, LayoutOptions options)
    {
        var textures = windows
            .Select(w => built.Layout(w, options))
            .SelectMany(root => new[] { root }.Concat(root.Descendants()))
            .Select(b => b.Visual)
            .OfType<GuiSpriteVisual>()
            .Select(v => v.TextureFullPath)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var decodable = textures.Where(t => !t.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();
        var before = GuiImageDecoder.DecodeCalls;
        var failed = decodable.Where(t => GuiImageDecoder.TryDecode(t) is null).ToList();
        _output.WriteLine($"{textures.Count} Texturen, {decodable.Count} DDS/TGA, {failed.Count} nicht dekodiert");

        Assert.True(decodable.Count > 10, $"nur {decodable.Count} Texturen gefunden");
        Assert.Empty(failed);

        // Der Zähler, mit dem die Bau-Tests "nichts dekodiert" prüfen, zählt wirklich jeden Aufruf.
        Assert.Equal(before + decodable.Count, GuiImageDecoder.DecodeCalls);
    }
}
