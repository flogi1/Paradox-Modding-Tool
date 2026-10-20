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

    [Fact]
    public void ForInstall_combines_root_and_subfolder() =>
        Assert.Equal(@"C:\Steam\Hearts of Iron IV\documentation", ScriptDocsLocation.ForInstall(@"C:\Steam\Hearts of Iron IV\", "documentation"));

    [Fact]
    public void ForInstall_returns_null_without_root()
    {
        Assert.Null(ScriptDocsLocation.ForInstall(null, "documentation"));
        Assert.Null(ScriptDocsLocation.ForInstall("  ", "documentation"));
    }

    [Fact]
    public void Import_with_missing_install_folder_returns_null()
    {
        var database = new ScriptDocDatabase();
        var missing = Path.Combine(Path.GetTempPath(), "pmt-no-docs-" + Guid.NewGuid().ToString("N"), "documentation");

        Assert.Null(ScriptDocImporter.Import(database, missing, DateTime.UtcNow, RealHoi4DocsTests.Names));
        Assert.Empty(database.Entries);
        Assert.Empty(ScriptDocImporter.ReadWriteTicks(missing, RealHoi4DocsTests.Names));
    }
}
