using ParadoxModTool.Updates;
using Xunit;

namespace ParadoxModTool.Updates.Tests;

public class WindowsProcessControlTests
{
    [Theory]
    [InlineData(@"C:\PMT\ParadoxModTool.exe", @"C:\PMT", true)]
    [InlineData(@"C:\PMT\ParadoxModTool.exe", @"C:\PMT\", true)]
    [InlineData(@"c:\pmt\ParadoxModTool.exe", @"C:\PMT", true)]
    [InlineData(@"C:\PMT2\ParadoxModTool.exe", @"C:\PMT", false)]
    [InlineData(@"C:\PMT\sub\ParadoxModTool.exe", @"C:\PMT", false)]
    [InlineData(@"D:\ParadoxModTool.exe", @"D:\", true)]
    [InlineData(@"D:\dev\ParadoxModTool.exe", @"D:\", false)]
    public void Programmdatei_gehoert_zum_Programmordner(string exe, string folder, bool expected) =>
        Assert.Equal(expected, WindowsProcessControl.IsInstalledIn(exe, folder));
}
