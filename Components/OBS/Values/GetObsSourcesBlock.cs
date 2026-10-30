using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsSourcesBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Sources";
    [JsonIgnore] public override string Description => "Every source in a scene, top to bottom as OBS lists them, separated by \", \". Scene left empty = the live scene.";



    // private



    private readonly ValueSlot<string> _scene;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("scene")]
    private ValueSlotData JsonScene
    {
        get => _scene.GetData();
        set => _scene.LoadData(value);
    }



    // constructor



    public GetObsSourcesBlock()
    {
        _scene = new("Scene");
        _slots = [_scene];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var scene = await _scene.EvaluateAsync(context);
        var (ok, sources) = await obs.TryGetSourcesAsync(scene);
        return ok ? string.Join(", ", sources!) : throw new BlockFailedException("sources not read");
    }
}
