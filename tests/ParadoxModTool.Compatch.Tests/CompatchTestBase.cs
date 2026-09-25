using Xunit;
using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Drei Ordner (Vanilla, A, B) im Temp-Verzeichnis und Standardregeln für EU V und EU IV.</summary>
public abstract class CompatchTestBase : IDisposable
{
    protected static readonly string[] EuVOps = { "INJECT", "REPLACE", "TRY_INJECT", "TRY_REPLACE", "INJECT_OR_CREATE", "REPLACE_OR_CREATE" };

    protected CompatchTestBase()
    {
        Directory.CreateDirectory(VanillaDir);
        Directory.CreateDirectory(ADir);
        Directory.CreateDirectory(BDir);
    }

    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "pmt-compatch-" + Guid.NewGuid().ToString("N"));

    protected string VanillaDir => Path.Combine(Dir, "vanilla");

    protected string ADir => Path.Combine(Dir, "a");

    protected string BDir => Path.Combine(Dir, "b");

    protected void PutVanilla(string relative, string content) => Put(VanillaDir, relative, content);

    protected void PutA(string relative, string content) => Put(ADir, relative, content);

    protected void PutB(string relative, string content) => Put(BDir, relative, content);

    protected static OverrideRules EuVRules() => new(new[]
    {
        new OverrideRule("*/common", OverrideBehavior.PerKey),
        new OverrideRule("*/common/defines", OverrideBehavior.PartialKeys),
        new OverrideRule("*/common/on_action", OverrideBehavior.AppendOnly),
        new OverrideRule("*/events", OverrideBehavior.FirstWins),
        new OverrideRule("*/gui", OverrideBehavior.LastWins),
        new OverrideRule("*/localization", OverrideBehavior.LocalizationKeys),
        new OverrideRule("*/gfx", OverrideBehavior.WholeFile),
    });

    protected static OverrideRules EuIVRules() => new(new[]
    {
        new OverrideRule("common", OverrideBehavior.PerKey, false),
        new OverrideRule("interface", OverrideBehavior.LastWins, false),
        new OverrideRule("localisation", OverrideBehavior.LocalizationKeys, false),
    });

    protected CompatchInput Input(bool euV = true, CompatchFileFilter? filter = null, bool bLoadsAfterA = true) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        BLoadsAfterA = bLoadsAfterA,
        Rules = euV ? EuVRules() : EuIVRules(),
        KeyOperators = euV ? EuVOps : Array.Empty<string>(),
        Filter = filter ?? CompatchFileFilter.Default,
    };

    /// <summary>
    /// Spielt den Plan als Mod nach B ein (Kopie von B plus Patch-Dateien, der Patch ersetzt gleiche Pfade), löst erneut auf und prüft
    /// für jede vom Plan berührte Adresse: Der Zustand ohne Patch entspricht jetzt dem gewünschten Zustand. Prüft die Folgerichtigkeit
    /// mit dem eigenen Lademodell, nicht mit dem Spiel.
    /// </summary>
    protected void AssertRoundTrip(CompatchInput input, CompatchRun run)
    {
        var overlay = Path.Combine(Dir, "overlay-" + Guid.NewGuid().ToString("N"));
        CopyDir(input.ModBFolder, overlay);
        foreach (var group in run.Plan.Entries.GroupBy(e => e.TargetFile))
        {
            var text = string.Concat(group.Select(e => e.Text));
            if (group.Key.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                var language = System.Text.RegularExpressions.Regex.Match(group.Key, "_l_([a-z_]+)\\.yml$").Groups[1].Value;
                text = "l_" + language + ":\n" + text;
            }

            Put(overlay, group.Key, text);
        }

        foreach (var file in run.Plan.Files)
        {
            var target = Path.Combine(overlay, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (file.CopyFrom is not null)
            {
                File.Copy(file.CopyFrom, target, overwrite: true);
            }
            else
            {
                var encoding = file.EncodingName == "windows-1252" ? System.Text.Encoding.Latin1 : new System.Text.UTF8Encoding(file.Utf8Bom);
                File.WriteAllText(target, file.Text!, encoding);
            }
        }

        var check = new CompatchInput
        {
            VanillaFolder = input.VanillaFolder,
            ModAFolder = input.ModAFolder,
            ModBFolder = overlay,
            BLoadsAfterA = true,
            Rules = input.Rules,
            KeyOperators = input.KeyOperators,
            Filter = input.Filter,
            LogicFieldKeys = input.LogicFieldKeys,
        };
        var after = EffectiveEntryResolver.Resolve(check);
        var touched = run.Plan.Entries.Select(e => e.Address).Concat(run.Plan.Files.SelectMany(f => f.Addresses)).ToHashSet();
        foreach (var address in touched)
        {
            Assert.True(after.Addresses.TryGetValue(address, out var info), "Adresse fehlt nach dem Patch: " + address);
            if (address.Kind == AddressKind.WholeFile)
            {
                var before = run.Resolved.Addresses[address];
                var expected = run.Applied.Desired[address].FileChoice switch { MergeSide.A => before.A, MergeSide.B => before.B, _ => before.Vanilla };
                Assert.Equal(expected.FileHash, info!.NoPatch.FileHash);
                continue;
            }

            var desired = run.Applied.Desired[address].Entries;
            Assert.True(StructuralEquality.Equivalent(desired, info!.NoPatch.Entries),
                $"{address}: erwartet\n{Text(desired)}\nnach dem Patch\n{Text(info.NoPatch.Entries)}");
        }
    }

    private static void CopyDir(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        Directory.CreateDirectory(target);
    }

    protected static string Text(IReadOnlyList<ScriptEntry> entries) =>
        ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }).Replace("\r\n", "\n");

    public static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Nicht gefunden: " + relative);
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }

    private static void Put(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
