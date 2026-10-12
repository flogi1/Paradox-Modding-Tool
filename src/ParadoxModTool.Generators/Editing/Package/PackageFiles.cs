using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Vorlagen eines Pakets: <c>templates/*.sbn</c> und <c>functions.sbn</c> (UTF-8 ohne BOM, Zeilenenden wie im Text).</summary>
public sealed class PackageFiles
{
    private static readonly Regex NamePattern = new(@"^[A-Za-z0-9_.-]+\.sbn$", RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding NoBom = new(false);

    private readonly string _folder;

    public PackageFiles(string folder) => _folder = folder;

    private string TemplatesFolder => Path.Combine(_folder, "templates");

    /// <summary>Dateinamen in <c>templates/</c>, sortiert.</summary>
    public IReadOnlyList<string> Templates =>
        Directory.Exists(TemplatesFolder)
            ? Directory.GetFiles(TemplatesFolder, "*.sbn").Select(Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();

    /// <summary>Name einer Vorlage: nur Datei, keine Unterordner, Endung <c>.sbn</c>.</summary>
    public static bool IsValidTemplateName(string name) => NamePattern.IsMatch(name);

    public string Read(string name) => File.ReadAllText(Path.Combine(TemplatesFolder, name));

    public void Write(string name, string text)
    {
        Directory.CreateDirectory(TemplatesFolder);
        File.WriteAllText(Path.Combine(TemplatesFolder, name), text, NoBom);
    }

    /// <summary>Text von <c>functions.sbn</c>; leer ohne Datei. Ein leerer Text wird geschrieben, nicht gelöscht.</summary>
    public string Functions
    {
        get => File.Exists(FunctionsPath) ? File.ReadAllText(FunctionsPath) : string.Empty;
        set => File.WriteAllText(FunctionsPath, value, NoBom);
    }

    public void Rename(string oldName, string newName) => File.Move(Path.Combine(TemplatesFolder, oldName), Path.Combine(TemplatesFolder, newName));

    public void Delete(string name) => File.Delete(Path.Combine(TemplatesFolder, name));

    private string FunctionsPath => Path.Combine(_folder, "functions.sbn");
}
