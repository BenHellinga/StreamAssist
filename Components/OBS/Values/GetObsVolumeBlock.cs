using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsVolumeBlock : ValueBlock<double>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Volume";
    [JsonIgnore] public override string Description => "An audio source's volume in dB, like the OBS mixer (0 is full volume, lower is quieter).";



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



    public GetObsVolumeBlock()
    {
        _source = new("Source");
        _slots = [_source];

        TrackSlots();
    }



    // compute



    protected override async Task<double> ComputeAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var (ok, volume) = await obs.TryGetVolumeAsync(source);
        return ok ? volume : throw new BlockFailedException("volume not read");
    }
}
