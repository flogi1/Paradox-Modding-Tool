namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>
/// Das echte IV2-Paket aus dem Repo (<c>generator-packages/iv2</c>) mit einem Temp-Datenordner (Test-Zip mit <c>coa/TST.dds</c>), dem Kunstset TST
/// und zwei Mods: "existing" aus <c>Base.bundle</c> und "fresh" ohne Basisdateien.
/// </summary>
internal sealed class Iv2Fixture : IDisposable
{
    public static readonly byte[] CoaBytes = { 0x44, 0x44, 0x53, 7 };

    public Iv2Fixture(string uiLanguage = "en")
    {
        Fixtures = Path.Combine(RepoRoot, "tests", "ParadoxModTool.Generators.Tests", "Iv2");
        AssetLibraryTests.Zip(Temp, "data/iv2/assets/coa.zip", ("coa/TST.dds", CoaBytes));
        Package = GeneratorPackageLoader.Load(Path.Combine(RepoRoot, "generator-packages", "iv2"), Temp.PathOf("data/iv2"), shipped: true, uiLanguage: uiLanguage);
        Directory.CreateDirectory(Temp.PathOf("data/iv2/states"));
        File.Copy(Path.Combine(Fixtures, "TST.json"), Temp.PathOf("data/iv2/states/TST.json"));
        Bundle.Unpack(Path.Combine(Fixtures, "Base.bundle"), ExistingMod);
        Directory.CreateDirectory(FreshMod);
    }

    public static string RepoRoot
    {
        get
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ParadoxModTool.slnx")))
            {
                folder = folder.Parent;
            }

            return folder?.FullName ?? throw new InvalidOperationException("ParadoxModTool.slnx nicht gefunden");
        }
    }

    public TempFolder Temp { get; } = new();

    public string Fixtures { get; }

    public GeneratorPackage Package { get; }

    public string ExistingMod => Temp.PathOf("mod-existing");

    public string FreshMod => Temp.PathOf("mod-fresh");

    /// <summary>Das Kunstset TST, frisch geladen (jede Änderung bleibt beim Aufrufer).</summary>
    public GeneratorState State() => GeneratorStateStore.Load(Package.DataFolder, "TST") ?? throw new InvalidOperationException("TST.json fehlt");

    public GeneratorPlan Plan(GeneratorState state, string modRoot) => GeneratorPlanner.Plan(Package, state, new GeneratorTarget(modRoot, null));

    public IReadOnlyDictionary<string, string> Expected(string scenario) => Bundle.Read(Path.Combine(Fixtures, $"Expected-{scenario}.bundle"));

    public void Dispose() => Temp.Dispose();
}
