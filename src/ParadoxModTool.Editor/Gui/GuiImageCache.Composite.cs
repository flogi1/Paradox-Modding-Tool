using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Effektbilder (<c>modify_texture</c>, <c>glow</c>, <see cref="JominiEffectRecipe"/>): dieselben zwei Hintergrund-Threads und dieselbe Warteschlange wie die
/// Texturen, dieselbe Grenze nach Pixelbytes und dieselbe Verdrängung. Schlüssel ist <see cref="JominiEffectRecipe.CacheKey"/>, je Rezept-Instanz einmal gebaut (<see cref="CompositeKeyText"/>).
/// <para>
/// Die Bestandteile (Grundbild und Ebenen) kommen als weiße Bilder über denselben Weg wie Texturen: einmal dekodiert, unter ihrem gewohnten Schlüssel
/// gemerkt, von <see cref="TryGet(string, GuiTint, out BitmapSource?)"/> mitbenutzt. Ein Effektbild merkt sich die Stempel aller Bestandteile; im nächsten
/// Lauf (<see cref="NewRun"/>) wird es neu zusammengesetzt, sobald einer abweicht, bis dahin bleibt das alte sichtbar.
/// </para>
/// <para>
/// Unlesbar ist ein Effektbild, wenn sein Grundbild nicht lädt oder das Zusammensetzen wirft (der Zeichner zeigt dann das schlichte Sprite mit seinem
/// Platzhalter); eine Ebene, die nicht lädt, fällt weg. Wurde ein Bestandteil in diesem Lauf schon dekodiert und wieder verdrängt, gilt das Effektbild
/// als <see cref="GuiImageState.OverBudget"/> (kein Bild ohne diesen Bestandteil, das im nächsten Lauf mit gleichen Stempeln stehen bliebe).
/// </para>
/// </summary>
public sealed partial class GuiImageCache
{
    /// <summary>
    /// Fragt das Effektbild ab, ohne je zu dekodieren oder zusammenzusetzen; wie <see cref="TryGet(string, GuiTint, out BitmapSource?)"/>. Das Bild hat
    /// gerades Alpha und enthält die Färbung des Elements, nicht seine Deckkraft (und mit <see cref="JominiEffectRules.ElementMirrorFlipsLayers"/> nicht
    /// seine Spiegelung).
    /// </summary>
    public GuiImageState TryGetComposite(JominiEffectRecipe recipe, out BitmapSource? bitmap) => TryGet(CompositeKey(recipe), recipe, out bitmap);

    /// <summary>Stellt Effektbilder in der angegebenen Reihenfolge in die Warteschlange, wie <see cref="Prefetch"/>.</summary>
    public void PrefetchComposites(IEnumerable<JominiEffectRecipe> recipes)
    {
        // Schlüssel außerhalb der Sperre bauen: eine träge Aufzählung und der Schlüsselbau laufen nicht unter der Sperre.
        var list = recipes.Select(recipe => (Key: CompositeKey(recipe), Recipe: recipe)).ToList();
        lock (_gate)
        {
            foreach (var (key, recipe) in list)
            {
                PrefetchLocked(key, recipe);
            }
        }
    }

    /// <summary>Das Effektbild ist gerade gemerkt (für Tests der Verdrängung).</summary>
    internal bool ContainsComposite(JominiEffectRecipe recipe)
    {
        var key = CompositeKey(recipe);
        lock (_gate)
        {
            return _entries.ContainsKey(key);
        }
    }

    private static Key CompositeKey(JominiEffectRecipe recipe) => new(CompositeKeyText(recipe), GuiTint.White, Composite: true);

    /// <summary>
    /// Schlüsseltexte je Rezept-Instanz (Vergleich nach Referenz, nicht nach Wertgleichheit des Records). Der Zeichner fragt jeden Kasten bei jedem Neuzeichnen
    /// ab; so wird der lange Text je Rezept nur einmal gebaut. Die Effekt-Regeln sind Prozess-Einstellungen: ein Rezept, das nach einer Änderung neu gebaut
    /// wird (neues Layout, auch eine <c>with</c>-Kopie), bekommt einen frischen Schlüssel. Die Tabelle hält die Rezepte nicht am Leben.
    /// </summary>
    private static readonly ConditionalWeakTable<JominiEffectRecipe, string> KeyTexts = new();

    /// <summary>Der Text des Schlüssels eines Effektbilds (<see cref="JominiEffectRecipe.CacheKey"/>), je Rezept-Instanz einmal gebaut. Thread-sicher.</summary>
    internal static string CompositeKeyText(JominiEffectRecipe recipe) => KeyTexts.GetValue(recipe, static r => r.CacheKey);

    /// <summary>Setzt ein Effektbild zusammen und speichert es, oder bestätigt ein gemerktes mit gleichen Stempeln. Läuft auf einem Hintergrund-Thread.</summary>
    private void ProcessComposite(Key key, JominiEffectRecipe recipe)
    {
        var paths = recipe.ComponentPaths;
        var stamps = paths.Select(StampOf).ToArray();
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node) && SameStamps(node.Value.ComponentStamps, stamps))
            {
                node.Value.Generation = _generation;
                Touch(node);
                return;
            }
        }

        // Ab hier ist ein gemerktes Effektbild veraltet; es bleibt sichtbar, bis das neue es ersetzt.
        var images = new Dictionary<string, GuiImage?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < paths.Count; i++)
        {
            var (state, image) = LoadComponent(paths[i], stamps[i]);
            if (state == GuiImageState.OverBudget)
            {
                lock (_gate)
                {
                    if (_entries.TryGetValue(key, out var stale))
                    {
                        Discard(stale);
                    }

                    _decodedThisRun.Add(key);
                }

                return;
            }

            images[paths[i]] = image;
        }

        if (recipe.Base.TextureFullPath is not { } basePath || images.GetValueOrDefault(basePath) is null)
        {
            lock (_gate)
            {
                FailCompositeLocked(key);
            }

            return;
        }

        RecordDecodeThread();
        var composed = JominiEffectComposer.Compose(recipe, path => images.GetValueOrDefault(path));
        if (composed.Width <= 0 || composed.Height <= 0)
        {
            lock (_gate)
            {
                FailCompositeLocked(key);
            }

            return;
        }

        // GuiImage hat gerades Alpha, das entspricht Bgra32 (nicht Pbgra32).
        var bitmap = BitmapSource.Create(composed.Width, composed.Height, 96, 96, PixelFormats.Bgra32, null, composed.Bgra, composed.Width * 4);
        bitmap.Freeze();
        lock (_gate)
        {
            StoreLocked(key, bitmap, default, stamps);
        }
    }

    /// <summary>
    /// Das weiße Bild eines Bestandteils als <see cref="GuiImage"/>: aus dem Puffer, wenn der Stempel gleich ist, sonst dekodiert und unter dem weißen
    /// Schlüssel gespeichert. Belegt den Pfad nur während des Dekodierens und wartet nie, während es selbst einen Pfad belegt (keine Verklemmung mit
    /// Textur-Aufträgen oder anderen Effektbildern). Läuft auf einem Hintergrund-Thread.
    /// </summary>
    /// <param name="stamp">Stempel der Datei, vor dem Aufruf gelesen; er gilt für das gelieferte Bild.</param>
    private (GuiImageState State, GuiImage? Image) LoadComponent(string path, (DateTime WriteTime, long Length) stamp)
    {
        var key = new Key(path, GuiTint.White);
        BitmapSource? white = null;
        lock (_gate)
        {
            // Dekodiert gerade ein anderer Thread diese Datei, findet sich das Ergebnis danach im Puffer.
            while (_busyPaths.Contains(path))
            {
                Monitor.Wait(_gate);
            }

            _entries.TryGetValue(key, out var node);
            if (node is not null && node.Value.Stamp == stamp)
            {
                node.Value.Generation = _generation;
                Touch(node);
                white = node.Value.Bitmap;
            }
            else if (stamp.Length < 0 || (_failed.TryGetValue(path, out var failure) && failure.Stamp == stamp))
            {
                FailLocked(key, stamp);
                return (GuiImageState.Unreadable, null);
            }
            else if (node is null && _decodedThisRun.Contains(key))
            {
                return (GuiImageState.OverBudget, null);
            }
            else
            {
                _busyPaths.Add(path);
            }
        }

        if (white is null)
        {
            BitmapSource? decoded = null;
            try
            {
                decoded = LoadCounted(path);
            }
            finally
            {
                lock (_gate)
                {
                    if (decoded is null)
                    {
                        FailLocked(key, stamp);
                    }
                    else
                    {
                        decoded = StoreLocked(key, decoded, stamp);
                    }

                    _busyPaths.Remove(path);
                    Monitor.PulseAll(_gate);
                }
            }

            if (decoded is null)
            {
                return (GuiImageState.Unreadable, null);
            }

            white = decoded;
        }

        var stride = white.PixelWidth * 4;
        var pixels = new byte[stride * white.PixelHeight];
        white.CopyPixels(pixels, stride, 0);
        return (GuiImageState.Ready, new GuiImage(white.PixelWidth, white.PixelHeight, pixels));
    }

    /// <summary>
    /// Merkt das Effektbild als unlesbar für diesen Lauf und verwirft ein altes; der nächste Lauf versucht es noch einmal (die Fehlschläge der Dateien
    /// selbst merkt sich der Puffer weiter je Datei, sie werden nicht erneut dekodiert). Unter der Sperre.
    /// </summary>
    private void FailCompositeLocked(Key key)
    {
        // _failed hält Dateipfade (absolut) und Schlüssel von Effektbildern (beginnen mit '|'); beide können nicht zusammenfallen.
        if (_entries.TryGetValue(key, out var node))
        {
            Discard(node);
        }

        RecordFailureLocked(key.Path, (default, -1));
    }

    /// <summary>Beide ohne Stempel der Bestandteile (Texturen) oder mit denselben in derselben Reihenfolge.</summary>
    private static bool SameStamps((DateTime WriteTime, long Length)[]? a, (DateTime WriteTime, long Length)[]? b) =>
        a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
}
