using System.Text.Json;
using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class PathBarSettingsTests
{
    [Fact]
    public void PathBarIsVisibleByDefaultAndInOldSessions()
    {
        Assert.True(new AppSettings().PathBarVisible);

        var settings = new AppSettings { PathBarVisible = false };
        settings.Load(JsonSerializer.Deserialize<AppSessionState>("{}")!, Array.Empty<string>());

        Assert.True(settings.PathBarVisible);
    }

    [Fact]
    public void PathBarRoundTripsThroughTheSession()
    {
        var state = new AppSessionState();
        new AppSettings { PathBarVisible = false }.WriteTo(state);
        Assert.False(state.PathBarVisible);

        var loaded = new AppSettings();
        loaded.Load(state, Array.Empty<string>());
        Assert.False(loaded.PathBarVisible);
    }

    [Fact]
    public void ChangingThePathBarRaisesChangedOnce()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.PathBarVisible = false;
        settings.PathBarVisible = false;

        Assert.Equal(new[] { nameof(AppSettings.PathBarVisible) }, names);
    }
}
