using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class OrCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Or";
    [JsonIgnore] public override string Description => "True if at least one condition inside is true (false when empty).";



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



    public OrCondition()
    {
        _conditions = new("", typeof(ConditionBlock));
        _slots = [_conditions];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        foreach (var condition in _conditions.Blocks.Where(ConditionBlock.IsCondition).ToArray())
            if (await EvaluateConditionAsync(condition, context)) return true;

        return false;
    }
}
