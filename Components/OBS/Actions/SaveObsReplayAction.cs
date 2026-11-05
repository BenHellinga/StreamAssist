using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SaveObsReplayAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Save OBS Replay";
    [JsonIgnore] public override string Description => "Saves the replay buffer's last moments to a file (the replay buffer has to be running, see Set OBS Output).";



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => [];



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        if (!(await obs.TrySaveReplayAsync()).Ok) throw new BlockFailedException("replay not saved");
    }
}
