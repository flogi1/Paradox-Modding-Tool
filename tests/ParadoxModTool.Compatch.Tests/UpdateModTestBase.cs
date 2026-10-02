using ParadoxModTool.ModCompat;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Ziel "Mod an neue Vanilla anpassen": Vanilla = alte Vanilla-Fassung, A = neue Vanilla-Fassung, B = die Mod, geschrieben wird in B
/// (Spec 2026-09-30-vanilla-update-merge).
/// </summary>
public abstract class UpdateModTestBase : WriterTestBase
{
    protected CompatchInput UpdateInput(bool euV = false, OverrideRules? rules = null, IReadOnlyCollection<string>? noBase = null, CompatchFileFilter? filter = null) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        BLoadsAfterA = true,
        Rules = rules ?? (euV ? EuVRules() : EuIVRules()),
        NoBasePaths = noBase ?? Array.Empty<string>(),
        Filter = filter ?? CompatchFileFilter.Default,
        KeyOperators = euV ? EuVOps : Array.Empty<string>(),
        ExcludeOwnPatchFilesOf = MergeSide.B,
    };

    protected static CompatchRun RunUpdate(CompatchInput input) => CompatchEngine.Run(input, PatchTarget.UpdateMod);

    /// <summary>Schreibt den Lauf in die Mod (B) und gibt die vorbereiteten Änderungen zurück.</summary>
    protected PreparedWrite WriteUpdate(CompatchInput input, CompatchRun run, IReadOnlyDictionary<string, string>? previousFiles = null)
    {
        var prepared = PatchWriter.Prepare(Request(input, run, PatchTarget.UpdateMod, BDir, previousFiles: previousFiles));
        PatchWriter.Commit(prepared, BDir, null);
        return prepared;
    }

    protected string ModPath(string relative) => Path.Combine(BDir, relative.Replace('/', Path.DirectorySeparatorChar));

    protected string ReadMod(string relative) => File.ReadAllText(ModPath(relative));

    protected byte[] ModBytes(string relative) => File.ReadAllBytes(ModPath(relative));

    protected static void PutBytes(string root, string relative, byte[] content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}
