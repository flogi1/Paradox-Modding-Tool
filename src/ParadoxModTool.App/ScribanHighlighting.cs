using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace ParadoxModTool.App;

/// <summary>
/// Einfache Scriban-Hervorhebung für den Vorlagen-Editor (Spec Teil 5a, Abschnitt 3): <c>{{ }}</c>-Bereiche in der Akzentfarbe, darin Schlüsselwörter fett,
/// Texte und <c>#</c>-Kommentare. Die Farben kommen aus dem aktiven Theme; bei jedem Aufbau der Maske neu erzeugt.
/// </summary>
internal static class ScribanHighlighting
{
    private const string Xshd = """
        <SyntaxDefinition name="Scriban" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Tag" foreground="{ACCENT}" fontWeight="bold" />
          <Color name="Code" foreground="{ACCENT}" />
          <Color name="Keyword" foreground="{ACCENT}" fontWeight="bold" />
          <Color name="String" foreground="{STRING}" />
          <Color name="Comment" foreground="{MUTED}" fontStyle="italic" />
          <RuleSet>
            <Span ruleSet="Code" color="Code" multiline="true">
              <Begin color="Tag">\{\{-?</Begin>
              <End color="Tag">-?\}\}</End>
            </Span>
          </RuleSet>
          <RuleSet name="Code">
            <Span color="Comment">
              <Begin>\#</Begin>
              <End>(?=-?\}\})</End>
            </Span>
            <Span color="String">
              <Begin>"</Begin>
              <End>"</End>
              <RuleSet>
                <Span begin="\\" end="." />
              </RuleSet>
            </Span>
            <Span color="String">
              <Begin>'</Begin>
              <End>'</End>
            </Span>
            <Keywords color="Keyword">
              <Word>for</Word>
              <Word>in</Word>
              <Word>end</Word>
              <Word>if</Word>
              <Word>else</Word>
              <Word>elseif</Word>
              <Word>func</Word>
              <Word>ret</Word>
              <Word>while</Word>
              <Word>capture</Word>
              <Word>with</Word>
            </Keywords>
          </RuleSet>
        </SyntaxDefinition>
        """;

    public static IHighlightingDefinition Create(FrameworkElement owner)
    {
        var text = Xshd
            .Replace("{ACCENT}", Hex(owner, "Theme.Accent", "#2F6FB3"))
            .Replace("{STRING}", Hex(owner, "Theme.Warning", "#B36B00"))
            .Replace("{MUTED}", Hex(owner, "Theme.TextMuted", "#808080"));
        using var reader = XmlReader.Create(new StringReader(text));
        return HighlightingLoader.Load(HighlightingLoader.LoadXshd(reader), HighlightingManager.Instance);
    }

    private static string Hex(FrameworkElement owner, string key, string fallback) =>
        owner.TryFindResource(key) is SolidColorBrush { Color: var c } ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : fallback;
}
