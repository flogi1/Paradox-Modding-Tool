using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Hält die Brotkrumen-Leiste mit dem Cursor gleich: 200 ms nach der letzten Cursorbewegung oder Änderung wird neu gerechnet, bei geändertem
/// Text vorher neu geparst (je Dokument ein <see cref="GuiLiveDocument"/>). Hat der Text Fehler, bleibt die letzte Anzeige stehen und
/// Klicks springen nicht, weil die Stellen nicht mehr zum Text passen.
/// </summary>
public sealed class GuiBreadcrumbController
{
    private readonly TextEditor _editor;
    private readonly GuiBreadcrumbBar _bar;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly ConditionalWeakTable<OpenDocument, GuiLiveDocument> _live = new();
    private OpenDocument? _document;
    private ITextSourceVersion? _checkedVersion;
    private ITextSourceVersion? _usableVersion;
    private GuiResolver? _resolver;
    private GuiDocument? _resolverDocument;
    private GuiWorkspace? _resolverWorkspace;

    public GuiBreadcrumbController(TextEditor editor, GuiBreadcrumbBar bar)
    {
        _editor = editor;
        _bar = bar;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Update();
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) => Restart();
        GuiSettings.Changed += (_, _) => Restart();
        _bar.CrumbClicked += (_, node) => Select(node);
        _bar.InspectClicked += (_, _) =>
        {
            if (TargetAtCaret() is { } target)
            {
                InspectRequested?.Invoke(this, target);
            }
        };
    }

    public event EventHandler<GuiInspectTarget>? InspectRequested;

    /// <summary>Zeigt die Leiste für dieses Dokument; null schaltet sie aus.</summary>
    public void Show(OpenDocument? document)
    {
        if (ReferenceEquals(document, _document))
        {
            return;
        }

        if (_document is not null)
        {
            _document.Document.TextChanged -= Document_TextChanged;
        }

        _timer.Stop();
        _document = document;
        _checkedVersion = null;
        _usableVersion = null;
        _bar.SetCrumbs(Array.Empty<GuiCrumb>(), canInspect: false);
        if (document is null)
        {
            return;
        }

        document.Document.TextChanged += Document_TextChanged;
        Update();
    }

    private void Document_TextChanged(object? sender, EventArgs e) => Restart();

    private void Restart()
    {
        if (_document is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Start();
    }

    private void Update()
    {
        if (CurrentDocument() is not { } document)
        {
            // Text hat Fehler: letzter gültiger Pfad bleibt stehen, "Inspizieren" ist gesperrt (Plan-Ruling 12).
            _bar.SetInspectEnabled(false);
            return;
        }

        var resolver = ResolverFor(document);
        var offset = _editor.CaretOffset;
        _bar.SetCrumbs(GuiElementPath.CrumbsAt(document, offset, resolver), GuiElementPath.ElementAt(document, offset, resolver) is not null);
    }

    /// <summary>Syntaxbaum, der genau zum aktuellen Text passt; null, solange der Text Fehler hat.</summary>
    private GuiDocument? CurrentDocument()
    {
        if (_document is null)
        {
            return null;
        }

        var live = _live.GetValue(_document, d => new GuiLiveDocument(d.Document.Text));
        var version = _document.Document.Version;
        if (!IsSame(version, _checkedVersion))
        {
            _checkedVersion = version;
            if (live.Update(_document.Document.Text))
            {
                _usableVersion = version;
            }
        }

        return IsSame(version, _usableVersion) ? live.Current : null;
    }

    private GuiInspectTarget? TargetAtCaret()
    {
        if (_document is null || CurrentDocument() is not { } document)
        {
            return null;
        }

        return GuiElementPath.ElementAt(document, _editor.CaretOffset, ResolverFor(document)) is { } node
            ? new GuiInspectTarget(node, document, _document.FilePath)
            : null;
    }

    private void Select(GuiNode node)
    {
        if (CurrentDocument() is null)
        {
            return;
        }

        var start = node.Span.Start;
        var end = node.NameSpan.End > start ? node.NameSpan.End : Math.Min(node.Span.End, start + 1);
        _editor.Select(start, end - start);
        _editor.ScrollToLine(_editor.Document.GetLineByOffset(start).LineNumber);
        _editor.TextArea.Focus();
    }

    private GuiResolver ResolverFor(GuiDocument document)
    {
        var workspace = GuiSettings.Workspace;
        if (_resolver is null || !ReferenceEquals(document, _resolverDocument) || !ReferenceEquals(workspace, _resolverWorkspace))
        {
            _resolver = workspace?.CreateResolver(document, _document?.FilePath)
                ?? new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, _document?.FilePath);
            _resolverDocument = document;
            _resolverWorkspace = workspace;
        }

        return _resolver;
    }

    private static bool IsSame(ITextSourceVersion current, ITextSourceVersion? other) =>
        other is not null && current.BelongsToSameDocumentAs(other) && current.CompareAge(other) == 0;
}
