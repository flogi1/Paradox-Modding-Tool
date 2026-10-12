using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Lokalisierung in gemeinsame Dateien im Planer und beim Einlesen (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 2).</summary>
public sealed class SharedLocalizationTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private GeneratorPackage Load(string namesKeys = """, "keys": "^n_{{ tag }}_\\d+$" """, string localizationOutput = """, "localizationOutput": "names" """)
    {
        _temp.Write("pkg/package.json", $$$"""
            {
              "id": "sl", "name": "SL", "gameId": "eu5", "keyField": "tag",
              "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
              "root": { "id": "ideas", "label": "Ideen", "min": 0, "max": 9, "fields": [ { "id": "name", "label": "Name", "type": "text", "multilingual": true } ] },
              "outputs": [
                { "id": "a", "kind": "localization", "path": "loc/{{ lang }}/a_l_{{ lang }}.yml", "template": "a.sbn" },
                { "id": "names", "kind": "localization", "path": "loc/{{ lang }}/names_l_{{ lang }}.yml", "template": "names.sbn" {{{namesKeys}}} },
                { "id": "m", "kind": "ownFile", "path": "m_{{ tag }}.txt", "template": "m.sbn" }
              ],
              "read": {
                "keys": "^m_(?<key>[A-Z]+)\\.txt$",
                "elements": { "file": "m_{{ tag }}.txt", "block": "m_{{ tag }}_(?<number>\\d+)", "level": "ideas" },
                "localization": [ { "key": "n_{{ tag }}_(?<number>\\d+)", "field": "ideas.name" } ]
                {{{localizationOutput}}}
              },
              "targets": [ "mod", "output" ]
            }
            """);
        _temp.Write("pkg/templates/a.sbn", " other_{{ tag }}: \"x\"\n");
        _temp.Write("pkg/templates/names.sbn", "{{ for i in ideas }} n_{{ tag }}_{{ i.number }}: \"{{ i.name }}\"\n{{ end }}");
        _temp.Write("pkg/templates/m.sbn", "{{ for i in ideas }}m_{{ tag }}_{{ i.number }} = {}\n{{ end }}");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    private GeneratorPlan Plan(GeneratorPackage package, string tag, params string[] names)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = tag;
        state.Head.Values["tag"] = tag;
        foreach (var name in names)
        {
            var idea = FormNode.Create(package.Root!.Fields, null, package.Constants);
            idea.Values["name"] = name;
            state.Head.Children["ideas"].Add(idea);
        }

        return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), null));
    }

    [Fact]
    public void Plan_localization_with_keys_keeps_other_entries()
    {
        _temp.Write("mod/loc/english/names_l_english.yml", "l_english:\n n_B_1: \"Bee\"\n n_A_1: \"Old\"\n n_A_2: \"Gone\"\n");

        var file = Plan(Load(), "A", "New").Files.Single(f => f.RelativePath.Contains("names"));

        Assert.Equal("l_english:\n n_B_1: \"Bee\"\n n_A_1: \"New\"\n", file.NewText!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Plan_localization_without_keys_writes_whole_file()
    {
        _temp.Write("mod/loc/english/names_l_english.yml", "l_english:\n n_B_1: \"Bee\"\n");

        var file = Plan(Load(namesKeys: string.Empty), "A", "New").Files.Single(f => f.RelativePath.Contains("names"));

        Assert.Equal("l_english:\n n_A_1: \"New\"\n", file.NewText!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Reader_uses_named_localization_output()
    {
        _temp.Write("mod/m_A.txt", "m_A_1 = {}\nm_A_2 = {}\n");
        _temp.Write("mod/loc/english/names_l_english.yml", "l_english:\n n_A_1: \"First\"\n n_A_2: \"Second\"\n");

        var state = GeneratorStateReader.Read(Load(), _temp.PathOf("mod"), "A", new[] { "english" })!.State;

        Assert.Equal(new[] { "First", "Second" }, state.Head.Children["ideas"].Select(i => i.Values["name"]));
    }

    [Fact]
    public void Unknown_localization_output_is_rejected()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load(localizationOutput: """, "localizationOutput": "nope" """));

        Assert.Contains("read.localizationOutput", error.Message);
    }
}
