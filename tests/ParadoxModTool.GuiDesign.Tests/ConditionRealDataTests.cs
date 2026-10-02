using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Bedingungs-Helfer gegen die echten Vanilla-GUI-Dateien dieses Rechners. Ohne Spiel läuft nichts.</summary>
public class ConditionRealDataTests
{
    private static readonly string[] ConditionProperties = { "visible", "enabled", "down", "checked" };

    private static readonly Lazy<IReadOnlyList<string>> Conditions = new(() =>
    {
        var result = new List<string>();
        foreach (var file in VanillaGui.Files())
        {
            var document = GuiParser.Parse(File.ReadAllText(file));
            foreach (var node in document.Root.Descendants())
            {
                if (node.Kind == GuiNodeKind.Property && ConditionProperties.Contains(node.Name) && node.ValueSpan is { } span)
                {
                    var text = document.TextOf(span);
                    if (text.Contains('['))
                    {
                        result.Add(text);
                    }
                }
            }
        }

        return result;
    });

    private readonly ITestOutputHelper _output;

    public ConditionRealDataTests(ITestOutputHelper output) => _output = output;

    internal static IReadOnlyList<string> VanillaConditions => Conditions.Value;

    [Fact]
    public void VanillaConditionsRoundTrip()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var errors = 0;
        var mismatches = new List<string>();
        foreach (var text in Conditions.Value)
        {
            var parsed = DataExpressionParser.Parse(text);
            errors += parsed.Errors.Count > 0 ? 1 : 0;
            var written = DataExpressionWriter.WriteFull(parsed.Prefix, parsed.Root, parsed.Suffix, parsed.Original);
            if (written != text)
            {
                mismatches.Add(text);
            }
        }

        _output.WriteLine($"{Conditions.Value.Count} Bedingungen, {errors} mit Fehlern");
        Assert.True(Conditions.Value.Count > 5000, $"nur {Conditions.Value.Count} Bedingungen");
        Assert.Empty(mismatches.Take(5));
    }

    [Fact]
    public void VanillaRawShareIsSmall()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var leaves = 0;
        var raw = new List<string>();
        foreach (var text in Conditions.Value)
        {
            var parsed = DataExpressionParser.Parse(text);
            var root = ConditionTree.From(parsed, ComparisonCatalog.BuiltIn);
            Assert.Equal(text, ConditionWriter.WriteFull(parsed, root));
            foreach (var leaf in Leaves(root))
            {
                leaves++;
                if (leaf is RawCondNode r)
                {
                    raw.Add(r.Text);
                }
                else if (leaf is RawNode n)
                {
                    raw.Add(n.Text);
                }
            }
        }

        var share = leaves == 0 ? 0 : (double)raw.Count / leaves;
        _output.WriteLine($"{leaves} Blätter, {raw.Count} Rohtext ({share:P2})");
        foreach (var group in raw.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(15))
        {
            _output.WriteLine($"{group.Count(),5}  {group.Key}");
        }

        Assert.True(share <= 0.02, $"Rohtext-Anteil {share:P2}");
    }

    [Fact]
    public void VanillaHintsAreRare()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Europa Universalis V", "logs", "data_types");
        if (!VanillaGui.Exists || !Directory.Exists(folder))
        {
            return;
        }

        // Der Export gehört zu der Spielversion, mit der er erzeugt wurde. Ist das Spiel danach aktualisiert worden, passt er nicht mehr zu den
        // Vanilla-Dateien: Der Test läuft wieder, sobald der Export neu erzeugt ist.
        var exe = Path.Combine(VanillaGui.InstallDirectory, "binaries", "eu5.exe");
        var exported = Directory.EnumerateFiles(folder).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
        if (File.Exists(exe) && exported < File.GetLastWriteTimeUtc(exe))
        {
            _output.WriteLine("Übersprungen: Der data_types-Export ist älter als das installierte Spiel.");
            return;
        }

        var db = new DataTypeDatabase(DataTypeLogParser.ParseFolder(folder));
        var catalog = ComparisonCatalog.From(db);
        var hints = new List<ConditionHint>();
        foreach (var text in Conditions.Value)
        {
            hints.AddRange(ConditionChecker.Check(ConditionTree.From(DataExpressionParser.Parse(text), catalog), db, null));
        }

        foreach (var group in hints.GroupBy(h => h.Kind))
        {
            _output.WriteLine($"{group.Key}: {group.Count()}  z. B. {string.Join(", ", group.GroupBy(h => h.Detail).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} ({g.Count()})"))}");
        }

        var noisy = hints.Count(h => h.Kind is ConditionHintKind.UnknownName or ConditionHintKind.TypeMismatch or ConditionHintKind.NotBool or ConditionHintKind.ArgumentCount);
        Assert.True(noisy <= Conditions.Value.Count / 50, $"{noisy} Hinweise auf {Conditions.Value.Count} Vanilla-Bedingungen");
    }

    /// <summary>Blätter eines Bedingungsbaums: Werte, Seiten von Vergleichen und Argumente von Prüfungen, Rohtext.</summary>
    private static IEnumerable<object> Leaves(CondNode? node) => node switch
    {
        LogicNode logic => logic.Items.SelectMany(Leaves),
        NotNode not => Leaves(not.Inner),
        CompareNode compare => new[] { compare.Left, compare.Right, compare.Right2 }.Where(e => e is not null).Cast<object>(),
        CheckNode check => check.Args.Count == 0 ? new object[] { check } : check.Args,
        ValueNode value => new object[] { value.Expr },
        RawCondNode raw => new object[] { raw },
        _ => Array.Empty<object>(),
    };
}
