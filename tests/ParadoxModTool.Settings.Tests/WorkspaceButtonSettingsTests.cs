using System.Text.Json;
using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class WorkspaceButtonSettingsTests
{
    [Fact]
    public void ButtonsAreVisibleByDefault()
    {
        var settings = new AppSettings();
        Assert.True(settings.WorkspaceFilterButtonVisible);
        Assert.True(settings.WorkspaceIconButtonVisible);
    }

    [Fact]
    public void OldSessionWithoutTheFieldsShowsBothButtons()
    {
        var state = JsonSerializer.Deserialize<AppSessionState>("{}")!;
        var settings = new AppSettings { WorkspaceFilterButtonVisible = false, WorkspaceIconButtonVisible = false };

        settings.Load(state, Array.Empty<string>());

        Assert.True(settings.WorkspaceFilterButtonVisible);
        Assert.True(settings.WorkspaceIconButtonVisible);
    }

    [Fact]
    public void ButtonsRoundTripThroughTheSession()
    {
        var state = new AppSessionState();
        new AppSettings { WorkspaceFilterButtonVisible = false }.WriteTo(state);

        Assert.False(state.WorkspaceFilterButtonVisible);
        Assert.True(state.WorkspaceIconButtonVisible);

        var loaded = new AppSettings();
        loaded.Load(state, Array.Empty<string>());
        Assert.False(loaded.WorkspaceFilterButtonVisible);
        Assert.True(loaded.WorkspaceIconButtonVisible);
    }

    [Fact]
    public void ChangingAButtonRaisesChangedOnce()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.WorkspaceIconButtonVisible = false;
        settings.WorkspaceIconButtonVisible = false;

        Assert.Equal(new[] { nameof(AppSettings.WorkspaceIconButtonVisible) }, names);
    }
}
