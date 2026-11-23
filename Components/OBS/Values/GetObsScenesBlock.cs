using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Values;



public class GetObsScenesBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Scenes";
    [JsonIgnore] public override string Description => "Every scene's name, top to bottom as OBS lists them, separated by \", \" (e.g. for a chat reply).";
    [JsonIgnore] public override string? Subgroup => "Scenes & Sources";



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



    protected override async Task<string> ComputeAsync(TriggerContext context) =>
        string.Join(", ", await Api(context).Obs.GetScenesAsync());
}
