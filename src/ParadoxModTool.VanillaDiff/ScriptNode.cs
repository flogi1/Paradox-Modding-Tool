namespace ParadoxModTool.VanillaDiff;

/// <summary>Operator einer Schlüssel/Wert-Anweisung. Paradox-Skript kennt neben "=" auch Vergleichsoperatoren.</summary>
public enum ScriptOperator
{
    Equals,
    LessThan,
    LessOrEqual,
    GreaterThan,
    GreaterOrEqual
}

/// <summary>Der Wert einer Anweisung oder eines Listeneintrags: entweder ein Skalar oder ein Block.</summary>
public abstract class ScriptValue
{
}

/// <summary>
/// Ein skalarer Wert: Zahl, Datum, Bezeichner (unquotiert) oder String (quotiert).
/// <see cref="Text"/> enthält bei Strings NICHT die umschließenden Anführungszeichen —
/// <see cref="IsQuoted"/> steuert, ob <see cref="ScriptWriter"/> sie beim Schreiben wieder ergänzt.
/// </summary>
public sealed class ScriptScalar : ScriptValue
{
    public required string Text { get; init; }
    public bool IsQuoted { get; init; }

    public override string ToString() => IsQuoted ? $"\"{Text}\"" : Text;
}

/// <summary>
/// Ein geklammerter Block ("{ ... }"), bzw. bei <see cref="ScriptDocument.Parse"/> auch die
/// implizite, ungeklammerte Wurzelebene einer ganzen Datei. Enthält eine geordnete Liste von
/// Einträgen, die entweder Schlüssel/Wert-Anweisungen oder reine Listenelemente sind.
/// </summary>
public sealed class ScriptBlock : ScriptValue
{
    public List<ScriptEntry> Entries { get; init; } = new();
}

/// <summary>
/// Ein Eintrag innerhalb eines <see cref="ScriptBlock"/>: entweder eine
/// <see cref="ScriptStatement"/> (key = value) oder ein <see cref="ScriptListItem"/>
/// (ein reiner Wert ohne Schlüssel, wie er in Listen-Blöcken wie "{ a b c }" vorkommt).
/// </summary>
public abstract class ScriptEntry
{
    /// <summary>
    /// Kommentarzeilen, die unmittelbar vor diesem Eintrag in eigenen Zeilen standen
    /// (ohne die führende Raute). Reihenfolge = Reihenfolge im Quelltext.
    /// </summary>
    public IReadOnlyList<string> LeadingComments { get; init; } = Array.Empty<string>();

    /// <summary>Ein Kommentar, der auf derselben Zeile wie das Ende dieses Eintrags stand, falls vorhanden.</summary>
    public string? TrailingComment { get; init; }

    /// <summary>
    /// Die komplette Quelldatei, aus der dieser Eintrag geparst wurde (derselbe String für
    /// alle Einträge EINES Parse-Aufrufs, keine Kopie) - null bei Einträgen, die im Code neu
    /// konstruiert wurden (z. B. Merge-Ergebnisse ohne einzelne, zusammenhängende Quellstelle).
    /// Zusammen mit <see cref="SourceStart"/>/<see cref="SourceEnd"/> ermöglicht das
    /// <see cref="ScriptWriter"/>, den Original-Rohtext dieses Eintrags unverändert
    /// zurückzuschreiben ("Mod-Formatierung beibehalten").
    /// </summary>
    public string? SourceText { get; init; }

    /// <summary>Start-Offset (inklusive) dieses Eintrags in <see cref="SourceText"/>.</summary>
    public int? SourceStart { get; init; }

    /// <summary>End-Offset (exklusiv) dieses Eintrags in <see cref="SourceText"/>.</summary>
    public int? SourceEnd { get; init; }
}

public sealed class ScriptStatement : ScriptEntry
{
    public required string Key { get; init; }
    public ScriptOperator Operator { get; init; } = ScriptOperator.Equals;
    public required ScriptValue Value { get; init; }
}

public sealed class ScriptListItem : ScriptEntry
{
    public required ScriptValue Value { get; init; }
}

/// <summary>
/// Ein oder mehrere aufeinanderfolgende Kommentarzeilen OHNE einen nachfolgenden Eintrag, an den
/// sie als <see cref="ScriptEntry.LeadingComments"/> angehängt werden könnten — z. B. ein
/// Kommentarblock ganz am Ende einer Datei oder unmittelbar vor der schließenden Klammer "}"
/// eines Blocks. Ohne diesen Eintragstyp gingen solche "hängenden" Kommentare beim Parsen
/// stillschweigend verloren (Runde 7, entdeckt beim Bau des Formatierungs-Werkzeugs, Abschnitt
/// 10 — <see cref="ScriptParser"/> sammelte sie zwar in einer lokalen "pendingComments"-Liste,
/// hängte diese aber nur an den JEWEILS NÄCHSTEN Eintrag an; gab es keinen weiteren Eintrag mehr
/// im Block, wurde die Liste nie in <see cref="ScriptBlock.Entries"/> übernommen). Besonders
/// kritisch für ein Werkzeug, das den AST direkt in die Originaldatei zurückschreibt — anders als
/// beim Vanilla-Merge (dort bewusst dokumentierte Einschränkung: keine 1:1-Formatierungstreue)
/// darf ein reines "hübscher darstellen" nie echten Dateiinhalt (Kommentare) verlieren. Die
/// eigentlichen Textzeilen stehen in <see cref="ScriptEntry.LeadingComments"/> — dieser Typ trägt
/// bewusst keine eigenen zusätzlichen Felder, um <see cref="ScriptWriter"/> und die
/// Vergleichslogik (<see cref="ScriptStructuralComparer"/>, die Kommentare ohnehin für die
/// strukturelle Gleichheit ignoriert) so einfach wie möglich zu halten.
/// </summary>
public sealed class ScriptCommentEntry : ScriptEntry
{
}
