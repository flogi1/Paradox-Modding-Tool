namespace ParadoxModTool.ReferenceTree;

/// <summary>Was <see cref="ReferenceTreeRules.Availability"/> mit dem Reiter tun soll.</summary>
public enum ReferenceAvailabilityStep
{
    /// <summary>Nichts ändern (Verfügbarkeit unverändert).</summary>
    None,

    /// <summary>Eben unverfügbar geworden: einmal leeren und "nicht verfügbar" zeigen.</summary>
    ShowNotSupported,

    /// <summary>Nach "nicht verfügbar" wieder verfügbar: einmal leeren, Hinweis weg.</summary>
    ClearNotSupported,
}

/// <summary>Kleine Entscheidungen des Reiters "Verweise" ohne WPF (testbar).</summary>
public static class ReferenceTreeRules
{
    /// <summary><c>ContentChanged</c> nur, wenn sich <c>ShowsContent</c> wirklich geändert hat (kein Flackern der Leiste).</summary>
    public static bool ContentChanged(bool before, bool after) => before != after;

    /// <summary>
    /// Nach einem Reset mit Hinweis (z. B. "neu laden") bleibt der Reiter Inhalt, wenn er vorher Inhalt war: die Leiste bleibt mit dem
    /// Hinweis stehen. Ein leerer Hinweis blendet sie aus; ein Hinweis auf einem nie benutzten Reiter (Start) blendet sie nicht ein.
    /// </summary>
    public static bool KeepsContentAfterReset(string hint, bool showedContent) => hint.Length > 0 && showedContent;

    /// <summary>
    /// Doppelklick auf den Text einer Stelle (auch Aufwärts-Stelle) öffnet die Datei; auf dem Aufklapp-Dreieck (<paramref name="onExpander"/>)
    /// klappt er nur auf und zu wie bei anderen Einträgen.
    /// </summary>
    public static bool OpensOnDoubleClick(RefNode node, bool onExpander) =>
        !onExpander && node is { NodeKind: RefNodeKind.Site, Location: not null };

    /// <summary>Übergang der Verfügbarkeit: setzt den Reiter nur bei einer Änderung zurück.</summary>
    public static ReferenceAvailabilityStep Availability(bool available, bool showsNotSupported) => (available, showsNotSupported) switch
    {
        (false, false) => ReferenceAvailabilityStep.ShowNotSupported,
        (true, true) => ReferenceAvailabilityStep.ClearNotSupported,
        _ => ReferenceAvailabilityStep.None,
    };

    /// <summary>
    /// Ob der Baum in seiner geteilten Sitzung einen eigenen Aufbau startet. Beim ersten Durchgang wie bisher: leer, fehlgeschlagen oder ⟳.
    /// Danach (nach dem Warten auf einen fremden Aufbau) nur, wenn die Sitzung leer ist: ein Fenster hat seinen Aufbau abgebrochen, bevor es
    /// einen Index gab; der Baum selbst bricht nie ab, sein "abgebrochen" wäre also nie sein eigenes.
    /// </summary>
    public static bool StartsOwnBuild(bool building, bool empty, bool failed, bool reload, bool firstPass) =>
        !building && (empty || (firstPass && (failed || reload)));
}
