using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public class FolderFilterSettingsTests : IDisposable
{
    public FolderFilterSettingsTests() => FolderFilterSettings.Load(null);

    public void Dispose() => FolderFilterSettings.Load(null);

    [Fact]
    public void Add_creates_a_visible_filter_and_reports_the_change()
    {
        var changes = 0;
        EventHandler handler = (_, _) => changes++;
        FolderFilterSettings.Changed += handler;
        try
        {
            var filter = FolderFilterSettings.Add("Idea Variation Hauptmod", @"C:\mod\Idea Variation");

            Assert.Equal(1, changes);
            Assert.True(filter.ShowInBar);
            Assert.False(string.IsNullOrEmpty(filter.Id));
            Assert.Same(filter, FolderFilterSettings.Find(filter.Id));
        }
        finally
        {
            FolderFilterSettings.Changed -= handler;
        }
    }

    [Fact]
    public void Rename_hide_and_remove_change_the_list()
    {
        var filter = FolderFilterSettings.Add("A", @"C:\mod\A");

        FolderFilterSettings.Rename(filter.Id, "B");
        FolderFilterSettings.SetShown(filter.Id, false);

        Assert.Equal("B", FolderFilterSettings.Find(filter.Id)!.Name);
        Assert.False(FolderFilterSettings.Find(filter.Id)!.ShowInBar);

        FolderFilterSettings.Remove(filter.Id);

        Assert.Empty(FolderFilterSettings.Filters);
    }

    [Fact]
    public void Load_drops_broken_entries_and_Snapshot_returns_copies()
    {
        FolderFilterSettings.Load(new[]
        {
            new FolderFilterDefinition { Id = "x", Name = "X", FolderPath = @"C:\mod\X" },
            new FolderFilterDefinition { Id = "y", Name = "", FolderPath = @"C:\mod\Y" },
        });

        var snapshot = FolderFilterSettings.Snapshot();
        snapshot[0].Name = "changed";

        Assert.Equal("X", FolderFilterSettings.Filters.Single().Name);
    }
}
