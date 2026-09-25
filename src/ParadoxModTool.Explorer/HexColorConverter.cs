using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
// ParadoxModTool.Explorer hat (wegen ColorDialog/FolderBrowserDialog in ExplorerPanel.xaml.cs)
// ebenfalls UseWindowsForms aktiviert - dieselbe CS0104-Mehrdeutigkeit wie in
// ParadoxModTool.App.CategoryColorsWindow.xaml.cs (Lessons Learned Punkt 7/12 der
// Architektur-Dokumentation) gilt daher projektweit, nicht nur in der Datei, die WinForms
// tatsächlich benutzt: System.Drawing kennt ebenfalls "Color"/"ColorConverter"/"Brushes".
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Wandelt einen Hex-Farbcode (<see cref="ExplorerEntry.ColorHex"/>) in einen WPF-<see cref="Brush"/>
/// für den schmalen Farbbalken vor jedem Baumeintrag um — dieselbe "schmaler Balken links"-
/// Darstellung wie bei den Dateikategorie-Farben im Editor (siehe
/// <c>ParadoxModTool.Editor.FileCategoryClassifier</c>), hier aber für frei zugewiesene
/// Ordner-/Dateifarben statt automatischer Kategorien. Ein fehlender oder ungültiger Farbcode
/// ergibt <see cref="Brushes.Transparent"/> statt einer Ausnahme (derselbe Rückfall wie
/// <c>FileCategoryClassifier.ParseColor</c>).
/// </summary>
public sealed class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return Brushes.Transparent;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
