using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class EditorArgumentsTests
{
    private const string Gui = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game\in_game\gui\topbar.gui";

    private static readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase) { Gui, @"C:\Mods\a.gui" };

    private static EditorRequest? Parse(params string[] args) => EditorArguments.Parse(args, Files.Contains);

    [Fact]
    public void QuotedPathWithAndWithoutLine()
    {
        Assert.Equal(new EditorRequest(Gui, null), Parse(Gui));
        Assert.Equal(new EditorRequest(Gui, 12), Parse(Gui, "12"));
    }

    [Fact]
    public void SplitPathsAndLineFormats()
    {
        Assert.Equal(new EditorRequest(Gui, 7), Parse(Gui.Split(' ').Append("7").ToArray()));
        Assert.Equal(new EditorRequest(Gui, 12), Parse("-n12", Gui));
        Assert.Equal(new EditorRequest(Gui, 12), Parse("+12", Gui));
        Assert.Equal(new EditorRequest(Gui, 12), Parse("-l12", Gui));
        Assert.Equal(new EditorRequest(Gui, 12), Parse("--line=12", Gui));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", 34), Parse(@"C:\Mods\a.gui:34"));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", 34), Parse(@"C:\Mods\a.gui:34:5"));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", 5), Parse(@"C:\Mods\a.gui(5)"));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", 5), Parse("\"C:\\Mods\\a.gui\"", "5"));
    }

    [Fact]
    public void OptionsAndMissingFilesGiveNothing()
    {
        Assert.Null(Parse());
        Assert.Null(Parse("--game", "eu5"));
        Assert.Null(Parse(@"C:\Mods\missing.gui", "3"));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", null), Parse("--game", "eu5", @"C:\Mods\a.gui"));
        Assert.Equal(new EditorRequest(@"C:\Mods\a.gui", null), Parse(@"C:\Mods\a.gui", "0"));
        Assert.Null(Parse("--target", @"C:\Mods"));
    }

    [Fact]
    public void RelativePathsAndSlashesBecomeFullPaths()
    {
        var relative = Path.Combine("sub", "x.gui");
        Assert.Equal(new EditorRequest(Path.GetFullPath(relative), 4), EditorArguments.Parse(new[] { relative, "4" }, path => path == relative));
        Assert.Equal(new EditorRequest(@"C:\Mods\b.gui", null), EditorArguments.Parse(new[] { "C:/Mods/b.gui" }, path => path == "C:/Mods/b.gui"));
    }

    [Fact]
    public void RoundTripKeepsPlusAndUmlauts()
    {
        var request = new EditorRequest(@"C:\Mods\Ärger++\ä gui.gui", 3);

        Assert.Equal(request, EditorHandOffMessage.Decode(EditorHandOffMessage.Encode(request)));
    }

    [Fact]
    public void CommandsRoundTrip()
    {
        foreach (var request in new[]
        {
            EditorRequest.ForMod(HandOffKind.OpenMod, "m1"),
            EditorRequest.ForMod(HandOffKind.OpenReleases, "m1"),
            EditorRequest.ForMod(HandOffKind.OpenBug, "m1", "b7"),
            EditorRequest.ForMod(HandOffKind.OpenBug, "m1"),
        })
        {
            Assert.Equal(request, EditorHandOffMessage.Decode(EditorHandOffMessage.Encode(request)));
        }
    }

    [Fact]
    public void OldFormatIsOpenFile()
    {
        var request = EditorHandOffMessage.Decode("{\"Path\":\"C:\\\\a.gui\",\"Line\":3}");

        Assert.NotNull(request);
        Assert.Equal(HandOffKind.OpenFile, request!.Kind);
        Assert.Equal(@"C:\a.gui", request.Path);
        Assert.Equal(3, request.Line);
        Assert.DoesNotContain("Kind", EditorHandOffMessage.Encode(new EditorRequest(@"C:\a.gui", 3)));
    }

    [Fact]
    public void CommandsNeedTheirIds()
    {
        Assert.Null(EditorHandOffMessage.Decode("{\"Kind\":\"OpenMod\"}"));
        Assert.Null(EditorHandOffMessage.Decode("{\"Kind\":\"OpenBug\",\"BugId\":\"b\"}"));
        Assert.Null(EditorHandOffMessage.Decode("{\"Kind\":\"Explode\",\"ModId\":\"m\"}"));
    }

    [Fact]
    public void BrokenMessagesGiveNothing()
    {
        Assert.Null(EditorHandOffMessage.Decode(string.Empty));
        Assert.Null(EditorHandOffMessage.Decode("{"));
        Assert.Null(EditorHandOffMessage.Decode("{\"Path\":\"\"}"));
        Assert.Equal(new EditorRequest(@"C:\a.gui", null), EditorHandOffMessage.Decode("{\"Path\":\"C:\\\\a.gui\",\"Line\":0}"));
    }

    [Fact]
    public void EditorCallIsAnyArgumentBesidesOptionsAndTheGameId()
    {
        Assert.False(EditorArguments.LooksLikeEditorCall(Array.Empty<string>()));
        Assert.False(EditorArguments.LooksLikeEditorCall(new[] { "--game", "eu5" }));
        Assert.False(EditorArguments.LooksLikeEditorCall(new[] { "--game", "eu5", "--safe" }));
        Assert.True(EditorArguments.LooksLikeEditorCall(new[] { "gui/topbar.gui" }));
        Assert.True(EditorArguments.LooksLikeEditorCall(new[] { "--game", "eu5", "unbekannt", "12" }));
    }
}
