namespace ParadoxModTool.ErrorLog.Tests;

internal static class Catalogs
{
    private static readonly Lazy<ErrorCatalog> Eu5Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "eu5.json")));

    public static ErrorCatalog Eu5 => Eu5Catalog.Value;

    public static readonly string UserLogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Europa Universalis V", "logs");

    public static List<ErrorEntry> Classify(params string[] lines) => ErrorClassifier.Classify(LogReader.Parse(lines), Eu5);
}
