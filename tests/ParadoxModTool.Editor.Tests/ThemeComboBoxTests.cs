using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Die eigene ComboBox-Vorlage der Themes (<c>src/ParadoxModTool.App/Themes/Controls.xaml</c>) muss bearbeitbare Auswahlfelder unterstützen: ohne
/// <c>PART_EditableTextBox</c> lässt sich in <c>IsEditable</c>-Feldern (Scope Finder "Wert am Ende", GUI-Eigenschaften, Bug-Quellen) weder tippen noch löschen.
/// </summary>
public sealed class ThemeComboBoxTests
{
    private static ResourceDictionary LoadTheme()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "ParadoxModTool.App", "Themes", "Controls.xaml");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                return (ResourceDictionary)XamlReader.Load(stream);
            }
        }

        throw new InvalidOperationException("Themes/Controls.xaml nicht gefunden");
    }

    private static ComboBox Themed(bool editable)
    {
        var box = new ComboBox { IsEditable = editable, ItemsSource = new[] { "bank_loan_takeover_cost", "other" } };
        box.Resources.MergedDictionaries.Add(LoadTheme());
        var window = new Window { Content = box, Width = 300, Height = 80, Left = -3000, Top = -3000, ShowActivated = false };
        window.Show();
        box.ApplyTemplate();
        box.UpdateLayout();
        window.Close();
        return box;
    }

    [Fact]
    public void EditableComboBox_HasAnEditableTextBox_ThatShowsAndTakesTheText()
    {
        Sta.Run(() =>
        {
            var box = Themed(editable: true);
            var textBox = box.Template.FindName("PART_EditableTextBox", box) as TextBox;

            Assert.NotNull(textBox);
            Assert.Equal(Visibility.Visible, textBox!.Visibility);
            Assert.False(textBox.IsReadOnly);

            textBox.Text = "bank";
            Assert.Equal("bank", box.Text);
        });
    }

    [Fact]
    public void NonEditableComboBox_ShowsTheSelectionInsteadOfATextBox()
    {
        Sta.Run(() =>
        {
            var box = Themed(editable: false);
            var textBox = box.Template.FindName("PART_EditableTextBox", box) as TextBox;
            var content = (FrameworkElement)box.Template.FindName("ContentSite", box);

            Assert.True(textBox is null || textBox.Visibility != Visibility.Visible);
            Assert.Equal(Visibility.Visible, content.Visibility);
        });
    }
}
