using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Paket-Texte: einfacher Text oder Tabelle Sprache → Text (UI-Sprache, dann en, dann erster Eintrag).</summary>
public sealed class PackageTextTests
{
    private static string Resolve(string json, string language)
    {
        using var document = JsonDocument.Parse(json);
        return PackageText.Resolve(document.RootElement, language);
    }

    [Fact]
    public void Resolve_picks_ui_language_then_en_then_first()
    {
        Assert.Equal("Ideen", Resolve("""{"de":"Ideen","en":"Ideas"}""", "de"));
        Assert.Equal("Ideas", Resolve("""{"de":"Ideen","en":"Ideas"}""", "fr"));
        Assert.Equal("Idées", Resolve("""{"fr":"Idées"}""", "de"));
        Assert.Equal("X", Resolve("\"X\"", "de"));
    }
}
