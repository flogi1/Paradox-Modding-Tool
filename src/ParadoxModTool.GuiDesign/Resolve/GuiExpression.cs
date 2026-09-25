using System.Globalization;

namespace ParadoxModTool.GuiDesign.Resolve;

/// <summary>Wertet den Inhalt von <c>@[ … ]</c> aus: Zahlen, Konstantennamen, + - * / und Klammern. Alles andere schlägt fehl.</summary>
public static class GuiExpression
{
    public static bool TryEvaluate(string expression, Func<string, double?> lookup, out double value)
    {
        var parser = new Parser(expression, lookup);
        try
        {
            value = parser.ParseSum();
            parser.SkipWhitespace();
            if (parser.AtEnd && double.IsFinite(value))
            {
                return true;
            }
        }
        catch (FormatException)
        {
        }

        value = 0;
        return false;
    }

    public static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private sealed class Parser
    {
        private readonly string _text;
        private readonly Func<string, double?> _lookup;
        private int _pos;

        public Parser(string text, Func<string, double?> lookup)
        {
            _text = text;
            _lookup = lookup;
        }

        public bool AtEnd => _pos >= _text.Length;

        public void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }

        public double ParseSum()
        {
            var value = ParseProduct();
            while (true)
            {
                SkipWhitespace();
                if (Accept('+'))
                {
                    value += ParseProduct();
                }
                else if (Accept('-'))
                {
                    value -= ParseProduct();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseProduct()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Accept('*'))
                {
                    value *= ParseFactor();
                }
                else if (Accept('/'))
                {
                    var divisor = ParseFactor();
                    if (divisor == 0)
                    {
                        throw new FormatException("Division durch 0");
                    }

                    value /= divisor;
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Accept('-'))
            {
                return -ParseFactor();
            }

            if (Accept('('))
            {
                var inner = ParseSum();
                SkipWhitespace();
                if (!Accept(')'))
                {
                    throw new FormatException("\")\" fehlt");
                }

                return inner;
            }

            var start = _pos;
            if (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
            {
                while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                {
                    _pos++;
                }

                return double.Parse(_text[start.._pos], NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
            {
                _pos++;
            }

            if (_pos == start)
            {
                throw new FormatException("Wert erwartet");
            }

            return _lookup(_text[start.._pos]) ?? throw new FormatException("Unbekannter Name");
        }

        private bool Accept(char c)
        {
            if (_pos < _text.Length && _text[_pos] == c)
            {
                _pos++;
                return true;
            }

            return false;
        }
    }
}
