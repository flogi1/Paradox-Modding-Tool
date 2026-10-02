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
            warnings.Add(GitTexts.WarnUncommitted(state.Changes.Count));
        }

        if (!state.HeadPushed)
        {
            warnings.Add(GitTexts.WarnNotPushed);
        }

        if (state.Behind > 0)
        {
            warnings.Add(GitTexts.WarnBehind(state.Behind, state.Upstream));
        }

        if (state.Branch is null)
        {
            warnings.Add(GitTexts.WarnDetached);
        }

        return warnings;
    }
}
