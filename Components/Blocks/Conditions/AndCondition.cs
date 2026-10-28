using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class AndCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "And";
    [JsonIgnore] public override string Description => "True if every condition inside is true (false when empty).";



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



    public AndCondition()
    {
        _conditions = new("", typeof(ConditionBlock));
        _slots = [_conditions];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var conditions = _conditions.Blocks.OfType<ConditionBlock>().ToArray();
        if (conditions.Length == 0) return false;

        foreach (var condition in conditions)
            if (!await condition.EvaluateAsync(context)) return false;

        return true;
    }
}
