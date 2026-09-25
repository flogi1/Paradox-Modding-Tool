using System.Text;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public sealed class GameEditorRegistrationTests : IDisposable
{
    private const string Pmt = @"C:\Tools\PMT\ParadoxModTool.exe";

    private readonly string _root = Directory.CreateTempSubdirectory("pmt-editor-").FullName;

    private string Settings => Path.Combine(_root, "pdx_settings.json");

    private string State => Path.Combine(_root, "state");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void RegisterThenRestoreIsByteIdentical()
    {
        File.WriteAllBytes(Settings, Encoding.UTF8.GetBytes(PdxSettingsTextTests.Sample));

        Assert.Equal(GameEditorOutcome.Registered, GameEditorRegistration.Register(Settings, Pmt, State).Outcome);
        Assert.Equal(Pmt, GameEditorRegistration.CurrentEditor(Settings).Editor);
        Assert.Equal(PdxSettingsTextTests.Sample, File.ReadAllText(Path.Combine(State, GameEditorRegistration.BackupFileName)));

        var restored = GameEditorRegistration.Restore(Settings, Pmt, State);
        Assert.Equal(GameEditorOutcome.Restored, restored.Outcome);
        Assert.Equal(@"C:\Program Files\Notepad++\notepad++.exe", restored.Detail);
        Assert.Equal(Encoding.UTF8.GetBytes(PdxSettingsTextTests.Sample), File.ReadAllBytes(Settings));
        Assert.Equal(GameEditorOutcome.NothingToRestore, GameEditorRegistration.Restore(Settings, Pmt, State).Outcome);
    }

    [Fact]
    public void RegisteringTwiceKeepsTheFirstOldEditor()
    {
        File.WriteAllText(Settings, PdxSettingsTextTests.Sample);

        GameEditorRegistration.Register(Settings, Pmt, State);
        Assert.Equal(GameEditorOutcome.AlreadyRegistered, GameEditorRegistration.Register(Settings, Pmt, State).Outcome);
        Assert.Equal(GameEditorOutcome.Registered, GameEditorRegistration.Register(Settings, @"D:\Moved\ParadoxModTool.exe", State).Outcome);

        Assert.Equal(@"C:\Program Files\Notepad++\notepad++.exe", GameEditorRegistration.Restore(Settings, @"D:\Moved\ParadoxModTool.exe", State).Detail);
        Assert.Equal(PdxSettingsTextTests.Sample, File.ReadAllText(Settings));
    }

    [Fact]
    public void RestoreLeavesAnEditorAloneThatTheUserChangedSince()
    {
        File.WriteAllText(Settings, PdxSettingsTextTests.Sample);
        GameEditorRegistration.Register(Settings, Pmt, State);
        var changed = PdxSettingsText.WithEditor(File.ReadAllText(Settings), @"C:\VSCode\code.exe")!;
        File.WriteAllText(Settings, changed);

        var result = GameEditorRegistration.Restore(Settings, Pmt, State);

        Assert.Equal(GameEditorOutcome.EditorChangedSince, result.Outcome);
        Assert.Equal(@"C:\VSCode\code.exe", result.Detail);
        Assert.Equal(changed, File.ReadAllText(Settings));
    }

    [Fact]
    public void WithoutAnOldEditorRestoreRemovesTheEntry()
    {
        File.WriteAllText(Settings, "{\n\t\"System\":\t{\n}\n}\n");

        GameEditorRegistration.Register(Settings, Pmt, State);
        Assert.Equal(GameEditorOutcome.Restored, GameEditorRegistration.Restore(Settings, Pmt, State).Outcome);

        Assert.Equal("{\n\t\"System\":\t{\n}\n}\n", File.ReadAllText(Settings));
    }

    [Fact]
    public void BomAndUnknownFormat()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(Settings, bom.Concat(Encoding.UTF8.GetBytes(PdxSettingsTextTests.Sample)).ToArray());
        GameEditorRegistration.Register(Settings, Pmt, State);
        Assert.Equal(bom, File.ReadAllBytes(Settings).Take(3));
        GameEditorRegistration.Restore(Settings, Pmt, State);
        Assert.Equal(bom.Concat(Encoding.UTF8.GetBytes(PdxSettingsTextTests.Sample)), File.ReadAllBytes(Settings));

        File.WriteAllText(Settings, "{ kaputt");
        var unknown = GameEditorRegistration.Register(Settings, Pmt, Path.Combine(_root, "other"));
        Assert.Equal(GameEditorOutcome.UnknownFormat, unknown.Outcome);
        Assert.Equal(Settings, unknown.Detail);
        Assert.Equal("{ kaputt", File.ReadAllText(Settings));
        Assert.False(Directory.Exists(Path.Combine(_root, "other")));

        Assert.Equal(GameEditorOutcome.SettingsMissing, GameEditorRegistration.Register(Path.Combine(_root, "missing.json"), Pmt, State).Outcome);
        Assert.Equal(PdxSettingsProblem.Missing, GameEditorRegistration.CurrentEditor(Path.Combine(_root, "missing.json")).Problem);
    }
}
