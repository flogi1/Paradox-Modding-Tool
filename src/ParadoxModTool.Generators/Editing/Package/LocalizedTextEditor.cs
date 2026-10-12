namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Tippen in ein Feld für einen mehrsprachigen Text: die bearbeitete Sprache steht beim Start fest (die, die das Feld zeigt). Würde sie bei jedem
/// Tastendruck neu bestimmt, fiele nach dem Leeren des Felds die Sprache weg und der nächste Buchstabe überschriebe Englisch.
/// </summary>
public sealed class LocalizedTextEditor
{
    private readonly string _language;

    public LocalizedTextEditor(LocalizedText value, string uiLanguage)
    {
        Value = value;
        _language = value.ResolvedLanguage(uiLanguage);
    }

    public LocalizedText Value { get; private set; }

    /// <summary>Neuer Text des Felds für die beim Start festgelegte Sprache.</summary>
    public LocalizedText Type(string text) => Value = Value.With(_language, text);
}
