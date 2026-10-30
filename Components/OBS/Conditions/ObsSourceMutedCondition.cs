using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Conditions;



public class ObsSourceMutedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "OBS Source Muted";
    [JsonIgnore] public override string Description => "True if an audio source is muted in OBS.";



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



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);

        var (ok, muted) = await obs.TryGetMutedAsync(source);
        return ok ? muted : throw new BlockFailedException("mute not read");
    }
}
