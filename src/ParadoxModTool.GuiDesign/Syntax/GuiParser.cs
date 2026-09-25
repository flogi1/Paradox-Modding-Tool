namespace ParadoxModTool.GuiDesign.Syntax;

/// <summary>
/// Rein syntaktischer, fehlertoleranter Parser für .gui-Dateien. Erkennt die GUI-Sonderformen <c>types X { }</c>,
/// <c>type a = b { }</c>, <c>template x { }</c>, <c>block "x" { }</c>, <c>blockoverride "x" { }</c>, <c>using = x</c> und
/// <c>@name = wert</c>. Ob <c>schluessel = { … }</c> ein Widget oder eine Block-Eigenschaft ist, entscheidet erst der Resolver.
/// Wirft nie: Überzählige "}" werden übersprungen, fehlende "}" enden am Dateiende, beides mit Diagnose.
/// </summary>
public static class GuiParser
{
    public static GuiDocument Parse(string text)
    {
        var diagnostics = new List<GuiDiagnostic>();
        var tokens = GuiLexer.Tokenize(text, diagnostics);
        var root = new GuiNode(GuiNodeKind.Document) { Span = new TextSpan(0, text.Length), Line = 1 };
        new Parser(tokens, diagnostics).ParseBody(root);
        return new GuiDocument(text, root, diagnostics);
    }

    private sealed class Parser
    {
        private readonly List<GuiToken> _tokens;
        private readonly List<GuiDiagnostic> _diagnostics;
        private int _pos;

        public Parser(List<GuiToken> tokens, List<GuiDiagnostic> diagnostics)
        {
            _tokens = tokens;
            _diagnostics = diagnostics;
        }

        private GuiToken Peek(int offset = 0) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];

        public void ParseBody(GuiNode parent)
        {
            var topLevel = parent.Kind == GuiNodeKind.Document;
            while (true)
            {
                var t = Peek();
                switch (t.Kind)
                {
                    case GuiTokenKind.Eof:
                        if (!topLevel)
                        {
                            _diagnostics.Add(new GuiDiagnostic(GuiDiagnosticKind.MissingCloseBrace, parent.OpenBrace, parent.OpenBrace + 1, parent.Line, "Schließende Klammer \"}\" fehlt"));
                            parent.Span = new TextSpan(parent.Span.Start, t.Start);
                        }

                        return;

                    case GuiTokenKind.Close:
                        _pos++;
                        if (topLevel)
                        {
                            _diagnostics.Add(new GuiDiagnostic(GuiDiagnosticKind.UnexpectedCloseBrace, t.Start, t.End, t.Line, "Überzählige schließende Klammer \"}\""));
                            continue;
                        }

                        parent.CloseBrace = t.Start;
                        parent.Span = new TextSpan(parent.Span.Start, t.End);
                        return;

                    case GuiTokenKind.Comment:
                        _pos++;
                        Add(parent, new GuiNode(GuiNodeKind.Comment) { Name = t.Text, Span = new TextSpan(t.Start, t.End), NameSpan = new TextSpan(t.Start, t.End), Line = t.Line });
                        continue;

                    case GuiTokenKind.Open:
                        _pos++;
                        var anonymous = new GuiNode(GuiNodeKind.KeyedBlock)
                        {
                            Span = new TextSpan(t.Start, t.End),
                            NameSpan = new TextSpan(t.Start, t.Start),
                            OpenBrace = t.Start,
                            Line = t.Line,
                        };
                        Add(parent, anonymous);
                        ParseBody(anonymous);
                        continue;

                    case GuiTokenKind.Operator:
                        _pos++;
                        _diagnostics.Add(new GuiDiagnostic(GuiDiagnosticKind.UnexpectedToken, t.Start, t.End, t.Line, $"Unerwarteter Operator \"{t.Text}\""));
                        continue;

                    default:
                        ParseEntry(parent);
                        continue;
                }
            }
        }

        private void ParseEntry(GuiNode parent)
        {
            var t = Peek();
            var t1 = Peek(1);
            var t2 = Peek(2);

            if (t.Kind == GuiTokenKind.Word)
            {
                if (string.Equals(t.Text, "types", StringComparison.OrdinalIgnoreCase) && t1.Kind == GuiTokenKind.Word && t2.Kind == GuiTokenKind.Open)
                {
                    OpenNamed(parent, GuiNodeKind.TypesContainer, t, t1, t2, 3);
                    return;
                }

                if (t.Text == "template" && t1.Kind == GuiTokenKind.Word && t2.Kind == GuiTokenKind.Open)
                {
                    OpenNamed(parent, GuiNodeKind.TemplateDefinition, t, t1, t2, 3);
                    return;
                }

                if (t.Text is "block" or "blockoverride" && t1.Kind is GuiTokenKind.String or GuiTokenKind.Word && t2.Kind == GuiTokenKind.Open)
                {
                    OpenNamed(parent, t.Text == "block" ? GuiNodeKind.Block : GuiNodeKind.BlockOverride, t, t1, t2, 3);
                    return;
                }

                var t3 = Peek(3);
                var t4 = Peek(4);
                if (t.Text == "type" && t1.Kind == GuiTokenKind.Word && t2 is { Kind: GuiTokenKind.Operator, Text: "=" } && t3.Kind == GuiTokenKind.Word && t4.Kind == GuiTokenKind.Open)
                {
                    var type = OpenNamed(parent, GuiNodeKind.TypeDefinition, t, t1, t4, 5, parseBody: false);
                    type.BaseName = t3.Text;
                    ParseBody(type);
                    return;
                }
            }

            if (t1.Kind == GuiTokenKind.Operator)
            {
                if (t2.Kind == GuiTokenKind.Open)
                {
                    _pos += 3;
                    var block = new GuiNode(GuiNodeKind.KeyedBlock)
                    {
                        Name = t.Text,
                        IsQuoted = t.Kind == GuiTokenKind.String,
                        Operator = t1.Text,
                        Span = new TextSpan(t.Start, t2.End),
                        NameSpan = new TextSpan(t.Start, t.End),
                        OpenBrace = t2.Start,
                        Line = t.Line,
                    };
                    Add(parent, block);
                    ParseBody(block);
                    return;
                }

                if (t2.Kind is GuiTokenKind.Word or GuiTokenKind.String)
                {
                    _pos += 3;
                    var kind = t.Text == "using" && t.Kind == GuiTokenKind.Word
                        ? GuiNodeKind.Using
                        : t.Kind == GuiTokenKind.Word && t.Text.StartsWith('@') && !t.Text.StartsWith("@[", StringComparison.Ordinal)
                            ? GuiNodeKind.ConstantDefinition
                            : GuiNodeKind.Property;
                    Add(parent, new GuiNode(kind)
                    {
                        Name = kind == GuiNodeKind.ConstantDefinition ? t.Text[1..] : t.Text,
                        Operator = t1.Text,
                        Value = t2.Text,
                        IsQuoted = t2.Kind == GuiTokenKind.String,
                        Span = new TextSpan(t.Start, t2.End),
                        NameSpan = new TextSpan(t.Start, t.End),
                        ValueSpan = new TextSpan(t2.Start, t2.End),
                        Line = t.Line,
                    });
                    return;
                }

                _pos += 2;
                _diagnostics.Add(new GuiDiagnostic(GuiDiagnosticKind.MissingValue, t.Start, t1.End, t.Line, $"Wert fehlt nach \"{t.Text} {t1.Text}\""));
                Add(parent, new GuiNode(GuiNodeKind.Property)
                {
                    Name = t.Text,
                    Operator = t1.Text,
                    Value = string.Empty,
                    Span = new TextSpan(t.Start, t1.End),
                    NameSpan = new TextSpan(t.Start, t.End),
                    Line = t.Line,
                });
                return;
            }

            _pos++;
            Add(parent, new GuiNode(GuiNodeKind.ListItem)
            {
                Value = t.Text,
                IsQuoted = t.Kind == GuiTokenKind.String,
                Span = new TextSpan(t.Start, t.End),
                NameSpan = new TextSpan(t.Start, t.Start),
                ValueSpan = new TextSpan(t.Start, t.End),
                Line = t.Line,
            });
        }

        private GuiNode OpenNamed(GuiNode parent, GuiNodeKind kind, GuiToken keyword, GuiToken name, GuiToken open, int consumed, bool parseBody = true)
        {
            _pos += consumed;
            var node = new GuiNode(kind)
            {
                Name = name.Text,
                IsQuoted = name.Kind == GuiTokenKind.String,
                Span = new TextSpan(keyword.Start, open.End),
                NameSpan = new TextSpan(name.Start, name.End),
                OpenBrace = open.Start,
                Line = keyword.Line,
            };
            Add(parent, node);
            if (parseBody)
            {
                ParseBody(node);
            }

            return node;
        }

        private static void Add(GuiNode parent, GuiNode child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
        }
    }
}
