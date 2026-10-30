using System;
using System.Globalization;

namespace StreamAssist.Core.Utilities;



// works out a typed arithmetic formula like "25*(25+1)/2": numbers, + - * / %, parentheses and negative numbers
// (× ÷ − work too). Like C#, whole numbers stay whole (7/2 = 3) and a decimal makes that part of the formula a
// decimal (7/2.0 = 3.5, 7/2*2.0 = 6)
public static class Formula
{
    // the answer, and whether it's a whole number (only whole numbers went into it)
    public readonly record struct Number(double Value, bool IsWhole);



    // false with a short reason if it isn't a formula or can't be worked out (e.g. dividing by zero)
    public static bool TryEvaluate(string text, out Number result, out string error)
    {
        try
        {
            var parser = new Parser(text);
            result = parser.ParseAll();
            error = "";
            return true;
        }
        catch (FormatException ex)
        {
            result = default;
            error = ex.Message;
            return false;
        }
    }



    // reads the text left to right, one rule per precedence level (+ - lowest, then * / %, then a sign, then a number
    // or parentheses)
    private sealed class Parser(string text)
    {
        private int _position;



        public Number ParseAll()
        {
            var value = ParseSum();
            SkipSpaces();
            if (_position < text.Length) throw new FormatException($"didn't expect '{text[_position]}' at character {_position + 1}");
            return value;
        }



        private Number ParseSum()
        {
            var value = ParseProduct();
            while (true)
            {
                var op = Peek();
                if (op is not ('+' or '-')) return value;

                _position++;
                var right = ParseProduct();
                value = Combine(value, right, op == '+' ? value.Value + right.Value : value.Value - right.Value);
            }
        }



        private Number ParseProduct()
        {
            var value = ParseSigned();
            while (true)
            {
                var op = Peek();
                if (op is not ('*' or '/' or '%')) return value;

                _position++;
                var right = ParseSigned();
                var whole = value.IsWhole && right.IsWhole;

                if (op != '*' && right.Value == 0) throw new FormatException("can't divide by zero");

                value = op switch
                {
                    '*' => new(value.Value * right.Value, whole),
                    '/' => new(whole ? Math.Truncate(value.Value / right.Value) : value.Value / right.Value, whole),
                    _ => new(value.Value % right.Value, whole),
                };
            }
        }



        private Number ParseSigned()
        {
            var op = Peek();
            if (op is '+' or '-')
            {
                _position++;
                var value = ParseSigned();
                return op == '-' ? value with { Value = -value.Value } : value;
            }

            return ParseAtom();
        }



        private Number ParseAtom()
        {
            var next = Peek();

            if (next == '(')
            {
                _position++;
                var value = ParseSum();
                if (Peek() != ')') throw new FormatException("a '(' is never closed");
                _position++;
                return value;
            }

            var start = _position;
            while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) _position++;
            var number = text[start.._position];

            if (number == "") throw new FormatException(_position < text.Length ? $"didn't expect '{text[_position]}' at character {_position + 1}" : "it ends too early");
            if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                throw new FormatException($"'{number}' isn't a number");

            return new(parsed, !number.Contains('.'));
        }



        // the next character that isn't a space (× ÷ − read as * / -), or \0 at the end
        private char Peek()
        {
            SkipSpaces();
            if (_position >= text.Length) return '\0';

            return text[_position] switch
            {
                '×' => '*',
                '÷' => '/',
                '−' => '-',
                var c => c,
            };
        }



        private void SkipSpaces()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }



        private static Number Combine(Number left, Number right, double value) => new(value, left.IsWhole && right.IsWhole);
    }
}
