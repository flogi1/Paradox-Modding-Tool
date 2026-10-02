using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Localization;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie 9: Oberflächensprache (wirkt nach Neustart). Unter jeder Sprache außer Deutsch steht ihr Übersetzungsstand aus
/// <see cref="LocaleValidator"/>, bei Fehlern mit aufklappbarer Liste; "Neu prüfen" liest die Dateien erneut.
/// </summary>
public sealed class LanguagePage : SettingsPage
{
    /// <summary>Mehr Einträge zeigt die Fehlerliste nicht; der Rest wird nur gezählt.</summary>
    private const int MaxErrors = 20;

    private readonly List<(string Code, StackPanel Panel)> _status = new();

    public LanguagePage(ISettingsHost host)
        : base(host)
    {
        foreach (var language in Host.Languages)
        {
            AddRadio("language", language.Name, () => Host.Settings.UiLanguage == language.Code, () => Host.Settings.UiLanguage = language.Code);
            if (language.Code != UiText.SourceLanguage)
            {
                var panel = new StackPanel { Margin = new Thickness(22, 0, 0, 4) };
                Root.Children.Add(panel);
                _status.Add((language.Code, panel));
            }
        }

        RebuildStatus();

        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        Root.Children.Add(buttons);
        AddButton(SettingsTexts.LanguageRecheck, null, RebuildStatus, buttons).Margin = new Thickness(0, 4, 8, 4);
        AddButton(SettingsTexts.LanguageOpenFolder, null, Host.OpenTranslationFolder, buttons).Margin = new Thickness(0, 4, 8, 4);
        AddButton(SettingsTexts.LanguageGuide, null, () => Host.OpenUrl(Host.TranslationGuideUrl), buttons).Margin = new Thickness(0, 4, 8, 4);
        AddButton(SettingsTexts.LanguageRestart, SettingsTexts.LanguageRestartTip, Host.RestartApp, buttons);

        AddHint(SettingsTexts.LanguageHint).Margin = new Thickness(0, 10, 0, 0);
        RefreshOnSettings();
    }

    /// <summary>Liest alle Sprachdateien neu und baut die Statuszeilen und Fehlerlisten neu auf.</summary>
    private void RebuildStatus()
    {
        foreach (var (code, panel) in _status)
        {
            panel.Children.Clear();
            var report = LocaleValidator.CheckLanguage(Host.LocaleSources, code);
            panel.Visibility = report is null ? Visibility.Collapsed : Visibility.Visible;
            if (report is null)
            {
                continue;
            }

            AddHint(StatusLine(report, Host.LocaleSources.Resolve(code)?.Origin ?? LocaleOrigin.Program), panel).Margin = new Thickness(0);
            if (report.HasErrors)
            {
                panel.Children.Add(ErrorList(report));
            }
        }
    }

    private static string StatusLine(LocaleReport report, LocaleOrigin origin)
    {
        var missing = report.MissingKeys.Count;
        var parts = new List<string>
        {
            SettingsTexts.LanguagePercent(report.Percent) + (missing > 0 ? " (" + SettingsTexts.LanguageMissing(missing) + ")" : string.Empty),
            origin == LocaleOrigin.User ? SettingsTexts.LanguageOriginUser : SettingsTexts.LanguageOriginProgram,
        };
        if (report.MachineTranslated)
        {
            parts.Add(SettingsTexts.LanguageMachine);
        }

        return string.Join(" · ", parts);
    }

    private static Expander ErrorList(LocaleReport report)
    {
        var errors = new List<string>();
        if (report.ParseError is { } parse)
        {
            errors.Add(SettingsTexts.LanguageParseError(parse.Line, parse.Position, parse.Message));
        }

        errors.AddRange(report.UnknownKeys.Select(SettingsTexts.LanguageUnknownKey));
        errors.AddRange(report.PlaceholderMismatches.Select(mismatch =>
            SettingsTexts.LanguagePlaceholderError(mismatch.Key, Placeholders(mismatch.Expected), Placeholders(mismatch.Found))));

        var list = new StackPanel { Margin = new Thickness(16, 2, 0, 4) };
        foreach (var error in errors.Take(MaxErrors))
        {
            list.Children.Add(Label(error));
        }

        if (errors.Count > MaxErrors)
        {
            list.Children.Add(Label(SettingsTexts.LanguageMoreErrors(errors.Count - MaxErrors)));
        }

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = "⚠", Margin = new Thickness(0, 0, 4, 0) });
        header.Children.Add(new TextBlock { Text = SettingsTexts.LanguageErrors(errors.Count) });
        return new Expander { Header = header, Content = list };
    }

    private static string Placeholders(IReadOnlyList<string> placeholders) => placeholders.Count == 0 ? "–" : string.Join(" ", placeholders);
}
