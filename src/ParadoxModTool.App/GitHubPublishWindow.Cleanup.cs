using System.IO;
using System.Windows;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.Localization;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

public partial class GitHubPublishWindow
{
    private sealed record CleanupRow(string Name, string Text, bool Deletable);

    private string? _cleanupRepo;
    private string _cleanupDefaultBranch = string.Empty;
    private bool _cleanupBusy;

    private void RefreshCleanupRepos()
    {
        var keep = CleanupRepoBox.SelectedItem as string;
        var names = _mod.GitHubRepos.Select(r => r.Repo).ToList();
        CleanupRepoBox.ItemsSource = names;
        CleanupRepoBox.SelectedItem = names.FirstOrDefault(n => string.Equals(n, keep, StringComparison.OrdinalIgnoreCase)) ?? names.FirstOrDefault();
        CleanupLoadButton.IsEnabled = names.Count > 0;
    }

    private async void CleanupLoad_Click(object sender, RoutedEventArgs e) => await LoadCleanupAsync();

    private async Task LoadCleanupAsync()
    {
        if (_cleanupBusy)
        {
            return;
        }

        if (_gh is null || !_ghLoggedIn)
        {
            CleanupText.Text = PublishTexts.CleanupGhMissing;
            return;
        }

        if (CleanupRepoBox.SelectedItem is not string repo)
        {
            CleanupText.Text = PublishTexts.CleanupNoRepo;
            return;
        }

        _cleanupBusy = true;
        CleanupLoadButton.IsEnabled = false;
        CleanupText.Text = PublishTexts.CleanupLoading;
        try
        {
            var info = await _gh.GetRepoAsync(repo, CancellationToken.None);
            var releases = await _gh.ListReleasesAsync(repo, CancellationToken.None);
            var tags = await _gh.ListTagsAsync(repo, CancellationToken.None);
            var branches = await _gh.ListBranchesAsync(repo, CancellationToken.None);
            _cleanupRepo = repo;
            _cleanupDefaultBranch = info.DefaultBranch;
            CleanupReleaseList.ItemsSource = releases
                .Select(r => new CleanupRow(r.Tag, $"{r.Tag}  {r.Name}{(r.IsDraft ? "  " + PublishTexts.CleanupDraft : string.Empty)}{(r.IsPrerelease ? "  " + PublishTexts.CleanupPreRelease : string.Empty)}", true))
                .ToList();
            CleanupTagList.ItemsSource = tags.Select(t => new CleanupRow(t, t, true)).ToList();
            CleanupBranchList.ItemsSource = branches
                .Select(b => new CleanupRow(
                    b.Name,
                    b.Name + (b.Name == info.DefaultBranch ? "  " + PublishTexts.CleanupDefaultBranch : string.Empty) + (b.Protected ? "  " + PublishTexts.CleanupProtected : string.Empty),
                    b.Name != info.DefaultBranch))
                .ToList();
            CleanupText.Text = PublishTexts.CleanupSummary(repo, releases.Count, tags.Count, branches.Count);
        }
        catch (GhException ex)
        {
            CleanupText.Text = PublishTexts.CleanupLoadFailed(ex.Message);
        }
        finally
        {
            _cleanupBusy = false;
            CleanupLoadButton.IsEnabled = true;
            UpdateCleanupButtons();
        }
    }

    private void CleanupList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateCleanupButtons();

    private void UpdateCleanupButtons()
    {
        CleanupDeleteReleaseButton.IsEnabled = CleanupReleaseList.SelectedItem is CleanupRow { Deletable: true };
        CleanupDeleteTagButton.IsEnabled = CleanupTagList.SelectedItem is CleanupRow { Deletable: true };
        CleanupDeleteBranchButton.IsEnabled = CleanupBranchList.SelectedItem is CleanupRow { Deletable: true };
    }

    private bool ConfirmDelete(string what, string repo, string extra = "") =>
        MessageBox.Show(this, PublishTexts.CleanupConfirmDelete(what, repo, extra), CommonTexts.Delete, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;

    private void RemoveMark(string repo, string tag)
    {
        try
        {
            _log.RemoveGitHub(repo, tag);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CleanupText.Text = PublishTexts.CleanupMarkFailed(ex.Message);
        }
    }

    private async Task DeleteAsync(Func<GhClient, string, Task> delete, string repo, Action? after = null)
    {
        if (_gh is null)
        {
            return;
        }

        _cleanupBusy = true;
        try
        {
            await delete(_gh, repo);
            after?.Invoke();
        }
        catch (GhException ex)
        {
            Report(PublishTexts.CleanupDeleteFailed(ex.Message));
        }
        finally
        {
            _cleanupBusy = false;
        }

        await LoadCleanupAsync();
        _ = CheckReposAsync();
    }

    private async void CleanupDeleteRelease_Click(object sender, RoutedEventArgs e)
    {
        if (CleanupReleaseList.SelectedItem is not CleanupRow { Deletable: true } row || _cleanupRepo is not { } repo)
        {
            return;
        }

        var withTag = CleanupTagCheck.IsChecked == true;
        if (!ConfirmDelete(PublishTexts.CleanupReleaseWhat(row.Name), repo, withTag ? "\n" + PublishTexts.CleanupTagAlso : string.Empty))
        {
            return;
        }

        await DeleteAsync((gh, r) => gh.DeleteReleaseAsync(r, row.Name, withTag, CancellationToken.None), repo, () => RemoveMark(repo, row.Name));
    }

    private async void CleanupDeleteTag_Click(object sender, RoutedEventArgs e)
    {
        if (CleanupTagList.SelectedItem is not CleanupRow { Deletable: true } row || _cleanupRepo is not { } repo || !ConfirmDelete(PublishTexts.CleanupTagWhat(row.Name), repo))
        {
            return;
        }

        await DeleteAsync((gh, r) => gh.DeleteTagAsync(r, row.Name, CancellationToken.None), repo, () => RemoveMark(repo, row.Name));
    }

    private async void CleanupDeleteBranch_Click(object sender, RoutedEventArgs e)
    {
        if (CleanupBranchList.SelectedItem is not CleanupRow { Deletable: true } row || _cleanupRepo is not { } repo)
        {
            return;
        }

        if (row.Name == _cleanupDefaultBranch)
        {
            Report(PublishTexts.CleanupDefaultBranchKept);
            return;
        }

        if (!ConfirmDelete(PublishTexts.CleanupBranchWhat(row.Name), repo))
        {
            return;
        }

        if (TextPromptWindow.Ask(this, PublishTexts.CleanupBranchPromptTitle, PublishTexts.CleanupBranchPrompt(row.Name), string.Empty)?.Trim() != row.Name)
        {
            Report(PublishTexts.CleanupBranchMismatch);
            return;
        }

        await DeleteAsync((gh, r) => gh.DeleteBranchAsync(r, row.Name, CancellationToken.None), repo);
    }
}
