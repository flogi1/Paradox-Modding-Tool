using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ParadoxModTool.SettingsUi.Tests;

/// <summary>Sucht Steuerelemente im logischen Baum einer Seite und klickt sie wie ein Nutzer (erst IsChecked, dann Click).</summary>
internal static class UiFind
{
    public static IEnumerable<T> All<T>(object root) where T : DependencyObject
    {
        if (root is not DependencyObject node)
        {
            yield break;
        }

        if (node is T match)
        {
            yield return match;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            foreach (var found in All<T>(child))
            {
                yield return found;
            }
        }
    }

    public static string? Label(ContentControl control) => (control.Content as TextBlock)?.Text ?? control.Content as string;

    public static CheckBox CheckBox(SettingsPage page, string label) => All<CheckBox>(page.View).Single(box => Label(box) == label);

    public static RadioButton Radio(SettingsPage page, string label) => All<RadioButton>(page.View).Single(radio => Label(radio) == label);

    public static Button Button(SettingsPage page, string label) => All<Button>(page.View).Single(button => Label(button) == label);

    public static IEnumerable<string> Texts(SettingsPage page) => All<TextBlock>(page.View).Select(text => text.Text);

    public static void Toggle(CheckBox box)
    {
        box.IsChecked = box.IsChecked != true;
        box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    public static void Choose(RadioButton radio)
    {
        radio.IsChecked = true;
        radio.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    public static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
}
