namespace ParadoxModTool.Templates;

/// <summary>Reine Änderungen an Regeldateien (Schalten, Kopieren, Reihenfolge); das Speichern übernimmt der Aufrufer.</summary>
public static class RuleEditing
{
    /// <summary>
    /// Eine Regel einer weniger spezifischen Ebene als der gewählten (auch eine mitgelieferte) schaltet man nur auf der gewählten Ebene ab, über deren Liste
    /// <see cref="RuleFile.DisabledRuleIds"/>. Eine Regel der gewählten oder einer spezifischeren Ebene schaltet man über ihren eigenen Schalter.
    /// </summary>
    public static bool UsesDisabledList(RuleLayer ruleLayer, RuleLayer chosen) => ruleLayer < chosen;

    /// <param name="ownFile">Die Datei der Ebene, auf der die Regel liegt (bei mitgelieferten Regeln unbenutzt).</param>
    /// <param name="chosenFile">Die Datei der gewählten Ebene.</param>
    public static void SetActive(RuleFile ownFile, RuleFile chosenFile, CountingRule rule, RuleLayer ruleLayer, RuleLayer chosen, bool active)
    {
        if (UsesDisabledList(ruleLayer, chosen))
        {
            if (active)
            {
                chosenFile.DisabledRuleIds.RemoveAll(id => id == rule.Id);
            }
            else if (!chosenFile.DisabledRuleIds.Contains(rule.Id))
            {
                chosenFile.DisabledRuleIds.Add(rule.Id);
            }

            return;
        }

        var own = ownFile.Rules.FirstOrDefault(r => r.Id == rule.Id);
        if (own is not null)
        {
            own.Enabled = active;
        }
    }

    /// <summary>Eine Kopie mit neuer Kennung und dem Zusatz " (Kopie)" im Namen.</summary>
    public static CountingRule Copy(CountingRule rule)
    {
        var copy = rule.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = rule.Name + TemplatesTexts.CopySuffix;
        copy.Enabled = true;
        return copy;
    }

    /// <summary>Hängt eine Kopie an die Datei an. Bei einer mitgelieferten Regel wird das Original in dieser Datei abgeschaltet, damit die Kopie an seine Stelle tritt.</summary>
    public static CountingRule AddCopy(RuleFile target, CountingRule original, RuleLayer originalLayer)
    {
        var copy = Copy(original);
        target.Rules.Add(copy);
        if (originalLayer == RuleLayer.Shipped && !target.DisabledRuleIds.Contains(original.Id))
        {
            target.DisabledRuleIds.Add(original.Id);
        }

        return copy;
    }

    public static bool MoveUp(RuleFile file, string id)
    {
        var index = file.Rules.FindIndex(r => r.Id == id);
        if (index <= 0)
        {
            return false;
        }

        (file.Rules[index - 1], file.Rules[index]) = (file.Rules[index], file.Rules[index - 1]);
        return true;
    }

    public static bool MoveDown(RuleFile file, string id)
    {
        var index = file.Rules.FindIndex(r => r.Id == id);
        if (index < 0 || index >= file.Rules.Count - 1)
        {
            return false;
        }

        (file.Rules[index + 1], file.Rules[index]) = (file.Rules[index], file.Rules[index + 1]);
        return true;
    }

    public static bool Delete(RuleFile file, string id) => file.Rules.RemoveAll(r => r.Id == id) > 0;

    /// <summary>Ersetzt die Regel mit derselben Kennung an ihrer Stelle.</summary>
    public static bool Replace(RuleFile file, CountingRule edited)
    {
        var index = file.Rules.FindIndex(r => r.Id == edited.Id);
        if (index < 0)
        {
            return false;
        }

        file.Rules[index] = edited;
        return true;
    }

    public static void Add(RuleFile file, CountingRule rule) => file.Rules.Add(rule);
}
