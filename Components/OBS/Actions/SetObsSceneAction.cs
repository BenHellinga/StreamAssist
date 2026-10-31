using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsSceneAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Scene";
    [JsonIgnore] public override string Description => "Switches OBS to this scene (its name as OBS shows it), remembering the scene it leaves for Return to Previous Scene (the last 5).";



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
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var data = GlobalContext.DataModel ?? throw Fail("data not set up"); // the scene left is remembered in the variables table
        var scene = await _scene.EvaluateAsync(context);

        if (!await ObsSceneMemory.TrySwitchAsync(obs, data, scene)) throw new BlockFailedException("scene not switched");
    }
}
