using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



// a random number between two others. Like Operation, what it gives (whole number or decimal) follows the slot it's
// dropped in, no dropdown
public class RandomNumberBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Random Number";
    [JsonIgnore] public override string Description => "A random number from Min to Max, picked each time it's read: 0 to 1 by default, e.g. If it's under 0.3 for a 30% chance. In a decimal slot it can be anything in between, in a whole number slot both ends can come up (Min 1, Max 6 is a dice roll).";



    // private



    private readonly ValueSlot<double> _min;
    private readonly ValueSlot<double> _max;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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

    [JsonInclude] [JsonPropertyName("min")]
    private ValueSlotData JsonMin
    {
        get => _min.GetData();
        set => _min.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("max")]
    private ValueSlotData JsonMax
    {
        get => _max.GetData();
        set => _max.LoadData(value);
    }



    // constructor



    public RandomNumberBlock() : base(ValueKind.Double)
    {
        _min = new("Min");
        _max = new("Max");
        _slots = [_min, _max];

        _min.SetText("0");
        _max.SetText("1");

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var min = await _min.EvaluateAsync(context);
        var max = await _max.EvaluateAsync(context);
        if (min > max) (min, max) = (max, min);

        if (Kind == ValueKind.Double) return min + Random.Shared.NextDouble() * (max - min);

        // whole numbers from the first whole one at or above Min to the last at or below Max, both included
        var low = Math.Ceiling(min);
        var high = Math.Floor(max);
        if (low > high) throw Fail($"no whole number from {min} to {max}");
        if (low < int.MinValue || high >= int.MaxValue) throw Fail("too big for a whole number");
        return Random.Shared.Next((int)low, (int)high + 1);
    }
}
