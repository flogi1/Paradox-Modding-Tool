using System.Globalization;

namespace ParadoxModTool.Scopes;

/// <summary>Was ein Schlüssel im Skript für den Scope bedeutet.</summary>
internal enum KeyKind
{
    /// <summary>Ändert den Scope nicht (<c>limit</c>, <c>OR</c>, <c>if</c>, Zahl-Gewichte in <c>random_list</c> …).</summary>
    Transparent,
    Root,
    This,
    Prev,
    From,

    /// <summary>Kette wie <c>scope:target.owner</c> oder <c>root.capital</c>.</summary>
    Chain,

    /// <summary><c>any_/every_/random_/ordered_</c>: der Block liegt im Scope des Ziels.</summary>
    Iterator,

    /// <summary>Ein Event-Target wie <c>owner</c>: der Block liegt im Scope des Ergebnisses.</summary>
    EventTarget,

    /// <summary>Ein Trigger/Effect/Modifier mit Block: der Inhalt sind Parameter, keine Anweisungen im Scope.</summary>
    Parameter,

    /// <summary>Ein Feld der Datei (Schlüssel, den das Spiel nicht als Trigger, Effect oder Event-Target kennt).</summary>
    Structural,

    /// <summary>
    /// Eine Zahl als Scope-Wechsel (Hearts of Iron IV: Staats-ID <c>763 = { … }</c>, EU IV: Provinz-ID); nur mit Spielprofil
    /// <c>numericKeysChangeScope</c> und nicht direkt in <c>random_list</c>. Der Scope dahinter gilt als unbekannt.
    /// </summary>
    NumericScope,
}

internal static class KeyClassifier
{
    private static readonly HashSet<string> TransparentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "nor", "nand", "if", "else_if", "else", "limit",
        "trigger_if", "trigger_else", "trigger_else_if", "hidden_effect", "show_as_tooltip", "random_list",
    };

    /// <param name="numericChangesScope">Zahlen wechseln den Scope (Spielprofil <c>numericKeysChangeScope</c>, außerhalb von <c>random_list</c>).</param>
    public static KeyKind Classify(string key, ScopeDb db, bool numericChangesScope = false)
    {
        if (double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return numericChangesScope ? KeyKind.NumericScope : KeyKind.Transparent;
        }

        if (TransparentKeys.Contains(key))
        {
            return KeyKind.Transparent;
        }

        switch (key.ToLowerInvariant())
        {
            case "root":
                return KeyKind.Root;
            case "this":
                return KeyKind.This;
            case "prev":
                return KeyKind.Prev;
            case "from":
            case "fromfrom":
                return KeyKind.From;
        }

        if (key.Contains(':') || key.Contains('.'))
        {
            return KeyKind.Chain;
        }

        var command = db.GetCommand(key);
        if (command is not null)
        {
            return ScopeDb.IsIteratorName(key) ? KeyKind.Iterator : KeyKind.Parameter;
        }

        if (db.GetTarget(key) is not null)
        {
            return KeyKind.EventTarget;
        }

        return db.IsModifier(key) ? KeyKind.Parameter : KeyKind.Structural;
    }

    /// <summary>Wechselt der Scope beim Betreten des Blocks (die Anweisungen darin zählen nicht zum umgebenden Feld)?</summary>
    public static bool ChangesScope(KeyKind kind) =>
        kind is KeyKind.Root or KeyKind.This or KeyKind.Prev or KeyKind.From or KeyKind.Chain or KeyKind.Iterator or KeyKind.EventTarget or KeyKind.NumericScope;

    /// <summary>Wechselt eine Zahl in einem Block mit dem Schlüssel <paramref name="parentKey"/> den Scope? Nie in <c>random_list</c> (Gewichte).</summary>
    public static bool NumericChangesScope(bool numericKeysChangeScope, string? parentKey) =>
        numericKeysChangeScope && !string.Equals(parentKey, "random_list", StringComparison.OrdinalIgnoreCase);
}
