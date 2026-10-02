using System.Globalization;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Wertet die Inline-Formeln des Spiels aus (<c>@[ normal_siege_value_mult_base + 0.05 ]</c>): Zahlen, Namen von Konstanten, <c>+ - * /</c>, Klammern
/// und ein Minus vor einem Wert. Alles andere (Funktionen wie <c>abs(...)</c>, unbekannte Namen, Division durch null) gibt <see langword="null"/>.
/// </summary>
public static class InlineMath
{
    /// <param name="expression">Der Text zwischen <c>@[</c> und <c>]</c>.</param>
    /// <param name="lookup">Zahl zu einem Namen aus der Formel; <see langword="null"/>, wenn er sich nicht auflösen lässt.</param>
    public static decimal? Evaluate(string expression, Func<string, decimal?> lookup)
    {
        var parser = new Parser(expression, lookup);
        var value = parser.Expression();
        return parser.AtEnd ? value : null;
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly Func<string, decimal?> _lookup;
        private int _position;

        public Parser(string text, Func<string, decimal?> lookup)
        {
            _text = text;
            _lookup = lookup;
        }

        public bool AtEnd
        {
            get
            {
                SkipSpaces();
                return _position >= _text.Length;
            }
        }

        public decimal? Expression()
        {
            var left = Term();
            while (left is not null && Peek() is '+' or '-')
            {
                var op = _text[_position++];
                var right = Term();
                left = right is null ? null : op == '+' ? left + right : left - right;
            }

            return left;
        }

        private decimal? Term()
        {
            var left = Factor();
            while (left is not null && Peek() is '*' or '/')
            {
                var op = _text[_position++];
                var right = Factor();
                left = right is null || (op == '/' && right == 0) ? null : op == '*' ? left * right : left / right;
            }

            return left;
        }

        private decimal? Factor()
        {
            switch (Peek())
            {
                case '-':
                    _position++;
                    return -Factor();
                case '(':
                    _position++;
                    var inner = Expression();
                    if (Peek() != ')')
                    {
                        return null;
                    }

                    _position++;
                    return inner;
                case { } c when char.IsAsciiDigit(c) || c == '.':
                    return Number();
                case { } c when char.IsLetter(c) || c == '_':
                    return Name();
                default:
                    return null;
            }
        }

        private decimal? Number()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsAsciiDigit(_text[_position]) || _text[_position] == '.'))
            {
                _position++;
            }

            return decimal.TryParse(_text.AsSpan(start, _position - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        private decimal? Name()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
            {
                _position++;
            }

            var name = _text[start.._position];

            // Ein Funktionsaufruf wie abs(x) ist keine Konstante.
            return Peek() == '(' ? null : _lookup(name);
        }

        private char? Peek()
        {
            SkipSpaces();
            return _position < _text.Length ? _text[_position] : null;
        }

        private void SkipSpaces()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }
    }
}
