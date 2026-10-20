using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Sprite anlegen" für HOI IV und EU IV (Spec "Sprites anlegen"): Bild wählen, Name, Art, Frames und Ziel-<c>.gfx</c>; die Probleme des
/// <see cref="ClassicSpriteDraft.Plan"/> stehen live in der Warnfarbe, "Anlegen" bleibt gesperrt, solange es welche gibt. Das Fenster schreibt nichts: Es liefert
/// <see cref="Request"/> und <see cref="Plan"/>, geschrieben wird im Hauptfenster (ein Rückgängig-Schritt).
/// </summary>
public sealed class ClassicSpriteWindow : Window
{
    private readonly ClassicAssets _assets;
    private readonly string _modRoot;
    private readonly IReadOnlyList<GuiSourceRoot> _roots;
    private readonly bool _nameFollowsImage;
    private readonly TextBox _image = new() { MinWidth = 440, Margin = new Thickness(0, 2, 0, 4) };
    private readonly TextBox _name = new() { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _kind = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { "spriteType", "corneredTileSpriteType" }, SelectedIndex = 0 };
    private readonly TextBox _frames = new() { Width = 60, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _borderX = new() { Width = 50, Text = "0" };
    private readonly TextBox _borderY = new() { Width = 50, Text = "0", Margin = new Thickness(6, 0, 0, 0) };
    private readonly StackPanel _borderRow = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly TextBox _gfx = new() { MinWidth = 300 };
    private readonly ClassicFramePreview _preview = new();
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _problems = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _create = new() { Content = GuiTexts.ClassicSpriteDoCreate, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), IsEnabled = false };
    private bool _loading;
    private bool _nameEdited;
    private string? _confirmedCopy;

    /// <param name="assets">Sprite-Tabelle und Texturen des aktuellen Stands (prüft Namenskollisionen).</param>
    /// <param name="modRoot">Ordner der aktiven Mod.</param>
    /// <param name="roots">Wurzeln in Ladereihenfolge (<see cref="GuiSourceSet.ClassicRootsFor"/>); die erste liefert den Startordner von "Aus gfx/ wählen…".</param>
    /// <param name="gfxFiles">Alle <c>.gfx</c>-Dateien des Index (für den Vorschlag des Ziel-Dateinamens).</param>
    /// <param name="suggestedName">Vorgabe für den Namen (z.B. im Eigenschaftenfeld getippt) oder <see langword="null"/>: dann folgt er dem Dateinamen des Bildes.</param>
    public ClassicSpriteWindow(ClassicAssets assets, string modRoot, IReadOnlyList<GuiSourceRoot> roots, IReadOnlyList<GuiSourceFile> gfxFiles, string? suggestedName)
    {
        _assets = assets;
        _modRoot = modRoot;
        _roots = roots;
        _nameFollowsImage = string.IsNullOrWhiteSpace(suggestedName);
        Title = GuiTexts.ClassicSpriteWindowTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _loading = true;
        _name.Text = suggestedName?.Trim() ?? string.Empty;
        _frames.Text = "1";
        _gfx.Text = ClassicSpriteDraft.DefaultGfxPath(modRoot, gfxFiles);
        _loading = false;
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _problems.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        _image.TextChanged += (_, _) => OnImageChanged();
        _name.TextChanged += (_, _) =>
        {
            _nameEdited |= !_loading;
            Refresh();
        };
        _kind.SelectionChanged += (_, _) =>
        {
            _borderRow.Visibility = Kind == ClassicSpriteKind.CorneredTile ? Visibility.Visible : Visibility.Collapsed;
            _borderLabel.Visibility = _borderRow.Visibility;
            Refresh();
        };
        _frames.TextChanged += (_, _) => OnFramesChanged();
        _borderX.TextChanged += (_, _) => Refresh();
        _borderY.TextChanged += (_, _) => Refresh();
        _gfx.TextChanged += (_, _) => Refresh();
        _create.Click += (_, _) =>
        {
            if (TryCreate() && IsVisible)
            {
                DialogResult = true;
            }
        };

        _borderRow.Children.Add(_borderX);
        _borderRow.Children.Add(_borderY);
        Content = Layout();
        Loaded += (_, _) => _image.Focus();
        Refresh();
    }

    /// <summary>Das Bild: voller Pfad der gewählten Datei.</summary>
    public string ImagePath
    {
        get => _image.Text.Trim();
        set => _image.Text = value;
    }

    public string SpriteName
    {
        get => _name.Text.Trim();
        set => _name.Text = value;
    }

    public ClassicSpriteKind Kind
    {
        get => _kind.SelectedIndex == 1 ? ClassicSpriteKind.CorneredTile : ClassicSpriteKind.Sprite;
        set => _kind.SelectedIndex = value == ClassicSpriteKind.CorneredTile ? 1 : 0;
    }

    /// <summary>Anzahl der Frames; 0, wenn der Text keine ganze Zahl ist.</summary>
    public int Frames => int.TryParse(_frames.Text.Trim(), out var frames) ? frames : 0;

    /// <summary>Ziel-<c>.gfx</c> relativ zum Mod-Ordner (<c>interface/x.gfx</c>).</summary>
    public string GfxPath
    {
        get => _gfx.Text.Trim();
        set => _gfx.Text = value;
    }

    /// <summary>Der Wunsch nach "Anlegen"; <see langword="null"/>, bis <see cref="TryCreate"/> gelungen ist.</summary>
    public ClassicSpriteRequest? Request { get; private set; }

    /// <summary>Der Plan zu <see cref="Request"/>; <see langword="null"/>, bis <see cref="TryCreate"/> gelungen ist.</summary>
    public ClassicSpritePlan? Plan { get; private set; }

    /// <summary>
    /// Rückfrage "Datei überschreiben?", wenn die Bildkopie schon im Mod liegt; <see langword="true"/> = ersetzen. Je Zieldatei wird nur einmal gefragt.
    /// Tests ersetzen sie.
    /// </summary>
    public Func<string, bool>? ConfirmOverwrite { get; set; }

    internal string FramesText
    {
        get => _frames.Text;
        set => _frames.Text = value;
    }

    internal string BorderXText
    {
        get => _borderX.Text;
        set => _borderX.Text = value;
    }

    internal string BorderYText
    {
        get => _borderY.Text;
        set => _borderY.Text = value;
    }

    internal bool BorderVisible => _borderRow.Visibility == Visibility.Visible;

    internal Button CreateButton => _create;

    /// <summary>Die Probleme, wie sie im Fenster stehen.</summary>
    internal IReadOnlyList<string> ShownProblems { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// "Anlegen": prüft noch einmal, fragt bei einer vorhandenen Bildkopie nach und setzt dann <see cref="Request"/> und <see cref="Plan"/>.
    /// <see langword="false"/>: Es gibt Probleme oder die Rückfrage wurde verneint.
    /// </summary>
    internal bool TryCreate()
    {
        var problems = new List<string>();
        if (!Evaluate(problems, out var request, out var plan) || problems.Count > 0 || plan is null)
        {
            Refresh();
            return false;
        }

        var copyExists = plan.CopyTo is { } copy && File.Exists(copy);
        if (copyExists && !string.Equals(_confirmedCopy, plan.CopyTo, StringComparison.OrdinalIgnoreCase))
        {
            if (!(ConfirmOverwrite ?? AskOverwrite)(plan.CopyTo!))
            {
                return false;
            }

            _confirmedCopy = plan.CopyTo;
        }

        // Ersetzen erlaubt nur, wer dazu gefragt wurde; der endgültige Plan entsteht mit genau diesem Wunsch.
        Request = request! with { OverwriteCopy = copyExists };
        Plan = PlanFor(Request, problems);
        return problems.Count == 0 && Plan.CanApply;
    }

    private bool AskOverwrite(string path) =>
        MessageBox.Show(this, GuiTexts.ClassicSpriteOverwriteText(path), GuiTexts.ClassicSpriteOverwriteTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private FrameworkElement Layout()
    {
        var fromGfx = new Button { Content = GuiTexts.ClassicSpriteImageFromGfx, Padding = new Thickness(8, 2, 8, 2) };
        fromGfx.Click += (_, _) => Browse(fromGfx: true);
        var external = new Button { Content = GuiTexts.ClassicSpriteImageExternal, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        external.Click += (_, _) => Browse(fromGfx: false);
        var pickers = new StackPanel { Orientation = Orientation.Horizontal };
        pickers.Children.Add(fromGfx);
        pickers.Children.Add(external);

        var form = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddRow(form, 0, GuiTexts.ClassicSpriteName, _name);
        AddRow(form, 1, GuiTexts.ClassicSpriteKindLabel, _kind);
        AddRow(form, 2, GuiTexts.ClassicSpriteFrames, _frames);
        _borderLabel = AddRow(form, 3, GuiTexts.ClassicSpriteBorder, _borderRow);
        AddRow(form, 4, GuiTexts.ClassicSpriteGfxFile, _gfx);
        _borderLabel.Visibility = _borderRow.Visibility;

        var cancel = new Button { Content = GuiTexts.ClassicSpriteCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_create);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.ClassicSpriteImage });
        panel.Children.Add(_image);
        panel.Children.Add(pickers);
        _preview.HorizontalAlignment = HorizontalAlignment.Left;
        _preview.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(_preview);
        panel.Children.Add(form);
        panel.Children.Add(_hint);
        panel.Children.Add(_problems);
        panel.Children.Add(buttons);
        return panel;
    }

    private TextBlock _borderLabel = null!;

    /// <summary>Eine Zeile "Beschriftung | Eingabe" im Formular; liefert die Beschriftung (die Zeile des Rands wird mit der Art ein- und ausgeblendet).</summary>
    private static TextBlock AddRow(Grid grid, int row, string label, UIElement input)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var block = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 12, 3) };
        Grid.SetRow(block, row);
        Grid.SetRow(input, row);
        Grid.SetColumn(input, 1);
        if (input is FrameworkElement element)
        {
            element.Margin = new Thickness(0, 3, 0, 3);
        }

        grid.Children.Add(block);
        grid.Children.Add(input);
        return block;
    }

    private void Browse(bool fromGfx)
    {
        var dialog = new OpenFileDialog
        {
            Filter = GuiTexts.ClassicSpriteImageFilter,
            Title = fromGfx ? GuiTexts.ClassicSpriteImageFromGfx : GuiTexts.ClassicSpriteImageExternal,
            ClientGuid = GameDialogMemory.For(fromGfx ? "gui.sprite.image" : "gui.sprite.imageExternal"),
        };
        if (fromGfx && _roots.Count > 0 && Path.Combine(_roots[0].Directory, "gfx") is { } start && Directory.Exists(start))
        {
            dialog.InitialDirectory = start;
        }

        if (dialog.ShowDialog(this) == true)
        {
            ImagePath = dialog.FileName;
        }
    }

    /// <summary>Neues Bild: Frames und (solange nichts Eigenes eingetragen ist) der Name folgen der Datei.</summary>
    private void OnImageChanged()
    {
        var path = ImagePath;
        var size = SizeOf(path);
        _loading = true;
        try
        {
            if (size is { } known)
            {
                _frames.Text = ClassicSpriteDraft.SuggestFrames(known.Width, known.Height).ToString();
            }

            if (_nameFollowsImage && !_nameEdited && path.Length > 0)
            {
                _name.Text = ClassicSpriteDraft.SuggestName(path);
            }
        }
        finally
        {
            _loading = false;
        }

        _preview.Show(size is null ? null : path, Math.Max(1, Frames));
        Refresh();
    }

    private void OnFramesChanged()
    {
        if (_loading)
        {
            return;
        }

        if (SizeOf(ImagePath) is not null)
        {
            _preview.Show(ImagePath, Math.Max(1, Frames));
        }

        Refresh();
    }

    private static (int Width, int Height)? SizeOf(string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        try
        {
            return TextureLocator.ReadSizeOfFile(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Plant mit den Eingaben und trägt die Probleme ein; <see langword="false"/>, solange kein Bild gewählt ist.</summary>
    private bool Evaluate(List<string> problems, out ClassicSpriteRequest? request, out ClassicSpritePlan? plan)
    {
        request = null;
        plan = null;
        if (ImagePath.Length == 0)
        {
            return false;
        }

        var cornered = Kind == ClassicSpriteKind.CorneredTile;
        var frames = Frames;
        if (frames < 1)
        {
            problems.Add(GuiTexts.ClassicSpriteFramesInvalid);
        }

        int borderX = 0, borderY = 0;
        if (cornered && !(int.TryParse(_borderX.Text.Trim(), out borderX) && borderX >= 0 && int.TryParse(_borderY.Text.Trim(), out borderY) && borderY >= 0))
        {
            problems.Add(GuiTexts.ClassicSpriteBorderInvalid);
        }

        if (problems.Count > 0)
        {
            return true;
        }

        // Eine vorhandene Bildkopie sperrt nicht, sondern führt zur Rückfrage in TryCreate: Der Plan wird deshalb mit erlaubtem Ersetzen gerechnet.
        request = new ClassicSpriteRequest(ImagePath, SpriteName, Kind, frames, borderX, borderY, GfxPath, OverwriteCopy: true);
        plan = PlanFor(request, problems);
        return true;
    }

    private ClassicSpritePlan PlanFor(ClassicSpriteRequest request, List<string> problems)
    {
        try
        {
            var plan = ClassicSpriteDraft.Plan(request, _modRoot, _assets, _roots, ReadExisting, "\n");
            problems.AddRange(plan.Problems);
            return plan;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            // Ein Pfad mit unzulässigen Zeichen oder eine nicht lesbare .gfx-Datei: als Problem zeigen statt abzustürzen.
            problems.Add(ex is IOException or UnauthorizedAccessException ? ex.Message : GuiTexts.ClassicSpriteImageUnreadable(request.ImageFullPath));
            return new ClassicSpritePlan(Array.Empty<GuiPlanFile>(), null, null, string.Empty, problems);
        }
    }

    private static string? ReadExisting(string path) => File.Exists(path) ? GuiTextReader.Read(path).Text : null;

    private void Refresh()
    {
        if (_loading)
        {
            return;
        }

        var problems = new List<string>();
        var evaluated = Evaluate(problems, out _, out var plan);
        ShownProblems = problems;
        _problems.Text = string.Join("\n", problems);
        _problems.Visibility = problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _create.IsEnabled = evaluated && problems.Count == 0 && plan is { CanApply: true };

        string? hint = null;
        if (!evaluated)
        {
            hint = GuiTexts.ClassicSpriteNoImage;
        }
        else if (plan?.CopyTo is { } copy && File.Exists(copy))
        {
            hint = GuiTexts.ClassicSpriteCopyWillAsk(copy);
        }

        _hint.Text = hint ?? string.Empty;
        _hint.Visibility = hint is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
