using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Vor dem Löschen oder Umbenennen eines Elements mit Vanilla-Namen wird nachgefragt (der Spielcode nutzt den Namen vielleicht).</summary>
public class ClassicNameGuardTests
{
    private static readonly GuiLayout Hoi4 = new(
        Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true);

    private const string VanillaText = "guiTypes = {\n\tcontainerWindowType = {\n\t\tName = \"top\"\n\t\ticonType = { name = \"vanilla_icon\" }\n\t}\n}\n";

    private const string ModText =
        "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"mine\"\n\t\ticonType = { name = \"vanilla_icon\" position = { x = 1 y = 2 } }\n"
        + "\t\ticonType = { name = \"mod_icon\" }\n\t}\n}\n";

    private static (GuiIndex Index, GuiNode Root) Setup()
    {
        var mod = GuiParser.Parse(ModText);
        var index = GuiIndex.FromDocuments(
            new[] { GuiIndexTests.F("interface/top.gui"), GuiIndexTests.F("interface/mod.gui", GuiLayer.Mod) },
            new[] { GuiParser.Parse(VanillaText), mod },
            Hoi4);
        return (index, mod.Root);
    }

    private static GuiNode Icon(GuiNode root, string name) =>
        root.Descendants().Single(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name == "iconType"
            && n.Children.Any(c => c.Name == "name" && c.Value == name));

    [Fact]
    public void Deleting_vanilla_named_element_is_flagged()
    {
        var (index, root) = Setup();

        Assert.Equal("vanilla_icon", ClassicNameGuard.VanillaNameAffected(Icon(root, "vanilla_icon"), deletes: true, index));
    }

    [Fact]
    public void Renaming_vanilla_name_is_flagged()
    {
        var (index, root) = Setup();
        var nameNode = Icon(root, "vanilla_icon").Children.Single(c => c.Name == "name");

        Assert.Equal("vanilla_icon", ClassicNameGuard.VanillaNameAffected(nameNode, deletes: false, index));
    }

    [Fact]
    public void Mod_only_name_is_not_flagged()
    {
        var (index, root) = Setup();
        var icon = Icon(root, "mod_icon");

        Assert.Null(ClassicNameGuard.VanillaNameAffected(icon, deletes: true, index));
        Assert.Null(ClassicNameGuard.VanillaNameAffected(icon.Children.Single(c => c.Name == "name"), deletes: false, index));
    }

    [Fact]
    public void Other_property_is_not_flagged()
    {
        var (index, root) = Setup();
        var position = Icon(root, "vanilla_icon").Children.Single(c => c.Name == "position");

        Assert.Null(ClassicNameGuard.VanillaNameAffected(position, deletes: false, index));
        // Nur ein Element wird beim Löschen geprüft, keine einzelne Eigenschaft.
        Assert.Null(ClassicNameGuard.VanillaNameAffected(Icon(root, "vanilla_icon").Children.Single(c => c.Name == "name"), deletes: true, index));
    }

    [Fact]
    public void Name_key_in_any_case_counts()
    {
        var vanilla = GuiParser.Parse(VanillaText);
        var index = GuiIndex.FromDocuments(new[] { GuiIndexTests.F("interface/top.gui") }, new[] { vanilla }, Hoi4);
        var window = vanilla.Root.Descendants().Single(n => n.Name == "containerWindowType");

        Assert.Equal("top", ClassicNameGuard.VanillaNameAffected(window, deletes: true, index));
        Assert.Equal("top", ClassicNameGuard.VanillaNameAffected(window.Children.Single(c => c.Name == "Name"), deletes: false, index));
    }
}
