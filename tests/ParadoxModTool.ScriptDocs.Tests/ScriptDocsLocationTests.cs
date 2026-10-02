using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public class ScriptDocsLocationTests
{
    [Fact]
    public void Uses_subfolder_next_to_mod_folder() =>
        Assert.Equal(@"C:\x\Crusader Kings III\logs", ScriptDocsLocation.Default(@"C:\x\Crusader Kings III\mod", "logs"));

    [Fact]
    public void Trailing_separator_of_mod_folder_is_ignored() =>
        Assert.Equal(@"C:\x\Europa Universalis V\docs", ScriptDocsLocation.Default(@"C:\x\Europa Universalis V\mod\", "docs"));

    [Fact]
    public void Empty_template_gives_null() =>
        Assert.Null(ScriptDocsLocation.Default("", "docs"));
}
