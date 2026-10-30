using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsScenesBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Scenes";
    [JsonIgnore] public override string Description => "Every scene's name, top to bottom as OBS lists them, separated by \", \" (e.g. for a chat reply).";



    // private



    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public GetObsScenesBlock()
    {
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var (ok, scenes) = await obs.TryGetScenesAsync();
        return ok ? string.Join(", ", scenes!) : throw new BlockFailedException("scenes not read");
    }
}
