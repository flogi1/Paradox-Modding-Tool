using System.Diagnostics;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Effekt-Rezepte aus den echten Installationen dieses Rechners zusammensetzen. Fehlt ein Spiel, kehrt der Test sofort zurück. Liest
/// <see cref="JominiEffectRules"/>, daher in deren Sammlung.
/// </summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiEffectVanillaTests
{
    private const int FileCount = 5;
    private const int ComposeCount = 40;

    private readonly ITestOutputHelper _output;

    public JominiEffectVanillaTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void TopWindows_ComposeWithoutException(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return;   // Spiel nicht installiert: nichts zu prüfen
        }

        // Aufbau wie GuiWorkspace (ohne Textdienst): Profilregeln, Index, Texturfinder in den Kontextordnern des Profils.
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")));
        var index = GuiIndex.Build(GuiSourceSet.Collect(GuiSourceSet.RootsFor(install.InstallDirectory, Array.Empty<string>(), null)), layout: rules.Layout);
        var resolver = new GuiResolver(index, rules);
        var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, rules.Layout.Contexts));
        var engine = new GuiLayoutEngine(rules, locator.SizeOf, null, locator);

        // Die fünf Dateien mit den meisten modify_texture im Text.
        var files = index.Files
            .Select(file => (File: file, Count: CountOf(File.ReadAllText(file.FullPath), "modify_texture")))
            .OrderByDescending(f => f.Count)
            .ThenBy(f => f.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(FileCount)
            .ToList();
        _output.WriteLine($"{game}: Dateien {string.Join(", ", files.Select(f => $"{f.File.RelativePath} ({f.Count})"))}");

        var layoutWatch = Stopwatch.StartNew();
        var recipes = new Dictionary<string, JominiEffectRecipe>(StringComparer.Ordinal);
        var boxesWithEffects = 0;
        foreach (var (file, _) in files)
        {
            foreach (var node in index.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var root = engine.Layout(resolver.Resolve(node));
                foreach (var box in root.Descendants().Prepend(root).Where(b => b.Effects is not null))
                {
                    boxesWithEffects++;
                    recipes.TryAdd(box.Effects!.CacheKey, box.Effects);
                }
            }
        }

        layoutWatch.Stop();
        _output.WriteLine($"{game}: gelegt in {layoutWatch.ElapsedMilliseconds} ms, {boxesWithEffects} Kästen mit Effekten, {recipes.Count} verschiedene Rezepte");
        Assert.True(recipes.Count > 0, $"{game}: kein Kasten mit Effekten");

        // Jede Textur einmal dekodieren (wie der Bild-Cache).
        var decoded = new Dictionary<string, GuiImage?>(StringComparer.OrdinalIgnoreCase);
        var decodeWatch = new Stopwatch();
        GuiImage? Load(string path)
        {
            if (!decoded.TryGetValue(path, out var image))
            {
                decodeWatch.Start();
                image = GuiImageDecoder.TryDecode(path);
                decodeWatch.Stop();
                decoded[path] = image;
            }

            return image;
        }

        var composeWatch = Stopwatch.StartNew();
        var largest = (Key: "", Pixels: 0L);
        foreach (var recipe in recipes.Values.Take(ComposeCount))
        {
            var image = JominiEffectComposer.Compose(recipe, Load);
            var pad = recipe.PaddingPixels;
            Assert.True(double.IsFinite(recipe.PixelsPerUnit) && recipe.PixelsPerUnit > 0, $"{game}: Pixel je Einheit {recipe.PixelsPerUnit}");
            Assert.InRange(image.Width, 0, JominiEffectRecipe.MaxSide + 2 * pad);
            Assert.InRange(image.Height, 0, JominiEffectRecipe.MaxSide + 2 * pad);
            Assert.Equal(image.Width * image.Height * 4, image.Bgra.Length);
            if ((long)image.Width * image.Height > largest.Pixels)
            {
                largest = (recipe.Base.Texture, (long)image.Width * image.Height);
            }
        }

        composeWatch.Stop();
        _output.WriteLine(
            $"{game}: {Math.Min(ComposeCount, recipes.Count)} Rezepte zusammengesetzt in {composeWatch.ElapsedMilliseconds} ms ({decoded.Count} Texturen dekodiert in {decodeWatch.ElapsedMilliseconds} ms), "
            + $"größtes Bild {largest.Pixels} Pixel ({largest.Key})");
    }

    private static int CountOf(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
