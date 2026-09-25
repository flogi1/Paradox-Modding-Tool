using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Mods;
using ParadoxModTool.Templates;

namespace ParadoxModTool.App;

/// <summary>Werkzeugleisten-Button und Menüpunkt "Templates erzeugen": öffnet das Fenster des Template-Generators (siehe <see cref="TemplateGeneratorWindow"/>).</summary>
public partial class MainWindow
{
    private TemplateGeneratorWindow? _templateWindow;

    private void OpenTemplateGenerator_Click(object sender, RoutedEventArgs e)
    {
        if (_templateWindow is null)
        {
            var host = new TemplateGeneratorHost
            {
                InsertAtCaret = InsertGeneratedText,
                NewDocument = OpenGeneratedTextInNewDocument,
                ReadOpenDocument = ReadOpenDocumentText,
                ReplaceOpenDocumentText = ReplaceOpenDocumentText,
                ModRoot = () => CollectOriginRoots().Mod.FirstOrDefault(),
                Highlighting = () => _activeHighlighting,
                Mods = () => _mods?.Registry.Mods.ToList() ?? new List<ModInfo>(),
                ActiveMod = () => _mods?.ActiveMod,
                ModTemplateDirectory = mod => Path.Combine(_mods!.Registry.DataDirectory(mod.Id), "templates"),
                GameId = GameContext.GameId,
                GlobalRulesFile = Path.Combine(GameContext.Repository.Root, "rules", "global.json"),
                GameRulesFile = Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "rules", "game.json"),
                ModRulesFile = mod => Path.Combine(_mods!.Registry.DataDirectory(mod.Id), "rules.json"),
                Games = () => GameContext.Profiles.Select(e => (e.Profile.Id, e.Profile.DisplayName)).ToList(),
                LocProfiles = () => LocProfileService.Profiles,
                LocProfileStatus = () => LocProfileService.Status,
                EnsureLocProfiles = async () => { await UpdateLocProfilesAsync(force: false); },
                SourceRelativeFolder = LocSourceRelativeFolder,
            };

            var store = new TemplateStore(Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "templates"));
            _templateWindow = new TemplateGeneratorWindow(host, store) { Owner = this };
            _templateWindow.Closed += (_, _) => _templateWindow = null;
        }

        if (_templateWindow.IsVisible)
        {
            _templateWindow.Activate();
        }
        else
        {
            _templateWindow.Show();
        }
    }

    /// <summary>Fügt erzeugten Text im aktiven Editor ein; gibt es kein offenes Dokument, wird ein neues angelegt.</summary>
    private void InsertGeneratedText(string text)
    {
        if (ActivePane.ActiveDocument is null)
        {
            OpenGeneratedTextInNewDocument(text);
            return;
        }

        ActivePane.InsertAtCaret(text);
    }

    private void OpenGeneratedTextInNewDocument(string text)
    {
        var document = ActivePane.NewDocument(_activeHighlighting);
        document.Document.Text = text;
    }

    private string? ReadOpenDocumentText(string path) =>
        AllPanes.SelectMany(p => p.Documents).FirstOrDefault(d => EditorPane.PathsEqual(d.FilePath, path))?.Document.Text;

    /// <summary>
    /// Setzt den Text der offenen Dokumente mit diesem Pfad. Beim Anhängen bleibt der Anfang gleich, dann wird nur der Schwanz ersetzt, damit Cursor und Rückgängig-Verlauf erhalten bleiben.
    /// </summary>
    private bool ReplaceOpenDocumentText(string path, string newText)
    {
        var handled = false;
        foreach (var open in AllPanes.SelectMany(p => p.Documents).Where(d => EditorPane.PathsEqual(d.FilePath, path)))
        {
            var document = open.Document;
            var existing = document.Text;
            var prefixLength = existing.TrimEnd().Length;
            if (newText.StartsWith(existing[..prefixLength], StringComparison.Ordinal))
            {
                document.Replace(prefixLength, document.TextLength - prefixLength, newText[prefixLength..]);
            }
            else
            {
                document.Replace(0, document.TextLength, newText);
            }

            handled = true;
        }

        return handled;
    }
}
