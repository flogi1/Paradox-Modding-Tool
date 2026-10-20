using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Sprache der Jomini-Vorschautexte: Voreinstellung aus den Spieleinstellungen, Umschalter in der Designer-Leiste, Inspektor folgt.</summary>
public sealed class GuiPreviewLanguageTests
{
    [Fact]
    public void Language_box_is_visible_only_for_jomini()
    {
        using var env = new Env(workspace: false);
        Sta.Run(() =>
        {
            var view = new GuiDesignerView();
            view.SyncScreenTools();
            Assert.Equal(Visibility.Visible, view.LanguageBox.Visibility);

            GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
            view.SyncScreenTools();
            Assert.Equal(Visibility.Collapsed, view.LanguageBox.Visibility);
            Assert.Equal("l_english", GuiPreviewScreen.GameLanguage());

            GuiSettings.Layout = GuiLayout.Default;
            view.SyncScreenTools();
            Assert.Equal(Visibility.Visible, view.LanguageBox.Visibility);
        });
    }

    [Fact]
    public void Language_defaults_to_the_game_language()
    {
        using var env = new Env();
        var folder = env.UserFolder("txt");
        File.WriteAllText(
            Path.Combine(folder, "pdx_settings.txt"),
            "\"System\"={\n\t\"language\"={\n\t\tversion=0\n\t\tvalue=\"l_german\"\n\t}\n}\n");
        GuiSettings.GameUserFolder = folder;
        Sta.Run(() =>
        {
            Assert.Equal("l_german", GuiPreviewScreen.GameLanguage());
            var view = new GuiDesignerView();
            view.SyncScreenTools();

            Assert.Equal("l_german", view.PreviewOptions.Language);
            var first = (ComboBoxItem)view.LanguageBox.Items[0]!;
            Assert.Null(first.Tag);
            Assert.Equal(GuiTexts.DesignerLanguageGame("german"), first.Content);
            Assert.Same(first, view.LanguageBox.SelectedItem);
            // Danach die Sprachen der Installation, ohne "l_".
            Assert.Equal(new[] { "english", "french", "german" }, view.LanguageBox.Items.OfType<ComboBoxItem>().Skip(1).Select(item => item.Content).ToArray());
        });
    }

    [Fact]
    public void Eu5_without_language_defaults_to_english()
    {
        using var env = new Env();
        var folder = env.UserFolder("json");
        File.WriteAllText(Path.Combine(folder, "pdx_settings.json"), "{\"GUI\":{\"scale\":\"1.2\"},\"System\":{}}");
        GuiSettings.GameUserFolder = folder;
        Sta.Run(() =>
        {
            Assert.Equal("l_english", GuiPreviewScreen.GameLanguage());
            var view = new GuiDesignerView();
            view.SyncScreenTools();
            Assert.Equal("l_english", view.PreviewOptions.Language);
            Assert.Equal(GuiTexts.DesignerLanguageGame("english"), ((ComboBoxItem)view.LanguageBox.Items[0]!).Content);
        });

        // Weder Ordner noch Datei: ebenfalls Englisch.
        GuiSettings.GameUserFolder = null;
        Assert.Equal("l_english", GuiPreviewScreen.GameLanguage());
    }

    [Fact]
    public void Choosing_a_language_sets_preview_options_and_inspector_follows()
    {
        using var env = new Env();
        var folder = env.UserFolder("choose");
        File.WriteAllText(Path.Combine(folder, "pdx_settings.json"), "{\"System\":{\"language\":\"l_german\"}}");
        GuiSettings.GameUserFolder = folder;
        Sta.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var view = new GuiDesignerView();
            view.SyncScreenTools();
            var changed = 0;
            view.PreviewOptionsChanged += (_, _) => changed++;
            var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
            inspector.Show();
            try
            {
                Assert.Equal("l_german", inspector.PreviewOptions.Language);

                view.LanguageBox.SelectedItem = view.LanguageBox.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, "l_french"));

                Assert.Equal("l_french", GuiSettings.PreviewLanguage);
                Assert.Equal("l_french", view.PreviewOptions.Language);
                Assert.True(changed >= 1);
                Assert.Equal("l_french", inspector.PreviewOptions.Language);

                // Zurück auf "Spiel": die Sprache des Spiels, ohne gemerkte Wahl.
                view.LanguageBox.SelectedIndex = 0;
                Assert.Null(GuiSettings.PreviewLanguage);
                Assert.Equal("l_german", view.PreviewOptions.Language);
                Assert.Equal("l_german", inspector.PreviewOptions.Language);

                // Ein neu aufgebauter Umschalter zeigt die gemerkte Wahl.
                GuiSettings.PreviewLanguage = "l_french";
                view.SyncScreenTools();
                Assert.Equal("l_french", ((ComboBoxItem)view.LanguageBox.SelectedItem).Tag);
            }
            finally
            {
                inspector.Close();
            }
        });
    }

    [Fact]
    public void Inspector_runs_the_preview_again_when_the_language_changes_and_stops_after_close()
    {
        using var env = new Env();
        Sta.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var document = GuiParser.Parse("window = { name = \"main\" size = { 100 100 } }");
            var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
            inspector.Show();
            try
            {
                inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, null));
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (inspector.PreviewScene is null)
                {
                    Assert.True(DateTime.UtcNow < deadline, "Inspektor hat nicht geladen");
                    WpfWindows.Pump();
                    Thread.Sleep(10);
                }

                var before = inspector.LayoutRuns;
                GuiSettings.PreviewLanguage = "l_french";
                WpfWindows.Pump(3);
                Assert.Equal(before + 1, inspector.LayoutRuns);
                Assert.Equal("l_french", inspector.PreviewOptions.Language);
            }
            finally
            {
                inspector.Close();
            }

            // Geschlossen: abgemeldet, kein weiterer Lauf.
            var closed = inspector.LayoutRuns;
            GuiSettings.PreviewLanguage = null;
            WpfWindows.Pump(3);
            Assert.Equal(closed, inspector.LayoutRuns);
        });
    }

    [Fact]
    public void Ui_never_runs_the_language_scan_and_the_box_fills_after_the_background_scan()
    {
        using var env = new Env(workspace: false);
        var previousScan = GuiSettings.LanguageScan;
        using var release = new ManualResetEventSlim();
        var scanThread = -1;
        try
        {
            GuiSettings.LanguageScan = text =>
            {
                scanThread = Environment.CurrentManagedThreadId;
                release.Wait(TimeSpan.FromSeconds(20));
                return text.Languages;
            };
            var workspace = env.CreateWorkspace();
            Sta.Run(() =>
            {
                var view = new GuiDesignerView();
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(workspace, "test"));
                // Die Suche hängt: die Oberfläche kommt trotzdem zurück und zeigt nur "Spiel (…)".
                view.SyncScreenTools();
                Assert.Single(view.LanguageBox.Items);
                Assert.Empty(GuiSettings.PreviewLanguages);

                release.Set();
                Assert.True(GuiSettings.LanguageScanTask!.Wait(TimeSpan.FromSeconds(20)));
                Assert.NotEqual(Environment.CurrentManagedThreadId, scanThread);
                view.SyncScreenTools();
                Assert.Equal(new[] { "english", "french", "german" }, view.LanguageBox.Items.OfType<ComboBoxItem>().Skip(1).Select(item => item.Content).ToArray());
            });
        }
        finally
        {
            release.Set();
            GuiSettings.LanguageScanTask?.Wait(TimeSpan.FromSeconds(20));
            GuiSettings.LanguageScan = previousScan;
        }
    }

    [Fact]
    public void Chosen_language_missing_in_the_new_workspace_falls_back_to_the_game()
    {
        using var env = new Env();
        var folder = env.UserFolder("missing");
        File.WriteAllText(Path.Combine(folder, "pdx_settings.json"), "{\"System\":{\"language\":\"l_german\"}}");
        GuiSettings.GameUserFolder = folder;
        GuiSettings.PreviewLanguage = "l_klingon";

        GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(env.CreateWorkspace(), "test"));
        GuiSettings.LanguageScanTask!.Wait(TimeSpan.FromSeconds(20));

        Assert.Null(GuiSettings.PreviewLanguage);
        Sta.Run(() =>
        {
            var view = new GuiDesignerView();
            view.SyncScreenTools();
            Assert.Equal("l_german", view.PreviewOptions.Language);
            Assert.Null(((ComboBoxItem)view.LanguageBox.SelectedItem).Tag);
        });

        // Eine Sprache, die der Arbeitsbereich hat, bleibt gewählt.
        GuiSettings.PreviewLanguage = "l_french";
        GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(env.CreateWorkspace(), "test"));
        GuiSettings.LanguageScanTask!.Wait(TimeSpan.FromSeconds(20));
        Assert.Equal("l_french", GuiSettings.PreviewLanguage);
    }

    [Fact]
    public void Designer_reruns_when_the_language_changed_while_it_was_unloaded()
    {
        using var env = new Env();
        Sta.Run(() =>
        {
            var view = new GuiDesignerView();
            var reruns = 0;
            view.PreviewOptionsChanged += (_, _) => reruns++;
            var window = new Window { Content = view, Left = -3000, Top = -3000, Width = 800, Height = 600, ShowActivated = false };
            window.Show();
            try
            {
                WpfWindows.Pump();
                var initial = reruns;

                // Entladen: die Ansicht ist vom Ereignis abgemeldet und merkt sich nur die Sprache.
                window.Content = null;
                WpfWindows.Pump();
                GuiSettings.PreviewLanguage = "l_french";
                WpfWindows.Pump();
                Assert.Equal(initial, reruns);

                window.Content = view;
                WpfWindows.Pump();
                Assert.Equal(initial + 1, reruns);
                Assert.Equal("l_french", view.PreviewOptions.Language);
                Assert.Equal("l_french", ((ComboBoxItem)view.LanguageBox.SelectedItem).Tag);

                // Ohne Änderung beim Entladen kein Lauf.
                window.Content = null;
                WpfWindows.Pump();
                window.Content = view;
                WpfWindows.Pump();
                Assert.Equal(initial + 1, reruns);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Jomini-Format, Arbeitsbereich mit drei Sprachen (englisch, französisch, deutsch) und alles danach zurück.</summary>
    private sealed class Env : IDisposable
    {
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-preview-language-" + Guid.NewGuid().ToString("N"));
        private readonly GuiLayout _layout = GuiSettings.Layout;
        private readonly GuiWorkspace? _workspace = GuiSettings.Workspace;
        private readonly string _status = GuiSettings.Status;
        private readonly string? _folder = GuiSettings.GameUserFolder;
        private readonly string? _language = GuiSettings.PreviewLanguage;

        public Env(bool workspace = true)
        {
            GuiSettings.Layout = GuiLayout.Default;
            GuiSettings.GameUserFolder = null;
            GuiSettings.PreviewLanguage = null;
            if (!workspace)
            {
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(null, "test"));
                return;
            }

            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(CreateWorkspace(), "test"));
            // Die Sprachsuche läuft im Hintergrund: abwarten, damit die Liste in jedem Test steht.
            GuiSettings.LanguageScanTask?.Wait(TimeSpan.FromSeconds(20));
        }

        /// <summary>Neuer Arbeitsbereich über der Installation des Tests (englisch, französisch, deutsch), ohne ihn zu setzen.</summary>
        public GuiWorkspace CreateWorkspace()
        {
            var install = Path.Combine(_temp, "install");
            foreach (var language in new[] { "english", "french", "german" })
            {
                var folder = Path.Combine(install, "game", "localization", language);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "a_l_" + language + ".yml"), "﻿l_" + language + ":\n a: \"x\"\n");
            }

            return GuiWorkspace.Create(new GuiWorkspaceOptions(install, null, Array.Empty<string>(), GuiRules.Empty, null));
        }

        /// <summary>Leerer Benutzerordner für die Spieleinstellungen eines Tests.</summary>
        public string UserFolder(string name)
        {
            var folder = Path.Combine(_temp, "user-" + name);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public void Dispose()
        {
            GuiSettings.Layout = _layout;
            GuiSettings.GameUserFolder = _folder;
            GuiSettings.PreviewLanguage = _language;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(_workspace, _status));
            try
            {
                if (Directory.Exists(_temp))
                {
                    Directory.Delete(_temp, recursive: true);
                }
            }
            catch (IOException)
            {
                // Der Hintergrundlauf (Warm) kann noch eine Datei halten; der Temp-Ordner bleibt dann liegen.
            }
        }
    }
}
