namespace ParadoxModTool.GfxAudit;

/// <summary>Ein Sprite aus einer Spritedatei. Gleichheit vergleicht auch die Texturen der Reihe nach.</summary>
/// <param name="Name">Der Sprite-Name (Wert von <c>name</c>).</param>
/// <param name="Textures">
/// Alle Bilder des Blocks (jeder Schlüssel aus <see cref="GfxAuditRules.SpriteTextureKeys"/>, in Dateireihenfolge), normalisiert durch
/// <see cref="GfxNames.NormalizeSpriteTexture"/> (ohne Präfixprüfung, ohne führenden <c>/</c>), mit der Zeile ihres Schlüssels; Platzhalter entfallen.
/// </param>
/// <param name="Line">Zeile des Sprite-Blocks (1-basiert).</param>
public sealed record GfxSprite(string Name, IReadOnlyList<GfxRef> Textures, int Line)
{
    public bool Equals(GfxSprite? other) =>
        other is not null && Name == other.Name && Line == other.Line
        && (ReferenceEquals(Textures, other.Textures) || (Textures is not null && other.Textures is not null && Textures.SequenceEqual(other.Textures)));

    public override int GetHashCode() => HashCode.Combine(Name, Line, Textures?.Count ?? 0);
}

/// <summary>Ein Verweis in einer Textdatei.</summary>
/// <param name="Value">Normalisierter Bildpfad (Texturverweis) oder genauer Sprite-Name (Sprite-Verweis).</param>
/// <param name="Line">Zeile (1-basiert).</param>
public sealed record GfxRef(string Value, int Line);

/// <summary>Was eine Textdatei an Sprites und Verweisen enthält.</summary>
public sealed record GfxScan(IReadOnlyList<GfxSprite> Sprites, IReadOnlyList<GfxRef> TexturePaths, IReadOnlyList<GfxRef> SpriteRefs);

/// <summary>Eine Erwähnung eines Mod-Namens außerhalb von Sprite-Blöcken.</summary>
/// <param name="Name">Der Mod-Name (logischer Bildpfad oder Sprite-Name), dessen Kandidat gepasst hat.</param>
/// <param name="Line">Zeile (1-basiert).</param>
public sealed record GfxMention(string Name, int Line);
