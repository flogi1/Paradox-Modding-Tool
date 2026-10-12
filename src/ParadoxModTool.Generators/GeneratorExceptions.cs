namespace ParadoxModTool.Generators;

/// <summary>Ein Paket lässt sich nicht laden (kaputtes package.json, unbekannte Art, fehlende Vorlage ...).</summary>
public sealed class GeneratorPackageException : Exception
{
    public GeneratorPackageException(string message)
        : base(message)
    {
    }
}

/// <summary>Scriban-Fehler beim Lesen oder Ausführen einer Vorlage; <see cref="Line"/> ist 1-basiert.</summary>
public sealed class GeneratorTemplateException : Exception
{
    public GeneratorTemplateException(string templateName, int line, string message)
        : base(GeneratorsTexts.TemplateError(templateName, line, message))
    {
        TemplateName = templateName;
        Line = line;
        Detail = message;
    }

    public string TemplateName { get; }

    public int Line { get; }

    public string Detail { get; }
}

/// <summary>Eine Ausgabe kann nicht angewendet werden (fehlende Datei ohne Grundgerüst, fehlender Block, kaputte Markierung ...).</summary>
public sealed class GeneratorOutputException : Exception
{
    public GeneratorOutputException(string message)
        : base(message)
    {
    }
}
