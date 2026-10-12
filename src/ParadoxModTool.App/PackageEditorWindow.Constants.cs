using System.Data;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using DataGrid = System.Windows.Controls.DataGrid;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

public sealed partial class PackageEditorWindow
{
    /// <summary>
    /// Gitter einer Konstanten-Tabelle (Spec Teil 5a, Abschnitt 3): Spalten und Zeilen frei, Zellen als Text. Mehrsprachige Zellen (z. B. <c>label</c>)
    /// zeigen die UI-Sprache; Tippen ändert diese Sprache, "Sprachen der Zelle" alle. Jede Änderung schreibt die ganze Tabelle.
    /// </summary>
    private sealed class ConstantsGrid
    {
        private readonly PackageEditorWindow _window;
        private readonly PackageDocument _document;
        private readonly string _table;
        private readonly List<Dictionary<string, JsonNode?>> _rows;
        private readonly LocalizedCellLanguages _cellLanguages = new();
        private readonly DataGrid _grid = new() { AutoGenerateColumns = true, CanUserAddRows = false, CanUserDeleteRows = false, CanUserSortColumns = false, MinHeight = 120, MaxHeight = 420, Margin = new Thickness(0, 6, 0, 4) };

        public ConstantsGrid(PackageEditorWindow window, PackageDocument document, string table)
        {
            _window = window;
            _document = document;
            _table = table;
            _rows = document.ConstantRows(table).Select(r => r.ToDictionary(p => p.Key, p => p.Value)).ToList();
        }

        private List<string> Columns => _rows.SelectMany(r => r.Keys).Distinct().ToList();

        public FrameworkElement Build()
        {
            var panel = new StackPanel();
            _grid.AutoGeneratingColumn += (_, e) => e.Column.Header = new TextBlock { Text = e.PropertyName };
            Fill();
            panel.Children.Add(_grid);
            panel.Children.Add(ButtonRow(
                (PackageEditorTexts.AddColumn, PackageEditorTexts.AddColumnTip, AddColumn),
                (PackageEditorTexts.RenameColumn, PackageEditorTexts.ColumnTip, RenameColumn),
                (PackageEditorTexts.RemoveColumn, PackageEditorTexts.ColumnTip, RemoveColumn),
                (PackageEditorTexts.CellLanguages, PackageEditorTexts.CellLanguagesTip, CellLanguages)));
            panel.Children.Add(ButtonRow(
                (PackageEditorTexts.AddRow, PackageEditorTexts.RowTip, () =>
                {
                    _rows.Add(Columns.ToDictionary(c => c, _ => (JsonNode?)JsonValue.Create(string.Empty)));
                    Write();
                }),
                (PackageEditorTexts.RemoveRow, PackageEditorTexts.RowTip, () =>
                {
                    if (CurrentRow() is { } row)
                    {
                        _rows.RemoveAt(row);
                        Write();
                    }
                }),
                (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => MoveRow(-1)),
                (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => MoveRow(1))));
            return panel;
        }

        private void Fill()
        {
            var data = new DataTable();
            foreach (var column in Columns)
            {
                data.Columns.Add(column, typeof(string));
            }

            foreach (var row in _rows)
            {
                data.Rows.Add(Columns.Select(c => (object)CellText(row.GetValueOrDefault(c))).ToArray());
            }

            data.ColumnChanged += (_, e) =>
            {
                var index = data.Rows.IndexOf(e.Row);
                if (index < 0 || index >= _rows.Count)
                {
                    return;
                }

                var text = e.ProposedValue as string ?? string.Empty;
                var key = e.Column.ColumnName;
                var existing = _rows[index].GetValueOrDefault(key);
                if (existing is JsonObject || _cellLanguages.IsMultilingual(_rows[index], key))
                {
                    // Ganz geleert: leerer Text statt null in package.json.
                    _rows[index][key] = _cellLanguages.Type(_rows[index], key, LocalizedText.From(existing), text, UiText.Current.Language).ToNode() ?? JsonValue.Create(string.Empty);
                }
                else
                {
                    _rows[index][key] = JsonValue.Create(text);
                }

                _document.SetConstantRows(_table, _rows);
            };
            _grid.ItemsSource = data.DefaultView;
        }

        private static string CellText(JsonNode? node) => node switch
        {
            JsonObject => LocalizedText.From(node).Resolve(UiText.Current.Language),
            null => string.Empty,
            _ => node.ToString(),
        };

        private void Write()
        {
            _document.SetConstantRows(_table, _rows);
            Fill();
        }

        private int? CurrentRow() => _grid.CurrentCell.Item is DataRowView view ? ((DataView)_grid.ItemsSource).Table!.Rows.IndexOf(view.Row) : null;

        private string? CurrentColumn() => _grid.CurrentCell.Column?.SortMemberPath;

        private void MoveRow(int delta)
        {
            if (CurrentRow() is not { } index || index + delta < 0 || index + delta >= _rows.Count)
            {
                return;
            }

            (_rows[index], _rows[index + delta]) = (_rows[index + delta], _rows[index]);
            Write();
        }

        private void AddColumn()
        {
            if (_window.AskName(PackageEditorTexts.ColumnName, string.Empty) is not { } name || Columns.Contains(name))
            {
                return;
            }

            if (_rows.Count == 0)
            {
                _rows.Add(new Dictionary<string, JsonNode?>());
            }

            foreach (var row in _rows)
            {
                row[name] = JsonValue.Create(string.Empty);
            }

            Write();
        }

        private void RenameColumn()
        {
            if (CurrentColumn() is not { } old || _window.AskName(PackageEditorTexts.ColumnName, old) is not { } name || name == old || Columns.Contains(name))
            {
                return;
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i] = _rows[i].ToDictionary(p => p.Key == old ? name : p.Key, p => p.Value);
            }

            _cellLanguages.Forget();
            Write();
        }

        private void RemoveColumn()
        {
            if (CurrentColumn() is not { } column)
            {
                return;
            }

            foreach (var row in _rows)
            {
                row.Remove(column);
            }

            Write();
        }

        private void CellLanguages()
        {
            if (CurrentRow() is not { } index || CurrentColumn() is not { } column)
            {
                return;
            }

            var value = LocalizedText.From(_rows[index].GetValueOrDefault(column));
            var current = value.Values.ToDictionary(p => p.Key.Length == 0 ? "en" : p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            if (TranslationsDialog.Ask(_window, PackageEditorTexts.LanguagesTitle(column), LocalizedTextBox.UiLanguages, current, multiline: false) is not { } result)
            {
                return;
            }

            _rows[index][column] = value.WithLanguages(LocalizedTextBox.UiLanguages.Where(result.ContainsKey).ToDictionary(l => l, l => result[l])).ToNode();
            _cellLanguages.Forget();
            Write();
        }
    }
}
