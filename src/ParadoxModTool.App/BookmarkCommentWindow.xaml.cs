using System.Windows;

namespace ParadoxModTool.App;

/// <summary>Dialog zum Anlegen/Bearbeiten/Entfernen eines Lesezeichens mit Kommentar (siehe BookmarkCommentWindow.xaml).</summary>
public partial class BookmarkCommentWindow : Window
{
    public BookmarkCommentWindow(string lineText, string preview, string comment, bool canRemove)
    {
        InitializeComponent();

        LineText.Text = lineText;
        PreviewText.Text = preview;
        CommentTextBox.Text = comment;
        RemoveButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) =>
        {
            CommentTextBox.Focus();
            CommentTextBox.CaretIndex = CommentTextBox.Text.Length;
        };
    }

    public string Comment => CommentTextBox.Text.Trim();

    /// <summary>True, wenn der Nutzer "Entfernen" gewählt hat (statt "Speichern").</summary>
    public bool Removed { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        Removed = true;
        DialogResult = true;
    }
}
