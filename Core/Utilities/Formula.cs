using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StreamAssist.Core.Utilities;



// works out a typed formula like "25*(25+1)/2" or "{n} == 0 || {n} > 10": numbers, + - * / %, parentheses and negative
// numbers (× ÷ − work too), comparisons == != < <= > >= (≠ ≤ ≥ too), and true/false logic && || ! with true and false.
// Like C#, whole numbers stay whole (7/2 = 3) and a decimal makes that part of the formula a decimal (7/2.0 = 3.5,
// 7/2*2.0 = 6), and maths and true/false don't mix (true + 1, 5 && 1 and 5 == true are errors). Precedence is C#'s:
// ! and signs, then * / %, + -, < <= > >=, == !=, &&, ||. Functions: round (halves away from zero, 2.5 -> 3), floor,
// ceil, truncate (all give whole numbers), abs, and min / max of any number of numbers
public static class Formula
{
    // the answer: a number (and whether it's whole, only whole numbers went into it), or true/false (Value 1 or 0)
    public readonly record struct Number(double Value, bool IsWhole, bool IsBool = false)
    {
        public bool AsBool => Value != 0;
        public static Number FromBool(bool value) => new(value ? 1 : 0, false, true);
    }



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
            var value = ParseOr();
            SkipSpaces();
            if (_position < text.Length) throw new FormatException($"didn't expect '{text[_position]}' at character {_position + 1}");
            return value;
        }



        private Number ParseOr()
        {
            var value = ParseAnd();
            while (Accept("||"))
            {
                var right = ParseAnd();
                value = Number.FromBool(Bool(value, "||") | Bool(right, "||"));
            }

            return value;
        }



        private Number ParseAnd()
        {
            var value = ParseEquality();
            while (Accept("&&"))
            {
                var right = ParseEquality();
                value = Number.FromBool(Bool(value, "&&") & Bool(right, "&&"));
            }

            return value;
        }



        // numbers with numbers (5 == 5.0), true/false with true/false
        private Number ParseEquality()
        {
            var value = ParseComparison();
            while (true)
            {
                bool equal;
                if (Accept("==")) equal = true;
                else if (Accept("!=") || Accept("≠")) equal = false;
                else return value;

                var right = ParseComparison();
                if (value.IsBool != right.IsBool) throw new FormatException("can't compare a number with true/false");
                value = Number.FromBool((value.Value == right.Value) == equal);
            }
        }



        private Number ParseComparison()
        {
            var value = ParseSum();
            while (true)
            {
                string op;
                if (Accept("<=") || Accept("≤")) op = "<=";
                else if (Accept(">=") || Accept("≥")) op = ">=";
                else if (Accept("<")) op = "<";
                else if (Accept(">")) op = ">";
                else return value;

                var right = ParseSum();
                var (a, b) = (Num(value, op), Num(right, op));
                value = Number.FromBool(op switch { "<=" => a <= b, ">=" => a >= b, "<" => a < b, _ => a > b });
            }
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
                Num(value, op.ToString());
                Num(right, op.ToString());
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
                Num(value, op.ToString());
                Num(right, op.ToString());
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
            // ! (not the start of !=, that's ParseEquality's)
            if (Peek() == '!' && !IsAt("!="))
            {
                _position++;
                return Number.FromBool(!Bool(ParseSigned(), "!"));
            }

            var op = Peek();
            if (op is '+' or '-')
            {
                _position++;
                var value = ParseSigned();
                Num(value, op.ToString());
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
                var value = ParseOr();
                if (Peek() != ')') throw new FormatException("a '(' is never closed");
                _position++;
                return value;
            }

            if (AcceptWord("true")) return Number.FromBool(true);
            if (AcceptWord("false")) return Number.FromBool(false);
            if (_position < text.Length && char.IsLetter(text[_position])) return ParseFunction();

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



        // name(number, ...), e.g. round({cost} / 3) or max({a}, {b}, 10)
        private Number ParseFunction()
        {
            var start = _position;
            while (_position < text.Length && char.IsLetter(text[_position])) _position++;
            var name = text[start.._position].ToLowerInvariant();

            if (Peek() != '(') throw new FormatException($"didn't expect '{text[start.._position]}' at character {start + 1}");
            _position++;

            var args = new List<Number>();
            if (Peek() != ')')
            {
                while (true)
                {
                    args.Add(ParseOr());
                    if (Peek() != ',') break;
                    _position++;
                }
            }

            if (Peek() != ')') throw new FormatException($"{name}( is never closed");
            _position++;

            foreach (var arg in args) Num(arg, name);
            Number One(Func<double, double> work, bool? whole = null)
            {
                if (args.Count != 1) throw new FormatException($"{name} takes one number");
                return new(work(args[0].Value), whole ?? args[0].IsWhole);
            }

            return name switch
            {
                "round" => One(v => Math.Round(v, MidpointRounding.AwayFromZero), true),
                "floor" => One(Math.Floor, true),
                "ceil" => One(Math.Ceiling, true),
                "truncate" => One(Math.Truncate, true),
                "abs" => One(Math.Abs),
                "min" or "max" when args.Count == 0 => throw new FormatException($"{name} needs at least one number"),
                "min" => new(args.Min(a => a.Value), args.All(a => a.IsWhole)),
                "max" => new(args.Max(a => a.Value), args.All(a => a.IsWhole)),
                _ => throw new FormatException($"there's no function '{name}'"),
            };
        }



        // the operator is next (after spaces): skips it and says so
        private bool Accept(string op)
        {
            if (!IsAt(op)) return false;
            _position += op.Length;
            return true;
        }



        private bool IsAt(string op)
        {
            SkipSpaces();
            return string.CompareOrdinal(text, _position, op, 0, op.Length) == 0;
        }



        // true / false (case ignored), not the start of a longer word
        private bool AcceptWord(string word)
        {
            SkipSpaces();
            if (_position + word.Length > text.Length || string.Compare(text, _position, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            if (_position + word.Length < text.Length && char.IsLetterOrDigit(text[_position + word.Length])) return false;

            _position += word.Length;
            return true;
        }



        // what an operator needs: true/false for && || !, numbers for maths and < >
        private static bool Bool(Number value, string op) =>
            value.IsBool ? value.AsBool : throw new FormatException($"{op} needs true/false, not a number");



        private static double Num(Number value, string op) =>
            !value.IsBool ? value.Value : throw new FormatException($"{op} needs numbers, not true/false");



        private void SkipSpaces()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }



        private static Number Combine(Number left, Number right, double value) => new(value, left.IsWhole && right.IsWhole);
    }
}
