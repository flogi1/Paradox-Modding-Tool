using ParadoxModTool.Updates;
using Xunit;

namespace ParadoxModTool.Updates.Tests;

public class ReadmeFilesTests
{
    [Fact]
    public void ChangelogLivesInTheReadmeFolder()
    {
        Assert.Equal(@"C:\PMT\Readme\Changelog.txt", ReadmeFiles.ChangelogIn(@"C:\PMT"));
    }
}
