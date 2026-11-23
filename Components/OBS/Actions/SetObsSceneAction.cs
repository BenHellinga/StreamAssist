using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsSceneAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Scene";
    [JsonIgnore] public override string Description => "Switches OBS to this scene (its name as OBS shows it).";
    [JsonIgnore] public override string? Subgroup => "Scenes & Sources";



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



    public SetObsSceneAction()
    {
        _scene = new("Scene");
        _slots = [_scene];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context) =>
        await Api(context).Obs.SetSceneAsync(await _scene.EvaluateAsync(context));
}
