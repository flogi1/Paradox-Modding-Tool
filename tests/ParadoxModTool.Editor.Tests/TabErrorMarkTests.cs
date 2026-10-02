using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class TabErrorMarkTests
{
    [Fact]
    public void ErrorIconFollowsProviderAndIconSetting()
    {
        TabIconSettings.LoadHidden(null);
        TabErrorProvider.Provider = path => path.EndsWith("a.txt", StringComparison.Ordinal) ? new TabErrorMark(3, "drei Meldungen") : null;
        try
        {
            var document = new OpenDocument(@"C:\mod\a.txt", string.Empty, null);
            var other = new OpenDocument(@"C:\mod\b.txt", string.Empty, null);
            var changed = new List<string?>();
            document.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Assert.True(document.ErrorIconVisible);
            Assert.Equal("⚠3", document.ErrorBadge);
            Assert.Equal("drei Meldungen", document.ErrorTooltip);
            Assert.False(other.ErrorIconVisible);

            TabIconSettings.SetVisible(TabIconKind.ErrorLog, false);
            document.NotifyIconSettingsChanged();
            Assert.False(document.ErrorIconVisible);
            Assert.Contains(nameof(OpenDocument.ErrorIconVisible), changed);

            TabIconSettings.SetVisible(TabIconKind.ErrorLog, true);
            TabErrorProvider.Provider = _ => null;
            document.RefreshErrorMark();
            Assert.False(document.ErrorIconVisible);
            Assert.Equal(string.Empty, document.ErrorBadge);
        }
        finally
        {
            TabErrorProvider.Provider = null;
            TabIconSettings.LoadHidden(null);
        }
    }
}
