using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Ordner mit Vanilla-Sprites (<c>GFX_a</c>, <c>GFX_alpha</c>, <c>GFX_beta</c>, <c>GFX_gamma_alpha</c>), leerer Mod und echten Assets für die Sprite-Fenster.</summary>
internal sealed class SpriteTestEnv : IDisposable
{
    private const string VanillaGfx =
        "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_a\"\n\t\ttexturefile = \"gfx/interface/a.dds\"\n\t\tnoOfFrames = 3\n\t}\n"
        + "\tspriteType = {\n\t\tname = \"GFX_alpha\"\n\t\ttexturefile = \"gfx/interface/alpha.dds\"\n\t}\n"
        + "\tcorneredTileSpriteType = {\n\t\tname = \"GFX_beta\"\n\t\ttexturefile = \"gfx/interface/beta.dds\"\n\t\tborderSize = { x = 4 y = 4 }\n\t}\n}\n";

    private const string MoreGfx =
        "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_gamma_alpha\"\n\t\ttexturefile = \"gfx/interface/gamma.dds\"\n\t\tnoOfFrames = 2\n\t}\n}\n";

    public SpriteTestEnv()
    {
        Temp = Path.Combine(Path.GetTempPath(), "pmt-sprite-window-" + Guid.NewGuid().ToString("N"));
        Vanilla = Path.Combine(Temp, "vanilla");
        ModRoot = Path.Combine(Temp, "mymod");
        Directory.CreateDirectory(ModRoot);
        Roots = new[]
        {
            new GuiSourceRoot(Vanilla, GuiLayer.Vanilla, "vanilla"),
            new GuiSourceRoot(ModRoot, GuiLayer.Mod, "mymod"),
        };
        Write(Vanilla, "interface/core.gfx", VanillaGfx);
        Write(Vanilla, "interface/more.gfx", MoreGfx);
        GfxFiles = GuiSourceSet.Collect(Roots, ".gfx", "interface");
        Assets = ClassicAssets.Build(Roots, GfxFiles, spriteFirstWins: false, caseSensitive: false, f => GuiParser.Parse(File.ReadAllText(f.FullPath)));
    }

    public string Temp { get; }

    public string Vanilla { get; }

    public string ModRoot { get; }

    public IReadOnlyList<GuiSourceRoot> Roots { get; }

    public IReadOnlyList<GuiSourceFile> GfxFiles { get; }

    public ClassicAssets Assets { get; }

    public void Dispose()
    {
        if (Directory.Exists(Temp))
        {
            Directory.Delete(Temp, recursive: true);
        }
    }

    public static string Write(string root, string relative, string text)
    {
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    /// <summary>Eine DDS-Datei mit nur dem Kopf (genug für die Größe).</summary>
    public static string Dds(string path, int width, int height)
    {
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, header);
        return path;
    }

    public ClassicSpriteWindow Window(string? suggestedName = null) => new(Assets, ModRoot, Roots, GfxFiles, suggestedName);
}

/// <summary>"Sprite anlegen" (klassische Spiele): Vorbelegung, Prüfung des Plans und die Rückfrage vor dem Überschreiben einer Bildkopie.</summary>
public sealed class ClassicSpriteWindowTests
{
    [Fact]
    public void Prefills_name_frames_and_target()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window();
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "My Icon.dds"), 120, 40);

            Assert.Equal("GFX_My_Icon", window.SpriteName);
            Assert.Equal(3, window.Frames);
            Assert.Equal("interface/mymod.gfx", window.GfxPath);
            Assert.Equal(ClassicSpriteKind.Sprite, window.Kind);
            Assert.Empty(window.ShownProblems);
            Assert.True(window.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void Suggested_name_wins_over_the_file_name()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window("GFX_wanted");
            Assert.Equal("GFX_wanted", window.SpriteName);

            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "other.dds"), 16, 16);

            Assert.Equal("GFX_wanted", window.SpriteName);
            Assert.Equal(1, window.Frames);
        });
    }

    [Fact]
    public void Gfx_target_avoids_a_vanilla_path()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            SpriteTestEnv.Write(env.Vanilla, "interface/mymod.gfx", "spriteTypes = { }\n");
            var gfxFiles = GuiSourceSet.Collect(env.Roots, ".gfx", "interface");

            var window = new ClassicSpriteWindow(env.Assets, env.ModRoot, env.Roots, gfxFiles, null);

            Assert.Equal("interface/mymod_sprites.gfx", window.GfxPath);
        });
    }

    [Fact]
    public void Collision_disables_create_and_lists_problem()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window();
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);
            window.SpriteName = "GFX_a";

            Assert.Contains(window.ShownProblems, p => p.Contains("GFX_a"));
            Assert.False(window.CreateButton.IsEnabled);

            window.SpriteName = "GFX_fresh";

            Assert.Empty(window.ShownProblems);
            Assert.True(window.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void Without_an_image_create_is_disabled_but_no_problem_is_shown()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window("GFX_new");

            Assert.False(window.CreateButton.IsEnabled);
            Assert.Empty(window.ShownProblems);
        });
    }

    [Fact]
    public void Frames_must_be_a_positive_whole_number()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window("GFX_new");
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);
            window.FramesText = "0";

            Assert.Single(window.ShownProblems);
            Assert.False(window.CreateButton.IsEnabled);

            window.FramesText = "2";

            Assert.Empty(window.ShownProblems);
        });
    }

    [Fact]
    public void Border_fields_only_for_cornered_tile()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window("GFX_new");
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);

            Assert.False(window.BorderVisible);

            window.Kind = ClassicSpriteKind.CorneredTile;
            window.BorderXText = "4";
            window.BorderYText = "6";

            Assert.True(window.BorderVisible);
            Assert.True(window.TryCreate());
            Assert.Equal(ClassicSpriteKind.CorneredTile, window.Request!.Kind);
            Assert.Equal((4, 6), (window.Request.BorderX, window.Request.BorderY));
            Assert.Contains("corneredTileSpriteType", window.Plan!.Files[0].NewText);

            window.Kind = ClassicSpriteKind.Sprite;

            Assert.False(window.BorderVisible);
        });
    }

    [Fact]
    public void Existing_copy_target_asks_once_and_sets_overwrite()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var existing = SpriteTestEnv.Write(env.ModRoot, "gfx/interface/mymod/x.dds", "old");
            var window = env.Window("GFX_new");
            var asked = new List<string>();
            window.ConfirmOverwrite = path =>
            {
                asked.Add(path);
                return true;
            };
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);

            // Das Überschreiben ist keine Sperre, sondern eine Rückfrage beim Anlegen.
            Assert.Empty(window.ShownProblems);
            Assert.True(window.CreateButton.IsEnabled);
            Assert.Empty(asked);

            Assert.True(window.TryCreate());
            Assert.True(window.TryCreate());

            Assert.Equal(new[] { existing }, asked);
            Assert.True(window.Request!.OverwriteCopy);
            Assert.Equal(existing, window.Plan!.CopyTo);
            Assert.True(window.Plan.CanApply);
        });
    }

    [Fact]
    public void Declining_the_overwrite_keeps_the_dialog_open()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            SpriteTestEnv.Write(env.ModRoot, "gfx/interface/mymod/x.dds", "old");
            var window = env.Window("GFX_new");
            window.ConfirmOverwrite = _ => false;
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);

            Assert.False(window.TryCreate());
            Assert.Null(window.Plan);
        });
    }

    [Fact]
    public void New_copy_target_asks_nothing()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = env.Window("GFX_new");
            window.ConfirmOverwrite = _ => throw new InvalidOperationException("keine Rückfrage erwartet");
            window.ImagePath = SpriteTestEnv.Dds(Path.Combine(env.Temp, "src", "x.dds"), 16, 16);

            Assert.True(window.TryCreate());
            Assert.False(window.Request!.OverwriteCopy);
            Assert.Equal(Path.Combine(env.ModRoot, "gfx", "interface", "mymod", "x.dds"), window.Plan!.CopyTo);
        });
    }
}
