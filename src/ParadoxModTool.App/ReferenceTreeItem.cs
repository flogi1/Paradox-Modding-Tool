using System.Collections.ObjectModel;
using System.ComponentModel;
using ParadoxModTool.ReferenceTree;

namespace ParadoxModTool.App;

/// <summary>
/// Ein Eintrag im Verweisbaum (Bindungsquelle für <see cref="ReferenceTreePanel"/>). Aufklappbare Knoten tragen zuerst einen Platzhalter;
/// die echten Kinder holt erst das Aufklappen über <see cref="ReferenceTreeModel.Children"/>, mit genau der <see cref="RefNode"/>-Instanz
/// aus dem Modell (Gruppen werden über ihre Identität gefunden).
/// </summary>
public sealed class ReferenceTreeItem : INotifyPropertyChanged
{
    private readonly ReferenceTreeModel? _model;
    private bool _isExpanded;
    private bool _loaded;

    private ReferenceTreeItem(ReferenceTreeModel? model, RefNode? node, string text, string? tooltip)
    {
        _model = model;
        Node = node;
        Text = text;
        Tooltip = tooltip;
    }

    /// <summary>Knoten aus dem Modell; null beim Platzhalter.</summary>
    public RefNode? Node { get; }

    public string Text { get; }

    public string? Tooltip { get; }

    public ObservableCollection<ReferenceTreeItem> Children { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Aufgeklappt (zwei Richtungen gebunden an <c>TreeViewItem.IsExpanded</c>); das erste Aufklappen lädt die Kinder.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            if (value)
            {
                LoadChildren();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    /// <summary>Eintrag für einen Knoten: Wurzeln und Äste aufgeklappt (Kinder sofort geladen), Gruppen und Stellen zugeklappt.</summary>
    public static ReferenceTreeItem Create(ReferenceTreeModel model, RefNode node)
    {
        var (text, tooltip) = RefNodeText.Format(node);
        var item = new ReferenceTreeItem(model, node, text, tooltip);
        if (node.Expandable)
        {
            item.Children.Add(new ReferenceTreeItem(null, null, "…", null));
        }

        if (node.NodeKind is RefNodeKind.Root or RefNodeKind.Branch)
        {
            item.IsExpanded = true;
        }

        return item;
    }

    /// <summary>
    /// Ersetzt den Platzhalter durch die Kinder; ein aufklappbarer Knoten ohne Kinder verliert nur den Platzhalter. Ein Fehler erscheint als
    /// einziges Kind; der Knoten gilt dann als nicht geladen, das nächste Aufklappen versucht es erneut.
    /// </summary>
    private void LoadChildren()
    {
        if (_loaded || _model is null || Node is null)
        {
            return;
        }

        Children.Clear();
        try
        {
            foreach (var child in _model.Children(Node))
            {
                Children.Add(Create(_model, child));
            }

            _loaded = true;
        }
        catch (Exception ex)
        {
            Children.Clear();
            Children.Add(new ReferenceTreeItem(null, null, ReferenceTreeTexts.BuildFailed(ex.Message), null));
        }
    }
}
