using System.IO;
using System.Text;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Klassische .gui-Dateien in Windows-1252 (EU IV) öffnen ohne Ersatzzeichen und werden wieder als Windows-1252 geschrieben.</summary>
public sealed class OpenDocumentEncodingTests
{
    private static string WriteBytes(string relative, byte[] bytes)
    {
        var path = TempFiles.Write(relative);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // Rohes 0xE9: kein gültiges UTF-8.
    private static byte[] Latin1Gui() => Encoding.ASCII.GetBytes("guiTypes = { # ").Concat(new byte[] { 0xE9 }).Concat(Encoding.ASCII.GetBytes(" }")).ToArray();

    [Fact]
    public void Gui_file_in_windows1252_opens_without_replacement_characters()
    {
        var path = WriteBytes(@"enc\end_of_combat.gui", Latin1Gui());

        var document = OpenDocument.LoadFromFile(path, null);

        Assert.Contains("é", document.Document.Text);
        Assert.DoesNotContain('\uFFFD', document.Document.Text);
    }

    [Fact]
    public void Gui_file_in_windows1252_is_written_back_as_windows1252()
    {
        var path = WriteBytes(@"enc\roundtrip.gui", Latin1Gui());
        var document = OpenDocument.LoadFromFile(path, null);

        var encoding = ModFileEncoding.ForWrite(null, path);
        File.WriteAllText(path, document.Document.Text, encoding);

        Assert.Equal(Latin1Gui(), File.ReadAllBytes(path));
    }

    [Fact]
    public void Reload_from_disk_also_reads_windows1252()
    {
        var path = WriteBytes(@"enc\reload.gui", Encoding.ASCII.GetBytes("a = 1"));
        var document = OpenDocument.LoadFromFile(path, null);

        File.WriteAllBytes(path, Latin1Gui());
        document.ReloadFromDisk();

        Assert.Contains("é", document.Document.Text);
    }

    [Fact]
    public void Utf8_gui_file_keeps_utf8()
    {
        var path = WriteBytes(@"enc\utf8.gui", Encoding.UTF8.GetBytes("a = \"é\""));
        var document = OpenDocument.LoadFromFile(path, null);

        File.WriteAllText(path, document.Document.Text, ModFileEncoding.ForWrite(null, path));

        Assert.Equal(Encoding.UTF8.GetBytes("a = \"é\""), File.ReadAllBytes(path));
    }
}
