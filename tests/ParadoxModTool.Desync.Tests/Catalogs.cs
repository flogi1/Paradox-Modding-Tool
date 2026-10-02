using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync.Tests;

internal static class Catalogs
{
    private static readonly Lazy<ErrorCatalog> Eu5Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "eu5.json")));

    public static ErrorCatalog Eu5 => Eu5Catalog.Value;
}
