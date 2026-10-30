using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsSceneBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Scene";
    [JsonIgnore] public override string Description => "The name of the scene that's live in OBS right now.";



    // private



    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public GetObsSceneBlock()
    {
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var (ok, scene) = await obs.TryGetCurrentSceneAsync();
        return ok ? scene! : throw new BlockFailedException("scene not read");
    }
}
