using System;
using System.Globalization;

namespace StreamAssist.Components.Blocks.Model;



// the types a value block can return, and a value slot can hold
public enum ValueKind { Text, Int, Double, Bool, Char }



// how the value types convert into each other (the one place that decides it)
//
//   from \ to   text        int                  double      bool        char
//   text        -           Convert (can fail)   Convert     Convert     Convert (1 character)
//   int         automatic   -                    automatic   no          no
//   double      automatic   Convert (truncates)  -           no          no
//   bool        automatic   no                   no          -           no
//   char        automatic   no                   no          no          -
//
// a slot accepts its own kind plus anything that converts automatically, everything else needs the Convert block
// (TryConvert, which fails with an error when the value doesn't fit). Numbers always use "." for decimals,
// whatever the PC's region
public static class ValueTypes
{
    public static Type ClrType(ValueKind kind) => kind switch
    {
        ValueKind.Text => typeof(string),
        ValueKind.Int => typeof(int),
        ValueKind.Double => typeof(double),
        ValueKind.Bool => typeof(bool),
        ValueKind.Char => typeof(char),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };



    public static ValueKind KindOf(Type type)
    {
        if (type == typeof(string)) return ValueKind.Text;
        if (type == typeof(int)) return ValueKind.Int;
        if (type == typeof(double)) return ValueKind.Double;
        if (type == typeof(bool)) return ValueKind.Bool;
        if (type == typeof(char)) return ValueKind.Char;
        throw new ArgumentException($"{type.Name} isn't a value type blocks can use");
    }



    // whether a value of one kind fits a slot of another without a conversion block
    public static bool CanConvert(ValueKind from, ValueKind to) =>
        from == to || to == ValueKind.Text || (from == ValueKind.Int && to == ValueKind.Double);



    // only call when CanConvert says so
    public static object Convert(object value, ValueKind from, ValueKind to)
    {
        if (from == to) return value;
        if (to == ValueKind.Text) return ToText(value);
        if (from == ValueKind.Int && to == ValueKind.Double) return (double)(int)value;
        throw new InvalidOperationException($"{from} doesn't convert to {to} automatically");
    }



    // everything the table allows, automatic or through Convert, error says why not (e.g. "'abc' is not a whole number")
    public static bool TryConvert(object value, ValueKind from, ValueKind to, out object result, out string error)
    {
        result = value;
        error = "";

        if (CanConvert(from, to))
        {
            result = Convert(value, from, to);
            return true;
        }

        if (from == ValueKind.Text)
        {
            if (TryParse((string)value, to, out result)) return true;

            error = $"'{value}' is not {Describe(to)}";
            return false;
        }

        if (from == ValueKind.Double && to == ValueKind.Int)
        {
            var truncated = Math.Truncate((double)value);
            if (double.IsNaN(truncated) || truncated < int.MinValue || truncated > int.MaxValue)
            {
                error = $"{ToText(value)} too big for a whole number";
                return false;
            }

            result = (int)truncated;
            return true;
        }

        error = $"{Describe(from)} can't become {Describe(to)}";
        return false;
    }



    public static string ToText(object value) => value switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        char character => character.ToString(),
        _ => value.ToString() ?? "",
    };



    // reads typed text as a value of this kind (spaces around it are ignored, except for text)
    public static bool TryParse(string text, ValueKind kind, out object value)
    {
        value = text;
        var trimmed = text.Trim();

        switch (kind)
        {
            case ValueKind.Text:
                return true;

            case ValueKind.Int when int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole):
                value = whole;
                return true;

            case ValueKind.Double when double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                value = number;
                return true;

            case ValueKind.Bool when trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("false", StringComparison.OrdinalIgnoreCase):
                value = trimmed.Equals("true", StringComparison.OrdinalIgnoreCase);
                return true;

            case ValueKind.Char when text.Length == 1:
                value = text[0];
                return true;

            default:
                return false;
        }
    }



    // for error messages, e.g. "'abc' is not a whole number"
    public static string Describe(ValueKind kind) => kind switch
    {
        ValueKind.Text => "text",
        ValueKind.Int => "a whole number",
        ValueKind.Double => "a number",
        ValueKind.Bool => "true or false",
        ValueKind.Char => "a single character",
        _ => kind.ToString(),
    };



    // what an empty slot shows, e.g. "+ int" (null = a slot that takes any kind)
    public static string Placeholder(ValueKind? kind) => kind switch
    {
        null => "+ value",
        ValueKind.Text => "+ text",
        ValueKind.Int => "+ int",
        ValueKind.Double => "+ double",
        ValueKind.Bool => "+ bool",
        ValueKind.Char => "+ char",
        _ => "+",
    };
}
