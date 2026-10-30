using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Values;



// a typed formula, e.g. {cost}*({cost}+1)/2: parameters are filled in first, then it's worked out (see Formula)
// what it gives (whole number or decimal) follows the slot it's dropped in, like Operation
public class ExpressionBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Expression";
    [JsonIgnore] public override string Description => "Works out a formula: numbers, + - * / %, parentheses, and parameters like {cost}, e.g. {cost}*({cost}+1)/2. Like C#, whole numbers stay whole (7/2 = 3), a decimal makes it a decimal (7/2.0 = 3.5).";



    // private



    private readonly ValueSlot<string> _formula;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Formula => _formula;
    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Int, ValueKind.Double];
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
        var text = await _formula.EvaluateAsync(context);
        if (!Core.Utilities.Formula.TryEvaluate(text, out var result, out var error)) throw Fail($"'{text}' {error}");

        if (Kind == ValueKind.Double) return result.Value;

        // in a whole number slot a decimal answer drops its decimals (like Convert)
        var whole = Math.Truncate(result.Value);
        if (whole is > int.MaxValue or < int.MinValue) throw Fail($"{whole} too big for a whole number");
        return (int)whole;
    }
}
