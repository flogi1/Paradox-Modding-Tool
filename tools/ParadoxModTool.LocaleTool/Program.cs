using System.Text;
using ParadoxModTool.LocaleTool;

// Werkzeug für die Sprachdateien in locales/ (Spec community-translations, Abschnitt 4):
//   missing <code> [--prefix <p>] [--max <n>]   fehlende Keys als JSON mit deutschem und englischem Text
//   merge <code> <chunk.json>                    übersetzte Keys einfügen
//   check [<code>]                               Dateien gegen de.json prüfen
// Ohne --locales <dir> gilt der Ordner locales neben der ParadoxModTool.slnx oberhalb des aktuellen Ordners.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var arguments = args.ToList();
var locales = TakeOption(arguments, "--locales") ?? FindLocales();
if (locales is null)
{
    Console.Error.WriteLine("locales folder not found; pass --locales <dir>.");
    return 64;
}

switch (arguments.FirstOrDefault())
{
    case "missing" when arguments.Count >= 2:
        var prefix = TakeOption(arguments, "--prefix");
        var max = int.TryParse(TakeOption(arguments, "--max"), out var parsed) && parsed > 0 ? parsed : int.MaxValue;
        Console.WriteLine(LocaleCommands.Missing(locales, arguments[1], prefix, max));
        return 0;
    case "merge" when arguments.Count >= 3:
        return LocaleCommands.Merge(locales, arguments[1], arguments[2]);
    case "check":
        return LocaleCommands.Check(locales, arguments.Count >= 2 ? arguments[1] : null, Console.Out);
    default:
        Console.Error.WriteLine("usage: LocaleTool missing <code> [--prefix <p>] [--max <n>] | merge <code> <chunk.json> | check [<code>] [--locales <dir>]");
        return 64;
}

static string? TakeOption(List<string> arguments, string name)
{
    var index = arguments.IndexOf(name);
    if (index < 0 || index + 1 >= arguments.Count)
    {
        return null;
    }

    var value = arguments[index + 1];
    arguments.RemoveRange(index, 2);
    return value;
}

static string? FindLocales()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
        {
            return Path.Combine(directory.FullName, "locales");
        }
    }

    return null;
}
