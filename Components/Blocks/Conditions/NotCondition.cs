using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class NotCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Not";
    [JsonIgnore] public override string Description => "True if the condition inside is false (false when empty).";



    // private



    private readonly BlockSlot _condition;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("condition")]
    private PendingBlockData? JsonCondition
    {
        get => _condition.GetData();
        set => _condition.LoadData(value);
    }



    // constructor



    public NotCondition()
    {
        _condition = new("", typeof(ConditionBlock));
        _slots = [_condition];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        if (_condition.Block == null) return false;
        return !await _condition.EvaluateAsync(context);
    }
}
