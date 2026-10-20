using System.Text;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Designer-Änderungen an einer klassischen Datei in Windows-1252 (wie EU IV end_of_combat.gui): gelesen mit <see cref="GuiTextReader"/>, geändert über
/// die Designer-Ersetzungen, wieder als Windows-1252 kodiert. Außerhalb der Ersetzung bleiben die Bytes gleich. Eigenschaftsnamen gelten klassisch ohne
/// Rücksicht auf Groß-/Kleinschreibung (<c>Orientation</c> = <c>orientation</c>), im Jomini-Format wie bisher genau.
/// </summary>
public sealed class ClassicTextEditTests : IDisposable
{
    // Zeichen über 0x7F (é, ê, ô) vor, in und nach den geänderten Stellen; CRLF; Werte in Anführungszeichen; Kommentare.
    private const string Text =
        "# Kommentar mit é\r\n"
        + "guiTypes = {\r\n"
        + "\tcontainerWindowType = {\r\n"
        + "\t\tname = \"fenêtre\"\r\n"
        + "\t\tposition = { x = 10 y = 20 } # Lage é\r\n"
        + "\t\tOrientation = \"UPPER_LEFT\" # Anker\r\n"
        + "\t\ticonType = {\r\n"
        + "\t\t\tname = \"icône\"\r\n"
        + "\t\t\tspriteType = \"GFX_a\" # é\r\n"
        + "\t\t}\r\n"
        + "\t}\r\n"
        + "}\r\n"
        + "# Ende é\r\n";

    private static readonly Encoding Windows1252 = Create1252();

    private readonly string _path = Path.Combine(Path.GetTempPath(), "pmt-classicedit-" + Guid.NewGuid().ToString("N") + ".gui");
    private readonly byte[] _bytes;
    private readonly GuiDocument _document;

    public ClassicTextEditTests()
    {
        _bytes = Windows1252.GetBytes(Text);
        File.WriteAllBytes(_path, _bytes);
        var (text, encoding) = GuiTextReader.Read(_path);
        Assert.Equal(GuiTextReader.Windows1252, encoding);
        _document = GuiParser.Parse(text);
    }

    public void Dispose() => File.Delete(_path);

    private GuiNode Window => _document.Root.Descendants().First(n => n.Name == "containerWindowType");

    [Fact]
    public void Set_property_in_other_case_replaces_the_existing_key_and_keeps_its_casing()
    {
        var edit = GuiDesignerEdits.SetProperty(_document, Window, "orientation", "\"CENTER\"", ignoreNameCase: true);

        var result = AssertOnlySpansChanged(edit.Edits);
        Assert.Contains("\t\tOrientation = \"CENTER\" # Anker\r\n", result);
        Assert.DoesNotContain("orientation =", result);
    }

    [Fact]
    public void Jomini_set_property_still_matches_the_exact_name()
    {
        var edit = GuiDesignerEdits.SetProperty(_document, Window, "orientation", "\"CENTER\"");

        var result = AssertOnlySpansChanged(edit.Edits);
        Assert.Contains("Orientation = \"UPPER_LEFT\"", result);
        Assert.Contains("orientation = \"CENTER\"", result);
    }

    [Fact]
    public void Remove_property_removes_only_the_property()
    {
        var property = Window.Children.First(c => c.Name == "Orientation");

        var edit = GuiDesignerEdits.RemoveProperty(_document, property);

        // Der Kommentar dahinter bleibt stehen (wie im Jomini-Format: die Zeile gehört nicht der Eigenschaft allein).
        var result = AssertOnlySpansChanged(edit.Edits);
        Assert.Equal(Text.Replace("\t\tOrientation = \"UPPER_LEFT\" # Anker\r\n", "\t\t# Anker\r\n"), result);
    }

    [Fact]
    public void Duplicate_keeps_the_rest_byte_identical()
    {
        var icon = Window.Children.First(c => c.Name == "iconType");

        var edit = GuiDesignerEdits.Duplicate(_document, icon);

        var result = AssertOnlySpansChanged(edit.Edits);
        Assert.Equal(2, CountOf(result, "name = \"icône\""));
    }

    /// <summary>Wendet die Ersetzungen an, kodiert als Windows-1252 und vergleicht die Bytes vor der ersten und nach der letzten Ersetzung.</summary>
    private string AssertOnlySpansChanged(IReadOnlyList<TextEdit> edits)
    {
        Assert.NotEmpty(edits);
        var result = TextEdit.Apply(_document.Text, edits);
        var bytes = Windows1252.GetBytes(result);

        // Windows-1252 hat ein Byte je Zeichen: Textstellen sind Bytestellen.
        var start = edits.Min(e => e.Offset);
        var end = edits.Max(e => e.Offset + e.Length);
        var tail = _bytes.Length - end;
        Assert.Equal(_bytes.AsSpan(0, start).ToArray(), bytes.AsSpan(0, start).ToArray());
        Assert.Equal(_bytes.AsSpan(end).ToArray(), bytes.AsSpan(bytes.Length - tail).ToArray());
        Assert.Equal(result, Windows1252.GetString(bytes));
        return result;
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static Encoding Create1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
