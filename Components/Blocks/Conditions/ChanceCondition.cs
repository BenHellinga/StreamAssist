using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class ChanceCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Chance";
    [JsonIgnore] public override string Description => "True some of the time, picked at random each time it's checked: Percent is how often (50 = half the time, kept to 0-100).";



    // private



    private readonly ValueSlot<double> _percent;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("percent")]
    private ValueSlotData JsonPercent
    {
        get => _percent.GetData();
        set => _percent.LoadData(value);
    }



    // constructor



    public ChanceCondition()
    {
        _percent = new("Percent");
        _slots = [_percent];

        _percent.SetText("50");

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var percent = Math.Clamp(await _percent.EvaluateAsync(context), 0, 100);
        return Random.Shared.NextDouble() * 100 < percent;
    }
}
