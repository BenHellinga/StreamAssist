using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class ReturnToPreviousSceneAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Return to Previous Scene";
    [JsonIgnore] public override string Description => "Switches OBS back to the scene the last Set OBS Scene left, like undo: pressing it again keeps going back, up to 5 scenes (Minecraft -> BRB -> Ending, then twice: back to BRB, then Minecraft). Kept in the variables previousScene1 to 5, so it survives a restart.";



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => [];



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");

        // a failure is already logged
        if (!await ObsSceneMemory.TryReturnAsync(obs, data)) throw new BlockFailedException("scene not switched");
    }
}
