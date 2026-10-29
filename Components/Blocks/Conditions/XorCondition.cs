using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class XorCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Xor";
    [JsonIgnore] public override string Description => "True if an odd number of the conditions inside are true (false when empty).";



    // private



    private readonly BlockSlotList _conditions;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("conditions")]
    private List<PendingBlockData> JsonConditions
    {
        get => _conditions.GetData();
        set => _conditions.LoadData(value);
    }



    // constructor



    public XorCondition()
    {
        _conditions = new("", typeof(ConditionBlock));
        _slots = [_conditions];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var result = false;

        // every condition has to be checked, each true one flips the result
        foreach (var condition in _conditions.Blocks.Where(ConditionBlock.IsCondition).ToArray())
            if (await EvaluateConditionAsync(condition, context)) result = !result;

        return result;
    }
}
