namespace ParadoxModTool.GitIntegration;

/// <summary>Hinweise zum Git-Stand vor dem Veröffentlichen (keine Sperren): ungespeicherte Änderungen, nicht gepushter Commit, Branch hinter Upstream, losgelöster HEAD.</summary>
public static class GitWarnings
{
    public static IReadOnlyList<string> From(GitState? state)
    {
        var warnings = new List<string>();
        if (state is null)
        {
            return warnings;
        }

        if (!state.IsClean)
        {
            warnings.Add($"Im Mod-Ordner gibt es {state.Changes.Count} ungespeicherte Änderung(en); die ZIP enthält sie, der Tag nicht.");
        }

        if (!state.HeadPushed)
        {
            warnings.Add("Der aktuelle Commit ist noch nicht gepusht (nicht gepusht = der Tag kann nicht auf ihn zeigen).");
        }

        if (state.Behind > 0)
        {
            warnings.Add($"Der Branch ist {state.Behind} Commit(s) hinter {state.Upstream}.");
        }

        if (state.Branch is null)
        {
            warnings.Add("Der Ordner steht auf einem losgelösten HEAD (kein Branch).");
        }

        return warnings;
    }
}
