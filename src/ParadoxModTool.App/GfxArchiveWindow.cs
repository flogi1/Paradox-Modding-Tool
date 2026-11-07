using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using ParadoxModTool.Explorer;
using ParadoxModTool.GfxAudit;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Archiv der GFX-Prüfung: zeigt die ausgelagerten Bilder einer Mod (<see cref="GfxArchive.List"/>) und holt die gewählten zurück. Modal und
/// vom GFX-Fenster besessen; <see cref="Changed"/> sagt dem GFX-Fenster, dass es neu prüfen soll.
/// </summary>
internal sealed class GfxArchiveWindow : Window
{
    private readonly GfxArchive _archive;
    private readonly Controls.DataGrid _grid = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = Controls.DataGridSelectionMode.Extended,
        SelectionUnit = Controls.DataGridSelectionUnit.FullRow,
        HeadersVisibility = Controls.DataGridHeadersVisibility.Column,
        GridLinesVisibility = Controls.DataGridGridLinesVisibility.None,
    };

    private readonly Controls.TextBlock _empty = new() { Text = GfxAuditTexts.ArchiveEmpty, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.Button _restore = new() { Content = GfxAuditTexts.ArchiveRestore, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _explorer = new() { Content = GfxAuditTexts.ArchiveShowInExplorer, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _close = new() { Content = GfxAuditTexts.ArchiveClose, Padding = new Thickness(10, 2, 10, 2), IsCancel = true };

    /// <summary>Läuft gerade ein Zurückholen im Hintergrund.</summary>
    private bool _busy;

    public GfxArchiveWindow(GfxArchive archive, string modName, Window owner)
    {
        _archive = archive;
        Title = GfxAuditTexts.ArchiveTitle(modName);
        Owner = owner;
        Width = 900;
        Height = 520;
        MinWidth = 520;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _empty.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        _grid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ArchiveColFile, Width = 260, Binding = new Binding(nameof(ArchiveRow.File)) });
        _grid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ArchiveColOriginal, Width = 260, Binding = new Binding(nameof(ArchiveRow.Original)) });
        _grid.Columns.Add(new Controls.DataGridTextColumn
        {
            Header = GfxAuditTexts.ArchiveColSize,
            Width = 90,
            SortMemberPath = nameof(ArchiveRow.Size),
            Binding = new Binding(nameof(ArchiveRow.SizeText)),
        });
        _grid.Columns.Add(new Controls.DataGridTextColumn
        {
            Header = GfxAuditTexts.ArchiveColDate,
            Width = 100,
            SortMemberPath = nameof(ArchiveRow.LastWriteUtc),
            Binding = new Binding(nameof(ArchiveRow.DateText)),
        });
        _grid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ArchiveColState, Width = new Controls.DataGridLength(1, Controls.DataGridLengthUnitType.Star), Binding = new Binding(nameof(ArchiveRow.State)) });
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        _restore.Click += (_, _) => RestoreSelected();
        _explorer.Click += (_, _) => ShowInExplorer();

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(_restore);
        buttons.Children.Add(_explorer);
        buttons.Children.Add(_close);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        Controls.DockPanel.SetDock(_empty, Controls.Dock.Top);
        Controls.DockPanel.SetDock(buttons, Controls.Dock.Bottom);
        Controls.DockPanel.SetDock(_status, Controls.Dock.Bottom);
        root.Children.Add(_empty);
        root.Children.Add(buttons);
        root.Children.Add(_status);
        root.Children.Add(_grid);
        Content = root;

        Reload();
    }

    /// <summary>Ein Zurückholen hat mindestens eine Datei verschoben: Das GFX-Fenster prüft danach neu.</summary>
    public bool Changed { get; private set; }

    /// <summary>Liest das Archiv neu; ein Lesefehler steht in der Statuszeile.</summary>
    private void Reload()
    {
        IReadOnlyList<GfxArchivedFile> files;
        try
        {
            files = _archive.List();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            files = Array.Empty<GfxArchivedFile>();
            _status.Text = ex.Message;
            _status.ToolTip = null;
        }

        _grid.ItemsSource = files.Select(f => new ArchiveRow(f)).ToList();
        _empty.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _restore.IsEnabled = !_busy && _grid.SelectedItems.Count > 0;
        _explorer.IsEnabled = !_busy;
        _close.IsEnabled = !_busy;
    }

    private async void RestoreSelected()
    {
        // Reihenfolge der Tabelle, nicht der Auswahl.
        var selected = _grid.SelectedItems.OfType<ArchiveRow>().ToHashSet();
        var paths = _grid.Items.OfType<ArchiveRow>().Where(selected.Contains).Select(r => r.Archived.ArchiveRelative).ToList();
        if (_busy || paths.Count == 0)
        {
            return;
        }

        _busy = true;
        UpdateButtons();
        GfxArchiveResult result;
        try
        {
            result = await Task.Run(() => _archive.Restore(paths));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Vor dem Fehler können schon Dateien verschoben sein: Neu prüfen schadet nicht.
            _busy = false;
            Changed = true;
            _status.Text = ex.Message;
            _status.ToolTip = null;
            Reload();
            return;
        }

        _busy = false;
        if (result.Moved.Count > 0)
        {
            Changed = true;
        }

        Reload();
        _status.Text = GfxAuditTexts.ArchiveRestored(result.Moved.Count, result.Failed.Count);
        _status.ToolTip = GfxAuditView.FailureTip(result.Failed);
    }

    /// <summary>Die erste gewählte Datei im Explorer, ohne Auswahl der Archivordner (fehlt er, steht ein Hinweis in der Statuszeile).</summary>
    private void ShowInExplorer()
    {
        // Gemischte Trennzeichen lassen den Explorer still "Dokumente" öffnen: Pfade vorher bereinigen.
        var folder = PathNormalization.ToWindowsPath(_archive.ArchiveDirectory);
        try
        {
            if (_grid.SelectedItem is ArchiveRow row && PathNormalization.ToWindowsPath(row.Archived.FullPath) is var file && File.Exists(file))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
            else
            {
                _status.Text = GfxAuditTexts.FileGone(folder);
                _status.ToolTip = null;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            _status.Text = GfxAuditTexts.OpenFailed(ex.Message);
            _status.ToolTip = null;
        }
    }

    /// <summary>Während eines Zurückholens bleibt das Fenster offen (Escape und Schließkreuz warten).</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    /// <summary>Zeile der Tabelle.</summary>
    private sealed class ArchiveRow(GfxArchivedFile archived)
    {
        public GfxArchivedFile Archived { get; } = archived;

        public string File => Archived.ArchiveRelative;

        public string Original => Archived.OriginalRelative;

        public long Size => Archived.Size;

        public string SizeText => Math.Ceiling(Archived.Size / 1024.0).ToString("N0", CultureInfo.CurrentCulture);

        public DateTime LastWriteUtc => Archived.LastWriteUtc;

        public string DateText => Archived.LastWriteUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

        public string State => Archived.OriginalExists ? GfxAuditTexts.ArchiveOriginalExists : string.Empty;
    }
}
