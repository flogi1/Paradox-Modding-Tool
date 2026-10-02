using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Compatch;

public enum FileChangeKind
{
    Create,
    Modify,
    Delete,
    Unchanged,
}

/// <summary>Eine Datei, die das Schreiben anlegt, ändert oder löscht. <see cref="PreviewText"/> ist der neue Text zur Anzeige (leer beim Löschen).</summary>
public sealed record FileChange(string RelativePath, FileChangeKind Kind, byte[]? Content, string PreviewText);

public enum WriteIssueKind
{
    DuplicateInPatch,
    Locked,
    NoSource,
    SameFile,
    UnreadableTarget,
    RemovalAmbiguous,
    KeptChangedFile,
}

public sealed record WriteIssue(WriteIssueKind Kind, string RelativePath, string Message);

/// <summary>Ein Eintrag, den PMT in eine fremde Patch-Datei geschrieben hat (Vergleichsbasis, Ruling 11).</summary>
public sealed record WrittenEntry(string RelativePath, string AddressId);

public sealed class WriteRequest
{
    public required CompatchRun Run { get; init; }

    public required CompatchInput Input { get; init; }

    public required PatchTarget Target { get; init; }

    public required string TargetFolder { get; init; }

    /// <summary>Gesperrte Pfade der Ziel-Mod (relativ, <c>/</c>); ein Ordner sperrt alles darunter.</summary>
    public IReadOnlyCollection<string> LockedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<WrittenEntry> PreviousForeignEntries { get; init; } = Array.Empty<WrittenEntry>();

    /// <summary>Früher geschriebene ganze Dateien: relativer Pfad (klein, <c>/</c>) → SHA-256 des geschriebenen Inhalts.</summary>
    public IReadOnlyDictionary<string, string> PreviousWholeFiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Kodierung PMT-eigener Dateien je Endung (<c>.txt</c>, <c>.gui</c>), aus dem Spielprofil. Lokalisierung ist immer <c>utf-8-bom</c>.</summary>
    public Func<string, string> EncodingFor { get; init; } = _ => "utf-8-bom";
}

public sealed class PreparedWrite
{
    public List<FileChange> Changes { get; } = new();

    public List<WriteIssue> Issues { get; } = new();

    /// <summary>Geschriebene Einträge, die nur wirken, wenn der Patch nach den Mods lädt (Anzeige "Bereich: Schlüssel" bzw. Dateipfad).</summary>
    public List<string> OrderDependent { get; } = new();

    public List<WrittenEntry> ForeignEntries { get; } = new();

    /// <summary>Geschriebene ganze Dateien: relativer Pfad (klein, <c>/</c>) → SHA-256.</summary>
    public Dictionary<string, string> WholeFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Count(FileChangeKind kind) => Changes.Count(c => c.Kind == kind);
}

public sealed record WriteResult(string? SnapshotPath, int Written, int Deleted);

/// <summary>Schreiben ist gescheitert. Die Meldung ist für den Nutzer gedacht und nennt den Snapshot.</summary>
public sealed class CompatchWriteException : Exception
{
    public CompatchWriteException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>Setzt einen <see cref="PatchPlan"/> in Dateiänderungen im Zielordner um (Spec, Abschnitt "Schreiben"; Rulings 9 bis 14).</summary>
public static class PatchWriter
{
    private static readonly Regex LocLanguage = new("_l_([a-z_]+)\\.yml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Berechnet alle Änderungen, ohne etwas zu schreiben.</summary>
    public static PreparedWrite Prepare(WriteRequest request)
    {
        var result = new PreparedWrite();
        var plan = request.Run.Plan;
        var locks = new CompatchFileFilter(new Dictionary<string, string>(), request.LockedPaths, skipLocked: true);
        var index = TargetIndex.Build(request.TargetFolder, request.Input, readForeignKeys: request.Target == PatchTarget.Compatch);
        foreach (var unreadable in index.Unreadable)
        {
            result.Issues.Add(new WriteIssue(WriteIssueKind.UnreadableTarget, unreadable.RelativePath, CompatchTexts.IssueUnreadable(unreadable.RelativePath, unreadable.Error)));
        }

        var wholeFilePaths = plan.Files.Select(f => Norm(f.RelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var splices = new Dictionary<string, List<(int Start, int End, string Text)>>(StringComparer.OrdinalIgnoreCase);
        var own = new Dictionary<string, List<PatchEntryOp>>(StringComparer.OrdinalIgnoreCase);
        var plannedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var op in plan.Entries)
        {
            plannedIds.Add(op.Address.Id);
            var found = index.Find(op.Address);
            if (found.Count > 1)
            {
                var files = string.Join(", ", found.Select(f => f.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase));
                result.Issues.Add(new WriteIssue(WriteIssueKind.DuplicateInPatch, found[0].RelativePath, CompatchTexts.IssueDuplicate(op.Address.Key, files)));
                continue;
            }

            if (found.Count == 1 && !wholeFilePaths.Contains(Norm(found[0].RelativePath)) && Span(found[0].Part) is { } span)
            {
                if (locks.IsLocked(found[0].RelativePath))
                {
                    result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, found[0].RelativePath, CompatchTexts.IssueLocked(found[0].RelativePath)));
                    continue;
                }

                AddSplice(splices, found[0].RelativePath, span, op.InlineText.TrimEnd('\n'));
                result.ForeignEntries.Add(new WrittenEntry(Norm(found[0].RelativePath), op.Address.Id));
                AddOrderDependent(result, op);
                continue;
            }

            if (locks.IsLocked(op.TargetFile))
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, op.TargetFile, CompatchTexts.IssueLocked(op.TargetFile)));
                continue;
            }

            if (!own.TryGetValue(op.TargetFile, out var list))
            {
                list = new List<PatchEntryOp>();
                own[op.TargetFile] = list;
            }

            list.Add(op);
            AddOrderDependent(result, op);
        }

        foreach (var previous in request.PreviousForeignEntries.Where(p => !plannedIds.Contains(p.AddressId)))
        {
            var matches = index.FindById(previous.AddressId).Where(l => string.Equals(Norm(l.RelativePath), previous.RelativePath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            if (matches.Count > 1 || Span(matches[0].Part) is not { } span)
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.RemovalAmbiguous, previous.RelativePath, CompatchTexts.IssueRemovalAmbiguous(previous.AddressId, previous.RelativePath)));
                continue;
            }

            if (locks.IsLocked(previous.RelativePath))
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, previous.RelativePath, CompatchTexts.IssueLocked(previous.RelativePath)));
                continue;
            }

            AddSplice(splices, matches[0].RelativePath, span, string.Empty);
        }

        foreach (var (path, ops) in own.OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
        {
            var isLoc = path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
            var text = OwnFileText(path, ops, isLoc);
            AddFile(result, request.TargetFolder, path, TextEncodings.Encode(text, isLoc ? "utf-8-bom" : request.EncodingFor(Path.GetExtension(path).ToLowerInvariant())), text);
        }

        // UpdateMod schreibt nur in die eigenen Dateien der Mod und löscht nie etwas.
        var stales = request.Target == PatchTarget.UpdateMod
            ? Enumerable.Empty<string>()
            : index.OwnFiles.Where(p => !own.Keys.Any(k => string.Equals(Norm(k), Norm(p), StringComparison.OrdinalIgnoreCase)));
        foreach (var stale in stales)
        {
            if (locks.IsLocked(stale))
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, stale, CompatchTexts.IssueLocked(stale)));
                continue;
            }

            result.Changes.Add(new FileChange(stale, FileChangeKind.Delete, null, string.Empty));
        }

        WriteWholeFiles(request, result, locks, splices);

        foreach (var (path, edits) in splices.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            var parts = index.ForeignFiles[path];
            var text = parts.Text;
            var eol = text.Contains("\r\n") ? "\r\n" : "\n";
            foreach (var (start, end, value) in edits.OrderByDescending(e => e.Start))
            {
                var stop = end;
                if (value.Length == 0)
                {
                    if (stop < text.Length && text[stop] == '\r')
                    {
                        stop++;
                    }

                    if (stop < text.Length && text[stop] == '\n')
                    {
                        stop++;
                    }
                }

                text = text[..start] + value.Replace("\r\n", "\n").Replace("\n", eol) + text[stop..];
            }

            var encoding = parts.EncodingName == "windows-1252" ? "windows-1252" : TextEncodings.HasUtf8Bom(parts.FullPath) ? "utf-8-bom" : "utf-8";
            AddFile(result, request.TargetFolder, path, TextEncodings.Encode(text, encoding), text);
        }

        return result;
    }

    /// <summary>
    /// Führt die Änderungen aus: Snapshot (nur wenn sich etwas ändert), alle neuen Inhalte als Temp-Dateien, dann Ersetzen, dann Löschen.
    /// Scheitert ein Schritt, werden übrige Temp-Dateien entfernt und <see cref="CompatchWriteException"/> nennt den Snapshot.
    /// </summary>
    public static WriteResult Commit(PreparedWrite prepared, string targetFolder, Func<string?>? snapshot)
    {
        var writes = prepared.Changes.Where(c => c.Kind is FileChangeKind.Create or FileChangeKind.Modify).ToList();
        var deletes = prepared.Changes.Where(c => c.Kind == FileChangeKind.Delete).ToList();
        if (writes.Count == 0 && deletes.Count == 0)
        {
            return new WriteResult(null, 0, 0);
        }

        var snapshotPath = snapshot?.Invoke();
        var temps = new List<(string Temp, string Full)>();
        try
        {
            foreach (var change in writes)
            {
                var full = FullPath(targetFolder, change.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                var temp = full + ".pmt-tmp";
                temps.Add((temp, full));
                File.WriteAllBytes(temp, change.Content!);
            }

            foreach (var (temp, full) in temps)
            {
                File.Move(temp, full, overwrite: true);
            }

            foreach (var change in deletes)
            {
                File.Delete(FullPath(targetFolder, change.RelativePath));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var (temp, _) in temps)
            {
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    // Aufräumen ist nur ein Versuch; die eigentliche Meldung zählt.
                }
            }

            throw new CompatchWriteException(CompatchTexts.WriteFailed(ex.Message, snapshotPath ?? "-"), ex);
        }

        return new WriteResult(snapshotPath, writes.Count, deletes.Count);
    }

    private static void WriteWholeFiles(WriteRequest request, PreparedWrite result, CompatchFileFilter locks, Dictionary<string, List<(int Start, int End, string Text)>> splices)
    {
        foreach (var file in request.Run.Plan.Files)
        {
            var relative = file.RelativePath.Replace('\\', '/');
            if (locks.IsLocked(relative))
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, relative, CompatchTexts.IssueLocked(relative)));
                continue;
            }

            var full = FullPath(request.TargetFolder, relative);
            byte[] content;
            string preview;
            if (file.Text is { } text)
            {
                content = TextEncodings.Encode(text, file.EncodingName == "windows-1252" ? "windows-1252" : file.Utf8Bom ? "utf-8-bom" : "utf-8");
                preview = text;
            }
            else if (file.CopyFrom is { } source)
            {
                if (string.Equals(Path.GetFullPath(source), full, StringComparison.OrdinalIgnoreCase))
                {
                    result.Issues.Add(new WriteIssue(WriteIssueKind.SameFile, relative, CompatchTexts.IssueSameFile(relative)));
                    continue;
                }

                content = File.ReadAllBytes(source);
                preview = TextEncodings.Decode(content);
            }
            else
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.NoSource, relative, CompatchTexts.IssueNoSource(relative)));
                continue;
            }

            splices.Remove(relative);
            AddFile(result, request.TargetFolder, relative, content, preview);
            if (request.Target == PatchTarget.UpdateMod)
            {
                // Die Datei gehört der Mod: nicht als "von PMT geschrieben" merken, sonst löschte ein späterer Lauf sie (Spec, Abschnitt 5).
                continue;
            }

            result.WholeFiles[Norm(relative)] = TextEncodings.Sha256(content);
            if (file.DependsOnLoadOrder && request.Target != PatchTarget.Compatch)
            {
                result.OrderDependent.Add(relative);
            }
        }

        if (request.Target == PatchTarget.UpdateMod)
        {
            return;
        }

        foreach (var (relative, hash) in request.PreviousWholeFiles)
        {
            if (result.WholeFiles.ContainsKey(relative) || result.Changes.Any(c => string.Equals(Norm(c.RelativePath), relative, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var full = FullPath(request.TargetFolder, relative);
            if (!File.Exists(full))
            {
                continue;
            }

            if (TextEncodings.Sha256(File.ReadAllBytes(full)) != hash)
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.KeptChangedFile, relative, CompatchTexts.IssueKeptChangedFile(relative)));
            }
            else if (locks.IsLocked(relative))
            {
                result.Issues.Add(new WriteIssue(WriteIssueKind.Locked, relative, CompatchTexts.IssueLocked(relative)));
            }
            else
            {
                result.Changes.Add(new FileChange(relative, FileChangeKind.Delete, null, string.Empty));
            }
        }
    }

    private static string OwnFileText(string path, IReadOnlyList<PatchEntryOp> ops, bool isLoc)
    {
        var sb = new StringBuilder();
        if (isLoc)
        {
            sb.Append("l_").Append(LocLanguage.Match(path).Groups[1].Value.ToLowerInvariant()).Append(":\n");
            sb.Append(" # ").Append(CompatchTexts.OwnFileHeader).Append('\n');
            foreach (var op in ops.OrderBy(o => o.Address.Key, StringComparer.Ordinal))
            {
                sb.Append(op.Text);
            }

            return sb.ToString();
        }

        sb.Append("# ").Append(CompatchTexts.OwnFileHeader).Append('\n');
        if (path.Replace('\\', '/').Split('/').Contains("events", StringComparer.OrdinalIgnoreCase))
        {
            foreach (var ns in ops.Select(o => o.Address.Key).Where(k => k.Contains('.')).Select(k => k[..k.IndexOf('.')]).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
            {
                sb.Append("namespace = ").Append(ns).Append('\n');
            }
        }

        foreach (var op in ops.OrderBy(o => o.Address.Key, StringComparer.Ordinal))
        {
            sb.Append('\n').Append(op.Text);
        }

        return sb.ToString();
    }

    private static void AddOrderDependent(PreparedWrite result, PatchEntryOp op)
    {
        if (op.DependsOnLoadOrder)
        {
            result.OrderDependent.Add(op.Address.Scope + ": " + op.Address.Key);
        }
    }

    /// <summary>Ersetzen oder (leerer Text) Entfernen einer Textstelle; beim Entfernen verschwindet auch der Zeilenumbruch dahinter.</summary>
    private static void AddSplice(Dictionary<string, List<(int Start, int End, string Text)>> splices, string path, (int Start, int End) span, string text)
    {
        if (!splices.TryGetValue(path, out var list))
        {
            list = new List<(int, int, string)>();
            splices[path] = list;
        }

        list.Add((span.Start, span.End, text));
    }

    internal static (int Start, int End)? Span(KeyPart part)
    {
        var entries = part.Original;
        if (entries.Count == 0 || entries.Any(e => e.SourceStart is null || e.SourceEnd is null))
        {
            return null;
        }

        var start = entries.Min(e => e.SourceStart!.Value);
        var end = entries.Max(e => e.SourceEnd!.Value);
        return (start, end);
    }

    internal static void AddFile(PreparedWrite result, string folder, string relative, byte[] content, string preview)
    {
        var full = FullPath(folder, relative);
        var kind = !File.Exists(full)
            ? FileChangeKind.Create
            : File.ReadAllBytes(full).AsSpan().SequenceEqual(content) ? FileChangeKind.Unchanged : FileChangeKind.Modify;
        result.Changes.Add(new FileChange(relative, kind, content, preview));
    }

    /// <summary>Voller Pfad im Zielordner; ein Pfad, der aus dem Ordner herausführt (<c>..</c>), ist ein Fehler.</summary>
    internal static string FullPath(string folder, string relative)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new CompatchInputException(CompatchTexts.PathOutsideTarget(relative));
        }

        return full;
    }

    internal static string Norm(string path) => path.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
