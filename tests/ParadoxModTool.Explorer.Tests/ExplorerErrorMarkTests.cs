using System.ComponentModel;
using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public sealed class ExplorerErrorMarkTests
{
    [Fact]
    public void SetErrorMarkNotifiesOnlyOnChange()
    {
        var entry = new ExplorerEntry { FullPath = @"C:\mod\common", Name = "common", IsDirectory = true };
        var changed = new List<string?>();
        ((INotifyPropertyChanged)entry).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        entry.SetErrorMark("⚠3", "drei");
        entry.SetErrorMark("⚠3", "drei");

        Assert.Equal("⚠3", entry.ErrorBadge);
        Assert.Equal("drei", entry.ErrorTooltip);
        Assert.Equal(new[] { nameof(ExplorerEntry.ErrorBadge), nameof(ExplorerEntry.ErrorTooltip) }, changed);

        entry.SetErrorMark(null, null);

        Assert.Null(entry.ErrorBadge);
        Assert.Equal(4, changed.Count);
    }
}
