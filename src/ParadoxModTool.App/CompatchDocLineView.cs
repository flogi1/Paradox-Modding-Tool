using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Ein Eintrag der Dokumentliste (Ansicht B). <see cref="Index"/> macht gleiche Zeilen (z. B. jedes "}") für die ListBox unterscheidbar.</summary>
public sealed record CompatchDocLineItem(int Index, DocLine Line, CompatchWindow Window);

/// <summary>
/// Eine Zeile des Ergebnisdokuments: Herkunft als Farbe am linken Rand (Spec, "Ansicht B"), Konflikte als eingebettete Auswahlzeile
/// mit "A übernehmen", "B übernehmen", "Bearbeiten". Vorlage einer virtualisierten Liste.
/// </summary>
public sealed class CompatchDocLineView : Controls.Border
{
    public CompatchDocLineView()
    {
        BorderThickness = new Thickness(4, 0, 0, 0);
        Padding = new Thickness(6, 0, 4, 0);
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        Child = null;
        if (DataContext is not CompatchDocLineItem item)
        {
            return;
        }

        var line = item.Line;
        BorderBrush = line.Kind == DocLineKind.Header ? System.Windows.Media.Brushes.Transparent : Stripe(line.Origin);
        switch (line.Kind)
        {
            case DocLineKind.Header:
                Child = new Controls.TextBlock { Text = line.Text, FontFamily = CompatchWindow.Mono, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0) };
                break;
            case DocLineKind.Choice:
                Child = ChoiceRow(item);
                break;
            case DocLineKind.Code:
                Child = new Controls.TextBlock { Text = line.Text, FontFamily = CompatchWindow.Mono, TextWrapping = TextWrapping.NoWrap };
                break;
            default:
                Child = new Controls.TextBlock { Text = line.Text, FontStyle = FontStyles.Italic };
                break;
        }
    }

    private static System.Windows.Media.Brush Stripe(RowOrigin origin) => origin switch
    {
        RowOrigin.FromA => ThemeManager.GetBrush("CompatchA"),
        RowOrigin.FromB => ThemeManager.GetBrush("CompatchB"),
        RowOrigin.Other => ThemeManager.GetBrush("CompatchReview"),
        _ => System.Windows.Media.Brushes.Transparent,
    };

    private Controls.Panel ChoiceRow(CompatchDocLineItem item)
    {
        var panel = new Controls.WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
        if (item.Window.Session is not { } session || item.Window.ActionById(item.Line.ActionId) is not { } action)
        {
            panel.Children.Add(new Controls.TextBlock { Text = item.Line.Text });
            return panel;
        }

        var open = CompatchWindow.IsOpenConflict(session, action);
        BorderBrush = ThemeManager.GetBrush(open ? "CompatchConflict" : "CompatchReview");
        var text = $"⚠ {action.DisplayPath}: {CompatchWindow.DecisionText(session.EffectiveDecision(action).Kind, item.Window.ModAName, item.Window.ModBName)}" + (open ? " · " + CompatchTexts.Open : string.Empty);
        panel.Children.Add(new Controls.TextBlock { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        panel.Children.Add(CompatchWindow.TintButton(Button(CompatchTexts.TakeMod(item.Window.ModAName), () => item.Window.ChangeAction(action, (s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeA)))), "CompatchA"));
        panel.Children.Add(CompatchWindow.TintButton(Button(CompatchTexts.TakeMod(item.Window.ModBName), () => item.Window.ChangeAction(action, (s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeB)))), "CompatchB"));
        var edit = Button(CompatchTexts.Edit, () => item.Window.OpenInList(action.Id, edit: true));
        edit.IsEnabled = action.Address.Kind != AddressKind.WholeFile;
        panel.Children.Add(edit);
        return panel;
    }

    private static Controls.Button Button(string text, Action click)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 4, 0) };
        button.Click += (_, _) => click();
        return button;
    }
}
