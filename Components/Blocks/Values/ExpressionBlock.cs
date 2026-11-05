using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Values;



// a typed C# expression, e.g. {cost}*({cost}+1)/2 or {user} == "Bob" (see Formula). Each {name} goes in as a value of
// its own (a number, true/false or text, see Formula.ReadValue), not as pasted text, except inside "quotes" where it's
// part of that text. What it gives (whole number, decimal, true/false or text) follows the slot it's dropped in
public class ExpressionBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Expression";
    [JsonIgnore] public override string Description => "Works out a C# expression with parameters like {cost}: maths ({cost}*({cost}+1)/2, round, min, max, Math.Sqrt(...)), checks ({n} == 0 || {n} > 10, {user} == \"Bob\", {message}.Contains(\"hi\")) and choices ({n} > 1 ? \"s\" : \"\"). Text goes in \"quotes\" and == on text is case-sensitive. Like C#, whole numbers stay whole (7/2 = 3), a decimal makes it a decimal (7/2.0 = 3.5). It gives whatever the slot it's in needs: true/false in an If, a number, or text.";



    // private



    private readonly ValueSlot<string> _formula;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Formula => _formula;
    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Int, ValueKind.Double, ValueKind.Bool, ValueKind.Text];
    [JsonIgnore] public override bool FollowsSlot => true;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    // what it gives isn't a setting, it comes from the slot it was dropped in, saved so a loaded block still fits there
    [JsonInclude] [JsonPropertyName("kind")]
    private string JsonKind
    {
        get => KindName;
        set => KindName = value;
    }

    [JsonInclude] [JsonPropertyName("formula")]
    private ValueSlotData JsonFormula
    {
        get => _formula.GetData();
        set => _formula.LoadData(value);
    }



    // constructor



    public ExpressionBlock() : base(ValueKind.Double)
    {
        _formula = new("");
        _slots = [_formula];

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var values = new Dictionary<string, object>(); // the formula's names -> their values
        var names = new Dictionary<string, string>(); // each {...} -> its name in the formula
        var typed = _formula.Text;

        var formula = await _formula.EvaluateTextAsync(context, (token, value, index) =>
        {
            if (InQuotes(typed, index)) return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            if (names.TryGetValue(token, out var name)) return name;

            name = "_" + token.Trim('{', '}').Replace('.', '_');
            while (values.ContainsKey(name)) name += "_";
            names[token] = name;
            values[name] = Core.Utilities.Formula.ReadValue(value);
            return name;
        });

        // errors show the values the formula got, e.g. 'TestUser == Bob'
        var shown = Regex.Replace(formula, @"\b_\w+\b", m => values.TryGetValue(m.Value, out var v) ? ValueTypes.ToText(v) : m.Value);
        var resultType = Kind == ValueKind.Bool ? typeof(bool) : null;
        if (!Core.Utilities.Formula.TryEvaluate(formula, values, resultType, out var result, out var error)) throw Fail($"'{shown}' {error}");

        if (Kind == ValueKind.Bool) return (bool)result!;
        if (Kind == ValueKind.Text) return result == null ? "" : ValueTypes.ToText(result);

        var number = result switch
        {
            int or double => result,
            long or float or decimal or short or byte or uint => Convert.ToDouble(result),
            bool => throw Fail($"'{shown}' gives true/false, not a number"),
            _ => throw Fail($"'{shown}' gives text, not a number"),
        };

        if (Kind == ValueKind.Double) return Convert.ToDouble(number);
        if (number is int whole) return whole;

        // in a whole number slot a decimal answer drops its decimals (like Convert)
        var truncated = Math.Truncate((double)number);
        if (double.IsNaN(truncated) || truncated is > int.MaxValue or < int.MinValue) throw Fail($"{truncated} too big for a whole number");
        return (int)truncated;
    }



    // whether the {name} starting here is inside "text" (an odd number of unescaped quotes before it)
    private static bool InQuotes(string text, int index)
    {
        var inside = false;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == '"') inside = !inside;
        }

        return inside;
    }
}
