using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicExpresso;

namespace StreamAssist.Core.Utilities;



// works out a C# expression (DynamicExpresso): maths, comparisons, && || !, ? :, "text" with == and its methods
// (.Contains, .StartsWith, .ToLower(), .Length, ...), Math.*, all with C#'s rules (7/2 = 3, 7/2.0 = 3.5, text == is
// case-sensitive). Named values come in as typed values, never pasted into the text, so a value with " or ) in it
// can't change the formula. Short functions on top of C#: round (halves away from zero, 2.5 -> 3), floor, ceil,
// truncate (all give whole numbers), abs, min / max (2 to 4 numbers), log10, pow. Only plain values, Math and the
// types C# keywords name can be used (no files, no reflection, no assigning)
public static class Formula
{
    // private



    private static readonly Interpreter _interpreter = CreateInterpreter();



    // public api



    // a value as the formula sees it: a whole number, a decimal, true/false, or else text (e.g. "5" -> 5, "Bob" -> "Bob")
    public static object ReadValue(string text)
    {
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return whole;
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) return number;
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return text;
    }



    // false with a short reason if it isn't a formula or can't be worked out (e.g. dividing by zero). With a
    // resultType (e.g. bool) a formula giving something else is refused before it runs
    public static bool TryEvaluate(string text, IReadOnlyDictionary<string, object> values, Type? resultType, out object? result, out string error)
    {
        try
        {
            var parameters = values.Select(v => new Parameter(v.Key, v.Value.GetType(), v.Value)).ToArray();
            var lambda = resultType == null ? _interpreter.Parse(text, parameters) : _interpreter.Parse(text, resultType, parameters);
            result = lambda.Invoke(parameters);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            result = null;
            error = Tidy(ex.Message);
            return false;
        }
    }



    // helpers



    private static Interpreter CreateInterpreter()
    {
        var interpreter = new Interpreter().EnableAssignment(AssignmentOperators.None);

        interpreter.SetFunction("round", (Func<double, int>)(v => checked((int)Math.Round(v, MidpointRounding.AwayFromZero))));
        interpreter.SetFunction("floor", (Func<double, int>)(v => checked((int)Math.Floor(v))));
        interpreter.SetFunction("ceil", (Func<double, int>)(v => checked((int)Math.Ceiling(v))));
        interpreter.SetFunction("truncate", (Func<double, int>)(v => checked((int)Math.Truncate(v))));
        interpreter.SetFunction("abs", (Func<int, int>)Math.Abs);
        interpreter.SetFunction("abs", (Func<double, double>)Math.Abs);
        interpreter.SetFunction("log10", (Func<double, double>)Math.Log10);
        interpreter.SetFunction("pow", (Func<double, double, double>)Math.Pow);

        // whole numbers stay whole when they're all whole, like the rest of C#
        interpreter.SetFunction("min", (Func<int, int, int>)Math.Min);
        interpreter.SetFunction("min", (Func<int, int, int, int>)((a, b, c) => Math.Min(a, Math.Min(b, c))));
        interpreter.SetFunction("min", (Func<int, int, int, int, int>)((a, b, c, d) => Math.Min(Math.Min(a, b), Math.Min(c, d))));
        interpreter.SetFunction("min", (Func<double, double, double>)Math.Min);
        interpreter.SetFunction("min", (Func<double, double, double, double>)((a, b, c) => Math.Min(a, Math.Min(b, c))));
        interpreter.SetFunction("min", (Func<double, double, double, double, double>)((a, b, c, d) => Math.Min(Math.Min(a, b), Math.Min(c, d))));
        interpreter.SetFunction("max", (Func<int, int, int>)Math.Max);
        interpreter.SetFunction("max", (Func<int, int, int, int>)((a, b, c) => Math.Max(a, Math.Max(b, c))));
        interpreter.SetFunction("max", (Func<int, int, int, int, int>)((a, b, c, d) => Math.Max(Math.Max(a, b), Math.Max(c, d))));
        interpreter.SetFunction("max", (Func<double, double, double>)Math.Max);
        interpreter.SetFunction("max", (Func<double, double, double, double>)((a, b, c) => Math.Max(a, Math.Max(b, c))));
        interpreter.SetFunction("max", (Func<double, double, double, double, double>)((a, b, c, d) => Math.Max(Math.Max(a, b), Math.Max(c, d))));

        return interpreter;
    }



    // the library's messages in the console's style: "Unknown identifier 'x' (at index 0)." -> "unknown identifier 'x'"
    private static string Tidy(string message)
    {
        message = Regex.Replace(message, @"\s*\(at index \d+\)\.?$", "").TrimEnd('.');
        if (message == "Invalid Operation") message = "can't use that operator on these values (e.g. a number == text)";
        return message.Length == 0 ? message : char.ToLowerInvariant(message[0]) + message[1..];
    }
}
