namespace ParadoxModTool.Generators;

/// <summary>Ausgabe "Zeile sicherstellen" (Spec Abschnitt 2, Art 4): eine Zeile in einem Block ergänzen, falls sie dort fehlt; ohne Markierung.</summary>
public static class LineEnsurer
{
    public static SectionResult Apply(string existing, string blockPath, string line, string newLine)
    {
        var block = BlockText.Find(existing, blockPath);
        var wanted = line.Trim();
        if (BlockText.Lines(existing, BlockText.LineEnd(existing, block.Open), block.Close).Any(l => l.Trimmed == wanted))
        {
            return new SectionResult(existing, Array.Empty<string>());
        }

        var indent = BlockText.EntryIndent(existing, block);
        return new SectionResult(BlockText.InsertBeforeClose(existing, block, indent + wanted + newLine, newLine), Array.Empty<string>());
    }
}
