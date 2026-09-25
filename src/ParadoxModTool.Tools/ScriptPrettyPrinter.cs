using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Tools;

/// <summary>
/// Ergebnis eines Formatierungsversuchs durch <see cref="ScriptPrettyPrinter"/>. Bewusst als
/// eigener Ergebnistyp statt einer geworfenen Ausnahme, damit der Aufrufer (die UI) einen
/// fehlgeschlagenen Versuch ohne try/catch behandeln kann - ein Fehlschlag ist hier der
/// ERWARTETE Fall bei Dateien außerhalb des Jomini-Skriptdialekts (z. B. .yml, .lua), nicht
/// ein Ausnahmezustand.
/// </summary>
public sealed class ScriptFormatResult
{
    public required bool Success { get; init; }

    /// <summary>Der neu formatierte Text, sofern <see cref="Success"/> true ist.</summary>
    public string? FormattedText { get; init; }

    /// <summary>
    /// Verständliche Fehlermeldung (inkl. Zeilennummer, siehe <see cref="ScriptParseException"/>),
    /// sofern <see cref="Success"/> false ist - z. B. zur Anzeige in einer MessageBox.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// "Code hübscher darstellen" (Konzept, Abschnitt 10) für Dateien im Jomini-Skriptdialekt
/// (.txt/.gui/.gfx - Textdateien und
/// GUI-Dateien). Baut bewusst NICHT auf einem neuen
/// Formatierer auf, sondern auf dem bereits für den Vanilla-Merge (Abschnitt 7) gehärteten
/// AST-Roundtrip <see cref="ScriptDocument.Parse"/> → <see cref="ScriptWriter.Write"/> - siehe
/// den Klassenkommentar von <see cref="ScriptWriter"/>, der genau diese Wiederverwendung bereits
/// vorgesehen hat.
///
/// <see cref="TryFormat"/> fängt <see cref="ScriptParseException"/> bewusst ab, statt sie
/// weiterzureichen: Anders als beim Merge (wo nur bereits als Skriptdatei bekannte Vanilla-/
/// Moddateien verarbeitet werden) kann dieses Werkzeug über die UI auf JEDE aktive Datei
/// angewendet werden, einschließlich .yml (eigene, hier nicht unterstützte Lokalisierungs-
/// syntax) oder versehentlich fehlerhaftem Skript - ein Parse-Fehler darf dabei niemals die
/// Originaldatei beschädigen oder das Programm abstürzen lassen, sondern muss dem Nutzer als
/// verständliches Ergebnis zurückgegeben werden (siehe <see cref="ScriptFormatResult"/>).
/// </summary>
public static class ScriptPrettyPrinter
{
    public static ScriptFormatResult TryFormat(string source, ScriptWriteOptions? options = null)
    {
        try
        {
            var root = ScriptDocument.Parse(source);
            var formatted = ScriptWriter.Write(root, options);
            return new ScriptFormatResult { Success = true, FormattedText = formatted };
        }
        catch (ScriptParseException ex)
        {
            return new ScriptFormatResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
