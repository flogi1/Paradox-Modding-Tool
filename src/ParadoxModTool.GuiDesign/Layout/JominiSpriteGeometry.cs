namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Ein Stück eines Sprites: der Ausschnitt <paramref name="Source"/> der Textur (in Texturpixeln) wird in <paramref name="Target"/> (in GUI-Einheiten,
/// Bildschirmkoordinaten) gezeichnet. <paramref name="TileWidth"/>/<paramref name="TileHeight"/> sind die Kachelgröße in GUI-Einheiten; 0 heißt strecken.
/// </summary>
public readonly record struct JominiSpritePiece(LayoutRect Source, LayoutRect Target, double TileWidth, double TileHeight);

/// <summary>
/// Reine Geometrie von Jomini-Sprites: Strecken, 9-Teile-Raster, Kacheln und <c>fittype</c>. Die Annahmen über das Spielverhalten stehen in
/// <see cref="JominiSpriteRules"/>. Spiegeln (<c>mirror</c>) gehört nicht hierher; der Zeichner spiegelt den ganzen Kasten.
/// </summary>
public static class JominiSpriteGeometry
{
    /// <summary>Mehr Kacheln als das in einem Stück werden stattdessen gestreckt (Schutz vor Zeichenzeit und Speicher).</summary>
    private const int MaxTiles = 4096;

    /// <summary>Kleinere Flächen zählen als leer (Rundungsreste der proportionalen Verkleinerung).</summary>
    private const double Epsilon = 1e-9;

    /// <summary>
    /// Die Stücke des Sprites im Kasten <paramref name="box"/>. Bei neun Stücken ist die Reihenfolge zeilenweise: links oben, oben, rechts oben, links,
    /// Mitte, rechts, links unten, unten, rechts unten. Stücke ohne Fläche (Quelle oder Ziel) fehlen, die Indizes verschieben sich dann; Platzhalter-
    /// Sprites und leere Kästen geben keine Stücke. <paramref name="notes"/> bekommt Hinweise, wenn Werte angenähert werden (null = keine Hinweise).
    /// <paramref name="renderScale"/> ist <see cref="LayoutBox.RenderScale"/> (Jomini <c>scale</c>): Ränder und Kachelgrößen im Ziel werden damit malgenommen,
    /// der Kasten <paramref name="box"/> ist schon skaliert. Nicht endlich oder ≤ 0 zählt als 1 (wie ohne Angabe).
    /// </summary>
    public static IReadOnlyList<JominiSpritePiece> Pieces(JominiSpriteVisual sprite, LayoutRect box, List<string>? notes = null, double renderScale = 1)
    {
        var scale = double.IsFinite(renderScale) && renderScale > 0 ? renderScale : 1;
        var source = sprite.Source;
        if (sprite.Placeholder != JominiSpritePlaceholder.None
            || !IsUsable(box)
            || !IsUsable(source))
        {
            return Array.Empty<JominiSpritePiece>();
        }

        var density = double.IsFinite(sprite.Density) && sprite.Density > 0 ? sprite.Density : 1;
        var tileScale = JominiSpriteRules.TileDividedByDensity ? density : 1;

        switch (sprite.Kind)
        {
            case JominiSpriteKind.Stretched:
                return new[] { Fitted(sprite.Fit, source, box) };

            case JominiSpriteKind.Tiled:
                return new[] { Tiled(source, box, tileScale, scale, notes) };
        }

        // Neun Teile. Ränder wie in spriteborder: negativ oder nicht endlich zählt als 0 (mit Hinweis).
        var border = sprite.Border;
        var left = Border(border.Left, notes);
        var top = Border(border.Top, notes);
        var right = Border(border.Right, notes);
        var bottom = Border(border.Bottom, notes);

        // Gemessen (13b): spriteborder ist in GUI-Einheiten, in der Textur sind es spriteborder × Dichte Pixel. Die Ecken erscheinen damit
        // unverkleinert in der Dichte der Textur. Die frühere Annahme (Pixel, im Kasten ÷ Dichte) bleibt hinter der Regel abrufbar.
        var (sourceScale, targetScale) = JominiSpriteRules.BorderInGuiUnits ? (density, 1.0) : (1.0, 1 / density);

        // scale am Kasten (oder an Vorfahren): der Rand im Ziel wächst und schrumpft mit (nicht gemessen, Hinweis an der Engine), die Quelle bleibt.
        targetScale *= scale;

        // Quelle: die Ränder müssen in den Ausschnitt passen, sonst schrumpfen Paare proportional.
        var (srcLeft, srcRight) = ShrinkPair(left * sourceScale, right * sourceScale, source.Width);
        var (srcTop, srcBottom) = ShrinkPair(top * sourceScale, bottom * sourceScale, source.Height);

        // Ziel: Rand in GUI-Einheiten, ebenfalls als Paar in den Kasten (13h: 8 + 8 in 12 gibt 6 + 6).
        var (dstLeft, dstRight) = ShrinkPair(left * targetScale, right * targetScale, box.Width);
        var (dstTop, dstBottom) = ShrinkPair(top * targetScale, bottom * targetScale, box.Height);

        double[] srcX = { source.X, source.X + srcLeft, source.X + source.Width - srcRight, source.X + source.Width };
        double[] srcY = { source.Y, source.Y + srcTop, source.Y + source.Height - srcBottom, source.Y + source.Height };
        double[] dstX = { box.X, box.X + dstLeft, box.X + box.Width - dstRight, box.X + box.Width };
        double[] dstY = { box.Y, box.Y + dstTop, box.Y + box.Height - dstBottom, box.Y + box.Height };

        var tiled = sprite.Kind == JominiSpriteKind.CorneredTiled;
        var pieces = new List<JominiSpritePiece>(9);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var src = new LayoutRect(srcX[column], srcY[row], Math.Max(0, srcX[column + 1] - srcX[column]), Math.Max(0, srcY[row + 1] - srcY[row]));
                var dst = new LayoutRect(dstX[column], dstY[row], Math.Max(0, dstX[column + 1] - dstX[column]), Math.Max(0, dstY[row + 1] - dstY[row]));
                if (!IsUsable(src) || !IsUsable(dst))
                {
                    continue;
                }

                // Ecken bleiben ungekachelt, Kanten und Mitte werden bei CorneredTiled gekachelt.
                var isCorner = row != 1 && column != 1;
                pieces.Add(tiled && !isCorner
                    ? Tiled(src, dst, tileScale, scale, notes)
                    : new JominiSpritePiece(src, dst, 0, 0));
            }
        }

        return pieces;
    }

    private static bool IsUsable(LayoutRect rect) =>
        double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height)
        && rect.Width > Epsilon && rect.Height > Epsilon;

    /// <summary>Ein Rand (wie <c>spriteborder</c>); negativ oder nicht endlich zählt als 0 (mit Hinweis).</summary>
    private static double Border(double value, List<string>? notes)
    {
        if (double.IsFinite(value) && value >= 0)
        {
            return value;
        }

        AddNoteOnce(notes, GuiTexts.LayoutTextureApproximated("spriteborder"));
        return 0;
    }

    /// <summary>Hinweis höchstens einmal je Aufruf.</summary>
    private static void AddNoteOnce(List<string>? notes, string note)
    {
        if (notes is not null && !notes.Contains(note))
        {
            notes.Add(note);
        }
    }

    /// <summary>Zwei Ränder an gegenüberliegenden Seiten; passen sie zusammen nicht in <paramref name="limit"/>, schrumpfen beide im gleichen Verhältnis.</summary>
    private static (double First, double Second) ShrinkPair(double first, double second, double limit)
    {
        var sum = first + second;
        if (sum > limit && sum > 0)
        {
            var factor = limit / sum;
            return (first * factor, second * factor);
        }

        return (first, second);
    }

    /// <summary>Gestrecktes Sprite mit <c>fittype</c>.</summary>
    private static JominiSpritePiece Fitted(JominiFit fit, LayoutRect source, LayoutRect box)
    {
        if (fit == JominiFit.Fill)
        {
            return new JominiSpritePiece(source, box, 0, 0);
        }

        var scaleX = box.Width / source.Width;
        var scaleY = box.Height / source.Height;
        if (fit == JominiFit.Center)
        {
            // Ganz sichtbar, im Kasten zentriert, Seitenverhältnis bleibt.
            var scale = Math.Min(scaleX, scaleY);
            var width = source.Width * scale;
            var height = source.Height * scale;
            return new JominiSpritePiece(source, new LayoutRect(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height), 0, 0);
        }

        // Kasten bedecken und überstehenden Teil abschneiden: Mitte, Anfang (links oben) oder Ende (rechts unten).
        var cover = Math.Max(scaleX, scaleY);
        var visibleWidth = box.Width / cover;
        var visibleHeight = box.Height / cover;
        var freeX = source.Width - visibleWidth;
        var freeY = source.Height - visibleHeight;
        var (offsetX, offsetY) = fit switch
        {
            JominiFit.Start => (0.0, 0.0),
            JominiFit.End => (freeX, freeY),
            _ => (freeX / 2, freeY / 2),
        };

        return new JominiSpritePiece(new LayoutRect(source.X + offsetX, source.Y + offsetY, visibleWidth, visibleHeight), box, 0, 0);
    }

    /// <summary>Gekacheltes Stück; zu viele Kacheln werden gestreckt. <paramref name="renderScale"/>: Faktor aus <c>scale</c> für die Kachelgröße im Ziel.</summary>
    private static JominiSpritePiece Tiled(LayoutRect source, LayoutRect target, double tileScale, double renderScale, List<string>? notes)
    {
        // Eine Kachel ist mindestens eine GUI-Einheit groß (vor scale), danach mit scale malgenommen.
        var tileWidth = Math.Max(1, source.Width / tileScale) * renderScale;
        var tileHeight = Math.Max(1, source.Height / tileScale) * renderScale;
        var count = Math.Ceiling(target.Width / tileWidth) * Math.Ceiling(target.Height / tileHeight);
        if (count > MaxTiles)
        {
            AddNoteOnce(notes, GuiTexts.LayoutTextureApproximated("tiling"));
            return new JominiSpritePiece(source, target, 0, 0);
        }

        return new JominiSpritePiece(source, target, tileWidth, tileHeight);
    }
}
