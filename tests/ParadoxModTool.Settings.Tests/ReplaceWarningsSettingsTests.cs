using System.Text.Json;
using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ReplaceWarningsSettingsTests
{
    [Fact]
    public void WarningsGoToTheNotesPanelByDefaultAndInOldSessions()
    {
        Assert.False(new AppSettings().ReplaceWarningsInWindow);

        var settings = new AppSettings { ReplaceWarningsInWindow = true };
        settings.Load(JsonSerializer.Deserialize<AppSessionState>("{}")!, Array.Empty<string>());

        Assert.False(settings.ReplaceWarningsInWindow);
    }

    [Fact]
    public void TheWindowOptionRoundTripsThroughTheSession()
    {
        var state = new AppSessionState();
        new AppSettings { ReplaceWarningsInWindow = true }.WriteTo(state);
        Assert.True(state.ReplaceWarningsInWindow);

        var loaded = new AppSettings();
        loaded.Load(state, Array.Empty<string>());
        Assert.True(loaded.ReplaceWarningsInWindow);
    }

    [Fact]
    public void ChangingTheOptionRaisesChangedOnce()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.ReplaceWarningsInWindow = true;
        settings.ReplaceWarningsInWindow = true;

        Assert.Equal(new[] { nameof(AppSettings.ReplaceWarningsInWindow) }, names);
    }
}
