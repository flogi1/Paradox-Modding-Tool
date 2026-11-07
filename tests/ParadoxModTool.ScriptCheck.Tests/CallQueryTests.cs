using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class CallQueryTests : IDisposable
{
    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    private ScriptCheckIndex Build()
    {
        var rules = ScriptCheckTestRoots.Rules("ck3");
        return new ScriptCheckBuilder(_roots.Sources("ck3"), rules).Build();
    }

    [Fact]
    public void CallersOfModEffect()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "events/b.txt", "namespace = b\nb.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");

        var rows = Build().CallersOf("my_fx");

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Owner == "a.1" && r.Location.RelativePath == "events/a.txt" && r.Location.Line == 4);
        Assert.Contains(rows, r => r.Owner == "b.1" && r.Location.RelativePath == "events/b.txt");
    }

    [Fact]
    public void CalleesOfModEvent()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = m.2\n\t}\n}\nm.2 = {\n\timmediate = { }\n}");

        var index = Build();
        var rows = index.CalleesOf("m.1");

        var row = Assert.Single(rows, r => r.Name == "m.2");
        Assert.Equal(CheckKind.Event, row.Kind);
        Assert.Equal("m.1", row.Owner);
        Assert.Equal(4, row.Location.Line);
        Assert.Single(index.DefinitionsOf("m.0001"));
        Assert.Single(index.CallersOf("m.0002"));
    }

    [Fact]
    public void SearchExactFirst()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m\nm.10 = {\n\timmediate = { }\n}\nm.1 = {\n\timmediate = { }\n}");

        var names = Build().SearchNames("m.1");

        Assert.Equal("m.1", names[0]);
        Assert.Contains("m.10", names);
    }

    [Fact]
    public void SearchSubstringIgnoresCase()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/fx.txt", "My_Special_Fx = {\n\tadd_gold = 1\n}");

        var names = Build().SearchNames("special");

        Assert.Contains("My_Special_Fx", names);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SearchNamesEmptyQuery(string? query)
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/fx.txt", "My_Fx = {\n\tadd_gold = 1\n}");

        Assert.Empty(Build().SearchNames(query!));
    }
}
