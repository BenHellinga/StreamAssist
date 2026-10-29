using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class EqualsIgnoreCaseCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Equals Ignore Case";
    [JsonIgnore] public override string Description => "True if both texts are the same, ignoring upper/lower case (e.g. a username).";



    // private



    private readonly ValueSlot<string> _first;
    private readonly ValueSlot<string> _second;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("first")]
    private ValueSlotData JsonFirst
    {
        get => _first.GetData();
        set => _first.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("second")]
    private ValueSlotData JsonSecond
    {
        get => _second.GetData();
        set => _second.LoadData(value);
    }



    // constructor



    public EqualsIgnoreCaseCondition()
    {
        _first = new("A");
        _second = new("B");
        _slots = [_first, _second];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context) =>
        string.Equals(await _first.EvaluateAsync(context), await _second.EvaluateAsync(context), StringComparison.OrdinalIgnoreCase);
}
