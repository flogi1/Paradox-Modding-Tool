using System.Globalization;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Liest die statischen Effekte eines aufgelösten Elements in ein <see cref="JominiEffectVisual"/>: die <c>modify_texture</c>-Ebenen in
/// Auflösungsreihenfolge (Typen, Templates, eigene Blöcke), <c>glow</c> und den Graustufen-Effekt aus <c>effectname</c>. Jede Ebene ist ein eigenes
/// Sprite aus <see cref="JominiSprites.Build"/> auf dem Unterelement (<see cref="ResolvedProperty.Block"/>), behält also Ausschnitt, Art, Rand, Spiegelung,
/// Farbe und Deckkraft. Die Annahmen über das Spielverhalten stehen in <see cref="JominiEffectRules"/>.
/// </summary>
internal static class JominiEffects
{
    private const string ModifyTexture = "modify_texture";
    private const string Glow = "glow";

    /// <summary>Namen in <c>effectname</c>, die grau zeichnen.</summary>
    private static readonly HashSet<string> GrayscaleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PdxGuiGrayscale", "PdxGuiDesaturate", "GreyedOut", "Grayscale", "Desaturate",
    };

    /// <summary>Namen in <c>effectname</c> ohne statische Wirkung; sie bleiben ohne Hinweis.</summary>
    private static readonly HashSet<string> SilentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NoHighlight", "NoAlphaNoHighlight", "NoDisabled", "PdxGuiDefault", "PdxGuiDefaultNoAlpha", "PdxGuiPreMultipliedAlpha", "PdxGuiAlphaMultisampled",
    };

    /// <summary>
    /// Die Effekte des Elements; <see langword="null"/> ohne <c>modify_texture</c>, <c>glow</c> und gezeichnetes <c>effectname</c>, ohne zeichenbares
    /// Grund-Sprite (<paramref name="baseSprite"/> fehlt oder ist ein Platzhalter; dann nennt je ein Hinweis die Effekte) und wenn alle Ebenen wegfallen.
    /// Wegfallende Ebenen: <c>visible = no</c>, Deckkraft 0, fehlende, dynamische oder nicht lesbare Textur (Hinweise wie bei Sprites), <c>video</c> und
    /// Ebenen ohne <c>texture</c> (ohne Hinweis). Graustufen-Namen in <c>effectname</c> zeichnen nur mit <see cref="JominiEffectRules.GrayscaleApplies"/>;
    /// sonst bleiben sie mit dem Hinweis "Effekt angenähert (effectname)" weg. <paramref name="notes"/> bekommt die Hinweise ohne Doppelte.
    /// </summary>
    internal static JominiEffectVisual? Build(ResolvedElement element, JominiSpriteVisual? baseSprite, TextureLocator textures, List<string> notes)
    {
        // modify_texture/glow in einem Zustand wird nicht angewandt (Entscheidung zu _show aus Plan 3b, nicht erweitert); der Hinweis gilt auch,
        // wenn das Element sonst keine Effekte hat.
        if (HasEffectInState(element))
        {
            AddOnce(notes, GuiTexts.LayoutEffectApproximated("state"));
        }

        var layerBlocks = element.StructuredBlocks(ModifyTexture).Where(p => p.Block is not null).Select(p => p.Block!).ToList();
        var glowBlock = element.StructuredBlocks(Glow).LastOrDefault(p => p.Block is not null)?.Block;
        var effectName = Unquote(element.Property("effectname")?.Value);
        if (effectName.Length > 0 && SilentNames.Contains(effectName))
        {
            effectName = string.Empty;
        }
        else if (effectName.Length > 0 && GrayscaleNames.Contains(effectName) && !JominiEffectRules.GrayscaleApplies)
        {
            // Gemessen 16u–16w: ohne sichtbare Wirkung. PMT zeichnet ihn bewusst nicht und sagt das.
            AddOnce(notes, GuiTexts.LayoutEffectApproximated("effectname"));
            effectName = string.Empty;
        }

        if (layerBlocks.Count == 0 && glowBlock is null && effectName.Length == 0)
        {
            return null;
        }

        var grayscale = effectName.Length > 0 && GrayscaleNames.Contains(effectName);
        if (effectName.Length > 0 && !grayscale)
        {
            AddOnce(notes, GuiTexts.LayoutEffectNotDrawn(effectName));
        }

        if (baseSprite is null || baseSprite.Placeholder != JominiSpritePlaceholder.None)
        {
            // Ohne gezeichnetes Grundbild (Text, fehlende oder dynamische Textur) gibt es nichts, worauf die Effekte wirken; die Ebenen werden gar
            // nicht erst gesucht.
            if (layerBlocks.Count > 0)
            {
                AddOnce(notes, GuiTexts.LayoutEffectNotDrawn(ModifyTexture));
            }

            if (glowBlock is not null && ReadGlow(glowBlock, new List<string>()) is not null)
            {
                AddOnce(notes, GuiTexts.LayoutEffectNotDrawn(Glow));
            }

            if (grayscale)
            {
                AddOnce(notes, GuiTexts.LayoutEffectNotDrawn(effectName));
            }

            return null;
        }

        var layers = new List<JominiTextureLayer>();
        foreach (var block in SameNameReplaced(layerBlocks))
        {
            if (ReadLayer(block, textures, notes) is { } layer)
            {
                layers.Add(layer);
            }
        }

        var glow = glowBlock is null ? null : ReadGlow(glowBlock, notes);
        var visual = new JominiEffectVisual(layers, glow, grayscale ? JominiColorEffect.Grayscale : JominiColorEffect.None);
        return visual.IsEmpty ? null : visual;
    }

    /// <summary>
    /// Mit <see cref="JominiEffectRules.SameNameLayerReplaces"/> ersetzt eine spätere Ebene mit demselben <c>name</c> die frühere an deren Stelle. Das geschieht
    /// vor dem Aussortieren, sodass eine spätere gleichnamige Ebene mit <c>visible = no</c> die frühere ausblendet. Ebenen ohne Namen ersetzen nichts.
    /// </summary>
    private static IEnumerable<ResolvedElement> SameNameReplaced(List<ResolvedElement> blocks)
    {
        if (!JominiEffectRules.SameNameLayerReplaces)
        {
            return blocks;
        }

        var result = new List<ResolvedElement>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            var name = Unquote(block.Name);
            if (name.Length > 0 && positions.TryGetValue(name, out var index))
            {
                result[index] = block;
                continue;
            }

            if (name.Length > 0)
            {
                positions[name] = result.Count;
            }

            result.Add(block);
        }

        return result;
    }

    /// <summary>Eine Ebene oder <see langword="null"/>, wenn sie wegfällt (siehe <see cref="Build"/>).</summary>
    private static JominiTextureLayer? ReadLayer(ResolvedElement block, TextureLocator textures, List<string> notes)
    {
        // visible und video vor dem Suchen der Textur: Ausgeblendete Vorgaben (EU V block "unused_color" { visible = no }) melden keine fehlende Textur.
        if (!IsVisible(block, notes))
        {
            return null;
        }

        if (HasKey(block, "video"))
        {
            AddOnce(notes, GuiTexts.LayoutEffectNotDrawn("video"));
            return null;
        }

        var spriteNotes = new List<string>();
        var sprite = JominiSprites.Build(block, textures, spriteNotes);
        if (sprite is null)
        {
            return null;
        }

        if (sprite.Placeholder == JominiSpritePlaceholder.None && sprite.Opacity <= 0)
        {
            // Deckkraft 0 wirkt in keiner Mischart (auch alphamultiply und mask lassen das Grundbild dann unverändert); die Hinweise der Ebene entfallen mit.
            return null;
        }

        foreach (var note in spriteNotes)
        {
            AddOnce(notes, note);
        }

        if (sprite.Placeholder != JominiSpritePlaceholder.None)
        {
            return null;
        }

        foreach (var animated in new[] { "translate_uv", "rotate_uv" })
        {
            if (HasKey(block, animated))
            {
                AddOnce(notes, GuiTexts.LayoutEffectApproximated(animated));
            }
        }

        return new JominiTextureLayer(sprite, ReadBlendMode(block, notes));
    }

    /// <summary>
    /// <c>blend_mode</c> ohne Anführungszeichen und Groß-/Kleinschreibung. Fehlend gilt <see cref="JominiEffectRules.DefaultBlendMode"/>; unbekannt ebenso,
    /// mit Hinweis.
    /// </summary>
    private static JominiBlendMode ReadBlendMode(ResolvedElement block, List<string> notes)
    {
        var value = Unquote(block.Property("blend_mode")?.Value);
        switch (value.ToLowerInvariant())
        {
            case "":
                return JominiEffectRules.DefaultBlendMode;
            case "normal":
                return JominiBlendMode.Normal;
            case "multiply":
                return JominiBlendMode.Multiply;
            case "overlay":
                return JominiBlendMode.Overlay;
            case "add":
                return JominiBlendMode.Add;
            case "alphamultiply":
                return JominiBlendMode.AlphaMultiply;
            case "colordodge":
                return JominiBlendMode.ColorDodge;
            case "lighten":
                return JominiBlendMode.Lighten;
            case "darken":
                return JominiBlendMode.Darken;
            case "mask":
                return JominiBlendMode.Mask;
            default:
                AddOnce(notes, GuiTexts.LayoutEffectApproximated("blend_mode"));
                return JominiEffectRules.DefaultBlendMode;
        }
    }

    /// <summary>
    /// <c>glow</c>: Radius aus <c>glow_radius</c> (≤ 0, fehlend oder keine Zahl: kein Leuchten, ohne Hinweis), Farbe aus <c>color</c> (fehlend: weiß),
    /// Deckkraft aus <c>alpha</c> (fehlend: 1, keine Zahl: 1 mit Hinweis, ≤ 0: kein Leuchten). <c>visible</c> wie bei Ebenen. <c>glow_generation_rules</c>
    /// und <c>glow_alpha</c> werden nicht ausgewertet (Hinweis).
    /// </summary>
    private static JominiGlow? ReadGlow(ResolvedElement block, List<string> notes)
    {
        if (Number(block.Property("glow_radius")?.Value) is not { } radius || radius <= 0)
        {
            return null;
        }

        var alpha = 1.0;
        if (block.Property("alpha") is { } alphaProperty)
        {
            if (Number(alphaProperty.Value) is { } value)
            {
                alpha = Math.Clamp(value, 0, 1);
            }
            else
            {
                AddOnce(notes, GuiTexts.LayoutEffectApproximated(Glow));
            }
        }

        if (alpha <= 0 || !IsVisible(block, notes))
        {
            return null;
        }

        if (HasKey(block, "glow_generation_rules") || HasKey(block, "glow_alpha"))
        {
            AddOnce(notes, GuiTexts.LayoutEffectApproximated("glow_generation_rules"));
        }

        var color = JominiTextureColor.Parse(block.Property("color"), notes) ?? new JominiColor(1, 1, 1, 1);
        return new JominiGlow(color, radius, alpha);
    }

    /// <summary>
    /// <c>visible = no</c> (auch <c>false</c>, <c>0</c>) blendet aus. Eine Bindung (<c>[…]</c>) gilt als sichtbar, mit Hinweis, wie bedingte Elemente gezeigt
    /// werden.
    /// </summary>
    private static bool IsVisible(ResolvedElement block, List<string> notes)
    {
        var value = Unquote(block.Property("visible")?.Value);
        if (value.Contains('['))
        {
            AddOnce(notes, GuiTexts.LayoutEffectApproximated("visible"));
            return true;
        }

        return !(value.Equals("no", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value == "0");
    }

    /// <summary>Steht in einem <c>state</c>-Block des Elements ein <c>modify_texture</c> oder <c>glow</c>? Gelesen am Rohknoten, ohne <c>using</c>.</summary>
    private static bool HasEffectInState(ResolvedElement element) =>
        element.StructuredBlocks("state").Any(state => state.Origin.Node is { } node
            && node.Children.Any(child => child.Name is ModifyTexture or Glow));

    /// <summary>
    /// Hat das Element den Schlüssel, gleich ob als Wert, Wertblock (<c>translate_uv = { 0 0.1 }</c>), strukturierter Block oder als Kind? Ein Block wie
    /// <c>glow_generation_rules = { glow_blur_passes = 2 }</c> zählt beim Einordnen als Engine-Widget, wenn die Regeln ihn nicht als strukturiert kennen.
    /// </summary>
    private static bool HasKey(ResolvedElement element, string name) =>
        element.Properties.Any(p => p.Name == name) || element.Children.Any(c => c.Key == name);

    private static string Unquote(string? text) => text?.Trim().Trim('"').Trim() ?? string.Empty;

    /// <summary>Eine endliche Zahl; Bindungen (<c>[…]</c>), Prozente und Namen sind keine.</summary>
    private static double? Number(string? text) =>
        double.TryParse(Unquote(text), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private static void AddOnce(List<string> notes, string note)
    {
        if (!notes.Contains(note))
        {
            notes.Add(note);
        }
    }
}
