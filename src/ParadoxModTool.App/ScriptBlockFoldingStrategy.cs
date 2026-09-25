using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit.Folding;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.App;

/// <summary>
/// Erzeugt AvalonEdit-Faltbereiche entlang der <see cref="ScriptBlock"/>-Struktur (nicht entlang
/// der Texteinrückung) - jeder geklammerte Block "{ ... }" ist ein eigener Faltbereich.
/// </summary>
public static class ScriptBlockFoldingStrategy
{
    public static List<NewFolding> CreateFoldings(ScriptBlock root)
    {
        var foldings = new List<NewFolding>();
        CollectFoldings(root, foldings);
        return foldings.OrderBy(f => f.StartOffset).ToList();
    }

    private static void CollectFoldings(ScriptBlock block, List<NewFolding> foldings)
    {
        foreach (var entry in block.Entries)
        {
            // Ein geklammerter Block kann sowohl als Wert einer Schlüssel/Wert-Anweisung
            // ("key = { ... }") als auch unkeytet als reines Listenelement auftreten
            // ("{ { a = 1 } { b = 2 } }", z. B. innerhalb eines Listen-Blocks) - beide Fälle
            // müssen einen eigenen Faltbereich bekommen, sonst fehlen alle Blöcke unterhalb
            // eines unkeyten Listenelements stillschweigend in JEDER Tiefe darunter.
            var nested = entry switch
            {
                ScriptStatement { Value: ScriptBlock b } => b,
                ScriptListItem { Value: ScriptBlock b } => b,
                _ => null
            };

            if (nested is null)
            {
                continue;
            }

            if (entry.SourceText is not null && entry.SourceStart is not null && entry.SourceEnd is not null)
            {
                // Faltbereich beginnt an der öffnenden Klammer, nicht am Schlüssel - AvalonEdit
                // zeigt sonst den Schlüsselnamen selbst nicht mehr an, wenn eingeklappt. Ein
                // ScriptListItem hat keinen Schlüssel - "{…}" ohne Präfix dient dort als
                // Platzhaltername.
                var openBraceOffset = entry.SourceText.IndexOf('{', entry.SourceStart.Value, entry.SourceEnd.Value - entry.SourceStart.Value);
                if (openBraceOffset >= 0)
                {
                    var name = entry is ScriptStatement stmt ? $"{stmt.Key} {{…}}" : "{…}";
                    foldings.Add(new NewFolding(openBraceOffset, entry.SourceEnd.Value) { Name = name });
                }
            }

            CollectFoldings(nested, foldings);
        }
    }
}
