using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PdxSettingsTextTests
{
    /// <summary>Aufbau wie die echte Datei (Tabs, LF, Klammer der Abschnitte am Zeilenanfang).</summary>
    internal const string Sample =
        "{\n\t\"Accessibility\":\t{\n},\n\t\"Audio\":\t{\n\t\t\"volume.bus:/\":\t0,\n\t\t\"volume.vca:/MUSIC_DENSITY\":\t0.200000\n},\n" +
        "\t\"System\":\t{\n\t\t\"editor\":\t\"C:\\\\Program Files\\\\Notepad++\\\\notepad++.exe\"\n},\n\t\"Tools\":\t{\n}\n}\n";

    [Fact]
    public void ReadsTheEditor()
    {
        Assert.Equal(new PdxEditorRead(PdxSettingsProblem.None, @"C:\Program Files\Notepad++\notepad++.exe"), PdxSettingsText.ReadEditor(Sample));
        Assert.Equal(new PdxEditorRead(PdxSettingsProblem.None, null), PdxSettingsText.ReadEditor("{\n\t\"System\":\t{\n}\n}\n"));
        Assert.Equal(PdxSettingsProblem.UnknownFormat, PdxSettingsText.ReadEditor("{ kaputt").Problem);
        Assert.Equal(PdxSettingsProblem.UnknownFormat, PdxSettingsText.ReadEditor("{ \"System\": 3 }").Problem);
        Assert.Equal(PdxSettingsProblem.UnknownFormat, PdxSettingsText.ReadEditor("{ \"System\": { \"editor\": 1 } }").Problem);
    }

    [Fact]
    public void ReplacingOnlyTouchesTheValue()
    {
        var pmt = @"C:\Tools\PMT Ä+\ParadoxModTool.exe";

        var registered = PdxSettingsText.WithEditor(Sample, pmt)!;

        Assert.Equal(Sample.Replace("C:\\\\Program Files\\\\Notepad++\\\\notepad++.exe", "C:\\\\Tools\\\\PMT Ä+\\\\ParadoxModTool.exe"), registered);
        Assert.Equal(Sample, PdxSettingsText.WithEditor(registered, @"C:\Program Files\Notepad++\notepad++.exe"));
    }

    [Fact]
    public void EditorIsInsertedWhereItIsMissing()
    {
        var emptySystem = "{\n\t\"System\":\t{\n},\n\t\"Tools\":\t{\n}\n}\n";
        var otherInSystem = "{\n\t\"System\":\t{\n\t\t\"x\":\t1\n}\n}\n";
        var noSystem = "{\n\t\"Tools\":\t{\n}\n}\n";

        foreach (var text in new[] { emptySystem, otherInSystem, noSystem, "{}" })
        {
            var added = PdxSettingsText.WithEditor(text, @"C:\p.exe")!;
            Assert.Equal(@"C:\p.exe", PdxSettingsText.ReadEditor(added).Editor);
            var removed = PdxSettingsText.WithEditor(added, null)!;
            Assert.Null(PdxSettingsText.ReadEditor(removed).Editor);
            Assert.Equal(PdxSettingsProblem.None, PdxSettingsText.ReadEditor(removed).Problem);
        }

        Assert.Equal("{\n\t\"System\":\t{\n\t\t\"editor\":\t\"C:\\\\p.exe\"\n},\n\t\"Tools\":\t{\n}\n}\n", PdxSettingsText.WithEditor(emptySystem, @"C:\p.exe"));
        Assert.Equal(emptySystem, PdxSettingsText.WithEditor(PdxSettingsText.WithEditor(emptySystem, @"C:\p.exe")!, null));
        Assert.Equal(otherInSystem, PdxSettingsText.WithEditor(PdxSettingsText.WithEditor(otherInSystem, @"C:\p.exe")!, null));
        Assert.Equal("{\n\t\"Tools\":\t{\n},\n\t\"System\":\t{\n\t\t\"editor\":\t\"C:\\\\p.exe\"\n}\n}\n", PdxSettingsText.WithEditor(noSystem, @"C:\p.exe"));
        Assert.Null(PdxSettingsText.WithEditor("{ kaputt", @"C:\p.exe"));
    }

    [Fact]
    public void QuotesAndControlCharactersAreEscaped()
    {
        var text = PdxSettingsText.WithEditor("{}", "C:\\a\"b\tc.exe")!;

        Assert.Contains("\"C:\\\\a\\\"b\\u0009c.exe\"", text);
        Assert.Equal("C:\\a\"b\tc.exe", PdxSettingsText.ReadEditor(text).Editor);
    }
}
