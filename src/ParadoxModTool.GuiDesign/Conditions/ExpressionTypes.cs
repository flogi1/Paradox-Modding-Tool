using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>Rückgabetyp eines Ausdrucks laut Datentyp-Dump; <c>null</c>, wenn er unbekannt oder <c>[unregistered]</c> ist.</summary>
public static class ExpressionTypes
{
    public static string? TypeOf(ExprNode node, DataTypeDatabase db, string? start) => node switch
    {
        LiteralNode literal => literal.Type ?? "string",
        CallNode call => Registered(Global(db, call.Name)),
        ChainNode chain => Resolve(chain, db, start).Type,
        _ => null,
    };

    /// <summary>Löst eine Kette Glied für Glied auf; <c>Unknown</c> ist das erste Glied, das es im Dump nicht gibt (nur solange der Typ davor bekannt ist).</summary>
    internal static (string? Type, string? Unknown) Resolve(ChainNode chain, DataTypeDatabase db, string? start)
    {
        var first = chain.Links[0];
        string? type;
        if (first.Args is null && db.IsType(first.Name))
        {
            type = first.Name;
        }
        else if (Global(db, first.Name) is { } global)
        {
            type = Registered(global);
        }
        else if (start is not null && Member(db, start, first.Name) is { } member)
        {
            type = Registered(member);
        }
        else
        {
            return (null, first.Name);
        }

        foreach (var link in chain.Links.Skip(1))
        {
            if (type is null)
            {
                return (null, null);
            }

            if (Member(db, type, link.Name) is not { } member)
            {
                return (null, link.Name);
            }

            type = Registered(member);
        }

        return (type, null);
    }

    internal static DataTypeEntry? Global(DataTypeDatabase db, string name) => db.Globals.FirstOrDefault(g => g.Name == name);

    private static DataTypeEntry? Member(DataTypeDatabase db, string type, string name) => db.Members(type).FirstOrDefault(m => m.Name == name);

    private static string? Registered(DataTypeEntry? entry) => entry is { HasRegisteredReturn: true } ? entry.ReturnType : null;
}
