using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Mitgelieferte Bausteine für HOI IV und EU IV gegen die echten Installationen: jeder Baustein, in ein 600×400-Testfenster eingefügt, legt sich ohne
/// fehlendes Sprite, fehlende Textur oder Schrift; seine <c>requires</c> stehen im Sprite-Index, Scrollleisten-Namen sind echte Fenster. Fehlt ein Spiel,
/// kehrt der Test sofort zurück.
/// </summary>
[Collection(nameof(RealClassicGuiTests))]
public sealed class ClassicShippedBlocksRenderTests
{
    private const string Host = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"pmt_block_test\"\n\t\tposition = { x = 100 y = 100 }\n"
        + "\t\tsize = { width = 600 height = 400 }\n\t}\n}\n";

    private static readonly GuiFindingKind[] MissingKinds = { GuiFindingKind.SpriteMissing, GuiFindingKind.TextureMissing, GuiFindingKind.FontMissing };

    private readonly RealClassicWorkspaces _workspaces;

    public ClassicShippedBlocksRenderTests(RealClassicWorkspaces workspaces) => _workspaces = workspaces;

    private static IReadOnlyList<GuiBlock> Shipped(string game) =>
        new GuiBlockLibrary(Path.Combine(AppContext.BaseDirectory, "profiles", "guiblocks", game), null, null).Effective.Where(b => !b.IsBuiltIn).ToList();

    private BuiltWorkspace? Workspace(string game) => game == "hoi4" ? _workspaces.Hoi4 : _workspaces.Eu4;

    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ClassicShippedBlocks_RenderWithoutMissing(string game)
    {
        if (Workspace(game) is not { } built)
        {
            return;
        }

        var blocks = Shipped(game);
        Assert.NotEmpty(blocks);
        foreach (var block in blocks)
        {
            var document = GuiParser.Parse(Host);
            var window = document.Root.Descendants().Single(n => n.Name == "containerWindowType");
            var code = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)) + "\n";
            var edit = GuiDesignerEdits.Insert(document, new GuiInsertion(window, InsertPosition.LastChild, null), code);
            var result = GuiParser.Parse(TextEdit.Apply(document.Text, edit.Edits));
            Assert.Empty(result.Diagnostics);

            var host = ClassicWindows.Of(result).Single();
            var resolver = built.Workspace.CreateResolver(result, Path.Combine(VanillaGui.For(game).InstallDirectory, "interface", "pmt_block_test.gui"));
            var box = built.Workspace.CreateLayoutEngine().Layout(resolver.Resolve(host), new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 });
            var boxes = new[] { box }.Concat(box.Descendants()).ToList();

            var missing = boxes.SelectMany(b => b.NoteKinds).Where(n => MissingKinds.Contains(n.Kind)).Select(n => n.Text).ToList();
            Assert.True(missing.Count == 0, $"{block.Name}: {string.Join("; ", missing)}");
            Assert.True(boxes.Count > 1, $"{block.Name}: kein Kasten gelegt");
            Assert.True(block.Requires.Count == 0 || boxes.Any(b => b.Visual is not null), $"{block.Name}: nichts gezeichnet");
        }
    }

    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ClassicShippedBlocks_RequiresExistInVanilla(string game)
    {
        if (Workspace(game) is not { } built)
        {
            return;
        }

        var sprites = built.Workspace.Classic!.Sprites;
        foreach (var block in Shipped(game))
        {
            foreach (var name in block.Requires)
            {
                Assert.True(sprites.Find(name) is { Sprite: not null, CaseMismatch: false } || sprites.Font(name) is not null, $"{block.Name}: {name} fehlt");
            }

            Assert.Empty(GuiBlockLibrary.MissingRequirements(block, built.Workspace.Index, sprites));

            // Jedes Sprite und jede Schrift im Rumpf steht in requires (sonst meldet das Fenster "Bausteine" ein Fehlen nicht).
            var text = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
            foreach (Match match in Regex.Matches(text, @"(?i)\b(spriteType|quadTextureSprite|font|buttonFont)\s*=\s*""([^""]+)"""))
            {
                Assert.Contains(match.Groups[2].Value, block.Requires);
            }
        }
    }

    /// <summary>Scrollleisten nennen ein Fenster (HOI IV <c>verticalScrollbar</c>, EU IV <c>scrollbartype</c>), kein Sprite: es muss in vanilla existieren.</summary>
    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ClassicShippedBlocks_ScrollbarsExist(string game)
    {
        if (Workspace(game) is not { } built)
        {
            return;
        }

        var found = 0;
        foreach (var block in Shipped(game))
        {
            var text = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
            foreach (Match match in Regex.Matches(text, @"(?i)\b(verticalScrollbar|horizontalScrollbar|scrollbartype)\s*=\s*""([^""]+)"""))
            {
                found++;
                Assert.True(ClassicWindows.Winner(built.Workspace.Index, match.Groups[2].Value) is not null, $"{block.Name}: {match.Groups[2].Value} fehlt");
            }
        }

        Assert.True(found > 0, "kein Baustein mit Scrollleiste");
    }
}
