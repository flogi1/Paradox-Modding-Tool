using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Zieldatei einer klassischen Überschreibung. <c>RelativePath</c> ist relativ zum Mod-Ordner mit "/". <c>Uncertain</c>: Die Quelle liegt in einem
/// DLC-Ordner; wie DLC-Ordner einsortiert werden, ist nicht gemessen. Ob eine andere DLC-Datei dasselbe Fenster definiert, prüft der Planer je Fenster.
/// </summary>
public sealed record ClassicTarget(string FullPath, string RelativePath, ClassicTargetRule Rule, bool Uncertain);

/// <summary>
/// Wohin eine klassische Überschreibung (HOI IV, EU IV) kommt, nach der gemessenen Ladereihenfolge (Spec "Vanilla-Fenster ändern", Schritt 5; gemessen
/// 2026-10-17): In <c>interface/</c> laden die Dateien eines Ordners nach Namen, Vanilla und Mods gemischt, danach seine Unterordner. HOI IV: Das zuletzt
/// geladene Fenster gewinnt, also <c>zz_&lt;mod&gt;_overrides.gui</c> im Ordner der Vanilla-Datei. EU IV: Das zuerst geladene gewinnt, also
/// <c>00_&lt;mod&gt;_overrides.gui</c> direkt in <c>interface/</c> (nie in einem Unterordner: ein <c>replace_path</c> dort ließ das Spiel abstürzen).
/// </summary>
public static class ClassicOverrideTargets
{
    /// <summary>Etikett der Installation selbst (<see cref="GuiSourceSet.ClassicRootsFor"/>); jedes andere Vanilla-Etikett ist ein DLC-Ordner.</summary>
    public const string VanillaLabel = "vanilla";

    private const string Interface = "interface";

    /// <summary>Höchstens so viele zusätzliche Präfix-Zeichen; danach entscheidet die Prüfung des Planers (eine Datei wie <c>000.gui</c> ließe sich nie überholen).</summary>
    private const int MaxExtraPrefix = 32;

    /// <summary>Standardname ohne Index: <c>zz_&lt;Basis&gt;_overrides.gui</c> (HOI IV, letzte gewinnt) oder <c>00_&lt;Basis&gt;_overrides.gui</c> (EU IV, erste gewinnt).</summary>
    public static string DefaultFileName(string modRoot, GuiLayout layout) => DefaultFileName(modRoot, layout.FirstWins);

    internal static string DefaultFileName(string modRoot, bool firstWins) => FileName(modRoot, firstWins, 2);

    /// <summary>
    /// Die Zieldatei für ein Fenster aus <paramref name="source"/>. Ein gesetzter Name (<paramref name="configuredFileName"/>) gilt wie angegeben
    /// (Regel <see cref="ClassicTargetRule.Configured"/>). Der Standardname wird verlängert (HOI IV ein <c>z</c> mehr, EU IV eine <c>0</c>), solange er nicht
    /// echt nach (HOI IV) bzw. vor (EU IV) jeder Vanilla-Datei direkt in seinem Ordner sortiert oder denselben Pfad hat wie eine Vanilla- oder
    /// Abhängigkeits-Datei (die er sonst ganz ersetzte).
    /// </summary>
    public static ClassicTarget For(GuiSourceFile source, GuiIndex index, string modRoot, string? configuredFileName)
    {
        var layout = index.Layout;
        var folder = layout.FirstWins ? Interface : FolderOf(source.RelativePath);
        var order = GameLoadOrder.For(layout.CaseSensitive);
        var neighbours = index.Files.Where(f => f.Layer == GuiLayer.Vanilla && SameFolder(f.RelativePath, folder)).ToList();
        // Eine DLC-Datei im selben Ordner, die das Fenster nicht definiert, ändert nicht, wer gewinnt (Global Constraints); die zählt der Planer je Fenster.
        var uncertain = !IsInstall(source);

        string relative;
        ClassicTargetRule rule;
        if (!string.IsNullOrWhiteSpace(configuredFileName))
        {
            relative = folder + "/" + configuredFileName.Trim();
            rule = ClassicTargetRule.Configured;
        }
        else
        {
            relative = folder + "/" + DefaultFileName(modRoot, layout);
            for (var extra = 1; extra <= MaxExtraPrefix && !Wins(relative); extra++)
            {
                relative = folder + "/" + FileName(modRoot, layout.FirstWins, 2 + extra);
            }

            rule = RuleOf(relative, layout);
        }

        return new ClassicTarget(FullPathOf(modRoot, relative), relative, rule, uncertain);

        bool Wins(string candidate) =>
            neighbours.All(f => layout.FirstWins ? order.Compare(candidate, f.RelativePath) < 0 : order.Compare(candidate, f.RelativePath) > 0)
            && Colliding(index, candidate) is null;
    }

    /// <summary>Gesetzter Name ohne Ordner, ".", ".." und unzulässige Zeichen: nur so bleibt die Datei im Zielordner.</summary>
    public static bool IsPlainFileName(string name) =>
        name.Trim().Length > 0
        && name.Trim() is not ("." or "..")
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.IndexOfAny(new[] { '/', '\\', ':' }) < 0;

    /// <summary>Die Regel, nach der eine Datei an diesem Pfad gewinnt (Standardname): EU IV erste, HOI IV letzte im Hauptordner bzw. im Unterordner.</summary>
    private static ClassicTargetRule RuleOf(string relativePath, GuiLayout layout) =>
        layout.FirstWins ? ClassicTargetRule.RootFirstWins
        : string.Equals(FolderOf(relativePath), Interface, StringComparison.OrdinalIgnoreCase) ? ClassicTargetRule.RootLastWins
        : ClassicTargetRule.SubfolderLastWins;

    /// <summary>Vanilla- oder Abhängigkeits-Datei mit demselben relativen Pfad; eine Mod-Datei dort ersetzte sie ganz.</summary>
    public static GuiSourceFile? Colliding(GuiIndex index, string relativePath) =>
        index.Files.FirstOrDefault(f => f.Layer != GuiLayer.Mod && string.Equals(f.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Voller Pfad zu einem Pfad relativ zum Mod-Ordner.</summary>
    public static string FullPathOf(string modRoot, string relativePath) =>
        Path.Combine(modRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Stammt die Datei aus der Installation selbst (nicht aus einem DLC-Ordner)?</summary>
    internal static bool IsInstall(GuiSourceFile file) => string.Equals(file.Label, VanillaLabel, StringComparison.OrdinalIgnoreCase);

    private static string FileName(string modRoot, bool firstWins, int prefixLength) =>
        new string(firstWins ? '0' : 'z', prefixLength) + "_" + GuiModTargets.BaseName(Path.GetFileName(modRoot.TrimEnd('\\', '/'))) + "_overrides.gui";

    private static string FolderOf(string relativePath)
    {
        var slash = relativePath.LastIndexOf('/');
        return slash < 0 ? Interface : relativePath[..slash];
    }

    private static bool SameFolder(string relativePath, string folder)
    {
        var slash = relativePath.LastIndexOf('/');
        return slash >= 0 && string.Equals(relativePath[..slash], folder, StringComparison.OrdinalIgnoreCase);
    }
}
