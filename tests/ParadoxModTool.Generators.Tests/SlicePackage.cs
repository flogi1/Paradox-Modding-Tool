using System.Text;
using ParadoxModTool.Patches;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// Das Test-Paket "iv2-slice" (ein Ausschnitt von IV2 mit allen sechs Ausgabe-Arten) in einem Temp-Ordner, dazu ein Mod-Ordner mit den
/// Basisdateien und Hilfen zum Füllen des Formulars und zum Schreiben eines Plans wie das Fenster.
/// </summary>
internal sealed class SlicePackage : IDisposable
{
    public const string SetupGui =
        "iv2_historic_shell_adm = {\n" +
        "\t# === IV2Generator:BYZ:START ===\n" +
        "\tiv2_historic_idea_select_BYZ_1 = {}\n" +
        "\t# === IV2Generator:BYZ:END ===\n" +
        "\t# === IV2Generator:FRA:START ===\n" +
        "\tiv2_historic_idea_select_FRA_1 = {}\n" +
        "\t# === IV2Generator:FRA:END ===\n" +
        "}\n" +
        "iv2_historic_shell_navy = {\n" +
        "}\n";

    public const string Triggers = "iv2_tag_list = {\n\tOR = {\n\t\thas_or_had_tag = FRA\n\t}\n}\n";

    public SlicePackage()
    {
        CopyDir(Path.Combine(AppContext.BaseDirectory, "TestPackages", "iv2-slice"), Temp.PathOf("package"));
        AssetLibraryTests.Zip(Temp, "package/assets/coa.zip", ("coa/BYZ_byzantium.dds", new byte[] { 0x44, 0x44, 0x53, 1 }), ("coa/FRA_france.dds", new byte[] { 0x44, 0x44, 0x53, 2 }));
        Package = GeneratorPackageLoader.Load(Temp.PathOf("package"));
        Temp.Write("mod/in_game/gui/setup.gui", SetupGui);
        Temp.Write("mod/in_game/common/scripted_triggers/setup.txt", Triggers);
    }

    public TempFolder Temp { get; } = new();

    public GeneratorPackage Package { get; }

    public string ModRoot => Temp.PathOf("mod");

    public static IReadOnlyList<string> Languages { get; } = new[] { "english", "german" };

    /// <summary>Ein Stand mit Tag, Wappen und je Kategorie <paramref name="adm"/> bzw. <paramref name="navy"/> Ideen mit je einer Stufe.</summary>
    public GeneratorState State(string tag, int adm, int navy)
    {
        var state = GeneratorState.CreateEmpty(Package, Languages);
        state.Key = tag;
        state.Head.Values["tag"] = tag;
        state.Head.Values["coa"] = tag == "BYZ" ? "coa/BYZ_byzantium.dds" : "coa/FRA_france.dds";
        var ideasLevel = Package.Root!.Child!;
        var counts = new[] { adm, navy };
        for (var c = 0; c < 2; c++)
        {
            for (var i = 0; i < counts[c]; i++)
            {
                var idea = FormNode.Create(ideasLevel.Fields, ideasLevel.Child, Package.Constants);
                idea.Values["name"] = $"{tag} idea {c}-{i + 1}";
                idea.Values["desc"] = "Line one\nline \"two\"";
                idea.Children["levels"][0].Values["modifiers"] = "monthly_legitimacy = 0.1";
                state.Head.Children["categories"][c].Children["ideas"].Add(idea);
            }
        }

        return state;
    }

    public GeneratorPlan Plan(GeneratorState state, string? outputFolder = null) =>
        GeneratorPlanner.Plan(Package, state, new GeneratorTarget(ModRoot, outputFolder, Languages));

    /// <summary>Schreibt neue und geänderte Dateien eines Plans wie später der Plan-Writer (ohne Rückgängig).</summary>
    public static void Write(GeneratorPlan plan)
    {
        foreach (var file in plan.Files.Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed))
        {
            // Externe Dateien (jsonEntry) an ihrem vollen Pfad, wie GeneratorPlanWriter
            var path = file.ExternalPath ?? Path.Combine(plan.WriteRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (file.Bytes is { } bytes)
            {
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                File.WriteAllText(path, file.NewText!, TextFile.EncodingFor(file.HasBom, file.EncodingName));
            }
        }
    }

    public string ReadMod(string relative) => File.ReadAllText(Path.Combine(ModRoot, relative.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

    public void Dispose() => Temp.Dispose();

    private static void CopyDir(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
