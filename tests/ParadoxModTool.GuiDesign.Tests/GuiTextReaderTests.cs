using System.Text;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiTextReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-guitext-" + Guid.NewGuid().ToString("N"));

    public GuiTextReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(byte[] bytes)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".gui");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Reads_windows1252_when_utf8_is_invalid()
    {
        // Rohes 0xE9 (é in Windows-1252), kein gültiges UTF-8: die Bytes nicht aus einem C#-String bauen, der wäre als UTF-8 gültig.
        var bytes = Encoding.ASCII.GetBytes("guiTypes = { # ").Concat(new byte[] { 0xE9 }).Concat(Encoding.ASCII.GetBytes(" }")).ToArray();

        var (text, name) = GuiTextReader.Read(Write(bytes));

        Assert.Equal("windows-1252", name);
        Assert.Contains("é", text);
        Assert.DoesNotContain('�', text);
    }

    [Fact]
    public void Reads_utf8_with_and_without_bom()
    {
        var body = Encoding.UTF8.GetBytes("guiTypes = { # é }");

        var plain = GuiTextReader.Read(Write(body));
        var bom = GuiTextReader.Read(Write(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray()));

        Assert.Equal(("guiTypes = { # é }", "utf-8"), plain);
        Assert.Equal(("guiTypes = { # é }", "utf-8-bom"), bom);
        Assert.DoesNotContain('﻿', bom.Text);
    }

    [Fact]
    public void Bom_with_windows1252_body_drops_the_bom()
    {
        // BOM, dann ein Körper, der kein gültiges UTF-8 ist: der Körper wird als Windows-1252 gelesen, das BOM erscheint nicht als "ï»¿".
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.ASCII.GetBytes("guiTypes = { # ")).Concat(new byte[] { 0xE9 }).Concat(Encoding.ASCII.GetBytes(" }")).ToArray();

        var (text, name) = GuiTextReader.Read(Write(bytes));

        Assert.Equal("windows-1252", name);
        Assert.Equal("guiTypes = { # é }", text);
    }

    [Theory]
    [InlineData(".gui")]
    [InlineData(".GFX")]
    public void ReadFile_falls_back_to_windows1252_for_gui_and_gfx(string extension)
    {
        // Wie OpenDocument beim Öffnen und MainWindow beim Neuladen nach einem Schreiben: sonst käme die Datei als Ersatzzeichen in Tab und Index.
        var path = Path.Combine(_dir, "end_of_combat" + extension);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("text = \"caf").Concat(new byte[] { 0xE9 }).Concat(Encoding.ASCII.GetBytes("\"")).ToArray());

        Assert.Equal("text = \"café\"", GuiTextReader.ReadFile(path));
    }

    [Fact]
    public void ReadFile_reads_other_files_as_utf8()
    {
        // Alle anderen Dateien bleiben UTF-8 (Architektur-Spezifikation 5h): ungültige Bytes werden zum Ersatzzeichen wie bisher.
        var path = Path.Combine(_dir, "notes.txt");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("caf").Concat(new byte[] { 0xE9 }).ToArray());

        Assert.Equal("caf�", GuiTextReader.ReadFile(path));
    }

    [Fact]
    public void Empty_file_is_utf8()
    {
        Assert.Equal((string.Empty, "utf-8"), GuiTextReader.Read(Write(Array.Empty<byte>())));
    }
}
