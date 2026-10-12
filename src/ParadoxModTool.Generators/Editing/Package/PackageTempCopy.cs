using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Der aktuelle Editor-Stand (package.json, Vorlagen samt ungespeicherten Texten, functions.sbn) in einem temporären Ordner, damit "Prüfen",
/// "Probelauf" und "Vorschau" den echten Loader nutzen können. Beim Dispose wird der Ordner gelöscht.
/// </summary>
public sealed class PackageTempCopy : IDisposable
{
    private const string FunctionsName = "functions.sbn";

    private readonly PackageDocument _document;

    private PackageTempCopy(PackageDocument document, string folder)
    {
        _document = document;
        Folder = folder;
    }

    public string Folder { get; }

    /// <summary>Vorlagen aus <paramref name="files"/>, überdeckt von ungespeicherten Texten in <paramref name="pending"/>.</summary>
    public static PackageTempCopy Create(PackageDocument document, PackageFiles files, IReadOnlyDictionary<string, string>? pending)
    {
        var (templates, functions) = Texts(files, pending);
        return Write(document, templates, functions);
    }

    /// <summary>Vorlagentexte (Name → Text) und functions.sbn des aktuellen Stands.</summary>
    internal static (Dictionary<string, string> Templates, string Functions) Texts(PackageFiles files, IReadOnlyDictionary<string, string>? pendingTemplates)
    {
        var pending = pendingTemplates ?? new Dictionary<string, string>();
        var templates = files.Templates.Concat(pending.Keys.Where(k => k != FunctionsName)).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => pending.TryGetValue(n, out var text) ? text : files.Read(n), StringComparer.OrdinalIgnoreCase);
        var functions = pending.TryGetValue(FunctionsName, out var f) ? f : files.Functions;
        return (templates, functions);
    }

    internal static PackageTempCopy Write(PackageDocument document, IReadOnlyDictionary<string, string> templates, string functions)
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-check-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(folder, "templates"));
        var json = document.Json.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(folder, "package.json"), json, new UTF8Encoding(false));
        foreach (var (name, text) in templates)
        {
            File.WriteAllText(Path.Combine(folder, "templates", name), text);
        }

        if (functions.Length > 0)
        {
            File.WriteAllText(Path.Combine(folder, FunctionsName), functions);
        }

        return new PackageTempCopy(document, folder);
    }

    /// <summary>Lädt die Kopie mit dem echten Loader; Arbeitsstände und Assets liegen in <paramref name="dataFolder"/> (ohne Angabe der Paketordner).</summary>
    public GeneratorPackage Load(string uiLanguage, string? dataFolder = null) =>
        GeneratorPackageLoader.Load(Folder, dataFolder ?? _document.Folder, uiLanguage: uiLanguage);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // Temp-Ordner bleibt liegen; harmlos.
        }
    }
}
