using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Conditions;



public class ObsSourceMutedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "OBS Source Muted";
    [JsonIgnore] public override string Description => "True if an audio source is muted in OBS.";
    [JsonIgnore] public override string? Subgroup => "Audio";



    // private



    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }



    // constructor



    public ObsSourceMutedCondition()
    {
        _source = new("Source");
        _slots = [_source];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context) =>
        await Api(context).Obs.IsMutedAsync(await _source.EvaluateAsync(context));
}
