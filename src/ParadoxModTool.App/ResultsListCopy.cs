using System.Windows;
using System.Windows.Input;
using ParadoxModTool.SearchReplace;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Kopieren aus einer Trefferliste (Suchfenster und Ergebnisleiste), seit die Treffer nicht mehr als Textfelder in der Liste stehen: In einen Treffer lässt sich
/// nicht mehr hineinklicken (kein Cursor, keine Auswahl); ein Doppelklick springt zur Stelle. Mehrere Treffer markiert man wie in jeder Liste (Strg+Klick,
/// Umschalt+Klick, Strg+A); Strg+C und das Kontextmenü kopieren alle markierten (je Treffer ein Block, durch eine Leerzeile getrennt), "Alle Treffer kopieren"
/// die ganze Liste. Teile eines Treffers markiert man im schreibgeschützten Textfeld unter der Liste, das den Code der markierten Treffer zeigt.
/// </summary>
internal static class ResultsListCopy
{
    /// <summary>Mehr Treffer zeigt das Textfeld nicht auf einmal (eine große Auswahl würde es zäh machen); kopiert werden immer alle markierten.</summary>
    private const int DetailLimit = 200;

    public static void Attach(Controls.ListBox list, Controls.TextBox? detail = null)
    {
        list.SelectionMode = Controls.SelectionMode.Extended;
        list.CommandBindings.Add(new CommandBinding(
            ApplicationCommands.Copy,
            (_, _) => CopyRows(list.SelectedItems.OfType<SearchResultRow>().ToList()),
            (_, e) => e.CanExecute = list.SelectedItems.OfType<SearchResultRow>().Any()));

        var menu = new Controls.ContextMenu();
        menu.Items.Add(new Controls.MenuItem { Header = SearchTexts.ResultCopy, Command = ApplicationCommands.Copy, CommandTarget = list });
        var copyAll = new Controls.MenuItem { Header = SearchTexts.ResultCopyAll };
        copyAll.Click += (_, _) => CopyRows(list.Items.OfType<SearchResultRow>().ToList());
        menu.Items.Add(copyAll);
        menu.Opened += (_, _) => copyAll.IsEnabled = list.Items.Count > 0;
        list.ContextMenu = menu;

        if (detail is not null)
        {
            detail.ToolTip = SearchTexts.ResultDetailTip;
            list.SelectionChanged += (_, _) => ShowDetail(list, detail);
            ShowDetail(list, detail);
        }
    }

    private static void CopyRows(IReadOnlyList<SearchResultRow> rows)
    {
        if (rows.Count > 0)
        {
            System.Windows.Clipboard.SetText(ResultCopyText.Join(rows.Select(TextOf)));
        }
    }

    /// <summary>Der Code der markierten Treffer im Textfeld; ohne Auswahl ist das Feld ausgeblendet.</summary>
    private static void ShowDetail(Controls.ListBox list, Controls.TextBox detail)
    {
        var texts = list.SelectedItems.OfType<SearchResultRow>().Select(TextOf).ToList();
        if (texts.Count == 0)
        {
            detail.Text = string.Empty;
            detail.Visibility = Visibility.Collapsed;
            return;
        }

        var text = ResultCopyText.Preview(texts, DetailLimit, out var hidden);
        detail.Text = hidden > 0 ? text + Environment.NewLine + Environment.NewLine + SearchTexts.ResultDetailMore(hidden) : text;
        detail.Visibility = Visibility.Visible;
    }

    /// <summary>Der gefundene Code; bei einer Ersetzungs-Zusammenfassung ohne einzelnen Treffer die beschreibende Zeile.</summary>
    private static string TextOf(SearchResultRow row) => row.MatchedText.Length > 0 ? row.MatchedText : row.Description;
}
