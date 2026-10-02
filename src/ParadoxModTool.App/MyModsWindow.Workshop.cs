using System.IO;
using System.Net.Http;
using System.Windows;
using ParadoxModTool.ModOverview;
using ParadoxModTool.SteamIntegration;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Workshop-IDs für Mods, die weder im Register noch in der Metadaten-Datei eine haben (z. B. EU V): auf Knopfdruck die öffentliche Workshop-Seite des
/// Steam-Profils lesen, gleichnamige Seiten vorschlagen; "Übernehmen" merkt die ID in der Link-Datei der Übersicht.
/// </summary>
internal sealed partial class MyModsWindow
{
    private IReadOnlyList<WorkshopIdSuggestion>? _workshopSuggestions;
    private bool _workshopSearching;

    /// <summary>Zahl für den Filter "Workshop-ID fehlt": vor der Suche die Mods ohne ID, danach die Vorschläge (sichtbare Spiele).</summary>
    private int WorkshopCount(OverviewSnapshot snapshot, IReadOnlySet<string> hiddenGames) =>
        _workshopSuggestions is null
            ? snapshot.Mods.Count(m => m.WorkshopId is null && !hiddenGames.Contains(m.GameId))
            : _workshopSuggestions.Count(s => !hiddenGames.Contains(s.Mod.GameId));

    /// <summary>Zeilen im Hinweisbereich: vor der Suche ein Knopf, danach die Vorschläge. Gibt die Zahl der Zeilen zurück.</summary>
    private int AddWorkshopRows(OverviewSnapshot snapshot, IReadOnlySet<string> hiddenGames)
    {
        if (_workshopSuggestions is null)
        {
            var missing = snapshot.Mods.Count(m => m.WorkshopId is null && !hiddenGames.Contains(m.GameId));
            if (missing == 0)
            {
                return 0;
            }

            _alertsPanel.Children.Add(InfoRow(OverviewTexts.WorkshopMissing(missing), (OverviewTexts.WorkshopSearch, OverviewTexts.WorkshopSearchTip, () => _ = SearchWorkshopIdsAsync())));
            return 1;
        }

        var visible = _workshopSuggestions.Where(s => !hiddenGames.Contains(s.Mod.GameId)).ToList();
        if (visible.Count == 0)
        {
            _alertsPanel.Children.Add(InfoRow(OverviewTexts.WorkshopNone));
            return 1;
        }

        if (visible.Count > 1)
        {
            _alertsPanel.Children.Add(InfoRow(string.Empty, (OverviewTexts.WorkshopAdoptAll, null, () => Adopt(visible))));
        }

        foreach (var suggestion in visible)
        {
            var text = suggestion.Mod.Mod.Name + "  " + OverviewTexts.WorkshopFound(suggestion.Title, suggestion.WorkshopId);
            _alertsPanel.Children.Add(InfoRow(text, (OverviewTexts.WorkshopAdopt, null, () => Adopt(new[] { suggestion }))));
        }

        return visible.Count;
    }

    private static FrameworkElement InfoRow(string text, params (string Label, string? Tip, Action Click)[] buttons)
    {
        var row = new Controls.DockPanel { Margin = new Thickness(6, 3, 6, 3) };
        foreach (var (label, tip, click) in buttons.Reverse())
        {
            var button = new Controls.Button { Content = label, ToolTip = tip, Padding = new Thickness(8, 0, 8, 1), Margin = new Thickness(8, 0, 0, 0) };
            button.Click += (_, _) => click();
            Controls.DockPanel.SetDock(button, Controls.Dock.Right);
            row.Children.Add(button);
        }

        var line = new Controls.TextBlock { Text = "🔗  " + text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(line);
        return row;
    }

    /// <summary>Liest je Spiel mit Mods ohne ID die Workshop-Seiten des zuletzt angemeldeten Steam-Kontos und bildet die Vorschläge.</summary>
    private async Task SearchWorkshopIdsAsync()
    {
        if (_workshopSearching || _snapshot is not { } snapshot)
        {
            return;
        }

        _workshopSearching = true;
        _statusText.Text = OverviewTexts.Fetching;
        try
        {
            var user = await Task.Run(ReadLoginUser);
            if (user is null)
            {
                MessageBox.Show(this, OverviewTexts.WorkshopNoAccount, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var hidden = _host.Settings.MyModsHiddenGames.ToHashSet(StringComparer.Ordinal);
            var games = snapshot.Mods.Where(m => m.WorkshopId is null && !hidden.Contains(m.GameId)).Select(m => m.GameId).Distinct().ToList();
            var suggestions = new List<WorkshopIdSuggestion>();
            foreach (var gameId in games)
            {
                if (_host.Profiles.FirstOrDefault(p => p.Id == gameId)?.SteamAppId is not { Length: > 0 } appId)
                {
                    continue;
                }

                var published = await WorkshopProfileClient.ListAsync(user.SteamId64, appId, (url, token) => Http.GetStringAsync(url, token), CancellationToken.None);
                suggestions.AddRange(WorkshopIdSuggestions.Find(snapshot.Mods, gameId, published));
            }

            _workshopSuggestions = suggestions;
            Rebuild();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            MessageBox.Show(this, OverviewTexts.WorkshopFailed(ex.Message), OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _workshopSearching = false;
            _statusText.Text = string.Empty;
        }
    }

    private static SteamLoginUser? ReadLoginUser()
    {
        try
        {
            if (SteamFolders.Find() is not { } steamFolder || !File.Exists(SteamFolders.LoginUsersFile(steamFolder)))
            {
                return null;
            }

            return SteamLoginUsers.Pick(SteamLoginUsers.Parse(File.ReadAllText(SteamFolders.LoginUsersFile(steamFolder))), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Merkt die IDs, nimmt sie aus den Vorschlägen und lädt samt Steam-Abruf neu.</summary>
    private void Adopt(IReadOnlyList<WorkshopIdSuggestion> chosen)
    {
        var linksFile = new OverviewPaths(_host.AppDataRoot).LinksFile;
        try
        {
            foreach (var suggestion in chosen)
            {
                ModLinkStore.SetWorkshopId(linksFile, suggestion.Mod.GameId, suggestion.Mod.Mod.Id, suggestion.WorkshopId);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _workshopSuggestions = (_workshopSuggestions ?? Array.Empty<WorkshopIdSuggestion>()).Where(s => !chosen.Contains(s)).ToList();
        _ = ReloadAsync(fetchSteam: true);
    }
}
