using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Englische Texte der klassischen Vorschau (<c>localisation/*_l_english.yml</c>).</summary>
public sealed class ClassicLocalisationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-classicloc-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private GuiSourceRoot Root(string name, GuiLayer layer, string file, string text)
    {
        var dir = Path.Combine(_temp, name);
        var path = Path.Combine(dir, "localisation", file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return new GuiSourceRoot(dir, layer, name);
    }

    [Fact]
    public void Reads_versioned_and_plain_keys()
    {
        var root = Root("vanilla", GuiLayer.Vanilla, "a_l_english.yml",
            "﻿l_english:\n"
            + "# Kommentar\n"
            + " VERSIONED:0 \"Versioned text\"\n"
            + " PLAIN: \"Plain text\"\n");

        var loc = ClassicLocalisation.Load(new[] { root });

        Assert.Equal("Versioned text", loc.Text("VERSIONED"));
        Assert.Equal("Plain text", loc.Text("PLAIN"));
    }

    [Fact]
    public void Mod_overrides_vanilla()
    {
        var vanilla = Root("vanilla", GuiLayer.Vanilla, "a_l_english.yml", "l_english:\n KEY:0 \"Vanilla\"\n ONLY_VANILLA:0 \"Stays\"\n");
        var mod = Root("mod", GuiLayer.Mod, "b_l_english.yml", "l_english:\n KEY:0 \"Mod\"\n");

        var loc = ClassicLocalisation.Load(new[] { vanilla, mod });

        Assert.Equal("Mod", loc.Text("KEY"));
        Assert.Equal("Stays", loc.Text("ONLY_VANILLA"));
    }

    [Fact]
    public void Unknown_key_is_null()
    {
        var root = Root("vanilla", GuiLayer.Vanilla, "a_l_english.yml", "l_english:\n KEY:0 \"x\"\n");

        Assert.Null(ClassicLocalisation.Load(new[] { root }).Text("MISSING"));
        Assert.Null(ClassicLocalisation.Empty.Text("KEY"));
    }

    [Fact]
    public void Escaped_quote_inside_text()
    {
        var root = Root("vanilla", GuiLayer.Vanilla, "a_l_english.yml", "l_english:\n SAY:0 \"say \\\"hi\\\"\" # Hinweis\n");

        Assert.Equal("say \"hi\"", ClassicLocalisation.Load(new[] { root }).Text("SAY"));
    }

    [Fact]
    public void Other_languages_and_missing_folder_are_ignored()
    {
        var root = Root("vanilla", GuiLayer.Vanilla, "a_l_german.yml", "l_german:\n KEY:0 \"Deutsch\"\n");
        var empty = new GuiSourceRoot(Path.Combine(_temp, "nowhere"), GuiLayer.Mod, "nowhere");

        var loc = ClassicLocalisation.Load(new[] { root, empty });

        Assert.Null(loc.Text("KEY"));
    }

    [Fact]
    public void Loading_is_lazy_and_assets_expose_it()
    {
        var root = Root("vanilla", GuiLayer.Vanilla, "a_l_english.yml", "l_english:\n KEY:0 \"before\"\n");
        var assets = ClassicAssets.Build(new[] { root }, Array.Empty<GuiSourceFile>(), false, true);

        // Die Datei ändert sich nach dem Aufbau, aber vor dem ersten Text: gelesen wird erst beim ersten Zugriff.
        File.WriteAllText(Path.Combine(root.Directory, "localisation", "a_l_english.yml"), "l_english:\n KEY:0 \"after\"\n");

        Assert.Equal("after", assets.Localisation.Text("KEY"));

        var gfx = new GuiSourceFile(Path.Combine(root.Directory, "interface", "x.gfx"), "interface/x.gfx", GuiLayer.Vanilla, "vanilla", 0, 0);
        Assert.Same(assets.Localisation, assets.WithGfxFile(gfx, "spriteTypes = {}").Localisation);
    }
}
