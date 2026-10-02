using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Mods;

/// <summary>
/// Das Mod-Verzeichnis eines Spiels (<c>games\&lt;spiel&gt;\mods.json</c>). Es liegt in den Programmdaten und nie im Mod-Ordner, weil Steam beim Hochladen den ganzen
/// Ordner mitnimmt. Es schreibt nur der Prozess dieses Spiels; jede Änderung wird sofort atomar gespeichert.
/// </summary>
public sealed class ModRegistry
{
    public const string FileName = "mods.json";

    private const int CurrentVersion = 1;

    private static readonly System.Text.RegularExpressions.Regex DiscordKeyPattern = new(@"^\d{1,20}/\d{1,20}$");

    private static bool IsSnowflake(string? text) => text is { Length: > 0 and <= 20 } && text.All(char.IsAsciiDigit);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _gameDirectory;
    private List<ModInfo> _mods = new();
    private List<string> _ignoredPaths = new();

    private ModRegistry(string gameDirectory)
    {
        _gameDirectory = gameDirectory;
    }

    public string FilePath => Path.Combine(_gameDirectory, FileName);

    public IReadOnlyList<ModInfo> Mods => _mods;

    /// <summary>Hinweis, wenn <c>mods.json</c> beim Laden nicht lesbar war (dann liegt eine Kopie als <c>mods.json.bad</c> daneben); sonst <see langword="null"/>.</summary>
    public string? LoadWarning { get; private set; }

    /// <summary>Lädt das Register des Spiels. Eine fehlende Datei ergibt ein leeres Register, eine kaputte wird als <c>.bad</c> gesichert.</summary>
    public static ModRegistry Load(string gameDirectory)
    {
        var registry = new ModRegistry(gameDirectory);
        var path = registry.FilePath;
        if (!File.Exists(path))
        {
            return registry;
        }

        try
        {
            var json = SharedFile.Locked<string>(path, () => File.ReadAllText(path));
            var file = JsonSerializer.Deserialize<RegistryFile>(json, Options) ?? throw new JsonException("Die Datei ist leer.");
            registry._mods = Sanitize(file.Mods);
            registry._ignoredPaths = (file.IgnoredPaths ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            registry.LoadWarning = ModsTexts.RegistryLoadWarning;
            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch; die Warnung bleibt.
            }
        }

        return registry;
    }

    public ModInfo? Get(string id) => _mods.FirstOrDefault(m => m.Id == id);

    /// <summary>Legt eine Mod an. Wirft <see cref="ArgumentException"/> bei leerem Namen oder ungültigem Pfad und <see cref="InvalidOperationException"/>, wenn der Ordner schon zu einer Mod gehört.</summary>
    public ModInfo Add(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(ModsTexts.RegistryEmptyName, nameof(name));
        }

        var normalized = ModPaths.Normalize(path);
        EnsurePathFree(normalized);
        var mod = new ModInfo { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), Paths = { normalized } };
        UnignoreIfListed(normalized);
        _mods.Add(mod);
        Save();
        return mod;
    }

    /// <summary>Die Mod mit dem längsten passenden Pfad; <see langword="null"/> bei keinem Treffer oder ungültigem Pfad.</summary>
    public ModInfo? Resolve(string? path)
    {
        if (!ModPaths.TryNormalize(path, out var normalized))
        {
            return null;
        }

        ModInfo? best = null;
        var bestLength = -1;
        foreach (var mod in _mods)
        {
            foreach (var modPath in mod.Paths)
            {
                if (ModPaths.TryNormalize(modPath, out var root) && root.Length > bestLength && ModPaths.IsUnder(normalized, root))
                {
                    best = mod;
                    bestLength = root.Length;
                }
            }
        }

        return best;
    }

    /// <summary>Legt die Changelog-Datei der Mod fest (relativ zum Mod-Ordner); leer oder <see langword="null"/> entfernt die Festlegung.</summary>
    public void SetChangelogFile(string id, string? relativePath)
    {
        var mod = Require(id);
        mod.ChangelogFile = string.IsNullOrWhiteSpace(relativePath) ? null : relativePath.Trim().Replace('\\', '/');
        Save();
    }

    /// <summary>Speichert die Workshop-ID der Mod. <see langword="null"/>, leer oder nur Nullen entfernt sie; alles außer Ziffern wirft <see cref="ArgumentException"/>.</summary>
    public void SetSteamWorkshopId(string id, string? workshopId)
    {
        var mod = Require(id);
        var text = workshopId?.Trim();
        if (!string.IsNullOrEmpty(text) && !text.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(ModsTexts.RegistryWorkshopDigits, nameof(workshopId));
        }

        mod.SteamWorkshopId = string.IsNullOrEmpty(text) || text.All(c => c == '0') ? null : text;
        Save();
    }

    /// <summary>Ersetzt die GitHub-Repositories der Mod. Wirft <see cref="ArgumentException"/> bei ungültigem oder doppeltem Repo und bei einem Tag-Format ohne <c>{version}</c>.</summary>
    public void SetGitHubRepos(string id, IEnumerable<GitHubRepoEntry> repos)
    {
        var mod = Require(id);
        var list = new List<GitHubRepoEntry>();
        foreach (var entry in repos)
        {
            var repo = entry.Repo?.Trim() ?? string.Empty;
            if (!GitHubNames.IsValidRepo(repo))
            {
                throw new ArgumentException(ModsTexts.RegistryNotRepository(repo), nameof(repos));
            }

            if (list.Any(r => r.Repo.Equals(repo, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(ModsTexts.RegistryRepositoryDuplicate(repo), nameof(repos));
            }

            var format = string.IsNullOrWhiteSpace(entry.TagFormat) ? GitHubNames.DefaultTagFormat : entry.TagFormat.Trim();
            if (!format.Contains("{version}", StringComparison.Ordinal))
            {
                throw new ArgumentException(ModsTexts.RegistryTagFormat, nameof(repos));
            }

            list.Add(new GitHubRepoEntry { Repo = repo, TagFormat = format, Enabled = entry.Enabled });
        }

        mod.GitHubRepos = list;
        Save();
    }

    /// <summary>Ersetzt die ZIP-Varianten der Mod. Wirft <see cref="ArgumentException"/> bei leerem oder doppeltem Namen, unzulässigen Ordnerangaben oder einem Wurzelordner mit Pfadzeichen.</summary>
    public void SetGitHubZipVariants(string id, IEnumerable<GitHubZipVariant> variants)
    {
        var mod = Require(id);
        var list = new List<GitHubZipVariant>();
        foreach (var variant in variants)
        {
            var name = variant.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                throw new ArgumentException(ModsTexts.RegistryVariantEmptyName, nameof(variants));
            }

            if (list.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(ModsTexts.RegistryVariantDuplicate(name), nameof(variants));
            }

            var only = variant.OnlyFolders.Select(f => f.Trim().Replace('\\', '/').Trim('/')).Where(f => f.Length > 0).ToList();
            if (only.Any(f => Path.IsPathRooted(f) || f.Split('/').Any(part => part == ".." || part == ".")))
            {
                throw new ArgumentException(ModsTexts.RegistryVariantRelative, nameof(variants));
            }

            var root = string.IsNullOrWhiteSpace(variant.RootFolder) ? null : variant.RootFolder.Trim();
            if (root is not null && (root.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || root is "." or ".."))
            {
                throw new ArgumentException(ModsTexts.RegistryVariantRoot, nameof(variants));
            }

            list.Add(new GitHubZipVariant
            {
                Name = name,
                FileNameFormat = string.IsNullOrWhiteSpace(variant.FileNameFormat) ? GitHubNames.DefaultFileNameFormat : variant.FileNameFormat.Trim(),
                ExcludedFolders = variant.ExcludedFolders.Select(f => f.Trim()).Where(f => f.Length > 0).ToList(),
                ExcludedFilePatterns = variant.ExcludedFilePatterns.Select(f => f.Trim()).Where(f => f.Length > 0).ToList(),
                OnlyFolders = only,
                RootFolder = root,
                Enabled = variant.Enabled,
            });
        }

        mod.GitHubZipVariants = list;
        Save();
    }

    /// <summary>Ersetzt die Bug-Quellen der Mod. Wirft <see cref="ArgumentException"/> bei unbekannter Art, ungültigem Repository, leerem Schlüssel oder doppelter Quelle.</summary>
    public void SetBugSources(string id, IEnumerable<BugSourceEntry> sources)
    {
        var mod = Require(id);
        var list = new List<BugSourceEntry>();
        foreach (var entry in sources)
        {
            var kind = entry.Kind?.Trim() ?? string.Empty;
            var key = entry.Key?.Trim() ?? string.Empty;
            if (kind != BugSourceKinds.GitHub && kind != BugSourceKinds.Discord)
            {
                throw new ArgumentException(ModsTexts.RegistryBugSourceKind(kind), nameof(sources));
            }

            if (key.Length == 0
                || (kind == BugSourceKinds.GitHub && !GitHubNames.IsValidRepo(key))
                || (kind == BugSourceKinds.Discord && !DiscordKeyPattern.IsMatch(key)))
            {
                throw new ArgumentException(ModsTexts.RegistryBugSourceInvalid(key), nameof(sources));
            }

            if (kind == BugSourceKinds.Discord)
            {
                var tags = new[] { entry.OpenTagId, entry.DoneTagId, entry.RejectedTagId };
                if (!tags.All(IsSnowflake) || tags.Distinct().Count() != 3)
                {
                    throw new ArgumentException(ModsTexts.RegistryDiscordTags, nameof(sources));
                }
            }

            if (list.Any(s => s.Kind == kind && s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(ModsTexts.RegistryBugSourceDuplicate(key), nameof(sources));
            }

            var labels = (entry.Labels ?? new List<string>())
                .Select(label => label?.Trim() ?? string.Empty)
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (kind == BugSourceKinds.Discord && !labels.All(IsSnowflake))
            {
                throw new ArgumentException(ModsTexts.RegistryDiscordFilter, nameof(sources));
            }

            list.Add(new BugSourceEntry
            {
                Kind = kind,
                Key = key,
                Enabled = entry.Enabled,
                Labels = labels,
                Name = entry.Name?.Trim() ?? string.Empty,
                OpenTagId = entry.OpenTagId ?? string.Empty,
                DoneTagId = entry.DoneTagId ?? string.Empty,
                RejectedTagId = entry.RejectedTagId ?? string.Empty,
                ArchiveClosed = entry.ArchiveClosed,
            });
        }

        mod.BugSources = list;
        Save();
    }

    public void Rename(string id, string name)
    {
        var mod = Require(id);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(ModsTexts.RegistryEmptyName, nameof(name));
        }

        mod.Name = name.Trim();
        Save();
    }

    /// <summary>Fügt einen weiteren Ordner hinzu (z. B. die Steam-Kopie neben dem Entwicklungsordner).</summary>
    public void AddPath(string id, string path)
    {
        var mod = Require(id);
        var normalized = ModPaths.Normalize(path);
        EnsurePathFree(normalized);
        mod.Paths.Add(normalized);
        UnignoreIfListed(normalized);
        Save();
    }

    public void RemovePath(string id, string path)
    {
        var mod = Require(id);
        var index = IndexOfPath(mod, ModPaths.Normalize(path));
        if (index < 0)
        {
            throw new InvalidOperationException(ModsTexts.RegistryFolderNotOfMod);
        }

        mod.Paths.RemoveAt(index);
        Save();
    }

    /// <summary>Ersetzt einen Ordner der Mod durch einen neuen (Ordner verschoben oder umbenannt). ID, Daten und Regeln bleiben.</summary>
    public void Relink(string id, string oldPath, string newPath)
    {
        var mod = Require(id);
        var index = IndexOfPath(mod, ModPaths.Normalize(oldPath));
        if (index < 0)
        {
            throw new InvalidOperationException(ModsTexts.RegistryOldFolderNotOfMod);
        }

        var normalizedNew = ModPaths.Normalize(newPath);
        if (!string.Equals(ModPaths.Normalize(mod.Paths[index]), normalizedNew, StringComparison.OrdinalIgnoreCase))
        {
            EnsurePathFree(normalizedNew);
        }

        mod.Paths[index] = normalizedNew;
        UnignoreIfListed(normalizedNew);
        Save();
    }

    /// <summary>Entfernt die Mod aus dem Register. Den Datenordner (<see cref="DataDirectory"/>) löscht der Aufrufer nach Rückfrage. Mods, die auf sie zeigten, werden eigenständig.</summary>
    public void Remove(string id)
    {
        var mod = Require(id);
        foreach (var child in _mods.Where(m => m.ParentModId == mod.Id))
        {
            child.ParentModId = null;
            child.Role = ModRole.Main;
        }

        _mods.Remove(mod);
        Save();
    }

    /// <summary>
    /// Ordnet die Mod einem Projekt zu (<paramref name="parentModId"/> = Hauptmod, Rolle Dev-Branch oder Submod) oder löst sie daraus
    /// (<paramref name="parentModId"/> leer). Wirft <see cref="InvalidOperationException"/> mit lesbarem Text, wenn die Zuordnung nicht möglich ist.
    /// </summary>
    public void SetProject(string id, string? parentModId, ModRole role)
    {
        var mod = Require(id);
        if (string.IsNullOrWhiteSpace(parentModId))
        {
            mod.ParentModId = null;
            mod.Role = ModRole.Main;
            Save();
            return;
        }

        if (role == ModRole.Main)
        {
            throw new InvalidOperationException(ModsTexts.RegistryChildHasParent);
        }

        if (mod.IsCompatch)
        {
            throw new InvalidOperationException(ModsTexts.RegistryCompatchNoProject(mod.Name));
        }

        var parent = Get(parentModId) ?? throw new InvalidOperationException(ModsTexts.RegistryParentMissing);
        if (parent.IsCompatch)
        {
            throw new InvalidOperationException(ModsTexts.RegistryCompatchNotMain(parent.Name));
        }

        if (parent.Id == mod.Id)
        {
            throw new InvalidOperationException(ModsTexts.RegistryOwnParent);
        }

        if (parent.ParentModId is not null)
        {
            throw new InvalidOperationException(ModsTexts.RegistryParentInProject(parent.Name));
        }

        if (_mods.Any(m => m.ParentModId == mod.Id))
        {
            throw new InvalidOperationException(ModsTexts.RegistryHasMembers(mod.Name));
        }

        mod.ParentModId = parent.Id;
        mod.Role = role;
        Save();
    }

    /// <summary>
    /// Setzt in einem Schritt die Mods, die dem Projekt des Hauptmods angehören (jeweils Dev-Branch oder Submod). Bisherige Kinder, die in der Liste fehlen, werden eigenständig.
    /// Alles wird vorher geprüft; bei einem Fehler (<see cref="InvalidOperationException"/> mit lesbarem Text) ändert sich nichts.
    /// </summary>
    public void SetProjectMembers(string mainId, IEnumerable<(string ModId, ModRole Role)> members)
    {
        var main = Require(mainId);
        if (main.IsCompatch)
        {
            throw new InvalidOperationException(ModsTexts.RegistryCompatchNotMain(main.Name));
        }

        if (main.ParentModId is not null)
        {
            throw new InvalidOperationException(ModsTexts.RegistryMainInProject(main.Name));
        }

        var list = members.ToList();
        var seen = new HashSet<string>();
        foreach (var (modId, role) in list)
        {
            var mod = Require(modId);
            if (mod.IsCompatch)
            {
                throw new InvalidOperationException(ModsTexts.RegistryCompatchNoProject(mod.Name));
            }

            if (role == ModRole.Main)
            {
                throw new InvalidOperationException(ModsTexts.RegistryMemberHasParent);
            }

            if (mod.Id == main.Id)
            {
                throw new InvalidOperationException(ModsTexts.RegistryOwnParent);
            }

            if (!seen.Add(mod.Id))
            {
                throw new InvalidOperationException(ModsTexts.RegistryMemberDuplicate(mod.Name));
            }

            if (mod.ParentModId is not null && mod.ParentModId != main.Id)
            {
                throw new InvalidOperationException(ModsTexts.RegistryMemberOtherProject(mod.Name));
            }

            if (_mods.Any(m => m.ParentModId == mod.Id))
            {
                throw new InvalidOperationException(ModsTexts.RegistryHasMembers(mod.Name));
            }
        }

        foreach (var child in _mods.Where(m => m.ParentModId == main.Id && !seen.Contains(m.Id)).ToList())
        {
            child.ParentModId = null;
            child.Role = ModRole.Main;
        }

        foreach (var (modId, role) in list)
        {
            var mod = Require(modId);
            mod.ParentModId = main.Id;
            mod.Role = role;
        }

        Save();
    }

    /// <summary>
    /// Hebt alle Sperren von Dateien und Ordnern im Ordner auf (auch der Ordner selbst und alles darunter); ein leerer Pfad meint die ganze Mod.
    /// Die Sperre der ganzen Mod bleibt. Liefert die Zahl der entfernten Einträge.
    /// </summary>
    public int ClearPathLocksUnder(string id, string relativeFolder)
    {
        var mod = Require(id);
        var removed = mod.LockedPaths.RemoveAll(entry => ModLocks.IsUnder(entry, relativeFolder));
        if (removed > 0)
        {
            Save();
        }

        return removed;
    }

    /// <summary>
    /// Macht die Mod zum Kompatch für die angegebenen Ziele (ersetzt frühere Ziele). Wirft <see cref="InvalidOperationException"/> mit lesbarem Text,
    /// wenn die Mod zu einem Projekt gehört oder ein Ziel ungültig ist; dann ändert sich nichts.
    /// </summary>
    public void SetCompatch(string id, IEnumerable<CompatchTarget> targets)
    {
        var mod = Require(id);
        if (mod.ParentModId is not null || _mods.Any(m => m.ParentModId == mod.Id))
        {
            throw new InvalidOperationException(ModsTexts.RegistryProjectToCompatch(mod.Name));
        }

        var list = new List<CompatchTarget>();
        foreach (var target in targets)
        {
            var modId = string.IsNullOrWhiteSpace(target.ModId) ? null : target.ModId.Trim();
            var workshopId = string.IsNullOrWhiteSpace(target.WorkshopId) ? null : target.WorkshopId.Trim();
            var folder = string.IsNullOrWhiteSpace(target.Folder) ? null : ModPaths.Normalize(target.Folder);
            if (new[] { modId, workshopId, folder }.Count(v => v is not null) != 1)
            {
                throw new InvalidOperationException(ModsTexts.RegistryTargetOneSource);
            }

            if (modId == mod.Id)
            {
                throw new InvalidOperationException(ModsTexts.RegistryTargetSelf);
            }

            if (modId is not null && Get(modId) is null)
            {
                throw new InvalidOperationException(ModsTexts.RegistryTargetMissing);
            }

            if (workshopId is not null && !workshopId.All(char.IsAsciiDigit))
            {
                throw new InvalidOperationException(ModsTexts.RegistryWorkshopDigits);
            }

            var copy = new CompatchTarget { ModId = modId, WorkshopId = workshopId, Folder = folder, DisplayName = (target.DisplayName ?? string.Empty).Trim() };
            if (list.Any(t => t.Key == copy.Key))
            {
                throw new InvalidOperationException(ModsTexts.RegistryTargetDuplicate(copy.DisplayName));
            }

            list.Add(copy);
        }

        if (list.Count == 0)
        {
            throw new InvalidOperationException(ModsTexts.RegistryNoTargets);
        }

        mod.IsCompatch = true;
        mod.CompatchTargets = list;
        Save();
    }

    /// <summary>Hebt die Kompatch-Art auf; die Mod wird wieder eine normale Mod.</summary>
    public void ClearCompatch(string id)
    {
        var mod = Require(id);
        mod.IsCompatch = false;
        mod.CompatchTargets = new List<CompatchTarget>();
        Save();
    }

    /// <summary>GUI-Einstellungen der Mod: Zieldateien und Abhängigkeits-Ordner. null (bzw. leerer Name) stellt die Vorbelegung wieder her.</summary>
    public void SetGuiSettings(string id, string? overrideFile, string? typesFile, IEnumerable<string>? dependencies)
    {
        var mod = Require(id);
        mod.GuiOverrideFile = string.IsNullOrWhiteSpace(overrideFile) ? null : overrideFile.Trim();
        mod.GuiTypesFile = string.IsNullOrWhiteSpace(typesFile) ? null : typesFile.Trim();
        mod.GuiDependencies = dependencies?.Select(ModPaths.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Save();
    }

    /// <summary>Sperrt oder entsperrt die Mod (siehe <see cref="ModInfo.IsLocked"/>).</summary>
    public void SetLocked(string id, bool locked)
    {
        Require(id).IsLocked = locked;
        Save();
    }

    /// <summary>
    /// Sperrt oder entsperrt eine Datei oder einen Ordner der Mod (siehe <see cref="ModLocks"/>). Entsperren entfernt nur den eigenen Eintrag: eine Datei unter einem gesperrten Ordner
    /// bleibt gesperrt, bis der Ordner entsperrt wird. Wirft <see cref="InvalidOperationException"/> bei ungültigem Pfad.
    /// </summary>
    public void SetPathLocked(string id, string relativePath, bool locked)
    {
        var mod = Require(id);
        var path = ModLocks.NormalizePath(relativePath);
        var index = mod.LockedPaths.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (locked && index < 0)
        {
            mod.LockedPaths.Add(path);
        }
        else if (!locked && index >= 0)
        {
            mod.LockedPaths.RemoveAt(index);
        }
        else
        {
            return;
        }

        Save();
    }

    /// <summary>Merkt sich einen Ordner, bei dem der Nutzer "keine Mod" gewählt hat, damit die Rückfrage nicht wiederkommt.</summary>
    public void Ignore(string path)
    {
        var normalized = ModPaths.Normalize(path);
        if (!IsIgnored(normalized))
        {
            _ignoredPaths.Add(normalized);
            Save();
        }
    }

    public bool IsIgnored(string path) =>
        ModPaths.TryNormalize(path, out var normalized)
        && _ignoredPaths.Any(p => ModPaths.TryNormalize(p, out var other) && string.Equals(other, normalized, StringComparison.OrdinalIgnoreCase));

    /// <summary>Datenordner der Mod: <c>games\&lt;spiel&gt;\mods\&lt;id&gt;</c>. Wird beim ersten Schreiben angelegt.</summary>
    public string DataDirectory(string id) => Path.Combine(_gameDirectory, "mods", Require(id).Id);

    /// <summary>Pfad relativ zum passenden Ordner der Mod, mit <c>/</c> als Trenner (Schlüsselteil für spätere Umstellung von Notizen und Bugs); <see langword="null"/> außerhalb der Mod.</summary>
    public string? ToRelative(string id, string path)
    {
        var mod = Get(id);
        if (mod is null || !ModPaths.TryNormalize(path, out var normalized))
        {
            return null;
        }

        string? bestRoot = null;
        foreach (var modPath in mod.Paths)
        {
            if (ModPaths.TryNormalize(modPath, out var root) && ModPaths.IsUnder(normalized, root) && (bestRoot is null || root.Length > bestRoot.Length))
            {
                bestRoot = root;
            }
        }

        if (bestRoot is null)
        {
            return null;
        }

        var relative = Path.GetRelativePath(bestRoot, normalized);
        return relative == "." ? string.Empty : relative.Replace('\\', '/');
    }

    /// <summary>Namensvorschlag für einen Mod-Ordner: <c>name</c> aus der Metadaten-Datei des Spielprofils, sonst der Ordnername.</summary>
    public static string SuggestName(string folder, string metadataFile)
    {
        var metadataName = ModMetadata.Read(folder, metadataFile)?.Name;
        if (!string.IsNullOrWhiteSpace(metadataName))
        {
            return metadataName;
        }

        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        return string.IsNullOrWhiteSpace(folderName) ? "Mod" : folderName;
    }

    private ModInfo Require(string id) =>
        Get(id) ?? throw new InvalidOperationException(ModsTexts.RegistryUnknownId(id));

    private static int IndexOfPath(ModInfo mod, string normalized) =>
        mod.Paths.FindIndex(p => ModPaths.TryNormalize(p, out var other) && string.Equals(other, normalized, StringComparison.OrdinalIgnoreCase));

    private void UnignoreIfListed(string normalized) =>
        _ignoredPaths.RemoveAll(p => ModPaths.TryNormalize(p, out var other) && string.Equals(other, normalized, StringComparison.OrdinalIgnoreCase));

    private void EnsurePathFree(string normalized)
    {
        var owner = _mods.FirstOrDefault(m => m.Paths.Any(p => ModPaths.TryNormalize(p, out var other) && string.Equals(other, normalized, StringComparison.OrdinalIgnoreCase)));
        if (owner is not null)
        {
            throw new InvalidOperationException(ModsTexts.RegistryFolderOwned(owner.Name));
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(
            new RegistryFile { Version = CurrentVersion, Mods = _mods.Cast<ModInfo?>().ToList(), IgnoredPaths = _ignoredPaths },
            Options);
        SharedFile.Locked(FilePath, () => SharedFile.WriteAllTextAtomic(FilePath, json));
    }

    private static List<ModInfo> Sanitize(List<ModInfo?>? loaded)
    {
        var result = new List<ModInfo>();
        foreach (var mod in loaded ?? new List<ModInfo?>())
        {
            if (mod is null)
            {
                continue;
            }

            mod.Id = string.IsNullOrWhiteSpace(mod.Id) ? Guid.NewGuid().ToString("N") : mod.Id;
            mod.Name ??= string.Empty;
            mod.Paths = (mod.Paths ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            mod.GitHubRepos = (mod.GitHubRepos ?? new List<GitHubRepoEntry>()).Where(r => r is not null && GitHubNames.IsValidRepo(r.Repo)).ToList();
            mod.GitHubZipVariants = (mod.GitHubZipVariants ?? new List<GitHubZipVariant>()).Where(v => v is not null && !string.IsNullOrWhiteSpace(v.Name)).ToList();
            mod.BugSources = (mod.BugSources ?? new List<BugSourceEntry>())
                .Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Key)
                    && ((s.Kind == BugSourceKinds.Discord && DiscordKeyPattern.IsMatch(s.Key)) || (s.Kind == BugSourceKinds.GitHub && GitHubNames.IsValidRepo(s.Key))))
                .ToList();
            foreach (var source in mod.BugSources)
            {
                source.Labels ??= new List<string>();
                source.Name ??= string.Empty;
                source.OpenTagId ??= string.Empty;
                source.DoneTagId ??= string.Empty;
                source.RejectedTagId ??= string.Empty;
            }
            mod.LockedPaths = ModLocks.NormalizeEntries(mod.LockedPaths);
            mod.CompatchTargets = (mod.CompatchTargets ?? new List<CompatchTarget>()).Where(t => t is not null).ToList();
            if (mod.IsCompatch)
            {
                mod.ParentModId = null;
                mod.Role = ModRole.Main;
            }

            result.Add(mod);
        }

        RepairProjects(result);
        return result;
    }

    /// <summary>Repariert widersprüchliche Projektangaben still: unbekannter oder eigener Elternteil, Elternteil ist selbst Kind (Enkel, Zyklus), Rolle ohne Elternteil, Elternteil ohne Rolle. Betroffene Mods werden Hauptmods.</summary>
    private static void RepairProjects(List<ModInfo> mods)
    {
        var byId = new Dictionary<string, ModInfo>();
        foreach (var mod in mods)
        {
            byId.TryAdd(mod.Id, mod);
        }

        var invalid = mods.Where(mod =>
            !string.IsNullOrEmpty(mod.ParentModId)
            && (mod.Role == ModRole.Main
                || mod.ParentModId == mod.Id
                || !byId.TryGetValue(mod.ParentModId, out var parent)
                || !string.IsNullOrEmpty(parent.ParentModId) || parent.IsCompatch)).ToList();

        foreach (var mod in invalid)
        {
            mod.ParentModId = null;
        }

        foreach (var mod in mods.Where(m => string.IsNullOrEmpty(m.ParentModId)))
        {
            mod.ParentModId = null;
            mod.Role = ModRole.Main;
        }
    }

    private sealed class RegistryFile
    {
        public int Version { get; set; } = CurrentVersion;

        public List<ModInfo?>? Mods { get; set; } = new();

        public List<string>? IgnoredPaths { get; set; } = new();
    }
}
