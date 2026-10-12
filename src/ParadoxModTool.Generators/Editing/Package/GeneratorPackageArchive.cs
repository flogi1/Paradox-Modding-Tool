using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Was exportiert wird: Paketdateien aus <see cref="PackageFolder"/>, Arbeitsstände und Assets (nur Herkunft Mod) aus <see cref="DataFolder"/>.</summary>
public sealed record PackageExportRequest(string PackageFolder, string DataFolder, bool WithStates, IReadOnlyList<string> AssetIds, string PmtVersion);

/// <summary>Ein Asset des Pakets in einer Import-Datei; <see cref="Included"/>: Datei liegt bei.</summary>
public sealed record ArchiveAsset(string Id, string Path, AssetOrigin Origin, bool Included);

/// <summary>Gelesene Import-Datei: Kennung, Spiel, Name, Zahl der Vorlagen und Arbeitsstände, Assets und übersprungene Einträge.</summary>
public sealed record PackageArchiveContent(
    string File,
    string PackageId,
    string GameId,
    string Name,
    int Templates,
    int States,
    IReadOnlyList<ArchiveAsset> Assets,
    IReadOnlyList<string> Skipped);

public enum ImportMode
{
    NewPackage,
    Replace,
}

/// <summary>
/// Export und Import eines Generator-Pakets als <c>.pmtgen</c> (Spec Teil 5a, Abschnitt 4): ZIP mit <c>manifest.json</c> und den Paketdateien an
/// ihren Pfaden. Import liest nur erlaubte Einträge (Positivliste, Pfadschutz) und zählt die Größen beim Lesen; ausgeführt wird nichts.
/// </summary>
public static class GeneratorPackageArchive
{
    public const string Extension = ".pmtgen";

    public const int MaxPackageFiles = 2000;

    public const long MaxFileBytes = 5L * 1024 * 1024;

    public const long MaxTotalBytes = 50L * 1024 * 1024;

    public const long MaxAssetBytes = 1024L * 1024 * 1024;

    public const long MaxAssetTotalBytes = 2048L * 1024 * 1024;

    private const string ManifestName = "manifest.json";

    private const string PackageName = "package.json";

    private const string FunctionsName = "functions.sbn";

    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private enum EntryKind
    {
        Ignore,
        Skip,
        Manifest,
        Package,
        Functions,
        Template,
        State,
        Asset,
    }

    /// <summary>Schreibt die Datei atomar (erst temporär im Zielordner, dann ersetzen). Assets aus dem Spiel kommen nie hinein.</summary>
    public static void Export(string targetFile, PackageExportRequest request)
    {
        var document = PackageDocument.Load(request.PackageFolder);
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetFile))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetRandomFileName() + ".tmp");
        try
        {
            var states = 0;
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(Path.Combine(request.PackageFolder, PackageName), PackageName);
                var functions = Path.Combine(request.PackageFolder, FunctionsName);
                if (File.Exists(functions))
                {
                    zip.CreateEntryFromFile(functions, FunctionsName);
                }

                foreach (var name in new PackageFiles(request.PackageFolder).Templates)
                {
                    zip.CreateEntryFromFile(Path.Combine(request.PackageFolder, "templates", name), "templates/" + name);
                }

                var statesFolder = Path.Combine(request.DataFolder, "states");
                if (request.WithStates && Directory.Exists(statesFolder))
                {
                    foreach (var state in Directory.GetFiles(statesFolder, "*.json"))
                    {
                        zip.CreateEntryFromFile(state, "states/" + Path.GetFileName(state));
                        states++;
                    }
                }

                var assets = document.Assets.Where(a => a.Asset.Origin == AssetOrigin.Mod && request.AssetIds.Contains(a.Id)).ToList();
                foreach (var (_, asset) in assets)
                {
                    AddAsset(zip, request.DataFolder, asset.Path);
                }

                var manifest = new JsonObject
                {
                    ["format"] = 1,
                    ["packageId"] = document.Id,
                    ["gameId"] = document.GameId,
                    ["pmtVersion"] = request.PmtVersion,
                    ["states"] = states,
                    ["assets"] = new JsonArray(document.Assets.Select(a => (JsonNode?)new JsonObject
                    {
                        ["id"] = a.Id,
                        ["origin"] = a.Asset.Origin == AssetOrigin.Mod ? "mod" : "game",
                    }).ToArray()),
                };
                using var stream = zip.CreateEntry(ManifestName).Open();
                stream.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString(new() { WriteIndented = true })));
            }

            File.Move(temp, targetFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>Liest eine Import-Datei; Größengrenze überschritten oder kein Paket darin ist ein <see cref="GeneratorPackageException"/>.</summary>
    public static PackageArchiveContent Read(string file)
    {
        try
        {
            using var zip = ZipFile.OpenRead(file);
            var package = ReadPackageJson(zip);
            var modAssets = ModAssetPaths(package);
            var skipped = new List<string>();
            var templates = 0;
            var states = 0;
            var included = new HashSet<string>(StringComparer.Ordinal);
            var budget = new Budget();
            foreach (var entry in zip.Entries)
            {
                var (kind, asset) = Classify(entry.FullName, modAssets);
                switch (kind)
                {
                    case EntryKind.Ignore:
                        continue;
                    case EntryKind.Skip:
                        skipped.Add(entry.FullName);
                        continue;
                    case EntryKind.Template:
                        templates++;
                        break;
                    case EntryKind.State:
                        states++;
                        break;
                    case EntryKind.Asset:
                        included.Add(asset!);
                        break;
                }

                using var stream = entry.Open();
                budget.Copy(stream, Stream.Null, entry.FullName, kind == EntryKind.Asset);
            }

            var assets = AllAssets(package).Select(a => new ArchiveAsset(a.Id, a.Asset.Path, a.Asset.Origin, a.Asset.Origin == AssetOrigin.Mod && included.Contains(Normalize(a.Asset.Path)))).ToList();
            return new PackageArchiveContent(
                file,
                Str(package, "id") ?? string.Empty,
                Str(package, "gameId") ?? string.Empty,
                LocalizedText.From(package["name"]).Resolve(UiText.Current.Language),
                templates,
                states,
                assets,
                skipped);
        }
        catch (InvalidDataException ex)
        {
            throw new GeneratorPackageException(GeneratorsTexts.ArchiveInvalid(ex.Message));
        }
    }

    /// <summary>
    /// Entpackt die erlaubten Einträge erst in einen temporären Ordner unter <paramref name="packagesRoot"/>, setzt bei <paramref name="newId"/> die neue
    /// Kennung und verschiebt dann. <see cref="ImportMode.Replace"/>: Paketdateien ersetzt; gleichnamige Arbeitsstände nur nach
    /// <paramref name="confirmOverwriteStates"/>, andere Arbeitsstände und Assets bleiben. Gibt den Paketordner zurück.
    /// </summary>
    public static string Extract(PackageArchiveContent content, string packagesRoot, string? newId, ImportMode mode, Func<IReadOnlyList<string>, bool> confirmOverwriteStates)
    {
        var id = newId ?? content.PackageId;
        var target = Path.Combine(packagesRoot, id);
        var temp = Path.Combine(packagesRoot, ".import-" + Path.GetRandomFileName());
        try
        {
            Unpack(content.File, temp);
            if (newId is not null)
            {
                var document = PackageDocument.Load(temp);
                document.ChangeIdKeepingMarkers(newId);
                document.Save();
            }

            if (mode == ImportMode.NewPackage || !Directory.Exists(target))
            {
                if (Directory.Exists(target))
                {
                    throw new IOException(target);
                }

                Directory.Move(temp, target);
                return target;
            }

            ReplaceInto(temp, target, confirmOverwriteStates);
            return target;
        }
        catch (InvalidDataException ex)
        {
            throw new GeneratorPackageException(GeneratorsTexts.ArchiveInvalid(ex.Message));
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    private static void Unpack(string file, string folder)
    {
        using var zip = ZipFile.OpenRead(file);
        var modAssets = ModAssetPaths(ReadPackageJson(zip));
        var budget = new Budget();
        Directory.CreateDirectory(folder);
        foreach (var entry in zip.Entries)
        {
            var (kind, _) = Classify(entry.FullName, modAssets);
            if (kind is EntryKind.Ignore or EntryKind.Skip or EntryKind.Manifest)
            {
                continue;
            }

            var path = Path.Combine(folder, Normalize(entry.FullName).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = entry.Open();
            using var target = File.Create(path);
            budget.Copy(source, target, entry.FullName, kind == EntryKind.Asset);
        }

        Directory.CreateDirectory(Path.Combine(folder, "templates"));
    }

    private static void ReplaceInto(string source, string target, Func<IReadOnlyList<string>, bool> confirmOverwriteStates)
    {
        File.Copy(Path.Combine(source, PackageName), Path.Combine(target, PackageName), overwrite: true);
        var functions = Path.Combine(source, FunctionsName);
        if (File.Exists(functions))
        {
            File.Copy(functions, Path.Combine(target, FunctionsName), overwrite: true);
        }
        else
        {
            File.Delete(Path.Combine(target, FunctionsName));
        }

        var templates = Path.Combine(target, "templates");
        if (Directory.Exists(templates))
        {
            Directory.Delete(templates, recursive: true);
        }

        PackageStore.CopyTree(Path.Combine(source, "templates"), templates);
        Directory.CreateDirectory(templates);

        var newStates = Path.Combine(source, "states");
        if (Directory.Exists(newStates))
        {
            var oldStates = Path.Combine(target, "states");
            var same = Directory.GetFiles(newStates).Select(Path.GetFileName).OfType<string>()
                .Where(n => File.Exists(Path.Combine(oldStates, n))).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var overwrite = same.Count == 0 || confirmOverwriteStates(same);
            Directory.CreateDirectory(oldStates);
            foreach (var state in Directory.GetFiles(newStates))
            {
                var name = Path.GetFileName(state);
                if (overwrite || !same.Contains(name))
                {
                    File.Copy(state, Path.Combine(oldStates, name), overwrite: true);
                }
            }
        }

        PackageStore.CopyTree(Path.Combine(source, "assets"), Path.Combine(target, "assets"));
    }

    private static JsonObject ReadPackageJson(ZipArchive zip)
    {
        var entry = zip.Entries.FirstOrDefault(e => Normalize(e.FullName) == PackageName)
            ?? throw new GeneratorPackageException(GeneratorsTexts.ArchiveInvalid(PackageName));
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        new Budget().Copy(stream, buffer, entry.FullName, asset: false);
        try
        {
            return JsonNode.Parse(buffer.ToArray(), documentOptions: Lenient) as JsonObject
                ?? throw new GeneratorPackageException(GeneratorsTexts.ArchiveInvalid(PackageName));
        }
        catch (JsonException ex)
        {
            throw new GeneratorPackageException(GeneratorsTexts.ArchiveInvalid(ex.Message));
        }
    }

    private static IEnumerable<(string Id, AssetDef Asset)> AllAssets(JsonObject package) =>
        package["assets"] is JsonObject map
            ? map.Select(p => (p.Key, p.Value is JsonObject o
                ? new AssetDef(Str(o, "path") ?? string.Empty, string.Equals(Str(o, "origin"), "mod", StringComparison.OrdinalIgnoreCase) ? AssetOrigin.Mod : AssetOrigin.Game)
                : new AssetDef(p.Value?.ToString() ?? string.Empty, AssetOrigin.Game)))
            : Enumerable.Empty<(string, AssetDef)>();

    /// <summary>Pfade der Assets mit Herkunft Mod, die ein Import annimmt (nur sichere Pfade).</summary>
    private static IReadOnlyList<string> ModAssetPaths(JsonObject package) =>
        AllAssets(package).Where(a => a.Asset.Origin == AssetOrigin.Mod && SafeRelativePath.IsSafe(a.Asset.Path)).Select(a => Normalize(a.Asset.Path)).ToList();

    /// <summary>Positivliste der Einträge (Spec Teil 5a, Abschnitt 4); Ordner-Einträge werden still übergangen.</summary>
    private static (EntryKind Kind, string? Asset) Classify(string fullName, IReadOnlyList<string> modAssets)
    {
        var name = Normalize(fullName);
        if (name.EndsWith('/') && SafeRelativePath.IsSafe(name.TrimEnd('/')))
        {
            return (EntryKind.Ignore, null);
        }

        if (!SafeRelativePath.IsSafe(name))
        {
            return (EntryKind.Skip, null);
        }

        switch (name)
        {
            case ManifestName:
                return (EntryKind.Manifest, null);
            case PackageName:
                return (EntryKind.Package, null);
            case FunctionsName:
                return (EntryKind.Functions, null);
        }

        if (name.StartsWith("templates/", StringComparison.Ordinal) && PackageFiles.IsValidTemplateName(name["templates/".Length..]))
        {
            return (EntryKind.Template, null);
        }

        if (name.StartsWith("states/", StringComparison.Ordinal) && name["states/".Length..] is { Length: > 5 } state
            && !state.Contains('/') && state.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return (EntryKind.State, null);
        }

        foreach (var asset in modAssets)
        {
            if (name == asset || name.StartsWith(asset + "/", StringComparison.Ordinal))
            {
                return (EntryKind.Asset, asset);
            }
        }

        return (EntryKind.Skip, null);
    }

    private static void AddAsset(ZipArchive zip, string dataFolder, string relative)
    {
        var path = Path.Combine(dataFolder, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            zip.CreateEntryFromFile(path, Normalize(relative));
        }
        else if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                zip.CreateEntryFromFile(file, Normalize(relative) + "/" + Path.GetRelativePath(path, file).Replace('\\', '/'));
            }
        }
    }

    private static string Normalize(string name) => name.Replace('\\', '/');

    private static string? Str(JsonObject o, string key) => o[key] is JsonValue v ? v.ToString() : null;

    /// <summary>Zählt beim Lesen (nicht nach Kopfangaben des ZIP) und bricht bei einer überschrittenen Grenze ab.</summary>
    private sealed class Budget
    {
        private int _files;
        private long _total;
        private long _assetTotal;

        public void Copy(Stream source, Stream target, string entry, bool asset)
        {
            if (!asset && ++_files > MaxPackageFiles)
            {
                throw new GeneratorPackageException(GeneratorsTexts.ArchiveTooLarge(entry));
            }

            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                size += read;
                if (asset)
                {
                    _assetTotal += read;
                    if (size > MaxAssetBytes || _assetTotal > MaxAssetTotalBytes)
                    {
                        throw new GeneratorPackageException(GeneratorsTexts.ArchiveTooLarge(entry));
                    }
                }
                else
                {
                    _total += read;
                    if (size > MaxFileBytes || _total > MaxTotalBytes)
                    {
                        throw new GeneratorPackageException(GeneratorsTexts.ArchiveTooLarge(entry));
                    }
                }

                target.Write(buffer, 0, read);
            }
        }
    }
}
