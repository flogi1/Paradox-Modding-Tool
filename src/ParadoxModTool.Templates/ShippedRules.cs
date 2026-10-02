namespace ParadoxModTool.Templates;

/// <summary>
/// Die mitgelieferten Regeln: die früher fest im Code stehenden Ignorier-Regeln. Sie sind im Programm fest und nicht änderbar; man schaltet sie je Ebene ab
/// oder legt über "Kopie erstellen" eine eigene Fassung an. Sie stehen in der Rangfolge ganz unten.
/// </summary>
public static class ShippedRules
{
    public const string TextureFileId = "shipped-texture-file";
    public const string TextureEntryId = "shipped-texture-entry";
    public const string ButtonId = "shipped-button";
    public const string PositionId = "shipped-position";
    public const string SizeId = "shipped-size";
    public const string NamePrefixId = "shipped-name-prefix";

    /// <summary>Die mitgelieferten Regeln in fester Reihenfolge; jeder Aufruf liefert neue Kopien.</summary>
    public static IReadOnlyList<CountingRule> All => new List<CountingRule>
    {
        Make(TextureFileId, TemplatesTexts.ShippedTextureFile, RuleWhere.Anywhere, string.Empty, RuleAction.Ignore, "*.dds", "*.png", "*.tga", "*.jpg", "*.jpeg", "*.bmp", "*.psd", "*.gif"),
        Make(TextureEntryId, TemplatesTexts.ShippedTextureEntry, RuleWhere.InsideKey, "texture", RuleAction.Ignore, "*"),
        Make(ButtonId, "Button", RuleWhere.Anywhere, string.Empty, RuleAction.Ignore, "*button*"),
        Make(PositionId, TemplatesTexts.ShippedPositionEntry, RuleWhere.InsideKey, "position", RuleAction.Ignore, "*"),
        Make(SizeId, TemplatesTexts.ShippedSizeEntry, RuleWhere.InsideKey, "size", RuleAction.Ignore, "*"),
        Make(NamePrefixId, TemplatesTexts.ShippedNamePrefix, RuleWhere.Anywhere, string.Empty, RuleAction.MarkedDoesNotCount, "iv[#]*", "iv_[#]*"),
    };

    /// <summary>Die mitgelieferten Regeln als wirksame Regeln der Ebene "mitgeliefert", ohne die abgeschalteten Kennungen.</summary>
    public static IReadOnlyList<EffectiveRule> Effective(IEnumerable<string>? disabledIds = null)
    {
        var disabled = new HashSet<string>(disabledIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        return All.Where(r => !disabled.Contains(r.Id)).Select(r => new EffectiveRule(r, RuleLayer.Shipped)).ToList();
    }

    private static CountingRule Make(string id, string name, RuleWhere where, string key, RuleAction action, params string[] patterns) => new()
    {
        Id = id,
        Name = name,
        Where = where,
        WhereKey = key,
        Action = action,
        Patterns = patterns.ToList(),
    };
}
