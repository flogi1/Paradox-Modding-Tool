using ParadoxModTool.ReferenceTree;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class SharedSessionsTests
{
    private static readonly object U = new();

    [Fact]
    public void Flags_SameKeyDifferentSpelling_SameInstance()
    {
        var s = new SharedSessions();
        var a = s.Flags(SharedSessions.FlagKey(@"C:\M", null, false), U);
        var b = s.Flags(SharedSessions.FlagKey(@"c:\m\", null, false), U);
        Assert.Same(a, b);
    }

    [Fact]
    public void Flags_DifferentPlayset_DifferentInstance()
    {
        var s = new SharedSessions();
        var a = s.Flags(SharedSessions.FlagKey(@"C:\M", "P1", true), U);
        var b = s.Flags(SharedSessions.FlagKey(@"C:\M", "P2", true), U);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Flags_IsPlaysetDiffers_DifferentInstance()
    {
        var s = new SharedSessions();
        var a = s.Flags(SharedSessions.FlagKey(@"C:\M", "P", true), U);
        var b = s.Flags(SharedSessions.FlagKey(@"C:\M", "P", false), U);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Flags_NullAndEmptyMod_AreSame()
    {
        var s = new SharedSessions();
        Assert.Same(s.Flags(SharedSessions.FlagKey(null, null, false), U), s.Flags(SharedSessions.FlagKey("", null, false), U));
    }

    [Fact]
    public void Checks_SameKeyDifferentSpelling_SameInstance()
    {
        var s = new SharedSessions();
        var a = s.Checks(SharedSessions.CheckKey(@"C:\M", "P"), U);
        var b = s.Checks(SharedSessions.CheckKey(@"c:\m\", "P"), U);
        Assert.Same(a, b);
    }

    [Fact]
    public void Checks_DifferentPlayset_DifferentInstance()
    {
        var s = new SharedSessions();
        Assert.NotSame(s.Checks(SharedSessions.CheckKey(@"C:\M", "P1"), U), s.Checks(SharedSessions.CheckKey(@"C:\M", "P2"), U));
    }

    [Fact]
    public void Checks_NullAndEmptyMod_AreSame()
    {
        var s = new SharedSessions();
        Assert.Same(s.Checks(SharedSessions.CheckKey(null, null), U), s.Checks(SharedSessions.CheckKey("", null), U));
    }

    [Fact]
    public void Flags_ReleaseLastUser_NextCallReturnsNewInstance()
    {
        var s = new SharedSessions();
        var key = SharedSessions.FlagKey(@"C:\M", "P", true);
        var window = new object();
        var a = s.Flags(key, window);
        s.Release(window);
        Assert.NotSame(a, s.Flags(key, new object()));
    }

    [Fact]
    public void Flags_TwoUsers_SurvivesOneRelease()
    {
        var s = new SharedSessions();
        var key = SharedSessions.FlagKey(@"C:\M", "P", true);
        var window = new object();
        var tree = new object();
        var a = s.Flags(key, window);
        Assert.Same(a, s.Flags(key, tree));
        s.Release(window);
        Assert.Same(a, s.Flags(key, new object()));
    }

    [Fact]
    public void Flags_UserSwitchesKey_OldSessionDropped()
    {
        var s = new SharedSessions();
        var keyA = SharedSessions.FlagKey(@"C:\A", "P", true);
        var user = new object();
        var a = s.Flags(keyA, user);
        s.Flags(SharedSessions.FlagKey(@"C:\B", "P", true), user);
        Assert.NotSame(a, s.Flags(keyA, new object()));
    }

    [Fact]
    public void Flags_SameUserTwice_OneReleaseDrops()
    {
        var s = new SharedSessions();
        var key = SharedSessions.FlagKey(@"C:\M", "P", true);
        var user = new object();
        var a = s.Flags(key, user);
        Assert.Same(a, s.Flags(key, user));
        s.Release(user);
        Assert.NotSame(a, s.Flags(key, new object()));
    }

    [Fact]
    public void Checks_ReleaseLastUser_NextCallReturnsNewInstance()
    {
        var s = new SharedSessions();
        var key = SharedSessions.CheckKey(@"C:\M", "P");
        var window = new object();
        var a = s.Checks(key, window);
        s.Release(window);
        Assert.NotSame(a, s.Checks(key, new object()));
    }

    [Fact]
    public void Checks_TwoUsers_SurvivesOneRelease()
    {
        var s = new SharedSessions();
        var key = SharedSessions.CheckKey(@"C:\M", "P");
        var window = new object();
        var tree = new object();
        var a = s.Checks(key, window);
        Assert.Same(a, s.Checks(key, tree));
        s.Release(window);
        Assert.Same(a, s.Checks(key, new object()));
    }

    [Fact]
    public void Checks_UserSwitchesKey_OldSessionDropped()
    {
        var s = new SharedSessions();
        var keyA = SharedSessions.CheckKey(@"C:\A", "P");
        var user = new object();
        var a = s.Checks(keyA, user);
        s.Checks(SharedSessions.CheckKey(@"C:\B", "P"), user);
        Assert.NotSame(a, s.Checks(keyA, new object()));
    }

    [Fact]
    public void Release_DropsBothKinds_KeepsOtherUsers()
    {
        var s = new SharedSessions();
        var flagKey = SharedSessions.FlagKey(@"C:\M", "P", true);
        var checkKey = SharedSessions.CheckKey(@"C:\M", "P");
        var tree = new object();
        var window = new object();
        var flags = s.Flags(flagKey, tree);
        var checks = s.Checks(checkKey, tree);
        var windowFlags = s.Flags(SharedSessions.FlagKey(@"C:\W", "P", true), window);
        s.Release(tree);
        Assert.NotSame(flags, s.Flags(flagKey, new object()));
        Assert.NotSame(checks, s.Checks(checkKey, new object()));
        Assert.Same(windowFlags, s.Flags(SharedSessions.FlagKey(@"C:\W", "P", true), new object()));
    }
}
